using Avalonia.Controls;
using NovaFolder.Services.Applications;
using NovaFolder.Services.Updates;
using NovaFolder.Services.Windows;
using NovaFolder.Views.Principal;

namespace NovaFolder.Views.Paginas
{
    // Todas las preferencias en un solo sitio. Cada interruptor se aplica
    // y se guarda al instante; no hay botón "Guardar".
    public partial class AjustesPagina : UserControl
    {
        private readonly ServiciosApp _servicios = null!;
        private readonly IAvisador _aviso = null!;
        private bool _cargando;

        public AjustesPagina() => InitializeComponent();

        public AjustesPagina(ServiciosApp servicios, IAvisador aviso)
        {
            InitializeComponent();
            _servicios = servicios;
            _aviso = aviso;
            var store = servicios.Store;

            InterruptorLimpiar.IsCheckedChanged += (_, _) => Si(() => servicios.LimpiarEscritorio(InterruptorLimpiar.IsChecked == true));
            InterruptorCarpetas.IsCheckedChanged += (_, _) => Si(() => store.EstablecerCarpetasEnEscritorio(InterruptorCarpetas.IsChecked == true));
            InterruptorWidget.IsCheckedChanged += (_, _) => Si(() => servicios.MostrarWidget(InterruptorWidget.IsChecked == true));
            InterruptorInicio.IsCheckedChanged += (_, _) => Si(() => servicios.Autoinicio(InterruptorInicio.IsChecked == true));

            BotonActualizar.Click += async (_, _) =>
            {
                if (UpdateService.DeLaTienda)
                {
                    AppLauncherService.Lanzar(UpdateService.EnlaceTienda);
                    return;
                }
                if (servicios.Actualizaciones.VersionLista != null)
                {
                    servicios.Actualizaciones.ReiniciarEInstalar();
                    return;
                }
                BotonActualizar.IsEnabled = false;
                EstadoActualizacion.Text = "Buscando…";
                EstadoActualizacion.Text = await servicios.Actualizaciones.BuscarAsync();
                BotonActualizar.IsEnabled = true;
                Cargar();
            };
            BotonDatos.Click += (_, _) => AppLauncherService.Lanzar(servicios.Rutas.Datos);
            BotonWeb.Click += (_, _) => AppLauncherService.Lanzar(UpdateService.RepositorioGitHub);
            BotonSalir.Click += (_, _) => servicios.Salir();

            // Se confirma en la barra de avisos: un clic suelto no desinstala nada.
            BotonDesinstalar.Click += (_, _) => _aviso.Avisar(
                "¿Seguro que quieres desinstalar NovaFolder?", esError: true,
                textoAccion: "Sí, desinstalar", accion: Desinstalar);

            // Se refresca al volver a la página: el widget pudo ocultarse desde
            // su propio menú, o la bienvenida pudo cambiar algo.
            AttachedToVisualTree += (_, _) => Cargar();
        }

        private void Cargar()
        {
            _cargando = true;
            var store = _servicios.Store;
            InterruptorLimpiar.IsChecked = store.CleanDesktop;
            InterruptorCarpetas.IsChecked = store.DesktopFolders;
            InterruptorWidget.IsChecked = _servicios.WidgetVisible();
            InterruptorInicio.IsChecked = store.StartWithWindows;

            var actualizaciones = _servicios.Actualizaciones;
            Version.Text = $"NovaFolder {actualizaciones.VersionActual}";
            RutaDatos.Text = _servicios.Rutas.Datos;
            if (actualizaciones.VersionLista is string lista)
            {
                EstadoActualizacion.Text = $"La versión {lista} está descargada.";
                BotonActualizar.Content = "Reiniciar y actualizar";
            }
            else
            {
                if (UpdateService.DeLaTienda)
                {
                    EstadoActualizacion.Text = "Microsoft Store mantiene NovaFolder actualizado.";
                    BotonActualizar.Content = "Ver en la Store";
                    _cargando = false;
                    return;
                }
                if (string.IsNullOrEmpty(EstadoActualizacion.Text))
                    EstadoActualizacion.Text = actualizaciones.EstaInstalada
                        ? "NovaFolder se actualiza solo en segundo plano."
                        : "Esta copia no se instaló con el instalador: no se actualiza sola.";
                BotonActualizar.Content = "Buscar ahora";
            }
            _cargando = false;
        }

        private void Desinstalar()
        {
            switch (DesinstaladorService.Desinstalar())
            {
                case ResultadoDesinstalar.Lanzado:
                    // El desinstalador espera a que NovaFolder se cierre.
                    _servicios.Salir();
                    break;
                case ResultadoDesinstalar.ConfiguracionAbierta:
                    _aviso.Avisar("Busca NovaFolder en la lista de Aplicaciones de Windows y pulsa Desinstalar.");
                    break;
                default:
                    _aviso.Avisar("Esta copia no está instalada (es de desarrollo): basta con borrar su carpeta.", esError: true);
                    break;
            }
        }

        // Ignora los cambios que hace Cargar al poner los valores iniciales.
        private void Si(System.Action accion)
        {
            if (_cargando) return;
            _aviso.Avisar("Guardado.");
            accion();   // si la acción tiene algo más útil que decir, lo dice después
        }
    }
}
