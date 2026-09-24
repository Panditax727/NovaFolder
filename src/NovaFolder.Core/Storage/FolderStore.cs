using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NovaFolder.Core.Diagnostics;
using NovaFolder.Core.Errors;
using NovaFolder.Core.Models;
using NovaFolder.Core.Validation;

namespace NovaFolder.Core.Storage
{
    // Lo que pasó al agregar elementos a una carpeta, para poder informarlo.
    public sealed record ResultadoAgregar(
        IReadOnlyList<AppShortcut> Agregados,
        IReadOnlyList<string> Repetidos,
        IReadOnlyList<string> Rechazados)
    {
        // Mensaje breve para el usuario cuando no entró nada.
        public string? MotivoSiNadaEntro =>
            Agregados.Count > 0 ? null
            : Rechazados.Count > 0 ? (Rechazados.Count == 1 ? Rechazados[0] : $"No se pudieron agregar {Rechazados.Count} elementos.")
            : Repetidos.Count == 1 ? "Ya estaba en la carpeta."
            : Repetidos.Count > 1 ? "Ya estaban todos en la carpeta."
            : null;
    }

    // Única fuente de verdad de las carpetas mientras la app está abierta,
    // con las operaciones CRUD de carpetas y de sus elementos.
    //
    // Toda modificación pasa por aquí: se valida, se aplica en memoria, se
    // guarda en disco y se avisa con Changed. Las ventanas no se hablan entre
    // sí; solo escuchan este evento y se redibujan.
    //
    // Errores:
    //   - datos inválidos -> ValidacionException (mensaje para el usuario);
    //   - carpeta que ya no existe -> CarpetaNoEncontradaException;
    //   - fallo al guardar en disco -> no se lanza: el cambio queda en memoria,
    //     se registra y se avisa con ErrorDeGuardado (el próximo guardado
    //     lo reintenta).
    public sealed class FolderStore : IDisposable
    {
        private readonly ConfigRepository _repositorio;
        private readonly AlmacenAccesos _almacen;
        private readonly string _escritorio;
        private NovaConfig _config;
        private string? _ultimoGuardado;
        private FileSystemWatcher? _vigilante;
        private Timer? _debounce;
        private SynchronizationContext? _hiloUi;

        public event Action? Changed;
        public event Action<string>? ErrorDeGuardado;

        public FolderStore(ConfigRepository repositorio, AlmacenAccesos almacen, RutasApp rutas)
        {
            ArgumentNullException.ThrowIfNull(repositorio);
            ArgumentNullException.ThrowIfNull(almacen);
            ArgumentNullException.ThrowIfNull(rutas);

            _repositorio = repositorio;
            _almacen = almacen;
            _escritorio = rutas.Escritorio;
            _config = repositorio.Cargar();
        }

        public static FolderStore Crear(RutasApp rutas) =>
            new(new ConfigRepository(rutas), new AlmacenAccesos(rutas), rutas);

        // ================= Carpetas: leer =================

        public IReadOnlyList<AppFolder> Folders => _config.Folders;

        // Los nombres se comparan sin distinguir mayúsculas: cada carpeta
        // acaba siendo un "Nombre.lnk" en el Escritorio, y en Windows
        // "Juegos.lnk" y "juegos.lnk" son el mismo archivo.
        public AppFolder? Buscar(string nombre) =>
            _config.Folders.FirstOrDefault(f => string.Equals(f.Name, nombre?.Trim(), StringComparison.OrdinalIgnoreCase));

        public AppFolder Obtener(string nombre) =>
            Buscar(nombre) ?? throw new CarpetaNoEncontradaException(nombre);

        public string NombreDisponible(string base_)
        {
            if (Buscar(base_) == null) return base_;
            for (int i = 2; ; i++)
                if (Buscar($"{base_} {i}") == null) return $"{base_} {i}";
        }

        // ================= Carpetas: crear / actualizar / eliminar =================

