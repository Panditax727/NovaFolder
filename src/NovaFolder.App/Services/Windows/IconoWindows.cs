using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Media.Imaging;

namespace NovaFolder.Services.Windows
{
    // Iconos en alta resolución del shell de Windows.
    //
    // Por qué no basta con ExtractAssociatedIcon (lo que usaba antes): devuelve
    // 32x32 y punto. En una pantalla 1080p o superior, escalado a 36 px o más,
    // se ve borroso y con bordes dentados. IShellItemImageFactory sirve el icono
    // del tamaño que le pidas hasta 256x256, que es el que Windows guarda para
    // las vistas grandes del Explorador.
    //
    // NO VERIFICADO: esto solo corre en Windows y se escribió desde Linux.
    // Si algo falla, cae con gracia al método antiguo y luego al recuadro con
    // la inicial, así que el peor caso es "se ve como antes", nunca un fallo.
    [SupportedOSPlatform("windows")]
    internal static class IconoWindows
    {
        public static Bitmap? Obtener(string path, int tamPx)
        {
            using var gdi = ObtenerGdi(path, tamPx);
            return gdi == null ? null : AvaloniaDesdeGdi(gdi);
        }

        // Mismo ícono pero como System.Drawing.Bitmap, que es lo que hace
        // falta para componerlo con otros (ver IconoCarpetaMorada y su
        // miniatura 2x2). El llamador se encarga de liberarlo.
        public static System.Drawing.Bitmap? ObtenerGdi(string path, int tamPx)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return null;

            return DesdeShellItem(path, tamPx) ?? DesdeIconoAsociado(path);
        }

        private static Bitmap? AvaloniaDesdeGdi(System.Drawing.Bitmap bmp)
        {
            try
            {
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                return new Bitmap(ms);
            }
            catch { return null; }
        }

        // ---- camino bueno: hasta 256x256 ----
        private static System.Drawing.Bitmap? DesdeShellItem(string path, int tamPx)
        {
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                var guid = new Guid(IID_IShellItemImageFactory);
                if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref guid, out var factory) != 0
                    || factory == null)
                    return null;

                try
                {
                    var tam = new SIZE { cx = tamPx, cy = tamPx };
                    // BIGGERSIZEOK: prefiere una fuente mayor y la reduce, en
                    // vez de agrandar una pequeña y emborronarla.
                    //
                    // ICONONLY es imprescindible: sin él el shell devuelve la
                    // MINIATURA del elemento cuando puede generarla, y la de
                    // una carpeta viene con fondo negro opaco en vez de
                    // transparente. Con ICONONLY siempre entrega el icono de
                    // verdad, con su canal alfa.
                    const int SIIGBF_BIGGERSIZEOK = 0x00000001;
                    const int SIIGBF_ICONONLY     = 0x00000004;
                    const int flags = SIIGBF_BIGGERSIZEOK | SIIGBF_ICONONLY;
                    if (factory.GetImage(tam, flags, out hBitmap) != 0 || hBitmap == IntPtr.Zero)
                        return null;

                    return DesdeHBitmap(hBitmap);
                }
                finally { Marshal.ReleaseComObject(factory); }
            }
            catch { return null; }
            finally { if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap); }
        }

        // Image.FromHbitmap() DESCARTA el canal alfa: todo lo que el icono
        // tenía transparente vuelve como negro opaco, y se ve un cuadrado
        // feo alrededor de cada icono. Por eso se leen los píxeles a mano
        // con GetDIBits, pidiendo explícitamente 32 bits y orden top-down.
        private static System.Drawing.Bitmap? DesdeHBitmap(IntPtr hBitmap)
        {
            try
            {
                if (GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), out var info) == 0)
                    return null;

                int ancho = info.bmWidth, alto = Math.Abs(info.bmHeight);
                if (ancho <= 0 || alto <= 0) return null;
                if (info.bmBitsPixel != 32 || info.bmBits == IntPtr.Zero) return null;

                // Se leen los píxeles del propio DIB en vez de pedirlos con
                // GetDIBits: con BI_RGB el cuarto byte está declarado como
                // "sin usar" y el driver lo devuelve en cero, así que todo lo
                // transparente del icono acababa pintado de negro opaco.
                // El DIB de IShellItemImageFactory es de 32 bits con alfa
                // premultiplicado, que es justo lo que hace falta.
                int filas = alto;
                int anchoBytes = info.bmWidthBytes;
                var pixeles = new byte[anchoBytes * filas];
                Marshal.Copy(info.bmBits, pixeles, 0, pixeles.Length);

                // Un icono sin canal alfa real llega con todo el cuarto byte
                // en cero. Respetarlo lo dejaría invisible, así que ahí sí se
                // fuerza opaco (y deja de ser premultiplicado).
                bool hayAlfa = false;
                for (int i = 3; i < pixeles.Length; i += 4)
                    if (pixeles[i] != 0) { hayAlfa = true; break; }

                var formato = PixelFormat.Format32bppPArgb;
                if (!hayAlfa)
                {
                    for (int i = 3; i < pixeles.Length; i += 4) pixeles[i] = 255;
                    formato = PixelFormat.Format32bppArgb;
                }

                var bmp = new System.Drawing.Bitmap(ancho, alto, formato);
                var datos = bmp.LockBits(
                    new System.Drawing.Rectangle(0, 0, ancho, alto),
                    ImageLockMode.WriteOnly, formato);
                try
                {
                    // El DIB viene BOTTOM-UP: su primera fila es la de abajo
                    // del icono. Comprobado en pantalla: sin invertir, las
                    // carpetas de Windows salían con la pestaña abajo y el
                    // logo de Spotify con las ondas del revés.
                    for (int y = 0; y < filas; y++)
                        Marshal.Copy(pixeles, (filas - 1 - y) * anchoBytes,
                                     datos.Scan0 + y * datos.Stride, ancho * 4);
                }
                finally { bmp.UnlockBits(datos); }

                return bmp;
            }
            catch { return null; }
        }

        // ---- respaldo: el metodo de siempre, 32x32 ----
        private static System.Drawing.Bitmap? DesdeIconoAsociado(string path)
        {
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                return icon?.ToBitmap();
            }
            catch { return null; }
        }

        private const string IID_IShellItemImageFactory = "bcc18b79-ba16-442f-80c4-8a59c30c463b";

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [ComImport, Guid(IID_IShellItemImageFactory)]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern int SHCreateItemFromParsingName(
            string pszPath, IntPtr pbc, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType, bmWidth, bmHeight, bmWidthBytes;
            public ushort bmPlanes, bmBitsPixel;
            public IntPtr bmBits;
        }

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);
    }
}
