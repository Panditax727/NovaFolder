using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Media.Imaging;

namespace android_folder_win11.Services
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
            if (!File.Exists(path) && !Directory.Exists(path)) return null;

            return DesdeShellItem(path, tamPx) ?? DesdeIconoAsociado(path);
        }

        // ---- camino bueno: hasta 256x256 ----
        private static Bitmap? DesdeShellItem(string path, int tamPx)
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
                    // RESIZETOFIT | BIGGERSIZEOK: prefiere una fuente mayor y la
                    // reduce, en vez de agrandar una pequeña y emborronarla.
                    const int flags = 0x00000000 | 0x00000001;
                    if (factory.GetImage(tam, flags, out hBitmap) != 0 || hBitmap == IntPtr.Zero)
                        return null;

                    return DesdeHBitmap(hBitmap);
                }
                finally { Marshal.ReleaseComObject(factory); }
            }
            catch { return null; }
            finally { if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap); }
        }

        private static Bitmap? DesdeHBitmap(IntPtr hBitmap)
        {
            try
            {
                using var bmp = System.Drawing.Image.FromHbitmap(hBitmap);
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                return new Bitmap(ms);
            }
            catch { return null; }
        }

        // ---- respaldo: el metodo de siempre, 32x32 ----
        private static Bitmap? DesdeIconoAsociado(string path)
        {
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon == null) return null;
                using var ms = new MemoryStream();
                using var bmp = icon.ToBitmap();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                return new Bitmap(ms);
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
    }
}
