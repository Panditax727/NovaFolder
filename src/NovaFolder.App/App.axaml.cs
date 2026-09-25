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
using NovaFolder.Core.Organization;
using NovaFolder.Core.Storage;
using NovaFolder.Services.Applications;
using NovaFolder.Services.Updates;
using NovaFolder.Services.Windows;
using NovaFolder.Views;
using NovaFolder.Views.Principal;

namespace NovaFolder
{
    // Raíz de composición: crea los servicios y las ventanas, y los conecta.
    // Es el único sitio que conoce a todos; cada pieza solo conoce lo que recibe.
    //
    // Cómo se comporta, como cualquier programa de Windows:
    //   - abierto por el usuario (instalador, menú Inicio) -> ventana principal;
    //   - abierto por Windows al iniciar sesión (--autostart) -> discreto en la bandeja;
    //   - cerrar la ventana -> sigue en la bandeja (las carpetas del Escritorio
    //     lo necesitan); un clic en el ícono la vuelve a abrir;
    //   - doble clic en una carpeta del Escritorio -> su popup, estilo Android.
    public partial class App : Application
    {
        private RutasApp _rutas = null!;
        private FolderStore _store = null!;
        private UpdateService _actualizaciones = null!;
        private MainWindow _principal = null!;
        private WidgetWindow _widget = null!;
        private IClassicDesktopStyleApplicationLifetime _escritorio = null!;

        // Referencia viva obligatoria: si TrayIcon se pierde, el recolector de
        // basura la destruye y el ícono desaparece de la bandeja sin avisar.
        private TrayIcon? _trayIcon;

        // La carpeta y la bienvenida abiertas ahora mismo, si las hay.
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
                _escritorio = desktop;
                // Cerrar la ventana no cierra la app: vive hasta "Salir".
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Dispatcher.UIThread.UnhandledException += AlFallarInterfaz;

                _rutas = Program.Rutas;
                _store = FolderStore.Crear(_rutas);
                _store.VigilarCambiosExternos();
                _store.Changed += SincronizarEscritorio;
                _store.ErrorDeGuardado += mensaje => Dispatcher.UIThread.Post(() => Avisar(mensaje, "", esError: true));

                _actualizaciones = new UpdateService();
                _actualizaciones.ActualizacionLista += version => Dispatcher.UIThread.Post(() =>
                {
                    _principal.ActualizarVersion();
                    Avisar($"NovaFolder {version} está lista",
                        "Se instalará sola la próxima vez que se inicie, o ahora mismo si reinicias.",
                        textoAccion: "Reiniciar", accion: _actualizaciones.ReiniciarEInstalar);
                });

                CrearVentanas();
                ConfigurarBandeja();
                AplicarAutoinicio();
                SincronizarEscritorio();
                _actualizaciones.Iniciar();

                desktop.Exit += (_, _) =>
                {
                    _store.Dispose();
                    _actualizaciones.Dispose();
                    Log.Info("NovaFolder se cerró.");
                };

                // Los accesos directos del Escritorio lanzan NovaFolder.exe con
                // "--folder <nombre>". Si ya había una instancia, Program se lo
                // reenvía a esta por el pipe; si esta es la primera, los trae en
                // sus propios argumentos.
                if (Program.Instancia is SingleInstanceService instancia)
                {
                    instancia.ArgumentosRecibidos += a => Dispatcher.UIThread.Post(() => ProcesarArgumentos(a));
                    instancia.Escuchar();
                }

                var args = desktop.Args ?? Array.Empty<string>();
                bool porWindows = args.Contains(StartupService.ArgumentoAutoinicio) || PaqueteService.AbiertoAlIniciarSesion();
                Log.Info($"NovaFolder {_actualizaciones.VersionActual} iniciado{(porWindows ? " con Windows" : "")}.");
                if (!porWindows || args.Length > 1)
                    Dispatcher.UIThread.Post(() => ProcesarArgumentos(args), DispatcherPriority.Background);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void CrearVentanas()
        {
            _principal = new MainWindow(new ServiciosApp(
                Store: _store,
                Rutas: _rutas,
                Actualizaciones: _actualizaciones,
                MostrarBienvenida: MostrarBienvenida,
                WidgetVisible: () => _widget.IsVisible,
                MostrarWidget: visible => { if (visible) MostrarWidget(); else OcultarWidget(); },
                Autoinicio: activo =>
                {
                    _store.EstablecerAutoinicio(activo);
                    AplicarAutoinicio();
                },
                LimpiarEscritorio: EstablecerLimpiarEscritorio,
                GuardarAccesosDelEscritorio: GuardarAccesosDelEscritorio,
                Salir: () => _escritorio.Shutdown()));
            _principal.Ocultada += AlOcultarPrincipal;

            // El widget es un complemento opcional (Ajustes → Widget flotante).
            _widget = new WidgetWindow(_store, AbrirCarpeta);
            _widget.AyudaPedida += () => _principal.MostrarYActivar(Seccion.Ayuda);
            _widget.AbrirAppPedido += () => _principal.MostrarYActivar();
            _widget.OcultarPedido += OcultarWidget;
            if (_store.ShowWidget) _widget.Show();
        }

        // Último recurso para errores de la interfaz: se registran con todo
        // el detalle y se avisa sin cerrar la app.
        private void AlFallarInterfaz(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error("Error no controlado en la interfaz.", e.Exception);
            e.Handled = true;
            Avisar(e.Exception is NovaFolderException nf
                    ? nf.Message
                    : "Algo salió mal. El detalle quedó en el registro (Ajustes → Configuración y registros).",
                "", esError: true);
        }

        // ---- argumentos: arranque, accesos del Escritorio y archivos soltados ----

