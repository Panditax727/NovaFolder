using System;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;
using NovaFolder.Core.Diagnostics;
using NovaFolder.Services.Applications;

namespace NovaFolder.Services.Windows
{
    public enum ResultadoDesinstalar
    {
        // Se lanzó el desinstalador: la app tiene que cerrarse para que termine.
        Lanzado,
        // Versión de la Store: se abrió Configuración → Aplicaciones.
        ConfiguracionAbierta,
        // Copia de desarrollo (dotnet run, carpeta bin): no hay nada instalado.
        NoInstalado
    }

    // "Desinstalar NovaFolder" desde la propia app, con el mismo mecanismo que
    // usa Windows en Configuración → Aplicaciones, así el resultado es idéntico.
    internal static class DesinstaladorService
    {
        private const string ClaveDesinstalar = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

        public static ResultadoDesinstalar Desinstalar()
        {
            // Una app de la Store no puede desinstalarse a sí misma: se abre la
            // página de Aplicaciones de Windows, donde está el botón.
            if (PaqueteService.EsPaquete)
            {
                AppLauncherService.Lanzar("ms-settings:appsfeatures");
                return ResultadoDesinstalar.ConfiguracionAbierta;
            }

            var comando = BuscarComando();
            if (comando == null) return ResultadoDesinstalar.NoInstalado;

            var (programa, argumentos) = Separar(comando);
            try
            {
                // El desinstalador de Velopack (Update.exe --uninstall) cierra
                // NovaFolder, ejecuta LimpiarAlDesinstalar (accesos guardados de
                // vuelta al Escritorio, sin autoinicio) y borra el programa.
                Process.Start(new ProcessStartInfo(programa, argumentos) { UseShellExecute = true });
                Log.Info("Desinstalación iniciada por el usuario.");
                return ResultadoDesinstalar.Lanzado;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                Log.Error("No se pudo lanzar el desinstalador; se abre Configuración.", ex);
                AppLauncherService.Lanzar("ms-settings:appsfeatures");
                return ResultadoDesinstalar.ConfiguracionAbierta;
            }
        }

        // La entrada que Velopack creó al instalar: se busca por nombre y por
        // que su desinstalador sea el Update.exe de NovaFolder, sin suponer el
        // nombre exacto de la clave.
        private static string? BuscarComando()
        {
            try
            {
                using var raiz = Registry.CurrentUser.OpenSubKey(ClaveDesinstalar);
                if (raiz == null) return null;

                foreach (var nombre in raiz.GetSubKeyNames())
                {
                    using var app = raiz.OpenSubKey(nombre);
                    if (app?.GetValue("DisplayName") as string != "NovaFolder") continue;
                    if (app.GetValue("UninstallString") is string comando
                        && comando.Contains("Update.exe", StringComparison.OrdinalIgnoreCase))
                        return comando;
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                Log.Advertencia($"No se pudo leer el registro de programas instalados: {ex.Message}");
            }
            return null;
        }

        // "C:\...\Update.exe" --uninstall  ->  (C:\...\Update.exe, --uninstall)
        internal static (string Programa, string Argumentos) Separar(string comando)
        {
            comando = comando.Trim();
            if (comando.StartsWith('"'))
            {
                int cierre = comando.IndexOf('"', 1);
                if (cierre > 0) return (comando[1..cierre], comando[(cierre + 1)..].Trim());
            }
            int espacio = comando.IndexOf(' ');
            return espacio < 0 ? (comando, "") : (comando[..espacio], comando[(espacio + 1)..].Trim());
        }
    }
}
