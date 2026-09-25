using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NovaFolder.Controls;
using NovaFolder.Core;
using NovaFolder.Core.Diagnostics;
using NovaFolder.Core.Errors;
using NovaFolder.Core.Organization;
using NovaFolder.Core.Storage;
using NovaFolder.Services.Windows;
using NovaFolder.Views.Principal;

namespace NovaFolder.Views.Paginas
{
    // El "simplificador": muestra lo que está suelto en el Escritorio,
    // agrupado por la carpeta que se le sugiere, y lo guarda todo con un
    // clic (con Deshacer). Cada fila se puede desmarcar o mandar a otra
    // carpeta antes de ordenar.
    public partial class EscritorioPagina : UserControl
    {
        private readonly ServiciosApp _servicios = null!;
        private readonly FolderStore _store = null!;
        private readonly IAvisador _aviso = null!;
        private readonly Action<Seccion> _irA = null!;
        private readonly List<Fila> _filas = new();
        private bool _cargando;
        private int _revision;

        private sealed record Fila(ElementoEscritorio Elemento, CheckBox Marcada, ComboBox Destino);

        public EscritorioPagina() => InitializeComponent();

        public EscritorioPagina(ServiciosApp servicios, IAvisador aviso, Action<Seccion> irA)
        {
            InitializeComponent();
            _servicios = servicios;
            _store = servicios.Store;
            _aviso = aviso;
            _irA = irA;

            BotonRevisar.Click += (_, _) => Revisar();
            BotonVerCarpetas.Click += (_, _) => _irA(Seccion.Carpetas);
            BotonOrganizar.Click += (_, _) => Organizar(_filas.Where(f => f.Marcada.IsChecked == true).ToList());
            InterruptorLimpiar.IsCheckedChanged += (_, _) =>
            {
                if (_cargando) return;
                _servicios.LimpiarEscritorio(InterruptorLimpiar.IsChecked == true);
                Revisar();
            };
        }

        // Vuelve a mirar el Escritorio. Se llama al entrar en la página.
        public void Revisar()
        {
            _cargando = true;
            InterruptorLimpiar.IsChecked = _store.CleanDesktop;
            _cargando = false;

            Estado.Text = "Revisando tu Escritorio…";
            BotonOrganizar.IsEnabled = false;
            int revision = ++_revision;
            var rutas = _servicios.Rutas;
            var enCarpetas = _store.Folders.SelectMany(f => f.Apps).Select(a => a.Path).ToList();

            // Leer cientos de accesos .lnk lleva su tiempo: fuera del hilo de la UI.
            Task.Run(() => EscritorioScanner.Sueltos(rutas, enCarpetas)).ContinueWith(t =>
                Dispatcher.UIThread.Post(() =>
                {
                    if (revision != _revision) return;   // llegó otra revisión más nueva
                    if (t.IsFaulted)
                    {
                        Log.Error("No se pudo revisar el Escritorio.", t.Exception?.GetBaseException());
                        Estado.Text = "No se pudo revisar el Escritorio.";
                        return;
                    }
                    Mostrar(t.Result);
                }), TaskScheduler.Default);
        }

        private void Mostrar(IReadOnlyList<ElementoEscritorio> sueltos)
        {
            _filas.Clear();
            Lista.Children.Clear();

            CantidadSueltos.Text = sueltos.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
            CantidadGuardados.Text = _store.Folders.Sum(f => f.Apps.Count).ToString(System.Globalization.CultureInfo.CurrentCulture);

            int pendientes = _store.CleanDesktop ? _store.ContarAccesosEnEscritorio() : 0;
            if (pendientes > 0) Lista.Children.Add(CrearBannerPendientes(pendientes));

            var nombresCarpetas = _store.Folders.Select(f => f.Name).ToList();
            foreach (var grupo in sueltos.GroupBy(s => s.Sugerencia))
            {
                Lista.Children.Add(new TextBlock
                {
                    Text = $"{grupo.Key.ToUpperInvariant()} · {grupo.Count()}",
                    Classes = { "seccion" },
                    Margin = new Thickness(12, 14, 0, 6)
                });
                foreach (var elemento in grupo)
                    Lista.Children.Add(CrearFila(elemento, nombresCarpetas));
            }

            bool hay = sueltos.Count > 0;
            MarcoLista.IsVisible = hay || pendientes > 0;
            Ordenado.IsVisible = !hay && pendientes == 0;
            BotonOrganizar.IsEnabled = hay;
            TextoOrganizar.Text = sueltos.Count switch
            {
                0 => "Ordenar todo",
                1 => "Ordenar 1 elemento",
                var n => $"Ordenar los {n}"
            };

            var comunes = sueltos.Count(s => s.Comun);
            Estado.Text = !hay
                ? ""
                : comunes > 0
                    ? $"Desmarca lo que quieras dejar donde está. {CarpetasPagina.Plural(comunes, "elemento es", "elementos son")} del Escritorio común: se ordenarán, pero Windows los seguirá mostrando."
                    : "Desmarca lo que quieras dejar donde está, o elige otra carpeta en cada fila.";
        }

