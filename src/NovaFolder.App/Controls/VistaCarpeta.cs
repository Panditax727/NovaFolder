using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NovaFolder.Core.Errors;
using NovaFolder.Core.Models;
using NovaFolder.Core.Storage;
using NovaFolder.Services.Applications;

namespace NovaFolder.Controls
{
    // El contenido de una carpeta: la rejilla de elementos con todo su
    // comportamiento (abrir, ordenar arrastrando, soltar archivos, sacar al
    // Escritorio, menú contextual, filtro y estado vacío).
    //
    // La usan el popup estilo Android y la ventana principal, así que las
    // dos se comportan exactamente igual. Lo que depende de dónde esté (qué
    // hacer tras abrir una app, dónde mostrar un aviso) lo decide quien la
    // contiene a través de los eventos.
    public sealed class VistaCarpeta : UserControl
    {
        private readonly FolderStore _store;
        private readonly Border _marco;
        private readonly ScrollViewer _desplazador;
        private readonly WrapPanel _elementos;
        private readonly StackPanel _vacia;
        private readonly TextBlock _sinResultados;
        private AppFolder? _carpeta;
        private string _filtro = "";

        // texto (null = ocultar), esError
        public event Action<string?, bool>? AvisoPedido;
        public event Action<AppShortcut>? AppAbierta;
        public event Action? AgregarPedido;
        public event Action? CarpetaDesaparecida;

        // El popup se cierra al perder el foco; mientras hay un menú abierto
        // lo tiene que saber para no cerrarse.
        public event Action? MenuAbierto;
        public event Action? MenuCerrado;

        public VistaCarpeta(FolderStore store)
        {
            _store = store;

            _elementos = new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = AppTile.Ancho };
            _desplazador = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = _elementos
            };

