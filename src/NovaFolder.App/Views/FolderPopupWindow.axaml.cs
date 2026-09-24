using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Transformation;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NovaFolder.Controls;
using NovaFolder.Core.Errors;
using NovaFolder.Core.Models;
using NovaFolder.Services.Applications;
using NovaFolder.Core.Storage;
using NovaFolder.Services.Windows;

namespace NovaFolder.Views
{
    // Una carpeta abierta, al estilo Android: una tarjeta flotante con su
    // contenido que se cierra al hacer clic afuera, con Escape o al abrir
    // algo. Es la única vista de "carpeta abierta" de la app: la usan
    // tanto el widget como los accesos directos del Escritorio.
    public partial class FolderPopupWindow : Window
    {
        private const int Columnas = 4;
        private const int UmbralBuscador = 9;   // a partir de aquí aparece el filtro
        private const double AltoMaximoLista = 420;
        private const int MargenAncla = 8;

        private readonly FolderStore _store = null!;
        private AppFolder _folder = null!;
        private readonly PixelRect? _ancla;

        // Mientras hay un selector de archivos o un menú abierto, la ventana
        // pierde el foco sin que el usuario haya "hecho clic afuera".
        private int _bloqueosCierre;
        private bool _renombrando;

        // Constructor sin argumentos: lo pide el cargador de XAML de
        // Avalonia (vista previa de diseño), nunca se usa en ejecución real.
        public FolderPopupWindow() => InitializeComponent();

        // ancla: rectángulo en pantalla junto al que abrirse (la tarjeta del
        // widget). null = junto al puntero, para los accesos del Escritorio.
        public FolderPopupWindow(FolderStore store, AppFolder folder, PixelRect? ancla)
        {
            InitializeComponent();
            _store = store;
            _folder = folder;

            // El puntero se lee UNA vez, al abrir. Si se leyera en cada
            // cambio de tamaño, la ventana perseguiría al ratón al mostrar
            // un aviso o filtrar.
            if (ancla == null && OperatingSystem.IsWindows() && PunteroService.Posicion() is PixelPoint p)
                ancla = new PixelRect(p, new PixelSize(0, 0));
            _ancla = ancla;

            // Acrílico antes que transparencia plana: el popup se apoya sobre
            // el Escritorio o sobre otras ventanas, y sin un material detrás
            // el texto quedaba ilegible según el fondo.
            TransparencyLevelHint = new List<WindowTransparencyLevel>
            {
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Blur,
                WindowTransparencyLevel.Transparent
            };

            Raiz.Width = Columnas * AppTile.Ancho;
            Desplazador.MaxHeight = AltoMaximoLista;
            Elementos.ItemWidth = AppTile.Ancho;

            ConectarEventos();
            Reconstruir();
        }

        // Para carpetas recién creadas: se abren ya con el nombre en edición.
        public void EmpezarRenombradoAlAbrir() => Opened += (_, _) => EmpezarRenombrado();

        private void ConectarEventos()
        {
            Titulo.PointerPressed += (_, e) => { e.Handled = true; EmpezarRenombrado(); };
            EditorTitulo.KeyDown += EditorTitulo_KeyDown;
            EditorTitulo.LostFocus += (_, _) => ConfirmarRenombrado(desdePerdidaDeFoco: true);

            BotonAgregar.Click += async (_, _) => await AgregarConSelector();
            BotonAgregarVacia.Click += async (_, _) => await AgregarConSelector();
            BotonMas.Flyout = CrearMenuMas();

            Buscador.TextChanged += (_, _) => Filtrar();
            Buscador.KeyDown += Buscador_KeyDown;

            // Soltar en el fondo de la carpeta: al final. Sobre un elemento
            // concreto: en su lugar (ver Reconstruir).
            Interacciones.AceptarSoltar(Tarjeta,
                rutas => Agregar(rutas),
                elemento => Recolocar(elemento, _folder.Apps.Count),
                resaltar: Tarjeta);

            _store.Changed += AlCambiarStore;
            Closed += (_, _) => _store.Changed -= AlCambiarStore;

            // Se recoloca en CADA SizeChanged: con SizeToContent la ventana
            // pasa por su tamaño por defecto antes de encogerse al del
            // contenido, y quedarse con esa primera medida la mandaba a la
            // esquina de la pantalla.
            SizeChanged += (_, _) => Posicionar();

            // Sin activar, la ventana nunca toma el foco y Deactivated no
            // llegaría: el popup quedaría abierto para siempre.
            Opened += (_, _) =>
            {
                Activate();
                if (Buscador.IsVisible) Buscador.Focus();
                else Focus();

                // Entrada: de 94 % y transparente a su tamaño real.
                Dispatcher.UIThread.Post(() =>
                {
                    Tarjeta.Opacity = 1;
                    Tarjeta.RenderTransform = TransformOperations.Parse("scale(1)");
                }, DispatcherPriority.Background);
            };

            Deactivated += (_, _) =>
            {
                if (_bloqueosCierre == 0) Close();
            };

            Closing += (_, _) =>
            {
                // Clic afuera con el nombre a medio escribir: se guarda, como
                // al renombrar un archivo en el Explorador.
                if (_renombrando) ConfirmarRenombrado(desdePerdidaDeFoco: true);
            };

            KeyDown += Ventana_KeyDown;
        }

