using System.IO;
using NovaFolder.Core.Storage;
using Xunit;

namespace NovaFolder.Core.Tests
{
    public class AlmacenAccesosTests
    {
        [Fact]
        public void Solo_adopta_accesos_directos_del_Escritorio()
        {
            using var entorno = new EntornoPrueba();
            var almacen = new AlmacenAccesos(entorno.Rutas);
            var documento = entorno.EnEscritorio("informe.pdf");
            var fuera = entorno.Archivo("Steam.lnk");

            Assert.Equal(documento, almacen.Adoptar(documento));
            Assert.Equal(fuera, almacen.Adoptar(fuera));
            Assert.True(File.Exists(documento));
            Assert.True(File.Exists(fuera));
        }

        [Fact]
        public void Barrer_no_pisa_archivos_del_Escritorio_con_el_mismo_nombre()
        {
            using var entorno = new EntornoPrueba();
            var almacen = new AlmacenAccesos(entorno.Rutas);
            var gestionado = almacen.Adoptar(entorno.EnEscritorio("Steam.lnk"));
            entorno.EnEscritorio("Steam.lnk");   // el usuario creó otro igual

            almacen.Barrer(new string[0]);

            Assert.False(File.Exists(gestionado));
            Assert.True(File.Exists(Path.Combine(entorno.Rutas.Escritorio, "Steam.lnk")));
            Assert.True(File.Exists(Path.Combine(entorno.Rutas.Escritorio, "Steam (2).lnk")));
        }
    }

    public class LnkReaderTests
    {
        [Fact]
        public void Archivo_que_no_es_lnk_devuelve_null_sin_lanzar()
        {
            using var entorno = new EntornoPrueba();
            var falso = entorno.Archivo("roto.lnk");

            Assert.Null(LnkReader.Leer(falso));
            Assert.Null(LnkReader.Leer(Path.Combine(Path.GetTempPath(), "no-existe-123.lnk")));
        }

        [Fact]
        public void Un_lnk_roto_se_sigue_mostrando_con_su_nombre()
        {
            using var entorno = new EntornoPrueba();
            var falso = entorno.Archivo("Juego viejo.lnk");

            var acceso = ShortcutResolver.Crear(falso, entorno.Rutas.Escritorio);

            Assert.Equal("Juego viejo", acceso.Name);
            Assert.Null(acceso.TargetPath);
        }
    }
}
