using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NovaFolder.Core.Models;
using NovaFolder.Services.Applications;
using NovaFolder.Services.Windows;

namespace NovaFolder.Controls
{
    // Un elemento dentro de una carpeta abierta: ícono grande y nombre en
    // hasta dos líneas. Igual que FolderTile, solo avisa; el popup decide.
    public sealed class AppTile : Border
    {
        public const double Ancho = 88;

        public AppShortcut App { get; }

        public event Action<AppTile>? AbrirPedido;

        protected override Type StyleKeyOverride => typeof(Border);

        public AppTile(AppShortcut app, ContextMenu menu)
        {
            App = app;
            Classes.Add("nova-elemento");
            Width = Ancho;
            Padding = new Thickness(4, 8, 4, 6);
            ContextMenu = menu;

            var existe = AppLauncherService.Existe(app.Path);
            ToolTip.SetTip(this, existe
                ? $"{app.Name}\n{app.TargetPath ?? app.Path}"
                : $"No se encuentra:\n{app.Path}\n\nClic derecho → Quitar de la carpeta");

            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new Panel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            IconService.GetIconControl(app, 44),
                            // Marca de acceso roto sobre el ícono: se entiende
                            // de un vistazo sin tener que leer el tooltip.
                            new TextBlock
                            {
                                IsVisible = !existe,
                                Text = "\uE7BA",
                                FontFamily = Interacciones.FuenteIconos,
                                FontSize = 14,
                                Foreground = (IBrush?)Application.Current?.FindResource("NovaError"),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Bottom
                            }
                        }
                    },
                    new TextBlock
                    {
                        Text = app.Name,
                        Foreground = Brushes.White,
                        Opacity = existe ? 1 : 0.6,
                        FontSize = 11.5,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 2,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            };

            Interacciones.AlActivar(this, () => AbrirPedido?.Invoke(this));
        }
    }
}