        // Cuando el usuario suelta accesos directos sobre el ícono de una
        // carpeta en el Escritorio, el Explorador lanza el .lnk de la carpeta
        // añadiendo las rutas soltadas como argumentos:
        // "--folder "Juegos" C:\...\Steam.lnk C:\...\Portal.url".
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
                    if (resultado?.MotivoSiNadaEntro is string motivo) Avisar(motivo, "", esError: true);
                    return;
                }

                // Soltado sobre NovaFolder.exe (o su acceso) sin carpeta: una nueva.
                if (rutas.Count > 0)
                {
                    var nueva = _store.CrearCarpeta(rutas: rutas);
                    _principal.MostrarYActivar(Seccion.Carpetas);
                    _principal.Avisar($"Se creó «{nueva.Name}» con lo que soltaste.");
                    return;
                }
            }
            catch (NovaFolderException ex)
            {
                Avisar(ex.Message, "", esError: true);
            }

            // Abierto a secas (menú Inicio, instalador, o de nuevo estando ya
            // en marcha): la ventana principal, y la bienvenida la primera vez.
            _principal.MostrarYActivar();
            if (!_store.WelcomeSeen) Dispatcher.UIThread.Post(MostrarBienvenida, DispatcherPriority.Background);
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

        // Cerrar la ventana no es salir. La primera vez se explica, porque si
        // no parece que la app se cerró (o, al revés, que sigue "colgada").
        private void AlOcultarPrincipal()
        {
            if (_store.TrayHintShown) return;
            _store.MarcarAvisoBandejaMostrado();
            new NotificacionWindow(
                "NovaFolder sigue funcionando",
                "Queda junto al reloj para que tus carpetas del Escritorio se abran al instante. Clic en su ícono para volver.",
                "Abrir NovaFolder", () => _principal.MostrarYActivar()).Show();
        }

        private void MostrarWidget()
        {
            _store.EstablecerWidgetVisible(true);
            _widget.Show();
        }

        private void OcultarWidget()
        {
            _widget.Hide();
            _store.EstablecerWidgetVisible(false);
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
            var bienvenida = new BienvenidaWindow(limpiar, _store.StartWithWindows)
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            bienvenida.Closed += (_, _) =>
            {
                _bienvenida = null;
                bool primeraVez = !_store.WelcomeSeen;
                _store.MarcarBienvenidaVista();
                if (bienvenida.Resultado is not { } elegido) return;

                EstablecerLimpiarEscritorio(elegido.LimpiarEscritorio);
                _store.EstablecerAutoinicio(elegido.IniciarConWindows);
                AplicarAutoinicio();

                // Siguiente paso natural tras la guía: ordenar lo que ya hay.
                if (primeraVez) _principal.MostrarYActivar(Seccion.Escritorio);
            };
            _bienvenida = bienvenida;

            // Diálogo de la ventana principal: centrado sobre ella y por encima.
            _principal.MostrarYActivar();
            _ = bienvenida.ShowDialog(_principal);
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
                textoAccion: "Guardarlos", accion: GuardarAccesosDelEscritorio);
        }

        private void GuardarAccesosDelEscritorio()
        {
            int n = _store.GuardarAccesosDelEscritorio();
            Avisar(n == 1 ? "1 acceso guardado: salió del Escritorio" : $"{n} accesos guardados: salieron del Escritorio",
                "Siguen en sus carpetas.");
        }

        // Un aviso donde el usuario lo vaya a ver: en la ventana principal si
        // está abierta, en el widget si solo está él, o junto a la bandeja.
        private void Avisar(string titulo, string detalle, bool esError = false, string? textoAccion = null, Action? accion = null)
        {
            if (_principal.IsVisible) _principal.Avisar(titulo, esError, textoAccion, accion);
            else if (_widget.IsVisible) _widget.MostrarAviso(titulo, textoAccion, accion);
            else new NotificacionWindow(titulo, detalle, textoAccion, accion).Show();
        }

        // ---- integración con Windows ----

        // Los .lnk del Escritorio se regeneran en el hilo STA de fondo: crear
        // los íconos compuestos lleva su tiempo y la UI no tiene por qué esperar.
        // Con "Carpetas en el Escritorio" apagado se sincroniza una lista vacía,
        // lo que quita los accesos que hubiera.
        private void SincronizarEscritorio()
        {
            if (!OperatingSystem.IsWindows()) return;

            var copia = _store.DesktopFolders
                ? _store.Folders.Select(f => new AppFolder { Name = f.Name, Apps = f.Apps.ToList() }).ToList()
                : new List<AppFolder>();
            var rutas = _rutas;

            StaWorker.Compartido.Ejecutar(() => { if (OperatingSystem.IsWindows()) ShortcutService.Sincronizar(copia, rutas); })
                .ContinueWith(t => Log.Error("Error al sincronizar el Escritorio.", t.Exception?.GetBaseException()),
                    TaskContinuationOptions.OnlyOnFaulted);
        }

        private void AplicarAutoinicio()
        {
            if (!OperatingSystem.IsWindows()) return;
            if (PaqueteService.EsPaquete)
            {
                PaqueteService.AplicarInicioConWindows(_store.StartWithWindows);
                return;
            }
            if (_store.StartWithWindows) StartupService.AsegurarRegistrado();
            else StartupService.Desregistrar();
        }

        // Con la ventana cerrada, este ícono es la puerta de vuelta: un clic
        // abre NovaFolder, como en cualquier app que vive en la bandeja.
        private void ConfigurarBandeja()
        {
            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://NovaFolder/Assets/NovaFolder.ico"))),
                ToolTipText = "NovaFolder",
                IsVisible = true
            };
            _trayIcon.Clicked += (_, _) => _principal.MostrarYActivar();
        }
    }
}
