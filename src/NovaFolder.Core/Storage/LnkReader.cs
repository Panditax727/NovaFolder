using System;
using System.IO;
using System.Text;

namespace NovaFolder.Core.Storage
{
    // Lector del formato .lnk (MS-SHLLINK) escrito a mano en C#.
    //
    // Por qué no usar el shell de Windows: IWshShortcut o IShellLink obligan a
    // COM y solo funcionan en Windows, así que no habría forma de probar esto
    // mientras se desarrolla en Linux. El formato está documentado y son unas
    // pocas decenas de líneas, así que se lee directamente del binario y
    // funciona igual en cualquier sistema.
    //
    // Sirve para dos cosas: sacar el ejecutable de destino (y con él un icono
    // decente) y el nombre real, en vez de quedarse con el del acceso directo.
    public static class LnkReader
    {
        private const uint FirmaLnk = 0x0000004C;
        private static readonly byte[] GuidLnk =
        {
            0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46
        };

        [Flags]
        private enum Flags : uint
        {
            HasLinkTargetIDList = 1 << 0,
            HasLinkInfo         = 1 << 1,
            HasName             = 1 << 2,
            HasRelativePath     = 1 << 3,
            HasWorkingDir       = 1 << 4,
            HasArguments        = 1 << 5,
            HasIconLocation     = 1 << 6,
            IsUnicode           = 1 << 7
        }

        public sealed class Info
        {
            public string? Destino { get; init; }        // ruta del ejecutable
            public string? Descripcion { get; init; }    // el "comentario" del acceso directo
            public string? RutaRelativa { get; init; }
            public string? Icono { get; init; }          // archivo del que sacar el icono
            public string? Argumentos { get; init; }
        }

        public static bool EsLnk(string path) =>
            path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);

        // Devuelve null si el archivo no existe o no es un .lnk válido:
        // nunca lanza, para que un acceso directo corrupto no tumbe la interfaz.
        public static Info? Leer(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;

                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);

                if (fs.Length < 76) return null;
                if (br.ReadUInt32() != FirmaLnk) return null;

                var guid = br.ReadBytes(16);
                for (int i = 0; i < 16; i++)
                    if (guid[i] != GuidLnk[i]) return null;

                var flags = (Flags)br.ReadUInt32();
                fs.Seek(76, SeekOrigin.Begin);   // saltar el resto de la cabecera

                if (flags.HasFlag(Flags.HasLinkTargetIDList))
                {
                    ushort tamIdList = br.ReadUInt16();
                    fs.Seek(tamIdList, SeekOrigin.Current);
                }

                string? destino = null;
                if (flags.HasFlag(Flags.HasLinkInfo))
                    destino = LeerLinkInfo(fs, br);

                bool uni = flags.HasFlag(Flags.IsUnicode);
                string? nombre = flags.HasFlag(Flags.HasName)         ? LeerCadena(br, uni) : null;
                string? relativa = flags.HasFlag(Flags.HasRelativePath) ? LeerCadena(br, uni) : null;
                if (flags.HasFlag(Flags.HasWorkingDir))  LeerCadena(br, uni);
                string? args = flags.HasFlag(Flags.HasArguments)      ? LeerCadena(br, uni) : null;
                string? icono = flags.HasFlag(Flags.HasIconLocation)  ? LeerCadena(br, uni) : null;

                // Si LinkInfo no dio nada, la ruta relativa suele bastar:
                // viene como "..\..\Program Files\App\app.exe" respecto al .lnk
                if (string.IsNullOrEmpty(destino) && !string.IsNullOrEmpty(relativa))
                {
                    var baseDir = Path.GetDirectoryName(Path.GetFullPath(path));
                    if (baseDir != null)
                    {
                        // El .lnk trae separadores de Windows; se normalizan para
                        // que la ruta tambien se resuelva bien al desarrollar en Linux.
                        var rel = relativa.Replace('\\', Path.DirectorySeparatorChar);
                        try { destino = Path.GetFullPath(Path.Combine(baseDir, rel)); }
                        catch { /* ruta relativa rara: se ignora */ }
                    }
                }

