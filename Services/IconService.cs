using System;
using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using android_folder_win11.Models;

namespace android_folder_win11.Services
{
    // Toda la lógica de "cómo conseguir un ícono para mostrar" vive aquí.
    // En Windows extrae el ícono real en alta resolución; en cualquier otro
    // sistema (o si falla) genera una placa con la inicial y un color propio,
    // así puedes seguir probando la interfaz mientras desarrollas en Linux.
    public static class IconService
    {
        // Extraer un icono toca disco y, en Windows, el shell. Sin caché se
        // repetiría en cada redibujado: una carpeta de 8 apps que se abre y se
        // cierra son 16 extracciones por ciclo.
        private static readonly ConcurrentDictionary<string, Bitmap?> Cache = new();

        // Se pide siempre a 256 y se deja que Avalonia escale al vuelo: así el
        // mismo bitmap sirve para la vista previa de 24 px y para la de 36, y
        // se ve nítido en pantallas con escalado.
        private const int TamExtraccion = 256;

        public static Control GetIconControl(AppShortcut? app, double size)
        {
            if (app == null)
                return new Border { Width = size, Height = size, Margin = new Thickness(2) };

            var bitmap = Obtener(app.IconSource);

            if (bitmap != null)
            {
                var img = new Image
                {
                    Source = bitmap,
                    Width = size,
                    Height = size,
                    Margin = new Thickness(2)
                };
                // El icono se extrae a 256 px y se muestra a 24-36: sin filtrado
                // de calidad el reescalado deja bordes dentados. Es propiedad
                // adjunta, no una propiedad de Image.
                RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.HighQuality);
                ToolTip.SetTip(img, app.TargetPath ?? app.Path);
                return img;
            }

            return CrearPlaca(app, size);
        }

        private static Bitmap? Obtener(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta)) return null;

            return Cache.GetOrAdd(ruta, r =>
            {
                if (!OperatingSystem.IsWindows()) return null;
                return ObtenerEnWindows(r);
            });
        }

        [SupportedOSPlatform("windows")]
        private static Bitmap? ObtenerEnWindows(string ruta) =>
            IconoWindows.Obtener(ruta, TamExtraccion);

        // ---- placa de respaldo ----
        // Antes eran todas del mismo azul, así que una carpeta llena parecía un
        // tablero de fichas iguales. Ahora el color sale del nombre, es estable
        // entre arranques y distingue las apps de un vistazo.
        private static Control CrearPlaca(AppShortcut app, double size)
        {
            var falta = !AppLauncherService.Existe(app.Path);
            var (c1, c2) = ColoresDe(app.Name, falta);

            var borde = new Border
            {
                Width = size,
                Height = size,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(size >= 32 ? 8 : 5),
                Opacity = falta ? 0.5 : 1.0,
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(c1, 0),
                        new GradientStop(c2, 1)
                    }
                },
                Child = new TextBlock
                {
                    Text = InicialDe(app.Name),
                    Foreground = Brushes.White,
                    FontSize = Math.Max(10, size * 0.46),
                    FontWeight = FontWeight.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            ToolTip.SetTip(borde, falta
                ? $"No se encuentra:\n{app.Path}"
                : app.TargetPath ?? app.Path);

            return borde;
        }

        private static string InicialDe(string nombre)
        {
            foreach (var ch in nombre)
                if (char.IsLetterOrDigit(ch)) return char.ToUpperInvariant(ch).ToString();
            return "?";
        }

        // Hash estable (no GetHashCode, que varía entre ejecuciones) -> tono fijo.
        private static (Color, Color) ColoresDe(string nombre, bool apagado)
        {
            if (apagado)
                return (Color.FromRgb(104, 104, 116), Color.FromRgb(78, 78, 88));

            uint h = 2166136261;
            foreach (var ch in nombre) { h ^= ch; h *= 16777619; }

            double tono = h % 360;
            return (DesdeHsl(tono, 0.52, 0.56), DesdeHsl(tono, 0.55, 0.42));
        }

        private static Color DesdeHsl(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = l - c / 2;
            (double r, double g, double b) = h switch
            {
                < 60  => (c, x, 0.0),
                < 120 => (x, c, 0.0),
                < 180 => (0.0, c, x),
                < 240 => (0.0, x, c),
                < 300 => (x, 0.0, c),
                _     => (c, 0.0, x)
            };
            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }
    }
}
