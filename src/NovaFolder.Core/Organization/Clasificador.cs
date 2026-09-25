using System;
using System.IO;
using System.Linq;
using NovaFolder.Core.Storage;

namespace NovaFolder.Core.Organization
{
    // Decide en qué carpeta encaja un elemento del Escritorio, para poder
    // ordenarlo todo con un clic. Son reglas sencillas y predecibles (nada
    // de "magia"): el usuario ve la sugerencia antes y puede cambiarla.
    public static class Clasificador
    {
        public const string Juegos = "Juegos";
        public const string Apps = "Apps";
        public const string Documentos = "Documentos";
        public const string Imagenes = "Imágenes";
        public const string Multimedia = "Multimedia";
        public const string Carpetas = "Carpetas";
        public const string Enlaces = "Enlaces web";
        public const string Otros = "Otros";

        private static readonly string[] EsquemasDeJuegos =
        {
            "steam://", "com.epicgames.launcher://", "uplay://", "origin://", "origin2://",
            "battlenet://", "riotclient://", "goggalaxy://", "roblox://", "minecraft://"
        };

        // Fragmentos de ruta típicos de juegos instalados y de sus lanzadores.
        private static readonly string[] RutasDeJuegos =
        {
            @"\steamapps\", @"\steam\steam.exe", @"\epic games\", @"\riot games\", @"\gog galaxy\",
            @"\gog games\", @"\ubisoft\", @"\ea games\", @"\electronic arts\", @"\battle.net\",
            @"\rockstar games\", @"\xboxgames\", @"\minecraft", @"\roblox\", @"\games\"
        };

        private static readonly string[] ExtDocumentos = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md", ".csv", ".odt", ".ods", ".rtf" };
        private static readonly string[] ExtImagenes = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".heic", ".svg", ".ico" };
        private static readonly string[] ExtMultimedia = { ".mp4", ".mkv", ".avi", ".mov", ".mp3", ".wav", ".flac", ".m4a", ".ogg" };
        private static readonly string[] ExtApps = { ".exe", ".msi", ".appref-ms", ".bat", ".cmd" };

        // Lee lo necesario del disco (destino del .lnk, URL del .url) y clasifica.
        public static string Sugerir(string ruta)
        {
            if (Directory.Exists(ruta)) return Carpetas;

            var ext = Path.GetExtension(ruta).ToLowerInvariant();
            string? destino = null, argumentos = null, url = null;

            if (ext == ".lnk" && LnkReader.Leer(ruta) is { } info)
            {
                destino = info.Destino;
                argumentos = info.Argumentos;
            }
            else if (ext == ".url")
            {
                url = LeerUrl(ruta);
            }

            return Clasificar(ruta, destino, argumentos, url, destinoEsCarpeta: destino != null && Directory.Exists(destino));
        }

        // Pura, sin disco: es la que se prueba.
        public static string Clasificar(string ruta, string? destino, string? argumentos, string? url, bool destinoEsCarpeta)
        {
            var ext = Path.GetExtension(ruta).ToLowerInvariant();

            if (ext == ".url")
            {
                if (url == null) return Enlaces;
                return EsquemasDeJuegos.Any(e => url.StartsWith(e, StringComparison.OrdinalIgnoreCase)) ? Juegos : Enlaces;
            }

            if (ext == ".lnk")
            {
                if (string.IsNullOrWhiteSpace(destino)) return Apps;   // acceso roto o especial: lo más probable
                if (destinoEsCarpeta) return Carpetas;

                var todo = (destino + " " + argumentos).ToLowerInvariant();
                if (RutasDeJuegos.Any(todo.Contains) || EsquemasDeJuegos.Any(todo.Contains)) return Juegos;

                // Un acceso a un documento cuenta como el documento.
                var extDestino = Path.GetExtension(destino).ToLowerInvariant();
                return extDestino is ".exe" or "" ? Apps : PorExtension(extDestino);
            }

            return PorExtension(ext);
        }

        private static string PorExtension(string ext) =>
            ExtApps.Contains(ext) ? Apps
            : ExtDocumentos.Contains(ext) ? Documentos
            : ExtImagenes.Contains(ext) ? Imagenes
            : ExtMultimedia.Contains(ext) ? Multimedia
            : Otros;

        // Un .url es un .ini: la línea "URL=..." dice a dónde lleva.
        private static string? LeerUrl(string ruta)
        {
            try
            {
                foreach (var linea in File.ReadLines(ruta).Take(50))
                    if (linea.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)) return linea[4..].Trim();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return null;
        }
    }
}
