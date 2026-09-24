using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using NovaFolder.Core;
using NovaFolder.Core.Diagnostics;
using NovaFolder.Core.Errors;
using NovaFolder.Core.Models;
using NovaFolder.Core.Storage;
using NovaFolder.Services.Applications;
using NovaFolder.Services.Updates;
using NovaFolder.Services.Windows;
using NovaFolder.Views;

namespace NovaFolder
{
    // Raíz de composición: crea los servicios, las ventanas y el ícono de
    // bandeja, y los conecta. Es el único sitio que conoce a todos; cada
    // pieza por separado solo conoce lo que recibe.
    public partial class App : Application
    {
        private RutasApp _rutas = null!;
        private FolderStore _store = null!;
        private UpdateService _actualizaciones = null!;
        private MainWindow _ventana = null!;

        // Referencia viva obligatoria: si TrayIcon se pierde, el recolector de
        // basura la destruye y el ícono desaparece de la bandeja sin avisar.
        private TrayIcon? _trayIcon;

        // El panel de bandeja, la carpeta y la bienvenida abiertos ahora
        // mismo, si los hay: nunca se apilan dos del mismo tipo.
        private TrayPanelWindow? _panelBandeja;
        private FolderPopupWindow? _popup;
        private BienvenidaWindow? _bienvenida;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // La ventana principal solo se oculta; la app vive hasta "Salir".
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Dispatcher.UIThread.UnhandledException += AlFallarInterfaz;

                _rutas = Program.Rutas;
                _store = FolderStore.Crear(_rutas);
                _store.VigilarCambiosExternos();
                _store.Changed += SincronizarEscritorio;

                // No se asigna como MainWindow del ciclo de vida: Avalonia la
                // mostraría siempre al arrancar, y el usuario puede haberla
                // ocultado (vive en la bandeja).
                _ventana = new MainWindow(_store, AbrirCarpeta);
                _ventana.AyudaPedida += MostrarBienvenida;
                _ventana.OcultarPedido += OcultarWidget;
                if (_store.ShowWidget) _ventana.Show();

                _store.ErrorDeGuardado += mensaje =>
                    Dispatcher.UIThread.Post(() => _ventana.MostrarAviso(mensaje));

                _actualizaciones = new UpdateService();
                _actualizaciones.ActualizacionLista += version => Dispatcher.UIThread.Post(() =>
                    Avisar($"NovaFolder {version} está lista",
                        "Se instalará sola la próxima vez que se inicie, o ahora mismo si reinicias.",
                        "Reiniciar", _actualizaciones.ReiniciarEInstalar));
                _actualizaciones.Iniciar();

                desktop.Exit += (_, _) =>
                {
                    _store.Dispose();
                    _actualizaciones.Dispose();
                    Log.Info("NovaFolder se cerró.");
                };

                ConfigurarBandeja(desktop);
                AplicarAutoinicio();
                SincronizarEscritorio();
                Log.Info($"NovaFolder {_actualizaciones.VersionActual} iniciado.");

                // Los accesos directos del Escritorio lanzan NovaFolder.exe con
                // "--folder <nombre>". Si ya había una instancia, Program se lo
                // reenvía a esta por el pipe; si esta es la primera, los trae en
                // sus propios argumentos.
                if (Program.Instancia is SingleInstanceService instancia)
                {
                    instancia.ArgumentosRecibidos += args =>
                        Dispatcher.UIThread.Post(() => ProcesarArgumentos(args));
                    instancia.Escuchar();
                }

                var args = desktop.Args ?? Array.Empty<string>();
                if (args.Length > 0)
                    Dispatcher.UIThread.Post(() => ProcesarArgumentos(args), DispatcherPriority.Background);
                else if (!_store.WelcomeSeen)
                    Dispatcher.UIThread.Post(MostrarBienvenida, DispatcherPriority.Background);
            }

