using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using NovaFolder.Core.Diagnostics;

namespace NovaFolder.Services.Windows
{
    // Registra NovaFolder para que arranque solo al iniciar sesión en
    // Windows. Se usa la clave Run del usuario actual (no la de máquina):
    // no requiere permisos de administrador y solo afecta a quien la
    // ejecuta, igual que hace cualquier app de bandeja del sistema normal.
    [SupportedOSPlatform("windows")]
    internal static class StartupService
    {
        private const string Clave = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string NombreValor = "NovaFolder";

        // Con este argumento la app sabe que la abrió Windows al iniciar
        // sesión, no el usuario: arranca discreta en la bandeja, sin ventana.
        public const string ArgumentoAutoinicio = "--autostart";

        // Idempotente: si ya está registrado con la ruta correcta no toca
        // nada; si cambió de ubicación (ej. tras publicar una nueva
        // versión) actualiza el valor.
        public static void AsegurarRegistrado()
        {
            try
            {
                var rutaExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(rutaExe)) return;
                var comando = $"\"{rutaExe}\" {ArgumentoAutoinicio}";

                using var clave = Registry.CurrentUser.OpenSubKey(Clave, writable: true)
                                   ?? Registry.CurrentUser.CreateSubKey(Clave);

                var actual = clave?.GetValue(NombreValor) as string;
                if (!string.Equals(actual, comando, StringComparison.OrdinalIgnoreCase))
                    clave?.SetValue(NombreValor, comando);
            }
            catch (Exception ex)
            {
                Log.Advertencia($"No se pudo registrar el autoinicio: {ex.Message}");
            }
        }

        public static void Desregistrar()
        {
            try
            {
                using var clave = Registry.CurrentUser.OpenSubKey(Clave, writable: true);
                clave?.DeleteValue(NombreValor, throwOnMissingValue: false);
            }
            catch (Exception ex)
            {
                Log.Advertencia($"No se pudo quitar el autoinicio: {ex.Message}");
            }
        }
    }
}
