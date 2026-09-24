using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovaFolder.Core.Diagnostics;

namespace NovaFolder.Core.Storage
{
    // "Limpiar el Escritorio": los accesos directos que se meten en una
    // carpeta se trasladan del Escritorio a %APPDATA%\NovaFolder\accesos,
    // y vuelven al Escritorio en cuanto ninguna carpeta los usa.
    //
    // Reglas para no tocar nunca lo que no toca:
    //   - solo .lnk y .url (documentos, carpetas y .exe se quedan donde están);
    //   - solo los que están directamente en el Escritorio del usuario (el
    //     Escritorio público necesita permisos de administrador);
    //   - Readoptar/Barrer solo mueven archivos que están dentro del almacén,
    //     que solo contiene lo que NovaFolder puso ahí.
    public sealed class AlmacenAccesos
    {
        private readonly RutasApp _rutas;

        public AlmacenAccesos(RutasApp rutas) => _rutas = rutas;

        public string Carpeta => _rutas.CarpetaAccesos;

        public bool EsGestionado(string ruta) =>
            string.Equals(Path.GetDirectoryName(ruta), Carpeta, StringComparison.OrdinalIgnoreCase);

        // Devuelve la ruta con la que hay que guardar el elemento: la del
        // almacén si se movió, o la original si no aplica o falló.
        public string Adoptar(string ruta)
        {
            if (!EsAccesoDelEscritorio(ruta) || !File.Exists(ruta)) return ruta;

            try
            {
                Directory.CreateDirectory(Carpeta);
                var destino = RutaLibre(Carpeta, Path.GetFileName(ruta));
                File.Move(ruta, destino);
                return destino;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Advertencia($"No se pudo sacar {ruta} del Escritorio: {ex.Message}");
                return ruta;
            }
        }

        // Tras deshacer una eliminación, el acceso pudo haber vuelto ya al
        // Escritorio (lo devolvió Barrer). Se recupera por nombre.
        public string Readoptar(string rutaGestionada)
        {
            if (!EsGestionado(rutaGestionada) || File.Exists(rutaGestionada)) return rutaGestionada;

            var enEscritorio = Path.Combine(_rutas.Escritorio, Path.GetFileName(rutaGestionada));
            return File.Exists(enEscritorio) ? Adoptar(enEscritorio) : rutaGestionada;
        }

        // Devuelve al Escritorio todo lo del almacén que ya no usa ninguna
        // carpeta (se quitó, se eliminó la carpeta, se editó el JSON...).
        public void Barrer(IEnumerable<string> enUso)
        {
            if (!Directory.Exists(Carpeta)) return;

            var usados = new HashSet<string>(enUso, StringComparer.OrdinalIgnoreCase);
            foreach (var archivo in Directory.EnumerateFiles(Carpeta).Where(a => !usados.Contains(a)).ToList())
            {
                try { File.Move(archivo, RutaLibre(_rutas.Escritorio, Path.GetFileName(archivo))); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Advertencia($"No se pudo devolver {archivo} al Escritorio: {ex.Message}");
                }
            }
        }

        private bool EsAccesoDelEscritorio(string ruta)
        {
            var ext = Path.GetExtension(ruta);
            bool esAcceso = ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                         || ext.Equals(".url", StringComparison.OrdinalIgnoreCase);
            return esAcceso
                && string.Equals(Path.GetDirectoryName(ruta), _rutas.Escritorio, StringComparison.OrdinalIgnoreCase);
        }

        // "Steam.lnk" ocupado -> "Steam (2).lnk", como hace el Explorador.
        internal static string RutaLibre(string dir, string nombre)
        {
            var ruta = Path.Combine(dir, nombre);
            var baseNombre = Path.GetFileNameWithoutExtension(nombre);
            var ext = Path.GetExtension(nombre);
            for (int i = 2; File.Exists(ruta) || Directory.Exists(ruta); i++)
                ruta = Path.Combine(dir, $"{baseNombre} ({i}){ext}");
            return ruta;
        }
    }
}
