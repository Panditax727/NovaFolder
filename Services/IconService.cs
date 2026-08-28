using System;
using System.IO;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using android_folder_win11.Models;

namespace android_folder_win11.Services
{
    // Toda la lógica de "cómo conseguir un ícono para mostrar" vive aquí.
    // En Windows intenta extraer el ícono real del archivo; en cualquier otro
    // sistema (o si falla), devuelve un cuadro de color con la inicial del nombre,
    // así puedes seguir probando la interfaz mientras desarrollas en Linux.
    public static class IconService
    {
        public static Control GetIconControl(AppShortcut? app, double size)
        {
            if (app == null)
            {
                return new Border { Width = size, Height = size, Margin = new Avalonia.Thickness(2) };
            }

            Bitmap? bitmap = OperatingSystem.IsWindows()
                ? ExtraerIconoWindows(app.Path)
                : null;

            if (bitmap != null)
            {
                return new Image
                {
                    Source = bitmap,
                    Width = size,
                    Height = size,
                    Margin = new Avalonia.Thickness(2)
                };
            }

            return CrearFallback(app, size);
        }

        private static Control CrearFallback(AppShortcut app, double size)
        {
            return new Border
            {
                Width = size,
                Height = size,
                Margin = new Avalonia.Thickness(2),
                Background = new SolidColorBrush(Color.FromRgb(60, 130, 200)),
                CornerRadius = new Avalonia.CornerRadius(4),
                Child = new TextBlock
                {
                    Text = app.Name.Length > 0 ? app.Name[0].ToString().ToUpper() : "?",
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        [SupportedOSPlatform("windows")]
        private static Bitmap? ExtraerIconoWindows(string path)
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
            catch
            {
                return null;
            }
        }
    }
}
