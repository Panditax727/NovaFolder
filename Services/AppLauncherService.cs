using System;
using System.Diagnostics;
using System.IO;

namespace android_folder_win11.Services
{
    // Un solo responsable: abrir una app dada su ruta.
    // Si mañana quieres loguear qué se abre, medir tiempos de uso, etc.,
    // este es el único lugar que hay que tocar.
    public static class AppLauncherService
    {
        // Devuelve el motivo del fallo, o null si se lanzó bien.
        //
        // Antes esto era void y solo hacía Console.WriteLine. Como el proyecto
        // es <OutputType>WinExe</OutputType>, en Windows NO hay consola: si el
        // acceso directo no existía, el clic no hacía absolutamente nada y el
        // usuario no tenía forma de saber por qué. Ahora el llamador recibe el
        // error y puede enseñarlo en pantalla.
        public static string? Lanzar(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "La ruta está vacía.";

            if (!File.Exists(path) && !Directory.Exists(path))
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

        public static bool Existe(string path) =>
            !string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path));
    }
}
