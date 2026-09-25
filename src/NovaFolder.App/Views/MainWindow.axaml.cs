using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NovaFolder.Controls;
using NovaFolder.Views.Paginas;
using NovaFolder.Views.Principal;

namespace NovaFolder.Views
{
    public enum Seccion { Carpetas, Escritorio, Ajustes, Ayuda }

    // La ventana de la aplicación, con su barra de título, su botón en la
    // barra de tareas y su sitio en Alt+Tab, como cualquier programa de
    // Windows. Se abre al instalar y al lanzar NovaFolder desde el menú
    // Inicio; cerrarla la manda a la bandeja.
    public partial class MainWindow : Window, IAvisador
    {
        private static readonly TimeSpan DuracionAviso = TimeSpan.FromSeconds(8);

        private readonly ServiciosApp _servicios = null!;
        private readonly Dictionary<Seccion, Control> _paginas = new();
        private readonly DispatcherTimer _ocultarAviso = new() { Interval = DuracionAviso };
        private Action? _accionAviso;

        // Se cerró con la X: App decide si explicar que sigue en la bandeja.
        public event Action? Ocultada;

        public MainWindow() => InitializeComponent();

        public MainWindow(ServiciosApp servicios)
        {
            InitializeComponent();
            _servicios = servicios;

            Navegacion.ItemsSource = new[]
            {
                ItemNav(Seccion.Carpetas, "\uE8B7", "Carpetas"),
                ItemNav(Seccion.Escritorio, "\uEA99", "Ordenar Escritorio"),
                ItemNav(Seccion.Ajustes, "\uE713", "Ajustes"),
                ItemNav(Seccion.Ayuda, "\uE897", "Ayuda")
            };
            Navegacion.SelectionChanged += (_, _) =>
            {
                if (Navegacion.SelectedItem is ListBoxItem { Tag: Seccion s }) Mostrar(s);
            };

            BotonCerrarAviso.Click += (_, _) => OcultarAviso();
            BotonAccionAviso.Click += (_, _) =>
            {
                var accion = _accionAviso;
                OcultarAviso();
                accion?.Invoke();
            };
            _ocultarAviso.Tick += (_, _) => OcultarAviso();

            BotonReiniciar.Click += (_, _) => _servicios.Actualizaciones.ReiniciarEInstalar();
            ActualizarVersion();

            Closing += (_, e) =>
            {
                if (e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown) return;
                e.Cancel = true;
                Hide();
                Ocultada?.Invoke();
            };

            IrA(Seccion.Carpetas);
        }

        public void IrA(Seccion seccion)
        {
            foreach (var item in Navegacion.Items)
                if (item is ListBoxItem { Tag: Seccion s } li && s == seccion) Navegacion.SelectedItem = li;
            Mostrar(seccion);
        }

        public void MostrarYActivar(Seccion? seccion = null)
        {
            if (seccion is Seccion s) IrA(s);
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Show();
            Activate();
        }

        // Al aparecer una actualización descargada.
        public void ActualizarVersion()
        {
            var lista = _servicios.Actualizaciones.VersionLista;
            Version.Text = $"Versión {_servicios.Actualizaciones.VersionActual}";
            BannerActualizacion.IsVisible = lista != null;
            TextoActualizacion.Text = $"NovaFolder {lista} está lista para instalarse.";
        }

        private void Mostrar(Seccion seccion)
        {
            if (!_paginas.TryGetValue(seccion, out var pagina))
            {
                pagina = seccion switch
                {
                    Seccion.Carpetas => new CarpetasPagina(_servicios, this, IrA),
                    Seccion.Escritorio => new EscritorioPagina(_servicios, this, IrA),
                    Seccion.Ajustes => new AjustesPagina(_servicios, this),
                    _ => new AyudaPagina(_servicios)
                };
                _paginas[seccion] = pagina;
            }

            // La página del Escritorio se vuelve a revisar cada vez que se
            // entra: el usuario pudo haber dejado cosas nuevas.
            if (pagina is EscritorioPagina escritorio) escritorio.Revisar();
            Pagina.Content = pagina;
        }

        public void Avisar(string texto, bool esError = false, string? textoAccion = null, Action? accion = null)
        {
            TextoAviso.Text = texto;
            GlifoAviso.Text = esError ? "\uE783" : "\uE73E";
            GlifoAviso.Foreground = (IBrush?)Avalonia.Application.Current?.FindResource(esError ? "NovaError" : "NovaAcento");
            _accionAviso = accion;
            BotonAccionAviso.Content = textoAccion;
            BotonAccionAviso.IsVisible = accion != null && textoAccion != null;
            BarraAviso.IsVisible = true;
            _ocultarAviso.Stop();
            _ocultarAviso.Start();
        }

        private void OcultarAviso()
        {
            _ocultarAviso.Stop();
            _accionAviso = null;
            BarraAviso.IsVisible = false;
        }

        private static ListBoxItem ItemNav(Seccion seccion, string glifo, string texto) => new()
        {
            Tag = seccion,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = glifo,
                        FontFamily = Interacciones.FuenteIconos,
                        FontSize = 16,
                        Foreground = (IBrush?)Avalonia.Application.Current?.FindResource("NovaAcento"),
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock { Text = texto, FontSize = 14, VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
    }
}