        // nombre null = "Nueva carpeta" (o "Nueva carpeta 2"...).
        public AppFolder CrearCarpeta(string? nombre = null, IEnumerable<string>? rutas = null)
        {
            ExigirCupoDeCarpetas();
            var definitivo = nombre == null ? NombreDisponible("Nueva carpeta") : NombreLibre(Validador.NombreCarpeta(nombre), null);

            var carpeta = new AppFolder { Name = definitivo };
            _config.Folders.Add(carpeta);
            if (rutas != null) AgregarSinGuardar(carpeta, rutas, null);
            Confirmar();
            return carpeta;
        }

        // Sacar un elemento de su carpeta y soltarlo en el fondo del widget.
        public AppFolder CrearCarpetaMoviendo(AppFolder desde, AppShortcut app)
        {
            Exigir(desde);
            ExigirCupoDeCarpetas();

            var carpeta = new AppFolder { Name = NombreDisponible("Nueva carpeta") };
            desde.Apps.Remove(app);
            carpeta.Apps.Add(app);
            _config.Folders.Add(carpeta);
            Confirmar();
            return carpeta;
        }

        public void RenombrarCarpeta(AppFolder carpeta, string nuevo)
        {
            Exigir(carpeta);
            var nombre = NombreLibre(Validador.NombreCarpeta(nuevo), carpeta);
            if (nombre == carpeta.Name) return;

            carpeta.Name = nombre;
            Confirmar();
        }

        // Devuelve la posición que ocupaba, para poder deshacer con RestaurarCarpeta.
        public int EliminarCarpeta(AppFolder carpeta)
        {
            int indice = _config.Folders.IndexOf(carpeta);
            if (indice < 0) throw new CarpetaNoEncontradaException(carpeta.Name);

            _config.Folders.RemoveAt(indice);
            Confirmar();
            return indice;
        }

        public void RestaurarCarpeta(AppFolder carpeta, int indice)
        {
            ArgumentNullException.ThrowIfNull(carpeta);
            if (_config.Folders.Contains(carpeta)) return;
            ExigirCupoDeCarpetas();

            // Mientras tanto pudo crearse otra con el mismo nombre.
            if (Buscar(carpeta.Name) != null) carpeta.Name = NombreDisponible(carpeta.Name);

            // Si sus accesos ya habían vuelto al Escritorio, se recuperan.
            for (int i = 0; i < carpeta.Apps.Count; i++)
            {
                var ruta = _almacen.Readoptar(carpeta.Apps[i].Path);
                if (ruta != carpeta.Apps[i].Path) carpeta.Apps[i] = ShortcutResolver.Crear(ruta, _escritorio);
            }
            _config.Folders.Insert(Math.Clamp(indice, 0, _config.Folders.Count), carpeta);
            Confirmar();
        }

        // ================= Elementos de una carpeta =================

        // indice = dónde insertar; null = al final. Nunca lanza por una ruta
        // mala: la informa en el resultado y sigue con las demás.
        public ResultadoAgregar AgregarElementos(AppFolder carpeta, IEnumerable<string> rutas, int? indice = null)
        {
            Exigir(carpeta);
            ArgumentNullException.ThrowIfNull(rutas);

            var resultado = AgregarSinGuardar(carpeta, rutas, indice);
            if (resultado.Agregados.Count > 0) Confirmar();
            return resultado;
        }

        public void QuitarElemento(AppFolder carpeta, AppShortcut app) => QuitarElementos(carpeta, new[] { app });

        public void QuitarElementos(AppFolder carpeta, IEnumerable<AppShortcut> apps)
        {
            Exigir(carpeta);
            bool alguno = false;
            foreach (var app in apps.ToList()) alguno |= carpeta.Apps.Remove(app);
            if (alguno) Confirmar();
        }

