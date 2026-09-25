using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using NovaFolder.Core.Models;
using NovaFolder.Core.Diagnostics;

namespace NovaFolder.Controls
{
    // Un elemento que el usuario está arrastrando desde una carpeta abierta:
    // qué es y de dónde sale, para poder reordenarlo o moverlo a otra.
    public sealed class ElementoArrastrado
    {
        public ElementoArrastrado(AppFolder carpeta, AppShortcut app)
        {
            Carpeta = carpeta;
            App = app;
        }

        public AppFolder Carpeta { get; }
        public AppShortcut App { get; }

        // Lo recibió una ventana de NovaFolder (reordenar, mover a otra
        // carpeta). Si queda en false y hubo efecto, lo recibió otra app:
        // el Escritorio o el Explorador.
        public bool SoltadoDentro { get; set; }
    }

    // Comportamientos que comparten las tarjetas de carpeta, los elementos
    // del popup y las ventanas. Se escriben una vez aquí para que todo
    // responda igual al ratón, al teclado y al arrastrar.
    internal static class Interacciones
    {
        // La misma que el recurso NovaIconos de App.axaml, para lo que se
        // construye desde código. Segoe Fluent Icons es la de Windows 11;
        // MDL2 Assets la de Windows 10, con los mismos códigos.
        public static readonly FontFamily FuenteIconos = new("Segoe Fluent Icons, Segoe MDL2 Assets");

        // Solo existe dentro de este proceso: arrastrar un elemento fuera de
        // NovaFolder (al Explorador, al Escritorio) no copia nada.
        private static readonly DataFormat<ElementoArrastrado> FormatoElemento =
            DataFormat.CreateInProcessFormat<ElementoArrastrado>("NovaFolder.Elemento");

        // Cuánto hay que mover el ratón con el botón pulsado para que deje
        // de ser un clic y empiece un arrastre.
        private const double UmbralArrastre = 6;

