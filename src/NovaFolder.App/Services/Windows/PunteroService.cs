using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;

namespace NovaFolder.Services.Windows
{
    // Dónde está el puntero del ratón, y cómo colocar una ventana junto a
    // él (o junto a un control) sin que se salga de la pantalla.
    //
    // Avalonia solo informa la posición del puntero dentro de una ventana
    // propia y a partir de un evento; acá hace falta saberla antes de que
    // exista ninguna ventana, para abrir el popup justo donde el usuario
    // acaba de hacer doble clic sobre el ícono del Escritorio.
    //
    // OJO con el momento de llamar a esto: hay que hacerlo cuando la
    // ventana YA tiene tamaño. Con SizeToContent, en el evento Opened
    // Bounds todavía mide cero, y la cuenta salía mal (la ventana
    // terminaba pegada al borde izquierdo en vez de junto al cursor).
    [SupportedOSPlatform("windows")]
    internal static class PunteroService
    {
        public static PixelPoint? Posicion() =>
            GetCursorPos(out var p) ? new PixelPoint(p.X, p.Y) : null;

        // Caso del ícono de la bandeja: vive abajo a la derecha, así que el
        // menú se despliega hacia arriba y centrado en el puntero.
        public static void ColocarSobreElPuntero(Window ventana, int margen)
        {
            var puntero = Posicion();
            if (puntero == null || !Medir(ventana, puntero.Value, out var ancho, out var alto, out var area)) return;

            int x = puntero.Value.X - ancho / 2;
            int y = puntero.Value.Y - alto - margen;
            if (y < area.Y) y = puntero.Value.Y + margen;

            Recortar(ventana, x, y, ancho, alto, area);
        }

        public static void ColocarJuntoAlPuntero(Window ventana, int margen)
        {
            var puntero = Posicion();
            if (puntero == null) return;
            ColocarJuntoA(ventana, new PixelRect(puntero.Value, new PixelSize(0, 0)), margen);
        }

        // A la derecha del ancla y alineada con su borde superior; si no
        // cabe, a la izquierda. Es como se despliega un submenú, y deja a
        // la vista la tarjeta de la que salió la carpeta.
        public static void ColocarJuntoA(Window ventana, PixelRect ancla, int margen)
        {
            if (!Medir(ventana, ancla.Center, out var ancho, out var alto, out var area)) return;

            int x = ancla.Right + margen;
            int y = ancla.Y;
            if (x + ancho > area.Right) x = ancla.X - ancho - margen;
            if (y + alto > area.Bottom) y = area.Bottom - alto;

            Recortar(ventana, x, y, ancho, alto, area);
        }

        private static bool Medir(Window ventana, PixelPoint referencia, out int ancho, out int alto, out PixelRect area)
        {
            ancho = (int)Math.Ceiling(ventana.Bounds.Width * ventana.RenderScaling);
            alto = (int)Math.Ceiling(ventana.Bounds.Height * ventana.RenderScaling);
            area = default;
            if (ancho <= 0 || alto <= 0) return false;

            var pantalla = ventana.Screens.ScreenFromPoint(referencia) ?? ventana.Screens.Primary;
            if (pantalla == null) return false;
            area = pantalla.WorkingArea;
            return true;
        }

        // Último recorte, por si la ventana es más grande que el hueco
        // disponible a cualquiera de los dos lados.
        private static void Recortar(Window ventana, int x, int y, int ancho, int alto, PixelRect area)
        {
            x = Math.Max(area.X, Math.Min(x, area.Right - ancho));
            y = Math.Max(area.Y, Math.Min(y, area.Bottom - alto));
            ventana.Position = new PixelPoint(x, y);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);
    }
}