        public void MoverElemento(AppShortcut app, AppFolder desde, AppFolder hacia)
        {
            Exigir(desde);
            Exigir(hacia);
            if (desde == hacia || !desde.Apps.Contains(app)) return;

            if (!Contiene(hacia, app.Path))
            {
                ExigirCupoDeElementos(hacia, 1);
                hacia.Apps.Add(app);
            }
            desde.Apps.Remove(app);
            Confirmar();
        }

        // Arrastrar un elemento a otra posición dentro de su carpeta.
        // indice es la posición "antes de la que soltar" en la lista actual.
        public void ReordenarElemento(AppFolder carpeta, AppShortcut app, int indice)
        {
            Exigir(carpeta);
            int actual = carpeta.Apps.IndexOf(app);
            if (actual < 0) return;
            if (indice > actual) indice--;   // al quitarlo, los de detrás suben uno
            indice = Math.Clamp(indice, 0, carpeta.Apps.Count - 1);
            if (indice == actual) return;

            carpeta.Apps.RemoveAt(actual);
            carpeta.Apps.Insert(indice, app);
            Confirmar();
        }

        // ================= Preferencias =================
        //
        // No cambian lo que se ve en las carpetas: se guardan sin disparar
        // Changed para no redibujar nada.

        public bool StartWithWindows => _config.StartWithWindows;
        public bool CleanDesktop => _config.CleanDesktop;
        public (int X, int Y)? PosicionVentana =>
            _config.WindowX is int x && _config.WindowY is int y ? (x, y) : null;

        public void GuardarPosicion(int x, int y)
        {
            if (_config.WindowX == x && _config.WindowY == y) return;
            _config.WindowX = x;
            _config.WindowY = y;
            Guardar();
        }

        public void EstablecerAutoinicio(bool activo)
        {
            _config.StartWithWindows = activo;
            Guardar();
        }

        public void EstablecerLimpiarEscritorio(bool activo)
        {
            _config.CleanDesktop = activo;
            Guardar();
        }

        // ================= Internos =================

        private ResultadoAgregar AgregarSinGuardar(AppFolder carpeta, IEnumerable<string> rutas, int? indice)
        {
            var agregados = new List<AppShortcut>();
            var repetidos = new List<string>();
            var rechazados = new List<string>();
            int donde = Math.Clamp(indice ?? carpeta.Apps.Count, 0, carpeta.Apps.Count);

            foreach (var original in rutas)
            {
                string ruta;
                try
                {
                    ruta = Validador.RutaElemento(original);
                    ExigirCupoDeElementos(carpeta, 1);
                }
                catch (ValidacionException ex)
                {
                    rechazados.Add(ex.Message);
                    continue;
                }

                if (Contiene(carpeta, ruta)) { repetidos.Add(ruta); continue; }

                if (_config.CleanDesktop) ruta = _almacen.Adoptar(ruta);
                var app = ShortcutResolver.Crear(ruta, _escritorio);
                carpeta.Apps.Insert(donde++, app);
                agregados.Add(app);
            }

            if (rechazados.Count > 0)
                Log.Advertencia($"Elementos rechazados en «{carpeta.Name}»: {string.Join(" | ", rechazados)}");

            return new ResultadoAgregar(agregados, repetidos, rechazados);
        }

        private static bool Contiene(AppFolder carpeta, string ruta) =>
            carpeta.Apps.Any(a => string.Equals(a.Path, ruta, StringComparison.OrdinalIgnoreCase));

        // La referencia que tiene la interfaz puede estar vieja: la carpeta
        // se eliminó, o folders.json se recargó y los objetos son otros.
        private void Exigir(AppFolder carpeta)
        {
            ArgumentNullException.ThrowIfNull(carpeta);
            if (!_config.Folders.Contains(carpeta)) throw new CarpetaNoEncontradaException(carpeta.Name);
        }