        private void Posicionar()
        {
            if (!OperatingSystem.IsWindows() || _ancla == null) return;
            if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
            PunteroService.ColocarJuntoA(this, _ancla.Value, MargenAncla);
        }

        // ---- contenido ----

        private void AlCambiarStore()
        {
            // Si folders.json se recargó desde disco, los objetos son otros:
            // se vuelve a buscar la carpeta por nombre. Si ya no existe (la
            // eliminaron), no hay nada que mostrar.
            if (!_store.Folders.Contains(_folder))
            {
                var misma = _store.Buscar(_folder.Name);
                if (misma == null) { Close(); return; }
                _folder = misma;
            }
            Reconstruir();
        }

        private void Reconstruir()
        {
            Title = _folder.Name;
            Titulo.Text = _folder.Name;

            Elementos.Children.Clear();
            for (int i = 0; i < _folder.Apps.Count; i++)
            {
                var app = _folder.Apps[i];
                int indice = i;
                var tile = new AppTile(app, CrearMenuElemento(app));
                tile.AbrirPedido += t => Lanzar(t.App);

                Interacciones.HacerArrastrable(tile, () => new ElementoArrastrado(_folder, app));
                Interacciones.AceptarSoltar(tile,
                    rutas => Agregar(rutas, indice),
                    elemento => Recolocar(elemento, indice),
                    resaltar: tile);

                Elementos.Children.Add(tile);
            }

            var hay = _folder.Apps.Count > 0;
            Vacia.IsVisible = !hay;
            Desplazador.IsVisible = hay;
            Buscador.IsVisible = _folder.Apps.Count >= UmbralBuscador;
            Filtrar();
        }

        private void Filtrar()
        {
            var texto = Buscador.IsVisible ? Buscador.Text?.Trim() ?? "" : "";
            int visibles = 0;

            foreach (var tile in Elementos.Children.OfType<AppTile>())
            {
                tile.IsVisible = texto.Length == 0 || Coincide(tile.App.Name, texto);
                if (tile.IsVisible) visibles++;
            }

            SinResultados.IsVisible = _folder.Apps.Count > 0 && visibles == 0;
        }

        // Sin distinguir mayúsculas ni tildes: "musica" encuentra "Música".
        private static bool Coincide(string nombre, string texto) =>
            CultureInfo.CurrentCulture.CompareInfo.IndexOf(nombre, texto,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

        private AppTile? PrimerVisible() =>
            Elementos.Children.OfType<AppTile>().FirstOrDefault(t => t.IsVisible);

        // ---- acciones ----

        private void Lanzar(AppShortcut app)
        {
            var error = AppLauncherService.Lanzar(app.Path);
            if (error == null) Close();   // como en Android: abrir algo cierra la carpeta
            else MostrarAviso(error);
        }

        private void Agregar(IReadOnlyList<string> rutas, int? indice = null) => Intentar(() =>
        {
            if (rutas.Count == 0) return;
            var resultado = _store.AgregarElementos(_folder, rutas, indice);
            MostrarAviso(resultado.MotivoSiNadaEntro);
        });

        // Un elemento soltado dentro de esta carpeta: si ya era suyo se
        // reordena; si viene de otra (no debería, solo hay un popup) se mueve.
        private void Recolocar(ElementoArrastrado elemento, int indice) => Intentar(() =>
        {
            if (elemento.Carpeta == _folder) _store.ReordenarElemento(_folder, elemento.App, indice);
            else _store.MoverElemento(elemento.App, elemento.Carpeta, _folder);
        });

        // Los errores esperables (validación, carpeta que ya no existe) se
        // muestran en la propia tarjeta; los inesperados van al manejador
        // global, que los registra.
        private void Intentar(Action accion)
        {
            try { accion(); }
            catch (NovaFolderException ex) { MostrarAviso(ex.Message); }
        }

        private async Task AgregarConSelector()
        {
            var archivos = await ConBloqueo(() => StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Agregar a «{_folder.Name}»",
                AllowMultiple = true
            }));
            Agregar(RutasLocales(archivos));
        }

