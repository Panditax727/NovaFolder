using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NovaFolder.Controls;
using NovaFolder.Core.Errors;
using NovaFolder.Core.Models;
using NovaFolder.Core.Storage;
using NovaFolder.Services.Windows;
using NovaFolder.Views.Principal;

namespace NovaFolder.Views.Paginas
{
    // Gestión completa de las carpetas: la lista a la izquierda (crear,
    // renombrar, eliminar, soltar cosas encima) y el contenido de la
    // elegida a la derecha, con la misma rejilla que el popup.
    public partial class CarpetasPagina : UserControl
    {
        private readonly FolderStore _store = null!;
        private readonly IAvisador _aviso = null!;
        private readonly VistaCarpeta _vista = null!;
        private bool _renombrando;

        public CarpetasPagina() => InitializeComponent();

        public CarpetasPagina(ServiciosApp servicios, IAvisador aviso, Action<Seccion> irA)
        {
            InitializeComponent();
            _store = servicios.Store;
            _aviso = aviso;

            _vista = new VistaCarpeta(_store);
            _vista.AvisoPedido += (texto, esError) => { if (texto != null) _aviso.Avisar(texto, esError); };
            _vista.AgregarPedido += () => _ = AgregarConSelector();
            Vista.Content = _vista;

            BotonNueva.Click += (_, _) => Nueva();
            BotonPrimera.Click += (_, _) => Nueva();
            BotonIrAOrdenar.Click += (_, _) => irA(Seccion.Escritorio);
            BotonAgregar.Click += async (_, _) => await AgregarConSelector();
            BotonRenombrar.Click += (_, _) => EmpezarRenombrado();
            BotonEliminar.Click += (_, _) => Eliminar(Seleccionada);
            Buscador.TextChanged += (_, _) => _vista.Filtro = Buscador.Text ?? "";

            Lista.SelectionChanged += (_, _) => AlSeleccionar();
            EditorNombre.KeyDown += EditorNombre_KeyDown;
            EditorNombre.LostFocus += (_, _) => ConfirmarRenombrado(desdePerdidaDeFoco: true);
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.F2 && !_renombrando) { e.Handled = true; EmpezarRenombrado(); }
            };

            AttachedToVisualTree += (_, _) => _store.Changed += Recargar;
            DetachedFromVisualTree += (_, _) => _store.Changed -= Recargar;

