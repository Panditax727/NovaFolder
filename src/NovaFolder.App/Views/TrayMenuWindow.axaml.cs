using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NovaFolder.Services.Windows;
using NovaFolder.Core.Diagnostics;

namespace NovaFolder.Views
{
    // Menú propio para el ícono de la bandeja del sistema.
    //
    // Por qué no se usa TrayIcon.Menu, que sería lo natural: en Avalonia
    // 12, TrayIconImpl.OnRightClicked() crea la ventana del menú y llama a
    // Show() sin activarla. En Win32 un menú de bandeja tiene que pasar a
    // primer plano (SetForegroundWindow) o el sistema lo cierra en cuanto
    // el puntero se mueve, así que el menú aparecía y desaparecía sin
    // dejar elegir nada. Esta ventana hace lo mismo pero activándose, y de
    // paso queda con el aspecto del resto de la app.
    public partial class TrayMenuWindow : Window
    {
        private const int MargenPuntero = 8;

        public TrayMenuWindow() => InitializeComponent();

        // Una entrada del menú. Marcada != null la muestra como casilla
        // (con ✓ si está activa); Separador dibuja una línea en su lugar.
        public sealed record Opcion(string Texto, Action Accion, bool? Marcada = null)
        {
            public static readonly Opcion Separador = new("", () => { });
        }

        public TrayMenuWindow(IReadOnlyList<Opcion> opciones)
        {
            InitializeComponent();

            TransparencyLevelHint = new List<WindowTransparencyLevel>
            {
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Blur,
                WindowTransparencyLevel.Transparent
            };

            foreach (var opcion in opciones)
                Opciones.Children.Add(ReferenceEquals(opcion, Opcion.Separador)
                    ? new Border { Height = 1, Margin = new Thickness(8, 4), Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) }
                    : CrearOpcion(opcion));

            // Se recoloca en CADA SizeChanged: con SizeToContent la ventana
            // pasa por su tamaño por defecto antes de encogerse al del
            // contenido, y quedarse con esa primera medida la mandaba a la
            // esquina de la pantalla.
            SizeChanged += (_, _) =>
            {
                if (!OperatingSystem.IsWindows()) return;
                if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

                PunteroService.ColocarSobreElPuntero(this, MargenPuntero);
            };

            // Sin activar, la ventana se muestra pero no toma el foco, y
            // Deactivated no llegaría nunca: el menú quedaría abierto para
            // siempre. Es justo lo que le falta al menú nativo de Avalonia.
            Opened += (_, _) => Activate();

            Deactivated += (_, _) => Close();
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }

        private Control CrearOpcion(Opcion opcion)
        {
            var fondoNormal = new SolidColorBrush(Colors.Transparent);
            var fondoHover = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255));

            var item = new Border
            {
                Background = fondoNormal,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 14, 8),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        // Columna fija para la marca: los textos quedan
                        // alineados haya o no casilla.
                        new TextBlock
                        {
                            Text = opcion.Marcada == true ? "\uE73E" : "",
                            FontFamily = Controls.Interacciones.FuenteIconos,
                            FontSize = 12,
                            Width = 14,
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = opcion.Texto,
                            Foreground = Brushes.White,
                            FontSize = 13,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };

            item.PointerEntered += (_, _) => item.Background = fondoHover;
            item.PointerExited += (_, _) => item.Background = fondoNormal;
            item.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                // Cerrar primero: si la acción abre otra ventana, el menú ya
                // no está de por medio robándole el foco.
                Close();
                try { opcion.Accion(); }
                catch (Exception ex) { Log.Advertencia($"Error en el menú de bandeja: {ex.Message}"); }
            };

            return item;
        }
    }
}
