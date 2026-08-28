using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using android_folder_win11.Models;
using android_folder_win11.Services;

namespace android_folder_win11.Controls
{
    // Widget autocontenido: se le pasa un AppFolder con SetFolder() y él solo
    // se encarga de dibujarse, expandirse/colapsarse y lanzar las apps al hacer click.
    // MainWindow no sabe (ni le importa) cómo funciona por dentro.
    public partial class FolderControl : UserControl
    {
        private const double AnchoCerrado = 100, AltoCerrado = 130;
        private const double AnchoAbierto = 220, AltoAbierto = 210;

        private bool _abierta = false;

        public FolderControl()
        {
            InitializeComponent();
        }

        public void SetFolder(AppFolder folder)
        {
            Content = ConstruirVisual(folder);
        }

        private Border ConstruirVisual(AppFolder folder)
        {
            var collapsedView = ConstruirVistaColapsada(folder);
            var expandedView = ConstruirVistaExpandida(folder);

            var contentPanel = new Panel();
            contentPanel.Children.Add(collapsedView);
            contentPanel.Children.Add(expandedView);

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(10),
                Width = AnchoCerrado,
                Height = AltoCerrado,
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = contentPanel,
                ClipToBounds = true,
                Transitions = new Transitions
                {
                    new DoubleTransition { Property = Border.WidthProperty, Duration = TimeSpan.FromMilliseconds(200) },
                    new DoubleTransition { Property = Border.HeightProperty, Duration = TimeSpan.FromMilliseconds(200) }
                }
            };

            border.PointerPressed += async (s, e) => await Toggle(border, collapsedView, expandedView);

            return border;
        }

        private StackPanel ConstruirVistaColapsada(AppFolder folder)
        {
            var previewGrid = new Grid
            {
                Width = 56,
                Height = 56,
                RowDefinitions = new RowDefinitions("*,*"),
                ColumnDefinitions = new ColumnDefinitions("*,*")
            };

            for (int i = 0; i < 4; i++)
            {
                var app = i < folder.Apps.Count ? folder.Apps[i] : null;
                var cell = IconService.GetIconControl(app, 24);
                Grid.SetRow(cell, i / 2);
                Grid.SetColumn(cell, i % 2);
                previewGrid.Children.Add(cell);
            }

            var view = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            view.Children.Add(previewGrid);
            view.Children.Add(new TextBlock
            {
                Text = folder.Name,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            });
            return view;
        }

        private StackPanel ConstruirVistaExpandida(AppFolder folder)
        {
            var view = new StackPanel
            {
                IsVisible = false,
                Opacity = 0,
                Margin = new Thickness(10),
                Transitions = new Transitions
                {
                    new DoubleTransition { Property = StackPanel.OpacityProperty, Duration = TimeSpan.FromMilliseconds(150) }
                }
            };

            view.Children.Add(new TextBlock
            {
                Text = folder.Name,
                Foreground = Brushes.White,
                FontWeight = FontWeight.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var appsGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                RowDefinitions = new RowDefinitions(string.Join(",", folder.Apps.Select(_ => "Auto")))
            };

            for (int i = 0; i < folder.Apps.Count; i++)
            {
                var app = folder.Apps[i];

                var itemStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(4),
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                itemStack.Children.Add(IconService.GetIconControl(app, 36));
                itemStack.Children.Add(new TextBlock
                {
                    Text = app.Name,
                    Foreground = Brushes.White,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 70
                });

                itemStack.PointerPressed += (s, e) =>
                {
                    e.Handled = true; // evita que también dispare el toggle de la carpeta
                    AppLauncherService.Lanzar(app.Path);
                };

                Grid.SetRow(itemStack, i / 2);
                Grid.SetColumn(itemStack, i % 2);
                appsGrid.Children.Add(itemStack);
            }

            view.Children.Add(appsGrid);
            return view;
        }

        private async Task Toggle(Border border, StackPanel collapsedView, StackPanel expandedView)
        {
            _abierta = !_abierta;

            if (_abierta)
            {
                border.Width = AnchoAbierto;
                border.Height = AltoAbierto;
                collapsedView.IsVisible = false;
                expandedView.IsVisible = true;
                await Task.Delay(10); // deja aplicar IsVisible antes de animar la opacidad
                expandedView.Opacity = 1;
            }
            else
            {
                expandedView.Opacity = 0;
                border.Width = AnchoCerrado;
                border.Height = AltoCerrado;
                await Task.Delay(180);
                expandedView.IsVisible = false;
                collapsedView.IsVisible = true;
            }
        }
    }
}
