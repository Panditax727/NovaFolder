using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NovaFolder.Controls;
using NovaFolder.Core.Errors;
using NovaFolder.Core.Models;
using NovaFolder.Services.Applications;
using NovaFolder.Core.Storage;
using NovaFolder.Services.Windows;
using NovaFolder.Core.Diagnostics;

namespace NovaFolder.Views
{
    // El widget de escritorio: la rejilla de carpetas. Abrir una carpeta
    // no la expande aquí dentro (eso reacomodaba las demás tarjetas bajo el
    // cursor): se le pide a App que abra el popup junto a la tarjeta.
    public partial class WidgetWindow : Window
    {
        private const int Columnas = 3;
        private const double AltoMaximoRejilla = 520;

        // Esquina donde vive el widget mientras no haya una posición guardada.
        private static readonly PixelPoint PosicionPorDefecto = new(40, 40);

        private readonly FolderStore _store = null!;
        private readonly Action<AppFolder, PixelRect?, bool> _abrirCarpeta = null!;

        private DispatcherTimer? _mantenerAlFondo;
        private readonly DispatcherTimer _guardarPosicion = new() { Interval = TimeSpan.FromMilliseconds(600) };
        private readonly DispatcherTimer _ocultarDeshacer = new() { Interval = TimeSpan.FromSeconds(7) };
        private Action? _deshacer;

        // Los decide App: mostrar la bienvenida, y ocultar el widget
        // recordándolo y avisando la primera vez de que sigue en la bandeja.
        public event Action? AyudaPedida;
        public event Action? OcultarPedido;
        public event Action? AbrirAppPedido;

        public WidgetWindow() => InitializeComponent();

        // abrirCarpeta(carpeta, ancla, empezarRenombrando)
        public WidgetWindow(FolderStore store, Action<AppFolder, PixelRect?, bool> abrirCarpeta)
        {
            InitializeComponent();
            _store = store;
            _abrirCarpeta = abrirCarpeta;

            // Ancho exacto de N tarjetas: así la rejilla nunca queda con una
            // columna a medias ni con un hueco a la derecha.
            Width = Columnas * (FolderTile.Ancho + 8) + 20;
            Desplazador.MaxHeight = AltoMaximoRejilla;
            Position = _store.PosicionVentana is (int x, int y) ? new PixelPoint(x, y) : PosicionPorDefecto;

            AplicarFondoDelSistema();
            ConectarEventos();
            Reconstruir();
        }

        private void ConectarEventos()
        {
            _store.Changed += Reconstruir;

            BotonNueva.Click += (_, _) => NuevaCarpeta();
            BotonPrimera.Click += (_, _) => NuevaCarpeta();
            BotonAyuda.Click += (_, _) => AyudaPedida?.Invoke();
            BotonMas.Flyout = CrearMenuMas();
            BotonDeshacer.Click += (_, _) =>
            {
                _deshacer?.Invoke();
                OcultarDeshacer();
            };
            _ocultarDeshacer.Tick += (_, _) => OcultarDeshacer();

            // Mover el widget arrastrando el encabezado (no hay barra de título).
            Encabezado.PointerPressed += (_, e) =>
            {
                if (e.Source is Visual v && v.FindAncestorOfType<Button>(includeSelf: true) != null) return;
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
            };

            // Se guarda cuando el usuario deja de mover, no en cada píxel.
            PositionChanged += (_, _) =>
            {
                _guardarPosicion.Stop();
                _guardarPosicion.Start();
            };
            _guardarPosicion.Tick += (_, _) =>
            {
                _guardarPosicion.Stop();
                _store.GuardarPosicion(Position.X, Position.Y);
            };

            ConectarSoltarParaCrear();

            Opened += (_, _) =>
            {
                AsegurarVisibleEnPantalla();
                IniciarComoWidgetDeEscritorio();
            };

            // Al dejar de usarlo vuelve detrás de las demás ventanas.
            Deactivated += (_, _) => EnviarAlFondo();

            // No tiene botón de cerrar, pero Alt+F4 sí llega: se oculta en
            // vez de cerrarse, y se recupera desde el ícono de la bandeja.
            Closing += (_, e) =>
            {
                if (e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown) return;
                e.Cancel = true;
                OcultarPedido?.Invoke();
            };
        }

        private void Reconstruir()
        {
            FoldersPanel.Children.Clear();

            foreach (var folder in _store.Folders)
            {
                var tile = new FolderTile(folder);
                tile.AbrirPedido += t => _abrirCarpeta(t.Folder, t.RectEnPantalla(), false);
                tile.RenombrarPedido += t => _abrirCarpeta(t.Folder, t.RectEnPantalla(), true);
                tile.AgregarPedido += t => AgregarConSelector(t.Folder);
                tile.EliminarPedido += t => Intentar(() => Eliminar(t.Folder));
                tile.ArchivosSoltados += (t, rutas) => Intentar(() => AgregarConAviso(t.Folder, rutas));
                tile.ElementoSoltado += (t, e) => Intentar(() => MoverConAviso(e, t.Folder));
                FoldersPanel.Children.Add(tile);
            }

            Vacio.IsVisible = _store.Folders.Count == 0;
            PistaVacias.IsVisible = _store.Folders.Count > 0 && _store.Folders.All(f => f.Apps.Count == 0);
        }

