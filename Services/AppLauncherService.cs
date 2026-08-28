using System;
using System.Diagnostics;

namespace android_folder_win11.Services
{
    // Un solo responsable: abrir una app dada su ruta.
    // Si mañana quieres loguear qué se abre, medir tiempos de uso, etc.,
    // este es el único lugar que hay que tocar.
    public static class AppLauncherService
    {
        public static void Lanzar(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No se pudo abrir '{path}': {ex.Message}");
            }
        }
    }
}