        // Clic izquierdo "de verdad": se suelta encima del mismo control en
        // el que se pulsó. Así, pulsar y arrastrar fuera para arrepentirse
        // no abre nada, como en cualquier botón de Windows. También responde
        // a Enter/Espacio cuando tiene el foco (navegación con Tab).
        public static void AlActivar(Border control, Action accion)
        {
            control.Focusable = true;

            control.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
                    control.Classes.Add("presionado");
            };
            control.PointerReleased += (_, e) =>
            {
                bool eraClic = control.Classes.Contains("presionado");
                control.Classes.Remove("presionado");
                if (eraClic && e.InitialPressMouseButton == MouseButton.Left && control.IsPointerOver)
                {
                    e.Handled = true;
                    accion();
                }
            };
            control.PointerCaptureLost += (_, _) => control.Classes.Remove("presionado");
            control.KeyDown += (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Space)
                {
                    e.Handled = true;
                    accion();
                }
            };
        }

        // Permite arrastrar el control (un elemento del popup) para soltarlo
        // en otra posición de su carpeta, sobre otra carpeta del widget, o
        // fuera de NovaFolder: en el Escritorio o en el Explorador.
        //
        // Viaja con dos formatos: el interno (para las ventanas de NovaFolder)
        // y el archivo real (CF_HDROP), que es lo que entiende Windows.
        // efectos: qué puede hacer el destino externo con el archivo.
        // alTerminar: recibe el elemento y el efecto final, para actualizar la carpeta.
        // Se combina con AlActivar: si el ratón no se mueve, sigue siendo clic.
        public static void HacerArrastrable(
            Border control,
            Func<ElementoArrastrado> datos,
            Func<ElementoArrastrado, DragDropEffects> efectos,
            Action<ElementoArrastrado, DragDropEffects> alTerminar)
        {
            PointerPressedEventArgs? pulsacion = null;
            Point inicio = default;

            control.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed) return;
                pulsacion = e;
                inicio = e.GetPosition(control);
            };
            control.PointerReleased += (_, _) => pulsacion = null;

            control.PointerMoved += async (_, e) =>
            {
                if (pulsacion == null) return;
                var p = e.GetPosition(control);
                if (Math.Abs(p.X - inicio.X) < UmbralArrastre && Math.Abs(p.Y - inicio.Y) < UmbralArrastre) return;

                var disparador = pulsacion;
                pulsacion = null;

                // Ya no es un clic: al soltar no debe abrir la app.
                control.Classes.Remove("presionado");
                control.Classes.Add("arrastrando");
                try
                {
                    var elemento = datos();
                    var item = new DataTransferItem();
                    item.Set(FormatoElemento, elemento);
                    if (await ArchivoDe(control, elemento.App.Path) is IStorageItem archivo)
                        item.SetFile(archivo);

                    var transferencia = new DataTransfer();
                    transferencia.Add(item);
                    var efecto = await DragDrop.DoDragDropAsync(disparador, transferencia, efectos(elemento));
                    alTerminar(elemento, efecto);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or IOException)
                {
                    Log.Error("Error al arrastrar un elemento.", ex);
                }
                finally
                {
                    control.Classes.Remove("arrastrando");
                }
            };
        }

        // Convierte el control en destino de soltar. Acepta archivos que
        // vienen de fuera (Explorador, Escritorio) y, si se da alSoltarElemento,
        // elementos arrastrados desde una carpeta de NovaFolder. Mientras hay
        // algo aceptable encima, 'resaltar' recibe la clase "soltar-aqui".
        public static void AceptarSoltar(
            Control destino,
            Action<IReadOnlyList<string>> alSoltarArchivos,
            Action<ElementoArrastrado>? alSoltarElemento = null,
            StyledElement? resaltar = null)
        {
            DragDrop.SetAllowDrop(destino, true);

            bool Acepta(DragEventArgs e) =>
                HayArchivos(e) || (alSoltarElemento != null && ElementoDe(e) != null);

            void Apagar() => resaltar?.Classes.Remove("soltar-aqui");

            void Actualizar(DragEventArgs e)
            {
                if (!Acepta(e)) return;   // que lo atienda el contenedor
                e.DragEffects = ElementoDe(e) != null ? DragDropEffects.Move : DragDropEffects.Link;
                e.Handled = true;
                resaltar?.Classes.Add("soltar-aqui");
            }

            destino.AddHandler(DragDrop.DragEnterEvent, (object? _, DragEventArgs e) => Actualizar(e));
            destino.AddHandler(DragDrop.DragOverEvent, (object? _, DragEventArgs e) => Actualizar(e));
            destino.AddHandler(DragDrop.DragLeaveEvent, (object? _, DragEventArgs _) => Apagar());
            destino.AddHandler(DragDrop.DropEvent, (object? _, DragEventArgs e) =>
            {
                Apagar();
                if (!Acepta(e)) return;
                e.Handled = true;

                if (ElementoDe(e) is ElementoArrastrado elemento)
                {
                    elemento.SoltadoDentro = true;
                    alSoltarElemento?.Invoke(elemento);
                }
                else
                {
                    var rutas = RutasDe(e);
                    if (rutas.Count > 0) alSoltarArchivos(rutas);
                }
            });
        }

        private static async System.Threading.Tasks.Task<IStorageItem?> ArchivoDe(Visual control, string ruta)
        {
            var almacenamiento = TopLevel.GetTopLevel(control)?.StorageProvider;
            if (almacenamiento == null) return null;
            return Directory.Exists(ruta)
                ? await almacenamiento.TryGetFolderFromPathAsync(ruta)
                : File.Exists(ruta) ? await almacenamiento.TryGetFileFromPathAsync(ruta) : null;
        }

        public static bool HayArchivos(DragEventArgs e) => e.DataTransfer.Contains(DataFormat.File);

        public static ElementoArrastrado? ElementoDe(DragEventArgs e) =>
            e.DataTransfer.Contains(FormatoElemento) ? e.DataTransfer.TryGetValue(FormatoElemento) : null;

        public static List<string> RutasDe(DragEventArgs e) =>
            (e.DataTransfer.TryGetFiles() ?? Array.Empty<IStorageItem>())
                .Select(f => f.TryGetLocalPath())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!)
                .ToList();

        public static MenuItem Opcion(string texto, string glifo, Action accion)
        {
            var item = new MenuItem
            {
                Header = texto,
                Icon = new TextBlock { Text = glifo, FontFamily = FuenteIconos }
            };
            item.Click += (_, _) => accion();
            return item;
        }
    }
}
