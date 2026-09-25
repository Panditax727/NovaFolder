using System;
using System.IO;
using System.Runtime.InteropServices;
using NovaFolder.Core;
using NovaFolder.Core.Diagnostics;

namespace NovaFolder.Services.Windows
{
    // NovaFolder se distribuye de dos formas con el mismo ejecutable:
    //   - Setup.exe (Velopack): se instala en %LOCALAPPDATA%\NovaFolder y se
    //     actualiza solo desde GitHub Releases;
    //   - Microsoft Store (MSIX): Windows lo instala en WindowsApps, lo firma
    //     Microsoft y lo actualiza la Store.
    // Aquí se decide todo lo que cambia entre una y otra.
    internal static class PaqueteService
    {
        private const string IdTareaInicio = "NovaFolderInicio";   // igual que en Package.appxmanifest
        private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

        // true si se está ejecutando como paquete MSIX (instalado desde la Store).
        public static bool EsPaquete { get; } = DetectarPaquete();

        // Ruta estable para lanzar la app empaquetada: el alias de ejecución
        // que declara el manifiesto. La ruta real (WindowsApps\...\NovaFolder.exe)
        // cambia en cada actualización y dejaría rotos los accesos del Escritorio.
        public static string RutaAlias =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WindowsApps", "NovaFolder.exe");

        public static string? Version
        {
            get
            {
                if (!EsPaquete) return null;
                var v = global::Windows.ApplicationModel.Package.Current.Id.Version;
                return $"{v.Major}.{v.Minor}.{v.Build}";
            }
        }

        // Rutas de datos de la versión empaquetada.
        //
        // Las apps MSIX no escriben de verdad en %APPDATA%: Windows lo desvía a
        // una zona privada que el Explorador no ve, y que se borra al
        // desinstalar. Por eso:
        //   - configuración, registros e íconos -> LocalState del paquete (el
        //     Explorador sí puede leerlo: los íconos de las carpetas del
        //     Escritorio y "Abrir carpeta de datos" funcionan);
        //   - accesos guardados -> Documentos\NovaFolder: son archivos del
        //     usuario y tienen que sobrevivir a una desinstalación.
        public static RutasApp RutasEmpaquetadas()
        {
            var local = global::Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var documentos = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var rutas = new RutasApp(local,
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory))
            {
                CarpetaAccesos = Path.Combine(documentos, "NovaFolder", "Accesos guardados")
            };
            MigrarDesdeInstalador(rutas);
            return rutas;
        }

        // Quien tenía la versión del Setup y pasa a la de la Store conserva sus
        // carpetas: la primera vez se copia su folders.json.
        private static void MigrarDesdeInstalador(RutasApp rutas)
        {
            try
            {
                var anterior = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NovaFolder", "folders.json");
                if (File.Exists(rutas.ArchivoConfig) || !File.Exists(anterior)) return;
                Directory.CreateDirectory(rutas.Datos);
                File.Copy(anterior, rutas.ArchivoConfig);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Advertencia($"No se pudo traer la configuración de la versión anterior: {ex.Message}");
            }
        }

        // La Store no deja escribir en la clave Run del registro: el inicio con
        // Windows se hace con la "tarea de inicio" del manifiesto, que además
        // aparece en Administrador de tareas → Aplicaciones de arranque.
        public static async void AplicarInicioConWindows(bool activo)
        {
            try
            {
                var tarea = await global::Windows.ApplicationModel.StartupTask.GetAsync(IdTareaInicio);
                if (activo)
                {
                    var estado = await tarea.RequestEnableAsync();
                    // Si el usuario lo desactivó en Windows, solo él puede volver
                    // a activarlo desde el Administrador de tareas.
                    if (estado is global::Windows.ApplicationModel.StartupTaskState.DisabledByUser
                               or global::Windows.ApplicationModel.StartupTaskState.DisabledByPolicy)
                        Log.Advertencia($"Inicio con Windows bloqueado por el sistema ({estado}).");
                }
                else
                {
                    tarea.Disable();
                }
            }
            catch (Exception ex)
            {
                Log.Error("No se pudo cambiar el inicio con Windows del paquete.", ex);
            }
        }

        // Abierto por Windows al iniciar sesión (equivale a --autostart del Setup).
        public static bool AbiertoAlIniciarSesion()
        {
            if (!EsPaquete) return false;
            try
            {
                return global::Windows.ApplicationModel.AppInstance.GetActivatedEventArgs()?.Kind
                       == global::Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException)
            {
                return false;
            }
        }

        private static bool DetectarPaquete()
        {
            if (!OperatingSystem.IsWindows()) return false;
            int largo = 0;
            return GetCurrentPackageFullName(ref largo, null) != APPMODEL_ERROR_NO_PACKAGE;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
    }
}