            var botonElegir = new Button { Content = "Elegir archivos…", Classes = { "nova-accion" } };
            botonElegir.Click += (_, _) => AgregarPedido?.Invoke();
            _vacia = new StackPanel
            {
                Spacing = 10,
                Margin = new Thickness(12, 24, 12, 18),
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "\uE8B7",
                        FontFamily = Interacciones.FuenteIconos,
                        FontSize = 36,
                        Opacity = 0.35,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "Arrastra aquí apps, juegos o archivos\ndesde el Escritorio o el Explorador.",
                        Classes = { "suave" },
                        TextAlignment = TextAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    botonElegir
                }
            };

            _sinResultados = new TextBlock
            {
                Text = "Nada coincide con la búsqueda.",
                Classes = { "suave" },
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 24),
                IsVisible = false
            };

            // Marco transparente que ocupa todo: recibe lo que se suelta en
            // el hueco entre elementos y se ilumina durante el arrastre.
            // El aspecto va en el estilo "vista-marco" (App.axaml): un fondo
            // puesto aquí taparía el resaltado de "soltar-aqui".
            _marco = new Border
            {
                Classes = { "vista-marco" },
                Child = new Panel { Children = { _desplazador, _vacia, _sinResultados } }
            };
            Content = _marco;

            Interacciones.AceptarSoltar(_marco,
                rutas => Agregar(rutas, null),
                elemento => Recolocar(elemento, _carpeta?.Apps.Count ?? 0),
                resaltar: _marco);

            AttachedToVisualTree += (_, _) => _store.Changed += AlCambiarStore;
            DetachedFromVisualTree += (_, _) => _store.Changed -= AlCambiarStore;
        }

        public AppFolder? Carpeta
        {
            get => _carpeta;
            set
            {
                _carpeta = value;
                Reconstruir();
            }
        }

        // Sin distinguir mayúsculas ni tildes: "musica" encuentra "Música".
        public string Filtro
        {
            get => _filtro;
            set
            {
                _filtro = value?.Trim() ?? "";
                Filtrar();
            }
        }

        public double AltoMaximo
        {
            get => _desplazador.MaxHeight;
            set => _desplazador.MaxHeight = value;
        }

        public AppTile? PrimerVisible() =>
            _elementos.Children.OfType<AppTile>().FirstOrDefault(t => t.IsVisible);

        // ---------- contenido ----------

        private void AlCambiarStore()
        {
            if (_carpeta == null) return;

            // Si folders.json se recargó desde disco, los objetos son otros:
            // se vuelve a buscar la carpeta por nombre.
            if (!_store.Folders.Contains(_carpeta))
            {
                var misma = _store.Buscar(_carpeta.Name);
                if (misma == null)
                {
                    _carpeta = null;
                    Reconstruir();
                    CarpetaDesaparecida?.Invoke();
                    return;
                }
                _carpeta = misma;
            }
            Reconstruir();
        }

        private void Reconstruir()
        {
            _elementos.Children.Clear();
            var carpeta = _carpeta;

            if (carpeta != null)
            {
                for (int i = 0; i < carpeta.Apps.Count; i++)
                {
                    var app = carpeta.Apps[i];
                    int indice = i;
                    var tile = new AppTile(app, CrearMenu(carpeta, app));
                    tile.AbrirPedido += t => Lanzar(t.App);

                    Interacciones.HacerArrastrable(tile, () => new ElementoArrastrado(carpeta, app), EfectosAlSacar, AlTerminarArrastre);
                    Interacciones.AceptarSoltar(tile,
                        rutas => Agregar(rutas, indice),
                        elemento => Recolocar(elemento, indice),
                        resaltar: tile);

                    _elementos.Children.Add(tile);
                }
            }

            bool hay = carpeta?.Apps.Count > 0;
            _vacia.IsVisible = carpeta != null && !hay;
            _desplazador.IsVisible = hay;
            Filtrar();
        }

        private void Filtrar()
        {
            int visibles = 0;
            foreach (var tile in _elementos.Children.OfType<AppTile>())
            {
                tile.IsVisible = _filtro.Length == 0 || Coincide(tile.App.Name, _filtro);
                if (tile.IsVisible) visibles++;
            }
            _sinResultados.IsVisible = _carpeta?.Apps.Count > 0 && visibles == 0;
        }

        private static bool Coincide(string nombre, string texto) =>
            CultureInfo.CurrentCulture.CompareInfo.IndexOf(nombre, texto,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

        // ---------- acciones ----------

        public void Lanzar(AppShortcut app)
        {
            var error = AppLauncherService.Lanzar(app.Path);
            if (error == null) AppAbierta?.Invoke(app);
            else Avisar(error);
        }

        public void Agregar(IReadOnlyList<string> rutas, int? indice) => Intentar(() =>
        {
            if (_carpeta == null || rutas.Count == 0) return;
            var resultado = _store.AgregarElementos(_carpeta, rutas, indice);
            if (resultado.MotivoSiNadaEntro is string motivo) Avisar(motivo);
            else if (resultado.Agregados.Count > 0)
                Avisar(resultado.Agregados.Count == 1
                    ? $"«{resultado.Agregados[0].Name}» se agregó."
                    : $"Se agregaron {resultado.Agregados.Count} elementos.", esError: false);
        });

        // Soltado dentro de esta carpeta: si ya era suyo se reordena; si
        // viene de otra carpeta, se mueve aquí.
        private void Recolocar(ElementoArrastrado elemento, int indice) => Intentar(() =>
        {
            if (_carpeta == null) return;
            if (elemento.Carpeta == _carpeta) _store.ReordenarElemento(_carpeta, elemento.App, indice);
            else
            {
                _store.MoverElemento(elemento.App, elemento.Carpeta, _carpeta);
                _store.ReordenarElemento(_carpeta, elemento.App, indice);
            }
        });

        // Qué puede hacer Windows con el archivo si se suelta fuera de la app.
        // Nunca se permite mover lo que podría romper algo: el .exe de un
        // juego fuera de su carpeta, o una carpeta entera de otro sitio.
        private DragDropEffects EfectosAlSacar(ElementoArrastrado elemento)
        {
            var ruta = elemento.App.Path;
            if (_store.EsGestionado(elemento.App)) return DragDropEffects.Move;   // nuestra copia: vuelve tal cual
            if (EstaEnEscritorio(ruta)) return DragDropEffects.Move;              // ya vive ahí: no se duplica
            if (Directory.Exists(ruta) || EsPrograma(ruta)) return DragDropEffects.Link;
            return DragDropEffects.Copy | DragDropEffects.Link;                    // un documento: se copia
        }

        // Se soltó fuera de NovaFolder: si el archivo se movió, o si era uno
        // del Escritorio que se soltó en el Escritorio, sale de la carpeta.
        private void AlTerminarArrastre(ElementoArrastrado elemento, DragDropEffects efecto) => Intentar(() =>
        {
            if (elemento.SoltadoDentro || efecto == DragDropEffects.None) return;

            bool salio = _store.QuitarSiYaNoExiste(elemento.Carpeta, elemento.App);
            if (!salio && EstaEnEscritorio(elemento.App.Path))
            {
                _store.QuitarElemento(elemento.Carpeta, elemento.App);
                salio = true;
            }
            if (salio) Avisar($"«{elemento.App.Name}» salió de la carpeta.", esError: false);
        });

        private bool EstaEnEscritorio(string ruta) =>
            string.Equals(Path.GetDirectoryName(ruta), _store.Escritorio, StringComparison.OrdinalIgnoreCase);

        private static bool EsPrograma(string ruta) =>
            Path.GetExtension(ruta).Equals(".exe", StringComparison.OrdinalIgnoreCase);

        private void Avisar(string? texto, bool esError = true) => AvisoPedido?.Invoke(texto, esError);

        // Los errores esperables (validación, carpeta que ya no existe) se
        // avisan; los inesperados van al manejador global, que los registra.
        private void Intentar(Action accion)
        {
            try { accion(); }
            catch (NovaFolderException ex) { Avisar(ex.Message); }
        }

        // ---------- menú contextual de un elemento ----------

        private ContextMenu CrearMenu(AppFolder carpeta, AppShortcut app)
        {
            var mover = new MenuItem
            {
                Header = "Mover a",
                Icon = new TextBlock { Text = "\uE8DE", FontFamily = Interacciones.FuenteIconos }
            };
            foreach (var otra in _store.Folders.Where(f => f != carpeta))
            {
                var destino = otra;
                mover.Items.Add(Interacciones.Opcion(destino.Name, "\uE8B7",
                    () => Intentar(() => _store.MoverElemento(app, carpeta, destino))));
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
                        if (error == null) AppAbierta?.Invoke(app); else Avisar(error);
                    }),
                    mover,
                    new Separator(),
                    // Si NovaFolder lo sacó del Escritorio, "quitar" es devolverlo;
                    // si nunca salió de su sitio, solo deja de estar en la carpeta.
                    _store.EsGestionado(app)
                        ? Interacciones.Opcion("Devolver al Escritorio", "\uE8A0", () => Intentar(() =>
                        {
                            _store.SacarAlEscritorio(carpeta, app);
                            Avisar($"«{app.Name}» volvió al Escritorio.", esError: false);
                        }))
                        : Interacciones.Opcion("Quitar de la carpeta", "\uE711",
                            () => Intentar(() => _store.QuitarElemento(carpeta, app)))
                }
            };
            menu.Opened += (_, _) => MenuAbierto?.Invoke();
            menu.Closed += (_, _) => MenuCerrado?.Invoke();
            return menu;
        }
    }
}