        private Control CrearFila(ElementoEscritorio elemento, IReadOnlyList<string> carpetas)
        {
            var marcada = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center, MinWidth = 0 };

            // Opciones: la sugerida primero (aunque aún no exista) y luego
            // todas las carpetas del usuario.
            var opciones = new List<string> { elemento.Sugerencia };
            opciones.AddRange(carpetas.Where(c => !string.Equals(c, elemento.Sugerencia, StringComparison.OrdinalIgnoreCase)));
            var destino = new ComboBox
            {
                ItemsSource = opciones,
                SelectedIndex = 0,
                Width = 170,
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(destino, "Carpeta a la que irá. Si no existe, se crea.");

            var guardar = new Button { Content = "Guardar", Classes = { "secundario" }, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var fila = new Fila(elemento, marcada, destino);
            guardar.Click += (_, _) => Organizar(new[] { fila });
            _filas.Add(fila);

            var app = ShortcutResolver.Crear(elemento.Ruta, _servicios.Rutas.Escritorio);
            var nombre = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 12, 0),
                Children =
                {
                    new TextBlock { Text = elemento.Nombre, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Brushes.White }
                }
            };
            if (elemento.Comun)
                nombre.Children.Add(new TextBlock { Text = "Escritorio común · seguirá visible", Classes = { "suave" }, FontSize = 11 });

            var contenido = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto"),
                Children =
                {
                    marcada,
                    Columna(IconService.GetIconControl(app, 30), 1),
                    Columna(nombre, 2),
                    Columna(destino, 3),
                    Columna(guardar, 4)
                }
            };
            ToolTip.SetTip(nombre, elemento.Ruta);

            return new Border { Classes = { "nova-fila" }, Padding = new Thickness(10, 6), Child = contenido };
        }

        private Control CrearBannerPendientes(int pendientes)
        {
            var boton = new Button { Content = "Guardarlos", Classes = { "primario" }, Padding = new Thickness(12, 6) };
            boton.Click += (_, _) =>
            {
                _servicios.GuardarAccesosDelEscritorio();
                Revisar();
            };
            return new Border
            {
                Background = (IBrush?)Application.Current?.FindResource("NovaAcentoSuave"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10),
                Margin = new Thickness(4, 4, 4, 8),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = pendientes == 1
                                ? "1 acceso de tus carpetas sigue en el Escritorio."
                                : $"{pendientes} accesos de tus carpetas siguen en el Escritorio.",
                            Classes = { "texto" },
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        Columna(boton, 1)
                    }
                }
            };
        }

        private void Organizar(IReadOnlyCollection<Fila> filas)
        {
            if (filas.Count == 0)
            {
                _aviso.Avisar("No hay nada marcado para ordenar.", esError: true);
                return;
            }

            try
            {
                var asignaciones = filas.Select(f => (f.Elemento.Ruta, f.Destino.SelectedItem as string ?? f.Elemento.Sugerencia)).ToList();
                var resumen = _store.Organizar(asignaciones);
                int total = resumen.Sum(r => r.Agregados.Count);
                int carpetas = resumen.Count(r => r.Agregados.Count > 0);

                _aviso.Avisar(
                    $"Se ordenaron {CarpetasPagina.Plural(total, "elemento", "elementos")} en {CarpetasPagina.Plural(carpetas, "carpeta", "carpetas")}.",
                    false, "Deshacer", () =>
                    {
                        try { _store.DeshacerOrganizar(resumen); }
                        catch (NovaFolderException ex) { _aviso.Avisar(ex.Message, esError: true); }
                        Revisar();
                    });
            }
            catch (NovaFolderException ex)
            {
                _aviso.Avisar(ex.Message, esError: true);
            }
            Revisar();
        }

        private static Control Columna(Control c, int columna)
        {
            Grid.SetColumn(c, columna);
            return c;
        }
    }
}