            base.OnFrameworkInitializationCompleted();
        }

        // Último recurso para errores de la interfaz: se registran con todo
        // el detalle y se avisa sin cerrar la app. Un fallo en un clic no
        // debería llevarse por delante el widget entero.
        private void AlFallarInterfaz(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error("Error no controlado en la interfaz.", e.Exception);
            e.Handled = true;
            _ventana?.MostrarAviso(e.Exception is NovaFolderException nf
                ? nf.Message
                : "Algo salió mal. El detalle quedó en el registro (bandeja → Carpeta de datos).");
        }

        // ---- argumentos: accesos del Escritorio y archivos soltados ----

        // Cuando el usuario suelta accesos directos sobre el ícono de una
        // carpeta en el Escritorio, el Explorador no hace nada especial: lanza
        // el .lnk de la carpeta añadiendo las rutas soltadas como argumentos,
        // "--folder "Juegos" C:\...\Steam.lnk C:\...\Portal.url". Por eso
        // arrastrar al Escritorio funciona sin ningún código de shell propio.
        private void ProcesarArgumentos(string[] args)
        {
            var (nombre, rutas) = LeerArgumentos(args);
            var carpeta = nombre != null ? _store.Buscar(nombre) : null;

            try
            {
                if (carpeta != null)
                {
                    var resultado = rutas.Count > 0 ? _store.AgregarElementos(carpeta, rutas) : null;
                    // Se abre también al soltar: así se ve que entró, y dónde.
                    AbrirCarpeta(carpeta, null, false);
                    if (resultado?.MotivoSiNadaEntro is string motivo) _ventana.MostrarAviso(motivo);
                    return;
                }

                // Soltado sobre NovaFolder.exe (o su acceso) sin carpeta: una nueva.
                if (rutas.Count > 0)
                {
                    _ventana.NuevaCarpeta(rutas);
                    return;
                }
            }
            catch (NovaFolderException ex)
            {
                _ventana.MostrarAviso(ex.Message);
            }

            // Sin carpeta (o una que ya no existe): se abrió NovaFolder a secas
            // estando ya en marcha. Se muestra el widget en vez de no hacer nada.
            MostrarWidget();
        }

        // "--folder <nombre>" -> nombre. Todo lo demás que sea un archivo o
        // carpeta existente es algo soltado; el resto se ignora (los
        // argumentos pueden venir de otra instancia y no se confía en ellos).
        internal static (string? Carpeta, List<string> Rutas) LeerArgumentos(IReadOnlyList<string> args)
        {
            string? carpeta = null;
            var rutas = new List<string>();

            for (int i = 0; i < args.Count; i++)
            {
                if (args[i] == "--folder" && i + 1 < args.Count)
                    carpeta = args[++i];
                else if (AppLauncherService.Existe(args[i]))
                    rutas.Add(args[i]);
            }
            return (carpeta, rutas);
        }

        // ---- ventanas ----

        // ancla: dónde abrirse (la tarjeta del widget); null = junto al puntero.
        private void AbrirCarpeta(AppFolder carpeta, PixelRect? ancla, bool renombrar)
        {
            _popup?.Close();

            var popup = new FolderPopupWindow(_store, carpeta, ancla);
            if (renombrar) popup.EmpezarRenombradoAlAbrir();
            popup.Closed += (_, _) => { if (ReferenceEquals(_popup, popup)) _popup = null; };
            _popup = popup;
            popup.Show();
        }

        private void MostrarWidget()
        {
            _store.EstablecerWidgetVisible(true);
            _ventana.Show();
            _ventana.Activate();
        }

        // Ocultar no es cerrar: NovaFolder sigue en la bandeja. La primera
        // vez se explica, porque si no parece que la app se cerró.
        private void OcultarWidget()
        {
            _ventana.Hide();
            _store.EstablecerWidgetVisible(false);

            if (_store.TrayHintShown) return;
            _store.MarcarAvisoBandejaMostrado();
            new NotificacionWindow(
                "NovaFolder sigue aquí",
                "Tus carpetas están en el ícono morado junto al reloj (si no lo ves, pulsa la flecha ^).",
                "Mostrar el widget", MostrarWidget).Show();
        }

        private void MostrarBienvenida()
        {
            if (_bienvenida != null)
            {
                _bienvenida.Activate();
                return;
            }

            // La primera vez se recomienda limpiar; al volver a verla, se
            // muestra lo que el usuario ya tiene elegido.
            bool limpiar = !_store.WelcomeSeen || _store.CleanDesktop;
            var bienvenida = new BienvenidaWindow(limpiar, _store.StartWithWindows);
            bienvenida.Closed += (_, _) =>
            {
                _bienvenida = null;
                if (bienvenida.Resultado is { } elegido)
                {
                    EstablecerLimpiarEscritorio(elegido.LimpiarEscritorio);
                    _store.EstablecerAutoinicio(elegido.IniciarConWindows);
                    AplicarAutoinicio();
                    if (!_ventana.IsVisible) MostrarWidget();
                }
                _store.MarcarBienvenidaVista();
            };
            _bienvenida = bienvenida;
            bienvenida.Show();
        }

        // Activar "Limpiar" no mueve nada que ya estuviera en el Escritorio:
        // se ofrece, y el usuario decide.
        private void EstablecerLimpiarEscritorio(bool activo)
        {
            _store.EstablecerLimpiarEscritorio(activo);
            if (!activo) return;

            int pendientes = _store.ContarAccesosEnEscritorio();
            if (pendientes == 0) return;
            Avisar(pendientes == 1 ? "1 acceso de tus carpetas sigue en el Escritorio"
                                   : $"{pendientes} accesos de tus carpetas siguen en el Escritorio",
                "Guárdalos para dejarlo limpio. Si los quitas de la carpeta, vuelven solos.",
                "Guardarlos", GuardarAccesosDelEscritorio);
        }

        private void GuardarAccesosDelEscritorio()
        {
            int n = _store.GuardarAccesosDelEscritorio();
            Avisar(n == 1 ? "1 acceso guardado" : $"{n} accesos guardados",
                "Salieron del Escritorio y siguen en sus carpetas.");
        }

        // Un aviso donde el usuario lo vaya a ver: en el widget si está a la
        // vista, o junto a la bandeja si está oculto.
        private void Avisar(string titulo, string mensaje, string? textoAccion = null, Action? accion = null)
        {
            if (_ventana.IsVisible) _ventana.MostrarAviso(titulo, textoAccion, accion);
            else new NotificacionWindow(titulo, mensaje, textoAccion, accion).Show();
        }

        // ---- integración con Windows ----

        // Los .lnk del Escritorio se regeneran en el hilo STA de fondo: crear
        // los íconos compuestos lleva su tiempo y la UI no tiene por qué esperar.
        private void SincronizarEscritorio()
        {
            if (!OperatingSystem.IsWindows()) return;

            var copia = _store.Folders
                .Select(f => new AppFolder { Name = f.Name, Apps = f.Apps.ToList() })
                .ToList();
            var rutas = _rutas;

            StaWorker.Compartido.Ejecutar(() => { if (OperatingSystem.IsWindows()) ShortcutService.Sincronizar(copia, rutas); })
                .ContinueWith(t => Log.Error("Error al sincronizar el Escritorio.", t.Exception?.GetBaseException()),
                    TaskContinuationOptions.OnlyOnFaulted);
        }

        private void AplicarAutoinicio()
        {
            if (!OperatingSystem.IsWindows()) return;
            if (_store.StartWithWindows) StartupService.AsegurarRegistrado();
            else StartupService.Desregistrar();
        }

        // ---- bandeja del sistema ----

        // Sin barra de tareas, este ícono es la puerta a NovaFolder cuando el
        // widget está oculto: un clic abre el panel (TrayPanelWindow).
        private void ConfigurarBandeja(IClassicDesktopStyleApplicationLifetime desktop)
        {
            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://NovaFolder/Assets/NovaFolder.ico"))),
                ToolTipText = "NovaFolder: clic para ver tus carpetas y ajustes",
                IsVisible = true
            };

            _trayIcon.Clicked += (_, _) => AlternarPanelBandeja(desktop);
        }

        private void AlternarPanelBandeja(IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Un solo panel a la vez: si ya hay uno abierto, el clic lo cierra.
            if (_panelBandeja != null)
            {
                _panelBandeja.Close();
                return;
            }

            var acciones = new AccionesBandeja(
                AbrirCarpeta: (carpeta, ancla) => AbrirCarpeta(carpeta, ancla, false),
                NuevaCarpeta: () => _ventana.NuevaCarpeta(),
                WidgetVisible: () => _ventana.IsVisible,
                MostrarWidget: visible => { if (visible) MostrarWidget(); else OcultarWidget(); },
                Autoinicio: activo =>
                {
                    _store.EstablecerAutoinicio(activo);
                    AplicarAutoinicio();
                },
                LimpiarEscritorio: EstablecerLimpiarEscritorio,
                GuardarAccesosDelEscritorio: GuardarAccesosDelEscritorio,
                BuscarActualizaciones: _actualizaciones.BuscarAsync,
                ReiniciarParaActualizar: _actualizaciones.ReiniciarEInstalar,
                Version: _actualizaciones.VersionActual,
                VersionLista: () => _actualizaciones.VersionLista,
                MostrarAyuda: MostrarBienvenida,
                AbrirDatos: () => AppLauncherService.Lanzar(_rutas.Datos),
                Salir: () => desktop.Shutdown());

            var panel = new TrayPanelWindow(_store, acciones);
            _panelBandeja = panel;
            panel.Closed += (_, _) => { if (ReferenceEquals(_panelBandeja, panel)) _panelBandeja = null; };
            panel.Show();
        }
    }
}
