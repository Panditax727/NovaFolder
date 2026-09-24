using System.IO;
using NovaFolder.Core.Errors;
using NovaFolder.Core.Validation;
using Xunit;

namespace NovaFolder.Core.Tests
{
    public class ValidadorTests
    {
        [Theory]
        [InlineData("Juegos", "Juegos")]
        [InlineData("  Juegos  ", "Juegos")]
        [InlineData("Música y más", "Música y más")]
        public void NombreCarpeta_valido_se_devuelve_normalizado(string entrada, string esperado) =>
            Assert.Equal(esperado, Validador.NombreCarpeta(entrada));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("a/b")]
        [InlineData("a:b")]
        [InlineData("¿qué?")]
        [InlineData("con\"comillas")]
        [InlineData("termina.")]
        [InlineData("CON")]
        [InlineData("lpt1")]
        public void NombreCarpeta_invalido_lanza_ValidacionException(string? entrada) =>
            Assert.Throws<ValidacionException>(() => Validador.NombreCarpeta(entrada));

        [Fact]
        public void NombreCarpeta_demasiado_largo_lanza()
        {
            var largo = new string('a', Limites.LargoNombreCarpeta + 1);
            Assert.Throws<ValidacionException>(() => Validador.NombreCarpeta(largo));
            Assert.Equal(Limites.LargoNombreCarpeta, Validador.NombreCarpeta(largo[1..]).Length);
        }

        [Fact]
        public void RutaElemento_existente_se_normaliza()
        {
            using var entorno = new EntornoPrueba();
            var archivo = entorno.Archivo("juego.lnk");
            var conPuntos = Path.Combine(Path.GetDirectoryName(archivo)!, "..", "archivos", "juego.lnk");

            Assert.Equal(archivo, Validador.RutaElemento(conPuntos));
        }

        [Fact]
        public void RutaElemento_carpeta_con_barra_final_se_recorta()
        {
            using var entorno = new EntornoPrueba();
            var dir = entorno.Rutas.Escritorio;
            Assert.Equal(dir, Validador.RutaElemento(dir + Path.DirectorySeparatorChar));
        }

        [Theory]
        [InlineData("")]
        [InlineData("relativa\\juego.lnk")]
        [InlineData("C:\\no\\existe\\nada-de-esto-123.lnk")]
        public void RutaElemento_invalida_lanza(string ruta) =>
            Assert.Throws<ValidacionException>(() => Validador.RutaElemento(ruta));
    }
}