                return new Info
                {
                    Destino = destino,
                    Descripcion = nombre,
                    RutaRelativa = relativa,
                    Icono = icono,
                    Argumentos = args
                };
            }
            catch
            {
                return null;
            }
        }

        private static string? LeerLinkInfo(FileStream fs, BinaryReader br)
        {
            long inicio = fs.Position;
            uint tam = br.ReadUInt32();
            if (tam < 0x1C || inicio + tam > fs.Length) return null;

            uint tamCabecera = br.ReadUInt32();
            uint banderas = br.ReadUInt32();
            br.ReadUInt32();                       // VolumeIDOffset
            uint offRutaLocal = br.ReadUInt32();
            br.ReadUInt32();                       // CommonNetworkRelativeLinkOffset
            uint offSufijo = br.ReadUInt32();

            uint offRutaLocalUni = 0, offSufijoUni = 0;
            if (tamCabecera >= 0x24)
            {
                offRutaLocalUni = br.ReadUInt32();
                offSufijoUni = br.ReadUInt32();
            }

            // bit 0 = VolumeIDAndLocalBasePath
            if ((banderas & 1) == 0) { fs.Seek(inicio + tam, SeekOrigin.Begin); return null; }

            string ruta = "", sufijo = "";
            if (offRutaLocalUni != 0 && tamCabecera >= 0x24)
            {
                fs.Seek(inicio + offRutaLocalUni, SeekOrigin.Begin);
                ruta = LeerZTerminada(fs, Encoding.Unicode);
                if (offSufijoUni != 0)
                {
                    fs.Seek(inicio + offSufijoUni, SeekOrigin.Begin);
                    sufijo = LeerZTerminada(fs, Encoding.Unicode);
                }
            }
            else if (offRutaLocal != 0)
            {
                fs.Seek(inicio + offRutaLocal, SeekOrigin.Begin);
                ruta = LeerZTerminada(fs, Ansi);
                if (offSufijo != 0)
                {
                    fs.Seek(inicio + offSufijo, SeekOrigin.Begin);
                    sufijo = LeerZTerminada(fs, Ansi);
                }
            }

            fs.Seek(inicio + tam, SeekOrigin.Begin);   // dejar el cursor donde toca
            var completa = ruta + sufijo;
            return string.IsNullOrWhiteSpace(completa) ? null : completa;
        }

        // OJO: Encoding.Default en .NET Core es SIEMPRE UTF-8, no la pagina de
        // codigos ANSI de Windows. Los .lnk guardan las rutas "ANSI" en CP1252,
        // asi que con Default cualquier ruta con tilde o eñe salia corrupta
        // ("Aplicaci?n.exe"). Latin1 comparte con CP1252 todo el rango alto
        // (0xA0-0xFF), que es donde viven los acentos, y va incluido en el
        // framework sin añadir dependencias.
        private static readonly Encoding Ansi = Encoding.Latin1;

        private static string LeerZTerminada(FileStream fs, Encoding enc)
        {
            var bytes = new System.Collections.Generic.List<byte>();
            int anchoChar = enc.Equals(Encoding.Unicode) ? 2 : 1;
            var buf = new byte[anchoChar];

            while (fs.Read(buf, 0, anchoChar) == anchoChar)
            {
                bool fin = true;
                foreach (var b in buf) if (b != 0) { fin = false; break; }
                if (fin) break;
                bytes.AddRange(buf);
            }
            return enc.GetString(bytes.ToArray());
        }

        // Las cadenas de StringData van con un contador de CARACTERES por delante,
        // no de bytes: en unicode hay que multiplicar por dos.
        private static string? LeerCadena(BinaryReader br, bool unicode)
        {
            try
            {
                ushort chars = br.ReadUInt16();
                if (chars == 0) return null;
                int bytes = unicode ? chars * 2 : chars;
                var datos = br.ReadBytes(bytes);
                return (unicode ? Encoding.Unicode : Ansi).GetString(datos);
            }
            catch { return null; }
        }
    }
}
