using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NovaFolder.Core.Models;
using NovaFolder.Services.Windows;

namespace NovaFolder.Controls
{
    // Tarjeta de una carpeta en el widget: miniatura 2x2 de lo que tiene
    // dentro y su nombre, como las carpetas del escritorio de Android.
    //
    // No sabe abrir, renombrar ni guardar nada: solo avisa de lo que pidió
    // el usuario. Quien la crea (MainWindow) decide qué hacer con cada evento.
    public sealed class FolderTile : Border
    {
        public const double Ancho = 96, Alto = 112;

        public AppFolder Folder { get; }

        public event Action<FolderTile>? AbrirPedido;
        public event Action<FolderTile>? RenombrarPedido;
        public event Action<FolderTile>? AgregarPedido;
        public event Action<FolderTile>? EliminarPedido;
        public event Action<FolderTile, IReadOnlyList<string>>? ArchivosSoltados;
        public event Action<FolderTile, ElementoArrastrado>? ElementoSoltado;

        // Sin esto los estilos "Border.nova-carpeta" de App.axaml no se
        // aplicarían: el selector compara el tipo exacto.
        protected override Type StyleKeyOverride => typeof(Border);

        public FolderTile(AppFolder folder)
        {
            Folder = folder;
            Classes.Add("nova-carpeta");
            Width = Ancho;
            Height = Alto;
            Margin = new Thickness(4);
            Child = ConstruirContenido();

            ToolTip.SetTip(this, folder.Apps.Count switch
            {
                0 => $"{folder.Name}\nVacía: arrastra archivos aquí",
                1 => $"{folder.Name}\n1 elemento",
                var n => $"{folder.Name}\n{n} elementos"
            });

            Interacciones.AlActivar(this, () => AbrirPedido?.Invoke(this));
            Interacciones.AceptarSoltar(this,
                rutas => ArchivosSoltados?.Invoke(this, rutas),
                elemento => ElementoSoltado?.Invoke(this, elemento),
                resaltar: this);

            ContextMenu = new ContextMenu
            {
                Items =
                {
                    Interacciones.Opcion("Abrir", "\uE838", () => AbrirPedido?.Invoke(this)),
                    Interacciones.Opcion("Agregar elementos…", "\uE710", () => AgregarPedido?.Invoke(this)),
                    Interacciones.Opcion("Cambiar nombre", "\uE8AC", () => RenombrarPedido?.Invoke(this)),
                    new Separator(),
                    Interacciones.Opcion("Eliminar carpeta", "\uE74D", () => EliminarPedido?.Invoke(this))
                }
            };
        }

        // Rectángulo de la tarjeta en píxeles de pantalla: el popup se abre
        // pegado a él para que se note de dónde salió.
        public PixelRect RectEnPantalla()
        {
            var arriba = this.PointToScreen(new Point(0, 0));
            var abajo = this.PointToScreen(new Point(Bounds.Width, Bounds.Height));
            return new PixelRect(arriba, abajo);
        }

        private Control ConstruirContenido()
        {
            var rejilla = new Grid
            {
                RowDefinitions = new RowDefinitions("*,*"),
                ColumnDefinitions = new ColumnDefinitions("*,*")
            };

            for (int i = 0; i < 4; i++)
            {
                var app = i < Folder.Apps.Count ? Folder.Apps[i] : null;
                var celda = IconService.GetIconControl(app, 23);
                Grid.SetRow(celda, i / 2);
                Grid.SetColumn(celda, i % 2);
                rejilla.Children.Add(celda);
            }

            var miniatura = new Border
            {
                Width = 60,
                Height = 60,
                Padding = new Thickness(2),
                CornerRadius = new CornerRadius(14),
                Background = (IBrush?)Application.Current?.FindResource("NovaHueco"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = Folder.Apps.Count == 0
                    ? new TextBlock
                    {
                        Text = "\uE710",
                        FontFamily = Interacciones.FuenteIconos,
                        FontSize = 18,
                        Opacity = 0.45,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                    : rejilla
            };

            return new StackPanel
            {
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    miniatura,
                    new TextBlock
                    {
                        Text = Folder.Name,
                        Foreground = Brushes.White,
                        FontSize = 12,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = Ancho - 12,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            };
        }
    }
}