            Recargar();
        }

        private AppFolder? Seleccionada => (Lista.SelectedItem as ListBoxItem)?.Tag as AppFolder;

        // ---------- lista ----------

        private void Recargar()
        {
            var nombreAnterior = Seleccionada?.Name;
            Lista.ItemsSource = _store.Folders.Select(CrearItem).ToList();

            var total = _store.Folders.Sum(f => f.Apps.Count);
            Resumen.Text = _store.Folders.Count == 0
                ? "Todavía no tienes carpetas."
                : $"{Plural(_store.Folders.Count, "carpeta", "carpetas")} · {Plural(total, "elemento", "elementos")}";

            bool hay = _store.Folders.Count > 0;
            SinCarpetas.IsVisible = !hay;
            Contenido.IsVisible = hay;

            // Se mantiene la misma carpeta elegida (aunque se haya recargado
            // folders.json y el objeto sea otro).
            var elegida = nombreAnterior == null ? null : _store.Buscar(nombreAnterior);
            Seleccionar(elegida ?? _store.Folders.FirstOrDefault());
        }

        private void Seleccionar(AppFolder? carpeta)
        {
            Lista.SelectedItem = Lista.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag == carpeta);
            AlSeleccionar();
        }

        private void AlSeleccionar()
        {
            var carpeta = Seleccionada;
            if (_vista.Carpeta != carpeta) _vista.Carpeta = carpeta;
            if (carpeta == null) return;

            if (!_renombrando) Nombre.Text = carpeta.Name;
            Detalle.Text = carpeta.Apps.Count == 0
                ? "Vacía: arrastra aquí accesos directos, juegos o archivos"
                : Plural(carpeta.Apps.Count, "elemento", "elementos");
            Buscador.IsVisible = carpeta.Apps.Count >= 9;
        }

        // Fila de la lista: miniatura 2x2, nombre y cantidad. También es un
        // destino para soltar archivos o elementos de otra carpeta.
        private ListBoxItem CrearItem(AppFolder carpeta)
        {
            var mini = new UniformGrid { Rows = 2, Columns = 2, Width = 36, Height = 36 };
            for (int i = 0; i < 4; i++)
                mini.Children.Add(IconService.GetIconControl(i < carpeta.Apps.Count ? carpeta.Apps[i] : null, 14));

            var item = new ListBoxItem
            {
                Tag = carpeta,
                Content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    Children =
                    {
                        new Border
                        {
                            Width = 42, Height = 42,
                            CornerRadius = new CornerRadius(10),
                            Background = (IBrush?)Application.Current?.FindResource("NovaHueco"),
                            Child = mini,
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        Columna(new StackPanel
                        {
                            Margin = new Thickness(12, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                new TextBlock { Text = carpeta.Name, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis },
                                new TextBlock { Text = Plural(carpeta.Apps.Count, "elemento", "elementos"), Classes = { "suave" }, FontSize = 11 }
                            }
                        }, 1)
                    }
                },
                ContextMenu = new ContextMenu
                {
                    Items =
                    {
                        Interacciones.Opcion("Cambiar nombre", "\uE8AC", () => { Seleccionar(carpeta); EmpezarRenombrado(); }),
                        Interacciones.Opcion("Eliminar carpeta", "\uE74D", () => Eliminar(carpeta))
                    }
                }
            };

            Interacciones.AceptarSoltar(item,
                rutas => Intentar(() =>
                {
                    var r = _store.AgregarElementos(carpeta, rutas);
                    _aviso.Avisar(r.MotivoSiNadaEntro ?? $"{Plural(r.Agregados.Count, "elemento agregado", "elementos agregados")} a «{carpeta.Name}».",
                        esError: r.Agregados.Count == 0,
                        textoAccion: r.Agregados.Count > 0 ? "Deshacer" : null,
                        accion: r.Agregados.Count > 0 ? () => Intentar(() => _store.QuitarElementos(carpeta, r.Agregados)) : null);
                }),
                elemento => Intentar(() =>
                {
                    if (elemento.Carpeta == carpeta) return;
                    _store.MoverElemento(elemento.App, elemento.Carpeta, carpeta);
                    _aviso.Avisar($"«{elemento.App.Name}» se movió a «{carpeta.Name}».", false, "Deshacer",
                        () => Intentar(() => _store.MoverElemento(elemento.App, carpeta, elemento.Carpeta)));
                }),
                resaltar: item);

            return item;
        }

        // ---------- acciones ----------

        private void Nueva()
        {
            Intentar(() =>
            {
                var carpeta = _store.CrearCarpeta();
                Seleccionar(carpeta);
                Dispatcher.UIThread.Post(EmpezarRenombrado, DispatcherPriority.Background);
            });
        }

        private void Eliminar(AppFolder? carpeta)
        {
            if (carpeta == null) return;
            Intentar(() =>
            {
                int indice = _store.EliminarCarpeta(carpeta);
                _aviso.Avisar($"Se eliminó «{carpeta.Name}». Sus archivos no se tocaron.", false, "Deshacer",
                    () => Intentar(() =>
                    {
                        _store.RestaurarCarpeta(carpeta, indice);
                        Seleccionar(carpeta);
                    }));
            });
        }

        private async Task AgregarConSelector()
        {
            var carpeta = Seleccionada;
            var almacenamiento = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (carpeta == null || almacenamiento == null) return;

            var archivos = await almacenamiento.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Agregar a «{carpeta.Name}»",
                AllowMultiple = true
            });
            var rutas = archivos.Select(a => a.TryGetLocalPath()).Where(p => p != null).Select(p => p!).ToList();
            _vista.Agregar(rutas, null);
        }

        private void Intentar(Action accion)
        {
            try { accion(); }
            catch (NovaFolderException ex) { _aviso.Avisar(ex.Message, esError: true); }
        }

        // ---------- renombrar ----------

        private void EmpezarRenombrado()
        {
            if (Seleccionada is not AppFolder carpeta || _renombrando) return;
            _renombrando = true;
            EditorNombre.Text = carpeta.Name;
            Cabecera.IsVisible = false;
            EditorNombre.IsVisible = true;
            EditorNombre.Focus();
            EditorNombre.SelectAll();
        }

        private void ConfirmarRenombrado(bool desdePerdidaDeFoco)
        {
            if (!_renombrando || Seleccionada is not AppFolder carpeta) return;
            try
            {
                _store.RenombrarCarpeta(carpeta, EditorNombre.Text ?? "");
            }
            catch (NovaFolderException ex)
            {
                _aviso.Avisar(ex.Message, esError: true);
                if (!desdePerdidaDeFoco) return;   // con Enter se deja corregir
            }
            TerminarRenombrado();
        }

        private void TerminarRenombrado()
        {
            _renombrando = false;
            EditorNombre.IsVisible = false;
            Cabecera.IsVisible = true;
            AlSeleccionar();
        }

        private void EditorNombre_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { e.Handled = true; ConfirmarRenombrado(desdePerdidaDeFoco: false); }
            else if (e.Key == Key.Escape) { e.Handled = true; TerminarRenombrado(); }
        }

        // ---------- utilidades ----------

        internal static string Plural(int n, string uno, string varios) => n == 1 ? $"1 {uno}" : $"{n} {varios}";

        private static Control Columna(Control c, int columna)
        {
            Grid.SetColumn(c, columna);
            return c;
        }
    }
}
