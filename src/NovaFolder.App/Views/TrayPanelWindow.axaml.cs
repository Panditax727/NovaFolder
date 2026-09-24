using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using NovaFolder.Controls;
using NovaFolder.Core.Errors;
using NovaFolder.Core.Models;
using NovaFolder.Core.Storage;
using NovaFolder.Services.Windows;

namespace NovaFolder.Views
{
    // Lo que el panel de la bandeja puede pedir al resto de la app. Se
    // pasa entero desde App: el panel no conoce ventanas ni servicios.
    public sealed record AccionesBandeja(
        Action<AppFolder, PixelRect> AbrirCarpeta,
        Action NuevaCarpeta,
        Func<bool> WidgetVisible,
        Action<bool> MostrarWidget,
        Action<bool> Autoinicio,
        Action<bool> LimpiarEscritorio,
        Action GuardarAccesosDelEscritorio,
        Func<Task<string>> BuscarActualizaciones,
        Action ReiniciarParaActualizar,
        string Version,
        Func<string?> VersionLista,
        Action MostrarAyuda,
        Action AbrirDatos,
        Action Salir);

    // Panel que se abre al hacer clic en el ícono de NovaFolder junto al
    // reloj. Es el "centro de control" cuando el widget está oculto: tus
    // carpetas a un clic, los ajustes con interruptores y las acciones de
    // la app.
    //
    // Por qué no se usa TrayIcon.Menu: en Avalonia 12, TrayIconImpl crea la
    // ventana del menú y llama a Show() sin activarla. En Win32 un menú de
    // bandeja tiene que pasar a primer plano o el sistema lo cierra en cuanto
    // el puntero se mueve, así que aparecía y desaparecía sin dejar elegir.
    public partial class TrayPanelWindow : Window
    {
        private const int MargenBandeja = 10;
        private const int MaxCarpetasVisibles = 6;

        private readonly FolderStore _store = null!;
        private readonly AccionesBandeja _acciones = null!;
        private readonly PixelPoint? _puntoDeApertura;
        private bool _cargando;

        public TrayPanelWindow() => InitializeComponent();

        public TrayPanelWindow(FolderStore store, AccionesBandeja acciones)
        {
            InitializeComponent();
            _store = store;
            _acciones = acciones;

            if (OperatingSystem.IsWindows()) _puntoDeApertura = PunteroService.Posicion();

            TransparencyLevelHint = new List<WindowTransparencyLevel>
            {
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Blur,
                WindowTransparencyLevel.Transparent
            };

            ConectarEventos();
            Cargar();
        }

        private void ConectarEventos()
        {
            BotonAyuda.Click += (_, _) => CerrarY(_acciones.MostrarAyuda);
            BotonNueva.Click += (_, _) => CerrarY(_acciones.NuevaCarpeta);
            BotonVerTodas.Click += (_, _) => CerrarY(() => _acciones.MostrarWidget(true));
            BotonReiniciar.Click += (_, _) => _acciones.ReiniciarParaActualizar();
            BotonActualizar.Click += async (_, _) => await BuscarActualizaciones();
            BotonDatos.Click += (_, _) => CerrarY(_acciones.AbrirDatos);
            BotonSalir.Click += (_, _) => CerrarY(_acciones.Salir);
            BotonGuardarPendientes.Click += (_, _) =>
            {
                _acciones.GuardarAccesosDelEscritorio();
                ActualizarPendientes();
                CargarCarpetas();
            };

            InterruptorWidget.IsCheckedChanged += (_, _) =>
            {
                if (!_cargando) _acciones.MostrarWidget(InterruptorWidget.IsChecked == true);
            };
            InterruptorLimpiar.IsCheckedChanged += (_, _) =>
            {
                if (_cargando) return;
                _acciones.LimpiarEscritorio(InterruptorLimpiar.IsChecked == true);
                ActualizarPendientes();
            };
            InterruptorInicio.IsCheckedChanged += (_, _) =>
            {
                if (!_cargando) _acciones.Autoinicio(InterruptorInicio.IsChecked == true);
            };

            // Se recoloca en CADA SizeChanged: con SizeToContent la ventana
            // pasa por su tamaño por defecto antes de encogerse al del contenido.
            SizeChanged += (_, _) =>
            {
                if (!OperatingSystem.IsWindows() || Bounds.Height <= 0) return;
                PunteroService.ColocarSobreElPuntero(this, MargenBandeja, _puntoDeApertura);
            };

            // Sin activar, la ventana no toma el foco y Deactivated no
            // llegaría: el panel quedaría abierto para siempre.
            Opened += (_, _) =>
            {
                Activate();
                Dispatcher.UIThread.Post(() =>
                {
                    Tarjeta.Opacity = 1;
                    Tarjeta.RenderTransform = TransformOperations.Parse("translateY(0px)");
                }, DispatcherPriority.Background);
            };
            Deactivated += (_, _) => Close();
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }

