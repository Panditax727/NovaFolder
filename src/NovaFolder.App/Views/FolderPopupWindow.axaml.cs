using System;
using System.Collections.Generic;
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
using NovaFolder.Core.Storage;
using NovaFolder.Services.Windows;

namespace NovaFolder.Views
{
    // Una carpeta abierta, al estilo Android: una tarjeta flotante con su
    // contenido que se cierra al hacer clic afuera, con Escape o al abrir
    // algo. La abren el widget y los accesos directos del Escritorio; la
    // rejilla de elementos es VistaCarpeta, la misma de la ventana principal.
    public partial class FolderPopupWindow : Window
    {
        private const int Columnas = 4;
        private const int UmbralBuscador = 9;   // a partir de aquí aparece el filtro
        private const double AltoMaximoLista = 420;
        private const int MargenAncla = 8;

        private readonly FolderStore _store = null!;
        private readonly VistaCarpeta _vista = null!;
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

            Raiz.Width = Columnas * AppTile.Ancho + 4;
            _vista = new VistaCarpeta(store) { AltoMaximo = AltoMaximoLista };
            Contenido.Content = _vista;

            ConectarEventos();
            _vista.Carpeta = folder;
            ActualizarCabecera();
        }

        private AppFolder Carpeta => _vista.Carpeta!;

        // Para carpetas recién creadas: se abren ya con el nombre en edición.
        public void EmpezarRenombradoAlAbrir() => Opened += (_, _) => EmpezarRenombrado();

        private void ConectarEventos()
        {
            _vista.AvisoPedido += MostrarAviso;
            _vista.AppAbierta += _ => Close();   // como en Android: abrir algo cierra la carpeta
            _vista.AgregarPedido += () => _ = AgregarConSelector();
            _vista.CarpetaDesaparecida += Close;
            _vista.MenuAbierto += () => _bloqueosCierre++;
            _vista.MenuCerrado += AlCerrarMenu;

            _store.Changed += AlCambiarStore;
            Closed += (_, _) => _store.Changed -= AlCambiarStore;

            Titulo.PointerPressed += (_, e) => { e.Handled = true; EmpezarRenombrado(); };
            EditorTitulo.KeyDown += EditorTitulo_KeyDown;
            EditorTitulo.LostFocus += (_, _) => ConfirmarRenombrado(desdePerdidaDeFoco: true);

            BotonAgregar.Click += async (_, _) => await AgregarConSelector();
            BotonMas.Flyout = CrearMenuMas();

            Buscador.TextChanged += (_, _) => _vista.Filtro = Buscador.Text ?? "";
            Buscador.KeyDown += Buscador_KeyDown;

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

        // VistaCarpeta ya se redibuja sola; aquí solo la cabecera.
        private void AlCambiarStore()
        {
            if (_vista.Carpeta != null) ActualizarCabecera();
        }

        private void ActualizarCabecera()
        {
            Title = Carpeta.Name;
            if (!_renombrando) Titulo.Text = Carpeta.Name;
            Ayuda.IsVisible = Carpeta.Apps.Count > 0;
            Buscador.IsVisible = Carpeta.Apps.Count >= UmbralBuscador;
        }

        // ---- acciones ----

        private async Task AgregarConSelector()
        {
            var archivos = await ConBloqueo(() => StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Agregar a «{Carpeta.Name}»",
                AllowMultiple = true
            }));
            _vista.Agregar(RutasLocales(archivos), null);
        }

        private async Task AgregarCarpetaDelDisco()
        {
            var carpetas = await ConBloqueo(() => StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = $"Agregar una carpeta a «{Carpeta.Name}»",
                AllowMultiple = true
            }));
            _vista.Agregar(RutasLocales(carpetas), null);
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

        // esError: rojo para problemas; neutro para confirmaciones.
        private void MostrarAviso(string? texto, bool esError = true)
        {
            Aviso.Text = texto;
            Aviso.IsVisible = !string.IsNullOrEmpty(texto);
            Aviso.Classes.Set("error", esError);
            Aviso.Classes.Set("suave", !esError);
        }

        private void Intentar(Action accion)
        {
            try { accion(); }
            catch (NovaFolderException ex) { MostrarAviso(ex.Message); }
        }

        // ---- menús ----

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
                    Interacciones.Opcion("Eliminar carpeta (no borra los archivos)", "\uE74D", () => Intentar(() => _store.EliminarCarpeta(Carpeta)))
                }
            };
            menu.Opened += (_, _) => _bloqueosCierre++;
            menu.Closed += (_, _) => AlCerrarMenu();
            return menu;
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

            EditorTitulo.Text = Carpeta.Name;
            Titulo.IsVisible = false;
            EditorTitulo.IsVisible = true;
            EditorTitulo.Focus();
            EditorTitulo.SelectAll();
        }

        private void ConfirmarRenombrado(bool desdePerdidaDeFoco)
        {
            if (!_renombrando || _vista.Carpeta == null) return;

            try
            {
                _store.RenombrarCarpeta(Carpeta, EditorTitulo.Text ?? "");
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
            if (_vista.Carpeta != null) Titulo.Text = Carpeta.Name;
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
                case Key.Enter when _vista.PrimerVisible() is AppTile primero:
                    e.Handled = true;
                    _vista.Lanzar(primero.App);
                    break;
                case Key.Down when _vista.PrimerVisible() is AppTile primero:
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
