using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NovaFolder.Core.Diagnostics
{
    // Registro de la aplicación en archivos de texto, uno por día:
    //   %APPDATA%\NovaFolder\logs\novafolder-2026-09-24.log
    //
    // Antes los errores iban a Console.Error, y como NovaFolder es una app
    // de ventanas (WinExe) en Windows no hay consola: se perdían todos.
    // Ahora quedan escritos y el usuario puede adjuntarlos a un reporte.
    public static class Log
    {
        private const int DiasQueSeConservan = 14;
        private static readonly object Cerrojo = new();
        private static string? _carpeta;

        // Hasta que se llama, los mensajes solo van a la salida de depuración
        // (así las pruebas no ensucian el disco).
        public static void Iniciar(string carpeta)
        {
            try
            {
                Directory.CreateDirectory(carpeta);
                _carpeta = carpeta;
                BorrarAntiguos(carpeta);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"No se pudo preparar el registro en {carpeta}: {ex.Message}");
            }
        }

        public static string? Carpeta => _carpeta;

        public static void Info(string mensaje) => Escribir("INFO ", mensaje);
        public static void Advertencia(string mensaje) => Escribir("AVISO", mensaje);

        public static void Error(string mensaje, Exception? ex = null) =>
            Escribir("ERROR", ex == null ? mensaje : $"{mensaje}\n{ex}");

        private static void Escribir(string nivel, string mensaje)
        {
            var linea = $"{DateTime.Now:HH:mm:ss.fff} [{nivel}] {mensaje}";
            System.Diagnostics.Debug.WriteLine(linea);

            if (_carpeta == null) return;

            var archivo = Path.Combine(_carpeta,
                $"novafolder-{DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.log");

            // Nunca debe ser el registro el que tumbe la app.
            lock (Cerrojo)
            {
                try { File.AppendAllText(archivo, linea + Environment.NewLine); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* disco lleno o sin permiso */ }
            }
        }

        private static void BorrarAntiguos(string carpeta)
        {
            var limite = DateTime.Now.AddDays(-DiasQueSeConservan);
            foreach (var viejo in Directory.EnumerateFiles(carpeta, "novafolder-*.log")
                         .Where(f => File.GetLastWriteTime(f) < limite))
            {
                try { File.Delete(viejo); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* se reintenta otro día */ }
            }
        }
    }
}
