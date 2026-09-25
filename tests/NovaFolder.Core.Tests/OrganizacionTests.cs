using System.IO;
using System.Linq;
using NovaFolder.Core.Organization;
using Xunit;

namespace NovaFolder.Core.Tests
{
    public class ClasificadorTests
    {
        [Theory]
        [InlineData("C:\\Escritorio\\Counter-Strike 2.url", null, null, "steam://rungameid/730", "Juegos")]
        [InlineData("C:\\Escritorio\\Fortnite.url", null, null, "com.epicgames.launcher://apps/Fortnite?action=launch", "Juegos")]
        [InlineData("C:\\Escritorio\\Google.url", null, null, "https://google.com", "Enlaces web")]
        [InlineData("C:\\Escritorio\\Portal 2.lnk", "D:\\SteamLibrary\\steamapps\\common\\Portal 2\\portal2.exe", null, null, "Juegos")]
        [InlineData("C:\\Escritorio\\Valorant.lnk", "C:\\Riot Games\\Riot Client\\RiotClientServices.exe", "--launch-product=valorant", null, "Juegos")]
        [InlineData("C:\\Escritorio\\Discord.lnk", "C:\\Users\\yo\\AppData\\Local\\Discord\\Update.exe", "--processStart Discord.exe", null, "Apps")]
        [InlineData("C:\\Escritorio\\Informe.lnk", "C:\\Docs\\informe.pdf", null, null, "Documentos")]
        [InlineData("C:\\Escritorio\\Roto.lnk", null, null, null, "Apps")]
        [InlineData("C:\\Escritorio\\tesis.docx", null, null, null, "Documentos")]
        [InlineData("C:\\Escritorio\\foto.JPG", null, null, null, "Imágenes")]
        [InlineData("C:\\Escritorio\\cancion.mp3", null, null, null, "Multimedia")]
        [InlineData("C:\\Escritorio\\Firefox.exe", null, null, null, "Apps")]
        [InlineData("C:\\Escritorio\\cosa.xyz", null, null, null, "Otros")]
        public void Clasifica_por_destino_url_o_extension(string ruta, string? destino, string? args, string? url, string esperado) =>
            Assert.Equal(esperado, Clasificador.Clasificar(ruta, destino, args, url, destinoEsCarpeta: false));

        [Fact]
        public void Un_acceso_a_una_carpeta_va_a_Carpetas() =>
            Assert.Equal(Clasificador.Carpetas, Clasificador.Clasificar("C:\\E\\Proyectos.lnk", "D:\\Proyectos", null, null, destinoEsCarpeta: true));

        [Fact]
        public void Sugerir_lee_la_URL_de_un_archivo_url_real()
        {
            using var entorno = new EntornoPrueba();
            var url = entorno.EnEscritorio("Geometry Dash.url");
            File.WriteAllText(url, "[InternetShortcut]\r\nURL=steam://rungameid/322170\r\nIconIndex=0\r\n");

            Assert.Equal(Clasificador.Juegos, Clasificador.Sugerir(url));
            Assert.Equal(Clasificador.Carpetas, Clasificador.Sugerir(entorno.Rutas.Escritorio));
        }
    }

    public class EscritorioScannerTests
    {
        [Fact]
        public void Lista_solo_lo_que_no_esta_en_carpetas_ni_oculto()
        {
            using var entorno = new EntornoPrueba();
            var suelto = entorno.EnEscritorio("tesis.docx");
            var guardado = entorno.EnEscritorio("Steam.lnk");
            var oculto = entorno.EnEscritorio("desktop.ini");
            File.SetAttributes(oculto, FileAttributes.Hidden | FileAttributes.System);
            Directory.CreateDirectory(Path.Combine(entorno.Rutas.Escritorio, "Proyectos"));

            var sueltos = EscritorioScanner.Sueltos(entorno.Rutas, new[] { guardado });

            Assert.Equal(new[] { "Proyectos", "tesis.docx" }, sueltos.Select(s => s.Nombre).OrderBy(n => n));
            Assert.Equal(Clasificador.Documentos, sueltos.Single(s => s.Ruta == suelto).Sugerencia);
            Assert.All(sueltos, s => Assert.False(s.Comun));
        }
    }

    public class OrganizarTests
    {
        [Fact]
        public void Organizar_crea_las_carpetas_que_falten_y_reutiliza_las_existentes()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            store.EstablecerLimpiarEscritorio(false);
            var juegos = store.CrearCarpeta("juegos");
            var a = entorno.EnEscritorio("Portal.url");
            var b = entorno.EnEscritorio("tesis.docx");
            var c = entorno.EnEscritorio("notas.txt");

            var resumen = store.Organizar(new[] { (a, "Juegos"), (b, "Documentos"), (c, "Documentos") });

            Assert.False(resumen.Single(r => r.Carpeta == juegos).Creada);   // reutilizó la existente
            Assert.Single(juegos.Apps);
            var documentos = resumen.Single(r => r.Carpeta.Name == "Documentos");
            Assert.True(documentos.Creada);                                  // creó la nueva
            Assert.Equal(2, documentos.Agregados.Count);

            store.DeshacerOrganizar(resumen);

            Assert.Empty(juegos.Apps);
            Assert.NotNull(store.Buscar("juegos"));                           // la que ya existía se queda
            Assert.Null(store.Buscar("Documentos"));                          // la creada se va
        }
    }
}
