using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NovaFolder.Controls;

namespace NovaFolder.Views
{
    // Lo que el usuario eligió al terminar la bienvenida.
    public sealed record PreferenciasIniciales(bool LimpiarEscritorio, bool IniciarConWindows);

    // Recorrido guiado de primera vez: qué es NovaFolder, cómo se crean y
    // llenan las carpetas, cómo se usan, y las dos decisiones importantes.
    // Se puede volver a ver desde "Cómo se usa" (en ese caso sin el último
    // paso de preferencias ya elegidas, que se muestra igual por si quiere
    // cambiarlas).
    public partial class BienvenidaWindow : Window
    {
        private readonly List<Control> _pasos;
        private int _actual;

        // null si el usuario cerró sin terminar.
        public PreferenciasIniciales? Resultado { get; private set; }

        public BienvenidaWindow() : this(limpiarEscritorio: true, iniciarConWindows: true) { }

        public BienvenidaWindow(bool limpiarEscritorio, bool iniciarConWindows)
        {
            InitializeComponent();
            _pasos = new List<Control> { Paso1, Paso2, Paso3, Paso4 };

            InterruptorLimpiar.IsChecked = limpiarEscritorio;
            InterruptorInicio.IsChecked = iniciarConWindows;

            TransparencyLevelHint = new List<WindowTransparencyLevel>
            {
                WindowTransparencyLevel.Mica,
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Transparent
            };

            Rellenar(Lista2,
                ("\uE710", "Pulsa + en el widget para crear una carpeta y ponle nombre."),
                ("\uE7C2", "Arrastra accesos directos del Escritorio y suéltalos sobre la carpeta."),
                ("\uE8B7", "Cada carpeta aparece también en tu Escritorio: puedes soltar cosas sobre ella."));
            Rellenar(Lista3,
                ("\uE8A7", "Clic en una carpeta para abrirla, clic en un juego o app para lanzarlo."),
                ("\uE8CB", "Arrastra dentro para ordenar, o sobre otra carpeta para moverlo."),
                ("\uE8A0", "Arrastra al Escritorio para sacarlo. Clic derecho para más opciones."));

            BotonCerrar.Click += (_, _) => Close();
            BotonAtras.Click += (_, _) => Ir(_actual - 1);
            BotonSiguiente.Click += (_, _) =>
            {
                if (_actual < _pasos.Count - 1) Ir(_actual + 1);
                else Terminar();
            };

            // Sin barra de título: se mueve arrastrando la parte de arriba.
            Barra.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Source is not Button) BeginMoveDrag(e);
            };
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close();
                else if (e.Key == Key.Right) Ir(_actual + 1);
                else if (e.Key == Key.Left) Ir(_actual - 1);
            };

            Ir(0);
        }

        private void Ir(int paso)
        {
            _actual = Math.Clamp(paso, 0, _pasos.Count - 1);
            for (int i = 0; i < _pasos.Count; i++) _pasos[i].IsVisible = i == _actual;

            BotonAtras.IsVisible = _actual > 0;
            BotonSiguiente.Content = _actual == _pasos.Count - 1 ? "Empezar" : "Siguiente";

            Puntos.Children.Clear();
            for (int i = 0; i < _pasos.Count; i++)
            {
                Puntos.Children.Add(new Border
                {
                    Width = i == _actual ? 18 : 7,
                    Height = 7,
                    CornerRadius = new CornerRadius(4),
                    Background = (IBrush?)Application.Current?.FindResource(i == _actual ? "NovaAcento" : "NovaTarjetaHover")
                });
            }
            BotonSiguiente.Focus();
        }

        private void Terminar()
        {
            Resultado = new PreferenciasIniciales(
                InterruptorLimpiar.IsChecked == true,
                InterruptorInicio.IsChecked == true);
            Close();
        }

        // Una fila por consejo: ícono morado a la izquierda y el texto.
        private static void Rellenar(StackPanel lista, params (string Glifo, string Texto)[] consejos)
        {
            foreach (var (glifo, texto) in consejos)
            {
                lista.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    Children =
                    {
                        new Border
                        {
                            Width = 32,
                            Height = 32,
                            CornerRadius = new CornerRadius(8),
                            Background = (IBrush?)Application.Current?.FindResource("NovaHueco"),
                            VerticalAlignment = VerticalAlignment.Top,
                            Child = new TextBlock
                            {
                                Text = glifo,
                                FontFamily = Interacciones.FuenteIconos,
                                FontSize = 15,
                                Foreground = (IBrush?)Application.Current?.FindResource("NovaAcento"),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        },
                        Colocar(new TextBlock
                        {
                            Text = texto,
                            FontSize = 13.5,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = Brushes.White,
                            Opacity = 0.9,
                            Margin = new Thickness(12, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        }, columna: 1)
                    }
                });
            }
        }

        private static Control Colocar(Control control, int columna)
        {
            Grid.SetColumn(control, columna);
            return control;
        }
    }
}