        private async Task AgregarCarpetaDelDisco()
        {
            var carpetas = await ConBloqueo(() => StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = $"Agregar una carpeta a «{_folder.Name}»",
                AllowMultiple = true
            }));
            Agregar(RutasLocales(carpetas));
        }

        private static List<string> RutasLocales(IEnumerable<IStorageItem> items) =>
            items.Select(i => i.TryGetLocalPath()).Where(p => p != null).Select(p => p!).ToList();

        // Un diálogo del sistema le quita el foco al popup: sin el bloqueo,
        // Deactivated lo cerraría justo cuando el usuario va a elegir.
        private async Task<T> ConBloqueo<T>(Func<Task<T>> accion)
        {
            _bloqueosCierre++;
            try { return await accion(); }
            finally
            {
                _bloqueosCierre--;
                Activate();
            }
        }

        private void MostrarAviso(string? texto)
        {
            Aviso.Text = texto;
            Aviso.IsVisible = !string.IsNullOrEmpty(texto);
        }

        // ---- menús ----

        private ContextMenu CrearMenuElemento(AppShortcut app)
        {
            var mover = new MenuItem
            {
                Header = "Mover a",
                Icon = new TextBlock { Text = "\uE8DE", FontFamily = Interacciones.FuenteIconos }
            };
            foreach (var otra in _store.Folders.Where(f => f != _folder))
            {
                var destino = otra;
                mover.Items.Add(Interacciones.Opcion(destino.Name, "\uE8B7", () => Intentar(() => _store.MoverElemento(app, _folder, destino))));
            }
            mover.IsEnabled = mover.Items.Count > 0;

            var menu = new ContextMenu
            {
                Items =
                {
                    Interacciones.Opcion("Abrir", "\uE8A7", () => Lanzar(app)),
                    Interacciones.Opcion("Abrir ubicación del archivo", "\uE838", () =>
                    {
                        var error = AppLauncherService.AbrirUbicacion(app.Path);
                        if (error == null) Close(); else MostrarAviso(error);
                    }),
                    mover,
                    new Separator(),
                    Interacciones.Opcion("Quitar de la carpeta", "\uE711", () => Intentar(() => _store.QuitarElemento(_folder, app)))
                }
            };
            BloquearMientrasEsteAbierto(menu);
            return menu;
        }

        private MenuFlyout CrearMenuMas()
        {
            var menu = new MenuFlyout
            {
                Items =
                {
                    Interacciones.Opcion("Cambiar nombre (F2)", "\uE8AC", EmpezarRenombrado),
                    Interacciones.Opcion("Agregar elementos… (Ctrl+O)", "\uE710", () => _ = AgregarConSelector()),
                    Interacciones.Opcion("Agregar una carpeta del disco…", "\uE8F4", () => _ = AgregarCarpetaDelDisco()),
                    new Separator(),
                    Interacciones.Opcion("Eliminar carpeta (no borra los archivos)", "\uE74D", () => Intentar(() => _store.EliminarCarpeta(_folder)))
                }
            };
            menu.Opened += (_, _) => _bloqueosCierre++;
            menu.Closed += (_, _) => AlCerrarMenu();
            return menu;
        }

        private void BloquearMientrasEsteAbierto(ContextMenu menu)
        {
            menu.Opened += (_, _) => _bloqueosCierre++;
            menu.Closed += (_, _) => AlCerrarMenu();
        }

        // Si el menú se llevó el foco, se devuelve a la ventana: si no, un
        // clic afuera ya no dispararía Deactivated y el popup no se cerraría.
        private void AlCerrarMenu()
        {
            _bloqueosCierre = Math.Max(0, _bloqueosCierre - 1);
            if (!IsActive) Activate();
        }

        // ---- renombrar ----

        private void EmpezarRenombrado()
        {
            if (_renombrando) return;
            _renombrando = true;
            MostrarAviso(null);

            EditorTitulo.Text = _folder.Name;
            Titulo.IsVisible = false;
            EditorTitulo.IsVisible = true;
            EditorTitulo.Focus();
            EditorTitulo.SelectAll();
        }

        private void ConfirmarRenombrado(bool desdePerdidaDeFoco)
        {
            if (!_renombrando) return;

            try
            {
                _store.RenombrarCarpeta(_folder, EditorTitulo.Text ?? "");
                MostrarAviso(null);
            }
            catch (NovaFolderException ex)
            {
                MostrarAviso(ex.Message);
                // Con Enter se deja seguir corrigiendo; si el foco se fue a
                // otro lado, se vuelve al nombre anterior en vez de insistir.
                if (!desdePerdidaDeFoco) return;
            }
            TerminarRenombrado();
        }

        private void TerminarRenombrado()
        {
            _renombrando = false;
            Titulo.Text = _folder.Name;
            EditorTitulo.IsVisible = false;
            Titulo.IsVisible = true;
            Focus();
        }

        // ---- teclado ----

        private void EditorTitulo_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ConfirmarRenombrado(desdePerdidaDeFoco: false);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;   // cancela el renombrado, no cierra la carpeta
                MostrarAviso(null);
                TerminarRenombrado();
            }
        }

        private void Buscador_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter when PrimerVisible() is AppTile primero:
                    e.Handled = true;
                    Lanzar(primero.App);
                    break;
                case Key.Down when PrimerVisible() is AppTile primero:
                    e.Handled = true;
                    primero.Focus(NavigationMethod.Tab);
                    break;
                case Key.Escape when !string.IsNullOrEmpty(Buscador.Text):
                    e.Handled = true;
                    Buscador.Text = "";
                    break;
            }
        }

        private void Ventana_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Handled) return;

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
            else if (e.Key == Key.F2)
            {
                e.Handled = true;
                EmpezarRenombrado();
            }
            else if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;
                _ = AgregarConSelector();
            }
        }
    }
}
