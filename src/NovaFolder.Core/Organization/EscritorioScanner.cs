using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovaFolder.Core.Storage;

namespace NovaFolder.Core.Organization
{
    // Un elemento del Escritorio que todavía no está en ninguna carpeta.
    // Comun: está en el Escritorio de todos los usuarios (C:\Users\Public);
    // se puede organizar, pero seguirá visible porque sacarlo de ahí
    // necesita permisos de administrador.
    public sealed record ElementoEscritorio(string Ruta, string Nombre, string Sugerencia, bool Comun);

    // Recorre el Escritorio y devuelve lo que está "suelto": lo que la
    // página Escritorio ofrece organizar.
    public static class EscritorioScanner
    {
        public static IReadOnlyList<ElementoEscritorio> Sueltos(RutasApp rutas, IEnumerable<string> yaEnCarpetas)
        {
            ArgumentNullException.ThrowIfNull(rutas);
            var ocupados = new HashSet<string>(yaEnCarpetas, StringComparer.OrdinalIgnoreCase);
            var resultado = new List<ElementoEscritorio>();

            Recorrer(rutas.Escritorio, comun: false);
            if (rutas.EscritorioComun is string comun) Recorrer(comun, comun: true);

            return resultado
                .OrderBy(e => e.Sugerencia, StringComparer.CurrentCulture)
                .ThenBy(e => e.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            void Recorrer(string dir, bool comun)
            {
                if (!Directory.Exists(dir)) return;

                IEnumerable<string> entradas;
                try { entradas = Directory.EnumerateFileSystemEntries(dir).ToList(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }

                foreach (var ruta in entradas)
                {
                    if (ocupados.Contains(ruta) || EsOcultoODelSistema(ruta) || EsCarpetaDeNovaFolder(ruta)) continue;
                    resultado.Add(new ElementoEscritorio(ruta, NombreVisible(ruta), Clasificador.Sugerir(ruta), comun));
                }
            }
        }

        // desktop.ini, Thumbs.db y compañía.
        private static bool EsOcultoODelSistema(string ruta)
        {
            try
            {
                var atributos = File.GetAttributes(ruta);
                return (atributos & (FileAttributes.Hidden | FileAttributes.System)) != 0;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
        }

        // Los accesos que NovaFolder pone en el Escritorio para cada carpeta.
        private static bool EsCarpetaDeNovaFolder(string ruta) =>
            LnkReader.EsLnk(ruta) && LnkReader.Leer(ruta)?.Argumentos?.StartsWith("--folder ", StringComparison.Ordinal) == true;

        private static string NombreVisible(string ruta)
        {
            var nombre = Path.GetFileName(ruta);
            var ext = Path.GetExtension(nombre);
            return ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || ext.Equals(".url", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(nombre)
                : nombre;
        }
    }
}
