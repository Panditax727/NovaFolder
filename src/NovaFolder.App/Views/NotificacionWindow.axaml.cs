using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace NovaFolder.Views
{
    // Aviso breve en la esquina inferior derecha, junto a la bandeja, como
    // las notificaciones de Windows. Se usa cuando el widget está oculto y
    // no hay otro sitio donde decirle algo al usuario.
    public partial class NotificacionWindow : Window
    {
        private static readonly TimeSpan Duracion = TimeSpan.FromSeconds(7);
        private const int Margen = 12;

        private readonly DispatcherTimer _cierre = new() { Interval = Duracion };

        public NotificacionWindow() => InitializeComponent();

        // accion: qué hacer si pulsa el aviso o su botón (textoAccion).
        public NotificacionWindow(string titulo, string mensaje, string? textoAccion = null, Action? accion = null)
        {
            InitializeComponent();
            Titulo.Text = titulo;
            Mensaje.Text = mensaje;
            BotonAccion.Content = textoAccion;
            BotonAccion.IsVisible = textoAccion != null && accion != null;

            TransparencyLevelHint = new List<WindowTransparencyLevel>
            {
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Transparent
            };

            void Ejecutar()
            {
                Close();
                accion?.Invoke();
            }

            BotonAccion.Click += (_, e) => { e.Handled = true; Ejecutar(); };
            BotonCerrar.Click += (_, e) => { e.Handled = true; Close(); };
            Tarjeta.PointerReleased += (_, _) => Ejecutar();

            // Mientras el ratón está encima no se cierra: da tiempo a leer.
            Tarjeta.PointerEntered += (_, _) => _cierre.Stop();
            Tarjeta.PointerExited += (_, _) => _cierre.Start();
            _cierre.Tick += (_, _) => Close();

            SizeChanged += (_, _) => Colocar();
            Opened += (_, _) =>
            {
                _cierre.Start();
                Dispatcher.UIThread.Post(() =>
                {
                    Tarjeta.Opacity = 1;
                    Tarjeta.RenderTransform = TransformOperations.Parse("translateX(0px)");
                }, DispatcherPriority.Background);
            };
            Closed += (_, _) => _cierre.Stop();
        }

        // Abajo a la derecha del área de trabajo de la pantalla principal,
        // que es donde está la bandeja en la configuración normal de Windows.
        private void Colocar()
        {
            var pantalla = Screens.Primary;
            if (pantalla == null || Bounds.Height <= 0) return;

            var area = pantalla.WorkingArea;
            var ancho = (int)Math.Ceiling(Bounds.Width * RenderScaling);
            var alto = (int)Math.Ceiling(Bounds.Height * RenderScaling);
            var margen = (int)(Margen * RenderScaling);
            Position = new PixelPoint(area.Right - ancho - margen, area.Bottom - alto - margen);
        }
    }
}
