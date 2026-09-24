using System.IO;
using System.Linq;
using NovaFolder.Core.Models;
using NovaFolder.Core.Storage;
using NovaFolder.Core.Validation;
using Xunit;

namespace NovaFolder.Core.Tests
{
    public class ConfigRepositoryTests
    {
        [Fact]
        public void Primera_ejecucion_crea_una_carpeta_vacia()
        {
            using var entorno = new EntornoPrueba();
            var config = new ConfigRepository(entorno.Rutas).Cargar();

            var unica = Assert.Single(config.Folders);
            Assert.Empty(unica.Apps);
        }

        [Fact]
        public void Guardar_y_cargar_conserva_todo()
        {
            using var entorno = new EntornoPrueba();
            var repo = new ConfigRepository(entorno.Rutas);
            var archivo = entorno.Archivo("Steam.lnk");
            var original = new NovaConfig
            {
                StartWithWindows = false,
                CleanDesktop = true,
                WindowX = 120,
                WindowY = 80,
                Folders = { new AppFolder { Name = "Juegos", Apps = { new AppShortcut { Path = archivo } } } }
            };

            repo.Guardar(original);
            var leida = repo.Cargar();

            Assert.False(leida.StartWithWindows);
            Assert.True(leida.CleanDesktop);
            Assert.Equal((120, 80), (leida.WindowX, leida.WindowY));
            var carpeta = Assert.Single(leida.Folders);
            Assert.Equal("Juegos", carpeta.Name);
            Assert.Equal(archivo, Assert.Single(carpeta.Apps).Path);
            Assert.Equal("Steam", carpeta.Apps[0].Name);
        }

        [Fact]
        public void Formato_antiguo_se_migra_resolviendo_nombres_en_el_Escritorio()
        {
            using var entorno = new EntornoPrueba();
            entorno.EscribirConfig("""{ "Apps": ["Discord.lnk", "Spotify.lnk"], "Vacia": [] }""");

            var config = new ConfigRepository(entorno.Rutas).Cargar();

            Assert.Equal(new[] { "Apps", "Vacia" }, config.Folders.Select(f => f.Name));
            Assert.Equal(Path.Combine(entorno.Rutas.Escritorio, "Discord.lnk"), config.Folders[0].Apps[0].Path);
        }

        [Fact]
        public void Json_corrupto_se_aparta_y_se_arranca_limpio()
        {
            using var entorno = new EntornoPrueba();
            entorno.EscribirConfig("{ esto no es json");

            var config = new ConfigRepository(entorno.Rutas).Cargar();

            Assert.Single(config.Folders);
            Assert.True(File.Exists(entorno.Rutas.ArchivoConfig + ".roto"));
        }

        [Fact]
        public void Entradas_invalidas_o_repetidas_se_descartan_sin_perder_las_validas()
        {
            using var entorno = new EntornoPrueba();
            entorno.EscribirConfig("""
                {
                  "version": 2,
                  "folders": [
                    { "name": "Juegos", "items": ["C:\\a.lnk", "C:\\a.lnk", ""] },
                    { "name": "juegos", "items": [] },
                    { "name": "", "items": [] },
                    { "name": "a/b", "items": [] },
                    { "name": "Trabajo" }
                  ]
                }
                """);

            var config = new ConfigRepository(entorno.Rutas).Cargar();

            Assert.Equal(new[] { "Juegos", "Trabajo" }, config.Folders.Select(f => f.Name));
            Assert.Single(config.Folders[0].Apps);
        }

        [Fact]
        public void Mas_carpetas_que_el_limite_se_recortan()
        {
            using var entorno = new EntornoPrueba();
            var carpetas = string.Join(",", Enumerable.Range(1, Limites.MaxCarpetas + 5).Select(i => $$"""{ "name": "C{{i}}" }"""));
            entorno.EscribirConfig($$"""{ "folders": [{{carpetas}}] }""");

            var config = new ConfigRepository(entorno.Rutas).Cargar();

            Assert.Equal(Limites.MaxCarpetas, config.Folders.Count);
        }
    }
}
