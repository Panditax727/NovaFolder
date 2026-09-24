using System;
using System.IO;
using NovaFolder.Core.Models;

namespace NovaFolder.Core.Storage
{
    // Convierte una ruta cualquiera (lo que venga del JSON, de arrastrar y
    // soltar o del selector de archivos) en un AppShortcut listo para mostrar.
    public static class ShortcutResolver
    {
        // escritorio: dónde buscar un nombre suelto como "Steam.lnk" (así
        // guardaba las rutas el formato antiguo de folders.json).
        public static AppShortcut Crear(string filename, string escritorio)
        {
            ArgumentNullException.ThrowIfNull(filename);

            var ruta = Path.IsPathRooted(filename) ? filename : Path.Combine(escritorio, filename);

            var acceso = new AppShortcut
            {
                Name = NombreVisible(ruta),
                Path = ruta
            };

            // Si es un .lnk se lee para sacar el ejecutable real: de ahí sale un
            // icono de verdad en vez del genérico con la flechita del acceso directo.
            //
            // El nombre se deja como el del archivo, que es lo que muestra
            // Windows en el Escritorio. La descripción del .lnk es el texto del
            // tooltip y suele ser una frase larga ("Discord - https://discord.com").
            if (LnkReader.EsLnk(ruta) && LnkReader.Leer(ruta) is { } info)
            {
                acceso.TargetPath = info.Destino;
                acceso.IconPath = ResolverIcono(info.Icono);
            }

            return acceso;
        }

        // Windows oculta la extensión de los accesos directos (.lnk, .url)
        // incluso con "mostrar extensiones" activado; con un documento en
        // cambio la extensión es información útil ("informe.pdf" vs ".docx").
        private static string NombreVisible(string ruta)
        {
            var nombre = Path.GetFileName(ruta.TrimEnd('\\', '/'));
            if (string.IsNullOrEmpty(nombre)) return ruta;

            var ext = Path.GetExtension(nombre);
            return ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".url", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(nombre)
                : nombre;
        }

        // El IconLocation de un .lnk puede venir con variables de entorno
        // (%LOCALAPPDATA%\...) y con el índice del icono pegado al final
        // ("app.exe,0"). Se devuelve null si no apunta a nada que exista,
        // para que AppShortcut caiga al siguiente candidato.
        internal static string? ResolverIcono(string? iconLocation)
        {
            if (string.IsNullOrWhiteSpace(iconLocation)) return null;

            var ruta = iconLocation.Trim();

            // Solo se recorta lo que venga después de la última coma si eso
            // es un número: una carpeta puede llamarse "Fotos, viejas".
            int coma = ruta.LastIndexOf(',');
            if (coma > 0 && int.TryParse(ruta[(coma + 1)..], out _))
                ruta = ruta[..coma];

            ruta = Environment.ExpandEnvironmentVariables(ruta);
            return File.Exists(ruta) ? ruta : null;
        }
    }
}