        private FolderTile? TileDe(AppFolder folder) =>
            FoldersPanel.Children.OfType<FolderTile>().FirstOrDefault(t => t.Folder == folder);

        // ---- acciones ----

        public void NuevaCarpeta(IReadOnlyList<string>? rutas = null)
        {
            if (!IsVisible) Show();
            Intentar(() => AbrirRecienCreada(_store.CrearCarpeta(rutas: rutas)));
        }

        // Una carpeta nueva se abre con el nombre ya en edición.
        private void AbrirRecienCreada(AppFolder folder)
        {
            // La tarjeta nueva aún no tiene posición en pantalla hasta el
            // siguiente pase de layout; se espera a él para anclar el popup.
            Dispatcher.UIThread.Post(() =>
            {
                var tile = TileDe(folder);
                tile?.BringIntoView();
                _abrirCarpeta(folder, tile?.RectEnPantalla(), true);
            }, DispatcherPriority.Background);
        }

        private async void AgregarConSelector(AppFolder folder)
        {
            try
            {
                var archivos = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = $"Agregar a «{folder.Name}»",
                    AllowMultiple = true
                });
                var rutas = archivos.Select(a => a.TryGetLocalPath()).Where(p => p != null).Select(p => p!).ToList();
                if (rutas.Count > 0) AgregarConAviso(folder, rutas);
            }
            catch (NovaFolderException ex)
            {
                MostrarAviso(ex.Message);
            }
            catch (Exception ex)
            {
                // async void: si se escapara, tumbaría el proceso.
                Log.Error("Error al agregar elementos con el selector.", ex);
                MostrarAviso("No se pudieron agregar los elementos.");
            }
        }

        private void Eliminar(AppFolder folder)
        {
            int indice = _store.EliminarCarpeta(folder);
            MostrarAviso($"Se eliminó «{folder.Name}»", "Deshacer", () => _store.RestaurarCarpeta(folder, indice));
        }

        // Soltar archivos sobre una tarjeta: se confirma qué entró, porque
        // la tarjeta solo cambia su miniatura y con 5+ elementos ni eso.
        public void AgregarConAviso(AppFolder folder, IReadOnlyList<string> rutas)
        {
            var resultado = _store.AgregarElementos(folder, rutas);
            if (resultado.MotivoSiNadaEntro is string motivo)
            {
                MostrarAviso(motivo);
                return;
            }

            var agregados = resultado.Agregados;
            var que = agregados.Count == 1 ? $"«{agregados[0].Name}»" : $"{agregados.Count} elementos";
            MostrarAviso($"{que} → {folder.Name}", "Deshacer", () => _store.QuitarElementos(folder, agregados));
        }

        private void MoverConAviso(ElementoArrastrado elemento, AppFolder destino)
        {
            if (elemento.Carpeta == destino) return;
            int posicion = elemento.Carpeta.Apps.IndexOf(elemento.App);
            _store.MoverElemento(elemento.App, elemento.Carpeta, destino);
            MostrarAviso($"«{elemento.App.Name}» → {destino.Name}", "Deshacer", () =>
            {
                _store.MoverElemento(elemento.App, destino, elemento.Carpeta);
                _store.ReordenarElemento(elemento.Carpeta, elemento.App, posicion);
            });
        }

        // Barra inferior del widget: confirma la última acción y, si hay
        // algo que hacer con ella (Deshacer, Reiniciar...), ofrece el botón
        // durante unos segundos.
        public void MostrarAviso(string texto, string? textoAccion = null, Action? accion = null)
        {
            _deshacer = accion == null ? null : () => Intentar(accion);
            TextoDeshacer.Text = texto;
            ToolTip.SetTip(TextoDeshacer, texto);
            BotonDeshacer.Content = textoAccion;
            BotonDeshacer.IsVisible = accion != null;
            BarraDeshacer.IsVisible = true;
            _ocultarDeshacer.Stop();
            _ocultarDeshacer.Start();
        }

        // Errores esperables (validación, carpeta que ya no existe): se
        // muestran en la barra. Los inesperados van al manejador global.
        private void Intentar(Action accion)
        {
            try { accion(); }
            catch (NovaFolderException ex) { MostrarAviso(ex.Message); }
        }

        private void OcultarDeshacer()
        {
            _ocultarDeshacer.Stop();
            _deshacer = null;
            BarraDeshacer.IsVisible = false;
        }

        private MenuFlyout CrearMenuMas() => new()
        {
            Items =
            {
                Interacciones.Opcion("Nueva carpeta", "\uE8F4", () => NuevaCarpeta()),
                Interacciones.Opcion("Abrir NovaFolder", "\uE8A7", () => AbrirAppPedido?.Invoke()),
                Interacciones.Opcion("C\u00F3mo se usa", "\uE897", () => AyudaPedida?.Invoke()),
                Interacciones.Opcion("Ocultar widget", "\uED1A", () => OcultarPedido?.Invoke()),
                new Separator(),
                Interacciones.Opcion("Salir de NovaFolder", "\uE7E8", () =>
                    (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown())
            }
        };

        // Soltar archivos sobre el fondo del widget (no sobre una carpeta)
        // crea una carpeta nueva con ellos. Las tarjetas manejan su propio
        // soltar y marcan el evento como atendido; aquí se escucha igual
        // (handledEventsToo) solo para esconder el aviso cuando se está
        // encima de una tarjeta.
        private void ConectarSoltarParaCrear()
        {
            DragDrop.SetAllowDrop(this, true);

            AddHandler(DragDrop.DragOverEvent, (object? _, DragEventArgs e) =>
            {
                bool sobreCarpeta = e.Source is Visual v && v.FindAncestorOfType<FolderTile>(includeSelf: true) != null;
                var elemento = Interacciones.ElementoDe(e);
                bool acepta = Interacciones.HayArchivos(e) || elemento != null;
                ZonaNueva.IsVisible = acepta && !sobreCarpeta;

                if (e.Handled) return;
                e.DragEffects = elemento != null ? DragDropEffects.Move
                              : acepta ? DragDropEffects.Link
                              : DragDropEffects.None;
                e.Handled = true;
            }, RoutingStrategies.Bubble, handledEventsToo: true);

            AddHandler(DragDrop.DragLeaveEvent, (object? _, DragEventArgs _) => ZonaNueva.IsVisible = false);

            AddHandler(DragDrop.DropEvent, (object? _, DragEventArgs e) =>
            {
                ZonaNueva.IsVisible = false;

                if (Interacciones.ElementoDe(e) is ElementoArrastrado elemento)
                {
                    e.Handled = true;
                    Intentar(() => AbrirRecienCreada(_store.CrearCarpetaMoviendo(elemento.Carpeta, elemento.App)));
                    return;
                }

                var rutas = Interacciones.RutasDe(e);
                if (rutas.Count == 0) return;
                e.Handled = true;
                NuevaCarpeta(rutas);
            });
        }

        // ---- integración con el escritorio ----

        // Si la posición guardada quedó fuera de pantalla (se desconectó un
        // monitor, cambió la resolución), el widget sería inalcanzable.
        private void AsegurarVisibleEnPantalla()
        {
            var visible = Screens.All.Any(s => s.WorkingArea.Contains(Position + new PixelVector(24, 24)));
            if (!visible) Position = PosicionPorDefecto;
        }

        // NovaFolder no vive insertada dentro del Escritorio real (ver
        // DesktopIntegrationService para el porqué). En su lugar se
        // comporta como un widget: se manda sola al fondo del z-order, así
        // cualquier ventana que uses la tapa y vuelve a aparecer en cuanto
        // la cierras, minimizas todo, o muestras el Escritorio.
        private void IniciarComoWidgetDeEscritorio()
        {
            if (!OperatingSystem.IsWindows()) return;

            EnviarAlFondo();

            _mantenerAlFondo = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _mantenerAlFondo.Tick += (_, _) =>
            {
                // Mientras el usuario lo está usando (menú abierto, moviéndolo)
                // no se le quita de debajo del ratón.
                if (!IsActive) EnviarAlFondo();
            };
            _mantenerAlFondo.Start();
        }

        private void EnviarAlFondo()
        {
            if (!OperatingSystem.IsWindows()) return;

            // Con un popup o el menú de bandeja abiertos, mandar el widget al
            // fondo los hundiría a ellos también (comparten ventana dueña;
            // ver DesktopIntegrationService). El temporizador lo reintenta
            // en cuanto se cierren.
            if (HayOtraVentanaAbierta()) return;

            var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd != IntPtr.Zero)
                DesktopIntegrationService.MantenerAlFondo(hwnd);
        }

        private bool HayOtraVentanaAbierta() =>
            Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.Windows.Any(w => w != this && w.IsVisible);

        // Mica es el material de Windows 11: tiñe la ventana con el fondo de
        // escritorio en vez de desenfocar lo que haya debajo, así que no
        // "arrastra" las ventanas de atrás al moverla. Avalonia 12 lo expone
        // directamente, sin P/Invoke a DwmSetWindowAttribute.
        //
        // La lista es por orden de preferencia: el sistema coge la primera que
        // pueda dar. En Linux normalmente cae a Transparent o a ninguna, por eso
        // existe FondoRespaldo.
        private void AplicarFondoDelSistema()
        {
            TransparencyLevelHint = new List<WindowTransparencyLevel>
            {
                WindowTransparencyLevel.Mica,
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Blur
            };

            // ActualTransparencyLevel solo es fiable cuando la ventana ya existe.
            Opened += (_, _) =>
            {
                var nivel = ActualTransparencyLevel;
                var hayMaterial = nivel == WindowTransparencyLevel.Mica
                               || nivel == WindowTransparencyLevel.AcrylicBlur
                               || nivel == WindowTransparencyLevel.Blur;

                FondoRespaldo.IsVisible = !hayMaterial;
            };
        }
    }
}
