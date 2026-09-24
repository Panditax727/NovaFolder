using System;
using System.Diagnostics;
using System.IO;

namespace NovaFolder.Services.Applications
{
    // Un solo responsable: abrir una app dada su ruta.
    // Si mañana quieres loguear qué se abre, medir tiempos de uso, etc.,
    // este es el único lugar que hay que tocar.
    public static class AppLauncherService
    {
        // Devuelve el motivo del fallo, o null si se lanzó bien.
        //
        // Como el proyecto es <OutputType>WinExe</OutputType>, en Windows NO
        // hay consola: el llamador recibe el error y lo enseña en pantalla.
        public static string? Lanzar(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "La ruta está vacía.";

            if (!Existe(path))
                return $"No se encuentra:\n{path}";

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return null;
            }
            catch (Exception ex)
            {
                return $"No se pudo abrir:\n{ex.Message}";
            }
        }

        // Abre el Explorador con el elemento ya seleccionado, como el
        // "Abrir ubicación del archivo" del menú contextual de Windows.
        public static string? AbrirUbicacion(string path)
        {
            if (!Existe(path))
                return $"No se encuentra:\n{path}";

            try
            {
                if (OperatingSystem.IsWindows())
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                else
                    Process.Start(new ProcessStartInfo(Path.GetDirectoryName(path) ?? path) { UseShellExecute = true });
                return null;
            }
            catch (Exception ex)
            {
                return $"No se pudo abrir la ubicación:\n{ex.Message}";
            }
        }

        public static bool Existe(string path) =>
            !string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path));
    }
}
