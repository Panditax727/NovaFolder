using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using NovaFolder.Core;
using NovaFolder.Core.Diagnostics;
using NovaFolder.Core.Storage;
using NovaFolder.Services.Applications;
using NovaFolder.Services.Windows;
using Velopack;

namespace NovaFolder
{
    internal static class Program
    {
        // Rutas de datos de esta ejecución: distintas si viene de la
        // Microsoft Store (ver PaqueteService). Las pruebas usan otras.
        public static RutasApp Rutas { get; } =
            PaqueteService.EsPaquete ? PaqueteService.RutasEmpaquetadas() : RutasApp.PorDefecto();

        // La instancia única de esta ejecución; null en el diseñador de XAML.
        public static SingleInstanceService? Instancia { get; private set; }

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static int Main(string[] args)
        {
            // Siempre lo primero: el instalador lanza el .exe con argumentos
            // propios (instalar, actualizar, desinstalar) y Velopack los
            // atiende aquí y termina el proceso sin llegar a abrir la app.
            // La versión de la Store no usa Velopack: la instala y actualiza Windows.
            if (!PaqueteService.EsPaquete)
            {
                VelopackApp.Build()
                    .OnBeforeUninstallFastCallback(_ => LimpiarAlDesinstalar())
                    .Run();
            }

            Log.Iniciar(Rutas.CarpetaRegistros);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Log.Error("Error fatal no controlado.", e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Log.Error("Error en una tarea en segundo plano.", e.Exception);
                e.SetObserved();
            };

            try
            {
                using var instancia = new SingleInstanceService("NovaFolder");

                // Ya hay un NovaFolder abierto: se le pasa lo pedido (abrir una
                // carpeta, o simplemente mostrarse) y este proceso termina al
                // instante, sin llegar a cargar Avalonia.
                if (!instancia.EsPrimera)
                    return instancia.EnviarAPrimera(args) ? 0 : 1;

                Instancia = instancia;
                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Log.Error("NovaFolder no pudo arrancar.", ex);
                MostrarErrorFatal($"NovaFolder no pudo iniciarse:\n\n{ex.Message}\n\nEl detalle quedó en:\n{Rutas.CarpetaRegistros}");
                return 1;
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        // Desinstalar no debe dejar rastro en el sistema ni hacer perder
        // nada al usuario: se quita el autoinicio, se borran los accesos de
        // carpeta del Escritorio y los accesos directos que NovaFolder se
        // había llevado al almacén vuelven al Escritorio. La configuración
        // (folders.json) se conserva por si se vuelve a instalar.
        private static void LimpiarAlDesinstalar()
        {
            try
            {
                if (!OperatingSystem.IsWindows()) return;
                StartupService.Desregistrar();
                ShortcutService.BorrarTodos(Rutas);
                new AlmacenAccesos(Rutas).Barrer(Enumerable.Empty<string>());
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or COMException)
            {
                // Nunca bloquear la desinstalación.
            }
        }

        // Sin Avalonia todavía (o ya caído): el cuadro de mensaje nativo.
        private static void MostrarErrorFatal(string mensaje)
        {
            if (OperatingSystem.IsWindows())
                _ = MessageBoxW(IntPtr.Zero, mensaje, "NovaFolder", 0x10 /* MB_ICONERROR */);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    }
}
