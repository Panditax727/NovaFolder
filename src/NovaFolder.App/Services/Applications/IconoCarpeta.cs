using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using NovaFolder.Core.Models;
using NovaFolder.Services.Windows;

namespace NovaFolder.Services.Applications
{
    // Dibuja el ícono que representa una carpeta NovaFolder en el
    // Escritorio: la silueta morada (para distinguirla de una carpeta
    // normal de Windows) con una miniatura 2x2 de los primeros cuatro
    // elementos que contiene, igual que las carpetas de apps de Android.
    //
    // El nombre del archivo lleva un hash del contenido: si cambian las
    // apps de la carpeta, cambia la ruta del .ico y Windows se ve obligado
    // a releerlo. Sobrescribir el mismo archivo no basta, porque el shell
    // cachea los íconos por ruta y seguiría mostrando el viejo.
    [SupportedOSPlatform("windows")]
    internal static class IconoCarpeta
    {
        private const int Lienzo = 256;

        // Silueta de la carpeta dentro del lienzo de 256x256.
        private static readonly RectangleF Pestaña = new(26, 40, 86, 32);
        private static readonly RectangleF Cuerpo = new(26, 62, 204, 160);

        // Rejilla 2x2 de miniaturas, centrada en el cuerpo.
        private const int TamMiniatura = 58;
        private const int SeparacionMiniaturas = 12;

        private static readonly Color MoradoClaro = Color.FromArgb(255, 176, 129, 240);
        private static readonly Color MoradoOscuro = Color.FromArgb(255, 116, 64, 196);

        // Devuelve la ruta del .ico de esta carpeta, generándolo si hace
        // falta, y borra las versiones anteriores que hayan quedado.
        public static string Asegurar(AppFolder carpeta, string dir)
        {
            Directory.CreateDirectory(dir);

            var apps = carpeta.Apps.Take(4).ToList();
            var prefijo = "carpeta_" + Sanear(carpeta.Name) + "_";
            var ruta = Path.Combine(dir, prefijo + HashDe(apps) + ".ico");

            if (!File.Exists(ruta))
            {
                Generar(ruta, apps);
                BorrarVersionesViejas(dir, prefijo, conservar: ruta);
            }

            return ruta;
        }

        private static void Generar(string ruta, List<AppShortcut> apps)
        {
            using var bmp = new Bitmap(Lienzo, Lienzo, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                DibujarSilueta(g);
                DibujarMiniaturas(g, apps);
            }

            using var png = new MemoryStream();
            bmp.Save(png, ImageFormat.Png);
            GuardarComoIcoPng(ruta, png.ToArray());
        }

        private static void DibujarSilueta(Graphics g)
        {
            using (var path = Redondeado(Pestaña, 8))
            using (var brush = new LinearGradientBrush(Pestaña, MoradoClaro, MoradoOscuro, 90f))
                g.FillPath(brush, path);

            using (var path = Redondeado(Cuerpo, 16))
            using (var brush = new LinearGradientBrush(Cuerpo, MoradoClaro, MoradoOscuro, 90f))
            using (var borde = new Pen(Color.FromArgb(70, 255, 255, 255), 2))
            {
                g.FillPath(brush, path);
                g.DrawPath(borde, path);
            }
        }

        private static void DibujarMiniaturas(Graphics g, List<AppShortcut> apps)
        {
            int total = TamMiniatura * 2 + SeparacionMiniaturas;
            float x0 = Cuerpo.X + (Cuerpo.Width - total) / 2f;
            float y0 = Cuerpo.Y + (Cuerpo.Height - total) / 2f;

            for (int i = 0; i < 4; i++)
            {
                float x = x0 + (i % 2) * (TamMiniatura + SeparacionMiniaturas);
                float y = y0 + (i / 2) * (TamMiniatura + SeparacionMiniaturas);
                var celda = new RectangleF(x, y, TamMiniatura, TamMiniatura);

                // Hueco tenue también donde no hay app: da la pista visual de
                // que la carpeta tiene cuatro ranuras, como en Android.
                using (var hueco = Redondeado(celda, 10))
                using (var relleno = new SolidBrush(Color.FromArgb(48, 255, 255, 255)))
                    g.FillPath(relleno, hueco);

                if (i >= apps.Count) continue;

                using var icono = IconoWindows.ObtenerGdi(apps[i].IconSource, 128);
                if (icono == null) continue;

                // Se deja un margen dentro de la ranura para que el icono
                // respire y no toque los bordes redondeados.
                var destino = RectangleF.Inflate(celda, -5, -5);
                g.DrawImage(icono, destino);
            }
        }

        // Íconos de carpetas que ya no existen (eliminadas o renombradas).
        public static void BorrarHuerfanos(IEnumerable<AppFolder> vigentes, string dir)
        {
            if (!Directory.Exists(dir)) return;

            var prefijos = vigentes.Select(c => "carpeta_" + Sanear(c.Name) + "_").ToList();
            foreach (var ico in Directory.EnumerateFiles(dir, "carpeta_*.ico"))
            {
                var nombre = Path.GetFileName(ico);
                if (prefijos.Any(p => nombre.StartsWith(p, StringComparison.OrdinalIgnoreCase)
                                   && nombre.Length == p.Length + 8 + 4)) continue;   // + hash + ".ico"
                try { File.Delete(ico); } catch { /* lo tiene abierto el shell: se reintenta la próxima */ }
            }
        }

        private static void BorrarVersionesViejas(string dir, string prefijo, string conservar)
        {
            foreach (var viejo in Directory.EnumerateFiles(dir, prefijo + "*.ico"))
            {
                if (string.Equals(viejo, conservar, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(viejo); } catch { /* lo tiene abierto el shell: se reintenta la próxima */ }
            }
        }

        // Identifica el contenido de la carpeta: si cambia, cambia el nombre
        // del archivo y el shell no puede servir el ícono cacheado.
        private static string HashDe(IEnumerable<AppShortcut> apps)
        {
            uint h = 2166136261;
            foreach (var ch in string.Join("|", apps.Select(a => a.IconSource)))
            {
                h ^= ch;
                h *= 16777619;
            }
            return h.ToString("x8");
        }

        private static string Sanear(string nombre)
        {
            var sb = new StringBuilder(nombre.Length);
            foreach (var c in nombre)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }

        // Icon.FromHandle(bitmap.GetHicon()) cuantiza a 16 colores y pierde
        // el alfa: para un degradado se ve horrible, con bandas. Desde
        // Windows Vista el formato .ico admite un frame comprimido como PNG
        // directamente, así que se arma el contenedor a mano y se evita el
        // problema del todo: mismo PNG, 32 bits reales con transparencia.
        private static void GuardarComoIcoPng(string ruta, byte[] png)
        {
            using var fs = new FileStream(ruta, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(fs);

            w.Write((short)0);   // reservado
            w.Write((short)1);   // tipo: 1 = ícono
            w.Write((short)1);   // cantidad de imágenes

            w.Write((byte)0);    // ancho: 0 significa 256
            w.Write((byte)0);    // alto: idem
            w.Write((byte)0);    // paleta de colores: ninguna (32 bits)
            w.Write((byte)0);    // reservado
            w.Write((short)1);   // planos de color
            w.Write((short)32);  // bits por píxel
            w.Write(png.Length);
            w.Write(6 + 16);     // offset: cabecera (6) + una entrada (16)

            w.Write(png);
        }

        private static GraphicsPath Redondeado(RectangleF r, float radio)
        {
            var path = new GraphicsPath();
            float d = radio * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