        private string NombreLibre(string nombre, AppFolder? propia)
        {
            var otra = Buscar(nombre);
            if (otra != null && otra != propia)
                throw new ValidacionException($"Ya existe una carpeta llamada «{nombre}».");
            return nombre;
        }

        private void ExigirCupoDeCarpetas()
        {
            if (_config.Folders.Count >= Limites.MaxCarpetas)
                throw new ValidacionException($"Se alcanzó el máximo de {Limites.MaxCarpetas} carpetas.");
        }

        private static void ExigirCupoDeElementos(AppFolder carpeta, int nuevos)
        {
            if (carpeta.Apps.Count + nuevos > Limites.MaxElementosPorCarpeta)
                throw new ValidacionException($"«{carpeta.Name}» ya tiene el máximo de {Limites.MaxElementosPorCarpeta} elementos.");
        }

        private void Confirmar()
        {
            Guardar();
            BarrerAlmacen();
            Changed?.Invoke();
        }

        // Lo que ya no está en ninguna carpeta vuelve al Escritorio.
        private void BarrerAlmacen() =>
            _almacen.Barrer(_config.Folders.SelectMany(f => f.Apps).Select(a => a.Path));

        private void Guardar()
        {
            try
            {
                _ultimoGuardado = _repositorio.Guardar(_config);
            }
            catch (ConfiguracionException ex)
            {
                Log.Error("Fallo al guardar folders.json.", ex);
                ErrorDeGuardado?.Invoke(ex.Message);
            }
        }

        // ================= Ediciones hechas a mano en folders.json =================
        //
        // Quien prefiera editar el JSON ve el resultado al guardar, sin tener
        // que reiniciar NovaFolder. Hay que llamarlo desde el hilo de la UI:
        // Changed se dispara siempre ahí.
        public void VigilarCambiosExternos()
        {
            if (_vigilante != null) return;
            _hiloUi = SynchronizationContext.Current;

            try
            {
                var dir = Path.GetDirectoryName(_repositorio.RutaArchivo)!;
                Directory.CreateDirectory(dir);
                _vigilante = new FileSystemWatcher(dir, Path.GetFileName(_repositorio.RutaArchivo))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
                };
                _vigilante.Changed += (_, _) => ProgramarRecarga();
                _vigilante.Created += (_, _) => ProgramarRecarga();
                _vigilante.Renamed += (_, _) => ProgramarRecarga();
                _vigilante.EnableRaisingEvents = true;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            {
                Log.Advertencia($"No se pudo vigilar folders.json: {ex.Message}");
            }
        }

        // Un solo guardado en el editor dispara varios eventos seguidos:
        // se espera a que se calme antes de releer.
        private void ProgramarRecarga()
        {
            _debounce ??= new Timer(_ => Recargar());
            _debounce.Change(300, Timeout.Infinite);
        }

        private void Recargar()
        {
            string texto;
            try
            {
                if (new FileInfo(_repositorio.RutaArchivo).Length > Limites.TamanoMaxConfig) return;
                texto = File.ReadAllText(_repositorio.RutaArchivo);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return; // el editor aún lo tiene bloqueado: llegará otro evento
            }

            if (texto == _ultimoGuardado) return; // es nuestro propio guardado

            NovaConfig nueva;
            try { nueva = ConfigRepository.Interpretar(texto, _escritorio); }
            catch (System.Text.Json.JsonException ex)
            {
                // A medio escribir o con un error: se ignora hasta que quede bien.
                Log.Advertencia($"folders.json editado a mano no es válido todavía: {ex.Message}");
                return;
            }

            void Aplicar()
            {
                _config = nueva;
                _ultimoGuardado = texto;
                BarrerAlmacen();
                Changed?.Invoke();
            }

            if (_hiloUi != null) _hiloUi.Post(_ => Aplicar(), null);
            else Aplicar();
        }

        public void Dispose()
        {
            _vigilante?.Dispose();
            _debounce?.Dispose();
        }
    }
}
