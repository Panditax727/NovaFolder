using System;
using System.IO;
using NovaFolder.Core.Storage;

namespace NovaFolder.Core.Tests
{
    // Carpeta de datos y Escritorio falsos en un directorio temporal: las
    // pruebas nunca tocan la configuración ni el Escritorio reales.
    internal sealed class EntornoPrueba : IDisposable
    {
        private readonly string _raiz;

        public EntornoPrueba()
        {
            _raiz = Path.Combine(Path.GetTempPath(), "NovaFolderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_raiz, "escritorio"));
            Rutas = new RutasApp(Path.Combine(_raiz, "datos"), Path.Combine(_raiz, "escritorio"));
        }

        public RutasApp Rutas { get; }

        public FolderStore CrearStore() => FolderStore.Crear(Rutas);

        // Crea un archivo de mentira y devuelve su ruta completa.
        public string Archivo(string nombre, string? dir = null)
        {
            var carpeta = dir ?? Path.Combine(_raiz, "archivos");
            Directory.CreateDirectory(carpeta);
            var ruta = Path.Combine(carpeta, nombre);
            File.WriteAllText(ruta, "x");
            return ruta;
        }

        public string EnEscritorio(string nombre) => Archivo(nombre, Rutas.Escritorio);

        public void EscribirConfig(string json)
        {
            Directory.CreateDirectory(Rutas.Datos);
            File.WriteAllText(Rutas.ArchivoConfig, json);
        }

        public void Dispose()
        {
            try { Directory.Delete(_raiz, recursive: true); }
            catch (IOException) { /* algún archivo aún abierto: lo limpia el sistema */ }
        }
    }
}
