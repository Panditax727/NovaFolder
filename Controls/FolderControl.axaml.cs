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
        private const double AnchoAbierto = 220;

        // Medidas de una fila del panel abierto, para calcular el alto:
        //   icono 36 + su margen (2+2) = 40
        //   nombre a 11 px, hasta dos líneas por TextWrapping = 30
        //   margen del item (4+4) = 8
        private const double AltoFila = 78;
        private const double AltoTitulo = 30;   // título + su margen inferior
        private const double MargenPanel = 24;  // márgenes del StackPanel exterior

        // Solo una carpeta abierta a la vez. Sin esto, abrir la segunda dejaba
        // las dos expandidas: el WrapPanel se reacomodaba y las tarjetas saltaban
        // de sitio bajo el cursor.
        private static FolderControl? _abiertaActual;

        private bool _abierta;
        private Border? _borde;
        private StackPanel? _vistaColapsada, _vistaExpandida;
        private TextBlock? _aviso;
        private double _altoAbierto = 210;

        public FolderControl()
        {
            InitializeComponent();
        }

        public void SetFolder(AppFolder folder)
        {
            _abierta = false;
            Content = ConstruirVisual(folder);
        }

        private Border ConstruirVisual(AppFolder folder)
        {
            // Alto calculado a partir del número de apps. Antes era una constante
            // de 210 px con ClipToBounds, así que a partir de la quinta app las
            // filas de abajo quedaban recortadas y no había forma de verlas.
            int filas = (int)Math.Ceiling(folder.Apps.Count / 2.0);
            _altoAbierto = Math.Max(AltoCerrado, MargenPanel + AltoTitulo + filas * AltoFila);

            _vistaColapsada = ConstruirVistaColapsada(folder);
            _vistaExpandida = ConstruirVistaExpandida(folder);

            var contentPanel = new Panel();
            contentPanel.Children.Add(_vistaColapsada);
            contentPanel.Children.Add(_vistaExpandida);

            _borde = new Border
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

            // El handler es async void: si Toggle lanzara, la excepción no tendría
            // quién la recoja y se llevaría el proceso entero. Por eso el try.
            _borde.PointerPressed += async (s, e) =>
            {
                try { await Toggle(); }
                catch (Exception ex) { Console.Error.WriteLine($"Error al abrir la carpeta: {ex}"); }
            };

            return _borde;
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

            if (folder.Apps.Count == 0)
            {
                view.Children.Add(new TextBlock
                {
                    Text = "Carpeta vacía",
                    Foreground = Brushes.Gainsboro,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                return view;
            }

            // Una fila por CADA DOS apps. Antes se creaba una fila por app, así que
            // con 8 apps se definían 8 filas para colocar cosas solo en 4.
            int filas = (int)Math.Ceiling(folder.Apps.Count / 2.0);
            var appsGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", filas)))
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
                    var error = AppLauncherService.Lanzar(app.Path);
                    MostrarAviso(error);
                };

                Grid.SetRow(itemStack, i / 2);
                Grid.SetColumn(itemStack, i % 2);
                appsGrid.Children.Add(itemStack);
            }

            view.Children.Add(appsGrid);

            // Hueco para el mensaje de error: en Windows no hay consola donde
            // mirar, así que el fallo tiene que verse en la propia ventana.
            _aviso = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(255, 140, 140)),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0),
                IsVisible = false
            };
            view.Children.Add(_aviso);

            return view;
        }

        private void MostrarAviso(string? error)
        {
            if (_aviso == null) return;

            if (error == null)
            {
                _aviso.IsVisible = false;
                return;
            }

            _aviso.Text = error;
            _aviso.IsVisible = true;

            // El aviso necesita sitio: se agranda la tarjeta mientras se muestra.
            if (_borde != null && _abierta)
                _borde.Height = _altoAbierto + 36;
        }

        private async Task Toggle()
        {
            if (_borde == null || _vistaColapsada == null || _vistaExpandida == null) return;

            if (!_abierta && _abiertaActual != null && _abiertaActual != this)
                await _abiertaActual.Cerrar();

            _abierta = !_abierta;

            if (_abierta)
            {
                _abiertaActual = this;
                _borde.Width = AnchoAbierto;
                _borde.Height = _altoAbierto;
                _vistaColapsada.IsVisible = false;
                _vistaExpandida.IsVisible = true;
                await Task.Delay(10); // deja aplicar IsVisible antes de animar la opacidad
                _vistaExpandida.Opacity = 1;
            }
            else
            {
                await Cerrar();
            }
        }

        private async Task Cerrar()
        {
            if (_borde == null || _vistaColapsada == null || _vistaExpandida == null) return;

            _abierta = false;
            if (_abiertaActual == this) _abiertaActual = null;
            if (_aviso != null) _aviso.IsVisible = false;

            _vistaExpandida.Opacity = 0;
            _borde.Width = AnchoCerrado;
            _borde.Height = AltoCerrado;
            await Task.Delay(180);
            _vistaExpandida.IsVisible = false;
            _vistaColapsada.IsVisible = true;
        }
    }
}
