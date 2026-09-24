using System.IO;
using System.Linq;
using NovaFolder.Core.Errors;
using NovaFolder.Core.Storage;
using Xunit;

namespace NovaFolder.Core.Tests
{
    // CRUD de carpetas y de sus elementos.
    public class FolderStoreTests
    {
        // ---------- Carpetas ----------

        [Fact]
        public void Crear_sin_nombre_usa_nombres_unicos()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();

            var a = store.CrearCarpeta();
            var b = store.CrearCarpeta();

            Assert.Equal("Nueva carpeta", a.Name);
            Assert.Equal("Nueva carpeta 2", b.Name);
        }

        [Fact]
        public void Crear_guarda_en_disco_y_avisa()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            int avisos = 0;
            store.Changed += () => avisos++;

            store.CrearCarpeta("Juegos");

            Assert.Equal(1, avisos);
            using var otra = entorno.CrearStore();
            Assert.NotNull(otra.Buscar("juegos"));
        }

        [Fact]
        public void Crear_con_nombre_repetido_lanza_sin_importar_mayusculas()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            store.CrearCarpeta("Juegos");

            var ex = Assert.Throws<ValidacionException>(() => store.CrearCarpeta("JUEGOS"));
            Assert.Contains("Ya existe", ex.Message);
        }

        [Fact]
        public void Obtener_una_carpeta_inexistente_lanza()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();

            Assert.Throws<CarpetaNoEncontradaException>(() => store.Obtener("No existe"));
        }

        [Fact]
        public void Renombrar_valida_el_nombre()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            var juegos = store.CrearCarpeta("Juegos");
            store.CrearCarpeta("Trabajo");

            Assert.Throws<ValidacionException>(() => store.RenombrarCarpeta(juegos, "   "));
            Assert.Throws<ValidacionException>(() => store.RenombrarCarpeta(juegos, "trabajo"));

            store.RenombrarCarpeta(juegos, " Mis juegos ");
            Assert.Equal("Mis juegos", juegos.Name);
        }

        [Fact]
        public void Renombrar_cambiando_solo_mayusculas_esta_permitido()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            var juegos = store.CrearCarpeta("juegos");

            store.RenombrarCarpeta(juegos, "Juegos");

            Assert.Equal("Juegos", juegos.Name);
        }

        [Fact]
        public void Eliminar_y_restaurar_vuelve_a_la_misma_posicion()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            store.CrearCarpeta("A");
            var b = store.CrearCarpeta("B");
            store.CrearCarpeta("C");

            int indice = store.EliminarCarpeta(b);
            Assert.DoesNotContain(b, store.Folders);

            store.RestaurarCarpeta(b, indice);
            Assert.Equal(indice, store.Folders.ToList().IndexOf(b));
        }

        [Fact]
        public void Restaurar_con_el_nombre_ocupado_la_renombra()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            var b = store.CrearCarpeta("B");
            int indice = store.EliminarCarpeta(b);
            store.CrearCarpeta("B");

            store.RestaurarCarpeta(b, indice);

            Assert.Equal("B 2", b.Name);
        }

        [Fact]
        public void Operar_sobre_una_carpeta_eliminada_lanza()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            var b = store.CrearCarpeta("B");
            store.EliminarCarpeta(b);

            Assert.Throws<CarpetaNoEncontradaException>(() => store.RenombrarCarpeta(b, "C"));
            Assert.Throws<CarpetaNoEncontradaException>(() => store.EliminarCarpeta(b));
            Assert.Throws<CarpetaNoEncontradaException>(() => store.AgregarElementos(b, new[] { "x" }));
        }

        // ---------- Elementos ----------

        [Fact]
        public void Agregar_informa_agregados_repetidos_y_rechazados()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            var carpeta = store.CrearCarpeta("Juegos");
            var steam = entorno.Archivo("Steam.lnk");

            var primero = store.AgregarElementos(carpeta, new[] { steam, "C:\\no-existe-123.lnk", "relativa.lnk" });
            var segundo = store.AgregarElementos(carpeta, new[] { steam.ToUpperInvariant() });

            Assert.Single(primero.Agregados);
            Assert.Equal(2, primero.Rechazados.Count);
            Assert.Null(primero.MotivoSiNadaEntro);
            Assert.Empty(segundo.Agregados);
            Assert.Equal("Ya estaba en la carpeta.", segundo.MotivoSiNadaEntro);
        }

        [Fact]
        public void Agregar_en_una_posicion_inserta_en_orden()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            var carpeta = store.CrearCarpeta("Juegos");
            store.AgregarElementos(carpeta, new[] { entorno.Archivo("A.lnk"), entorno.Archivo("D.lnk") });

            store.AgregarElementos(carpeta, new[] { entorno.Archivo("B.lnk"), entorno.Archivo("C.lnk") }, indice: 1);

            Assert.Equal(new[] { "A", "B", "C", "D" }, carpeta.Apps.Select(a => a.Name));
        }

        [Fact]
        public void Quitar_varios_elementos()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            var carpeta = store.CrearCarpeta("Juegos");
            var agregados = store.AgregarElementos(carpeta, new[] { entorno.Archivo("A.lnk"), entorno.Archivo("B.lnk"), entorno.Archivo("C.lnk") }).Agregados;

            store.QuitarElementos(carpeta, new[] { agregados[0], agregados[2] });

            Assert.Equal(new[] { "B" }, carpeta.Apps.Select(a => a.Name));
        }

        [Fact]
        public void Mover_entre_carpetas_no_duplica()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            var juegos = store.CrearCarpeta("Juegos");
            var trabajo = store.CrearCarpeta("Trabajo");
            var app = store.AgregarElementos(juegos, new[] { entorno.Archivo("A.lnk") }).Agregados[0];

            store.MoverElemento(app, juegos, trabajo);

            Assert.Empty(juegos.Apps);
            Assert.Same(app, Assert.Single(trabajo.Apps));
        }

        [Theory]
        [InlineData(0, 3, "BCAD")]   // A antes de D
        [InlineData(0, 4, "BCDA")]   // A al final
        [InlineData(3, 0, "DABC")]   // D al principio
        [InlineData(1, 2, "ABCD")]   // B antes de C: no se mueve
        public void Reordenar_coloca_antes_del_indice_indicado(int desde, int antesDe, string esperado)
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            var carpeta = store.CrearCarpeta("X");
            store.AgregarElementos(carpeta, "ABCD".Select(c => entorno.Archivo($"{c}.lnk")));

            store.ReordenarElemento(carpeta, carpeta.Apps[desde], antesDe);

            Assert.Equal(esperado, string.Concat(carpeta.Apps.Select(a => a.Name)));
        }

        [Fact]
        public void Crear_carpeta_moviendo_saca_el_elemento_de_su_origen()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            var juegos = store.CrearCarpeta("Juegos");
            var app = store.AgregarElementos(juegos, new[] { entorno.Archivo("A.lnk") }).Agregados[0];

            var nueva = store.CrearCarpetaMoviendo(juegos, app);

            Assert.Empty(juegos.Apps);
            Assert.Same(app, Assert.Single(nueva.Apps));
        }

        // ---------- Limpiar el Escritorio ----------

        [Fact]
        public void Con_limpiar_escritorio_el_acceso_se_guarda_y_vuelve_al_quitarlo()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            store.EstablecerLimpiarEscritorio(true);
            var carpeta = store.CrearCarpeta("Juegos");
            var enEscritorio = entorno.EnEscritorio("Steam.lnk");

            var app = store.AgregarElementos(carpeta, new[] { enEscritorio }).Agregados[0];

            Assert.False(File.Exists(enEscritorio));
            Assert.StartsWith(entorno.Rutas.CarpetaAccesos, app.Path);

            store.QuitarElemento(carpeta, app);

            Assert.True(File.Exists(enEscritorio));
        }

        [Fact]
        public void Sin_limpiar_escritorio_no_se_mueve_nada()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            var carpeta = store.CrearCarpeta("Juegos");
            var enEscritorio = entorno.EnEscritorio("Steam.lnk");

            store.AgregarElementos(carpeta, new[] { enEscritorio });

            Assert.True(File.Exists(enEscritorio));
        }

        [Fact]
        public void Deshacer_eliminar_recupera_los_accesos_que_volvieron_al_Escritorio()
        {
            using var entorno = new EntornoPrueba();
            using var store = entorno.CrearStore();
            store.EstablecerLimpiarEscritorio(true);
            var carpeta = store.CrearCarpeta("Juegos");
            var enEscritorio = entorno.EnEscritorio("Steam.lnk");
            store.AgregarElementos(carpeta, new[] { enEscritorio });

            int indice = store.EliminarCarpeta(carpeta);
            Assert.True(File.Exists(enEscritorio));           // volvió al Escritorio

            store.RestaurarCarpeta(carpeta, indice);
            Assert.False(File.Exists(enEscritorio));          // y se recuperó
            Assert.True(File.Exists(carpeta.Apps[0].Path));
        }
    }
}
