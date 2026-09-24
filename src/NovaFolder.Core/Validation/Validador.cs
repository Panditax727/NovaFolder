using System;
using System.IO;
using System.Linq;
using NovaFolder.Core.Errors;

namespace NovaFolder.Core.Validation
{
    // Límites de la aplicación. Además de evitar configuraciones absurdas,
    // acotan lo que puede llegar desde fuera (folders.json editado a mano,
    // argumentos de otra instancia) para que nada pueda inflar la memoria.
    public static class Limites
    {
        public const int LargoNombreCarpeta = 60;
        public const int MaxCarpetas = 100;
        public const int MaxElementosPorCarpeta = 500;
        public const long TamanoMaxConfig = 5 * 1024 * 1024;   // 5 MB
        public const int LargoMaxRuta = 32_767;                 // límite de Windows con rutas largas
    }

    // Reglas de validación de todo lo que entra al modelo. Cada método
    // devuelve el valor ya normalizado o lanza ValidacionException con un
    // mensaje listo para mostrar.
    public static class Validador
    {
        // El nombre de una carpeta acaba siendo el nombre de un archivo
        // (su acceso directo en el Escritorio), así que se le aplican las
        // reglas de nombres de archivo de Windows.
        private static readonly char[] CaracteresProhibidos = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

        private static readonly string[] NombresReservados =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static string NombreCarpeta(string? nombre)
        {
            var limpio = (nombre ?? "").Trim();

            if (limpio.Length == 0)
                throw new ValidacionException("El nombre no puede quedar vacío.");
            if (limpio.Length > Limites.LargoNombreCarpeta)
                throw new ValidacionException($"El nombre puede tener como máximo {Limites.LargoNombreCarpeta} caracteres.");
            if (limpio.IndexOfAny(CaracteresProhibidos) >= 0 || limpio.Any(char.IsControl))
                throw new ValidacionException("El nombre no puede contener \\ / : * ? \" < > |");
            if (limpio.EndsWith('.'))
                throw new ValidacionException("El nombre no puede terminar en punto.");
            if (NombresReservados.Contains(limpio, StringComparer.OrdinalIgnoreCase))
                throw new ValidacionException($"«{limpio}» es un nombre reservado de Windows.");

            return limpio;
        }

        // Una ruta que se va a guardar en una carpeta: absoluta, bien
        // formada y existente. Se devuelve normalizada (sin "..", sin
        // barras dobles) para que dos formas de la misma ruta cuenten como
        // repetidas.
        public static string RutaElemento(string? ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta))
                throw new ValidacionException("La ruta está vacía.");
            if (ruta.Length > Limites.LargoMaxRuta || ruta.Any(char.IsControl))
                throw new ValidacionException("La ruta no es válida.");

            string completa;
            try
            {
                if (!Path.IsPathFullyQualified(ruta))
                    throw new ValidacionException($"La ruta tiene que ser completa:\n{ruta}");
                completa = Path.GetFullPath(ruta);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new ValidacionException($"La ruta no es válida:\n{ruta}", ex);
            }

            if (!File.Exists(completa) && !Directory.Exists(completa))
                throw new ValidacionException($"No se encuentra:\n{completa}");

            // "C:\Juegos\" y "C:\Juegos" son lo mismo; la raíz "C:\" se deja tal cual.
            return EsRaiz(completa)
                ? completa
                : completa.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool EsRaiz(string ruta) =>
            string.Equals(Path.GetPathRoot(ruta), ruta, StringComparison.OrdinalIgnoreCase);
    }
}