        private void Cargar()
        {
            _cargando = true;

            InterruptorWidget.IsChecked = _acciones.WidgetVisible();
            InterruptorLimpiar.IsChecked = _store.CleanDesktop;
            InterruptorInicio.IsChecked = _store.StartWithWindows;

            MostrarEstadoActualizacion(null);
            CargarCarpetas();
            ActualizarPendientes();

            _cargando = false;
        }

        private void CargarCarpetas()
        {
            Carpetas.Children.Clear();
            foreach (var folder in _store.Folders.Take(MaxCarpetasVisibles))
            {
                var tile = new FolderTile(folder) { ContextMenu = null };
                tile.AbrirPedido += t => CerrarY(() => _acciones.AbrirCarpeta(t.Folder, t.RectEnPantalla()));
                tile.ArchivosSoltados += (t, rutas) => Intentar(() => _store.AgregarElementos(t.Folder, rutas));
                tile.ElementoSoltado += (t, e) => Intentar(() => _store.MoverElemento(e.App, e.Carpeta, t.Folder));
                Carpetas.Children.Add(tile);
            }

            int ocultas = _store.Folders.Count - MaxCarpetasVisibles;
            BotonVerTodas.IsVisible = ocultas > 0;
            BotonVerTodas.Content = ocultas == 1 ? "Ver 1 carpeta más en el widget" : $"Ver {ocultas} carpetas más en el widget";
            SinCarpetas.IsVisible = _store.Folders.Count == 0;
        }

        // Con "Limpiar" activo, si quedan accesos de carpetas en el
        // Escritorio (de antes de activarlo) se ofrece guardarlos.
        private void ActualizarPendientes()
        {
            int n = _store.CleanDesktop ? _store.ContarAccesosEnEscritorio() : 0;
            BotonGuardarPendientes.IsVisible = n > 0;
            BotonGuardarPendientes.Content = n == 1
                ? "1 acceso de tus carpetas sigue en el Escritorio · Guardarlo"
                : $"{n} accesos de tus carpetas siguen en el Escritorio · Guardarlos";
        }

        private void MostrarEstadoActualizacion(string? mensaje)
        {
            var lista = _acciones.VersionLista();
            BannerActualizacion.IsVisible = lista != null;
            TextoActualizacion.Text = $"NovaFolder {lista} está lista para instalarse.";
            Estado.Text = mensaje ?? $"Versión {_acciones.Version}";
            BotonActualizar.IsVisible = lista == null;
        }

        private async Task BuscarActualizaciones()
        {
            BotonActualizar.IsEnabled = false;
            TextoBotonActualizar.Text = "Buscando…";
            try
            {
                var mensaje = await _acciones.BuscarActualizaciones();
                MostrarEstadoActualizacion(mensaje);
            }
            finally
            {
                BotonActualizar.IsEnabled = true;
                TextoBotonActualizar.Text = "Actualizar";
            }
        }

        private void Intentar(Action accion)
        {
            try
            {
                accion();
                CargarCarpetas();
            }
            catch (NovaFolderException ex) { Estado.Text = ex.Message; }
        }

        // Cerrar antes de actuar: si la acción abre otra ventana, el panel
        // ya no está de por medio robándole el foco.
        private void CerrarY(Action accion)
        {
            Close();
            accion();
        }
    }
}
