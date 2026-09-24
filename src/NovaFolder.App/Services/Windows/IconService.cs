using System;
using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NovaFolder.Core.Models;
using NovaFolder.Services.Applications;

namespace NovaFolder.Services.Windows
{
    // Toda la lógica de "cómo conseguir un ícono para mostrar" vive aquí.
    // En Windows extrae el ícono real en alta resolución; en cualquier otro
    // sistema (o si falla) genera una placa con la inicial y un color propio,
    // así puedes seguir probando la interfaz mientras desarrollas en Linux.
    //
    // La extracción es asíncrona: el control se devuelve al instante con un
    // hueco tenue y el ícono aparece con un fundido cuando está listo. Así
    // abrir una carpeta nunca espera al shell.
    public static class IconService
    {
        // Se guarda la tarea, no el bitmap: si dos controles piden el mismo
        // ícono mientras se extrae, esperan a la misma extracción.
        private static readonly ConcurrentDictionary<string, Task<Bitmap?>> Cache = new(StringComparer.OrdinalIgnoreCase);

        // Se pide siempre a 256 y se deja que Avalonia escale al vuelo: así el
        // mismo bitmap sirve para la vista previa de 22 px y para la de 48, y
        // se ve nítido en pantallas con escalado.
        private const int TamExtraccion = 256;

        private static readonly IBrush FondoHueco = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));

        public static Control GetIconControl(AppShortcut? app, double size)
        {
            if (app == null)
                return new Border { Width = size, Height = size, Margin = new Thickness(2) };

            var contenedor = new Panel { Width = size, Height = size, Margin = new Thickness(2) };
            var tarea = Obtener(app.IconSource);

            // Ya estaba en caché: se pinta directo, sin hueco ni fundido.
            if (tarea.IsCompleted)
            {
                contenedor.Children.Add(CrearVisual(app, size, tarea.IsCompletedSuccessfully ? tarea.Result : null, animar: false));
                return contenedor;
            }

            contenedor.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(size >= 32 ? 10 : 6),
                Background = FondoHueco
            });

            tarea.ContinueWith(t =>
            {
                var bmp = t.IsCompletedSuccessfully ? t.Result : null;
                Dispatcher.UIThread.Post(() =>
                {
                    contenedor.Children.Clear();
                    contenedor.Children.Add(CrearVisual(app, size, bmp, animar: true));
                });
            }, TaskScheduler.Default);

            return contenedor;
        }

        private static Control CrearVisual(AppShortcut app, double size, Bitmap? bitmap, bool animar)
        {
            Control visual;
            if (bitmap != null)
            {
                var img = new Image { Source = bitmap, Width = size, Height = size };
                // El icono se extrae a 256 px y se muestra a 22-48: sin filtrado
                // de calidad el reescalado deja bordes dentados. Es propiedad
                // adjunta, no una propiedad de Image.
                RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.HighQuality);
                visual = img;
            }
            else
            {
                visual = CrearPlaca(app, size);
            }

            if (animar)
            {
                var opacidadFinal = visual.Opacity;
                visual.Opacity = 0;
                visual.Transitions = new Transitions
                {
                    new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(140) }
                };
                // En el mismo frame en que se añade no hay "valor anterior" del
                // que animar: se deja pasar uno.
                Dispatcher.UIThread.Post(() => visual.Opacity = opacidadFinal, DispatcherPriority.Background);
            }

            return visual;
        }

        private static Task<Bitmap?> Obtener(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta) || !OperatingSystem.IsWindows())
                return Task.FromResult<Bitmap?>(null);

            return Cache.GetOrAdd(ruta, r => StaWorker.Compartido.Ejecutar(() =>
                OperatingSystem.IsWindows() ? ObtenerEnWindows(r) : null));
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

            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size >= 32 ? 10 : 5),
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
