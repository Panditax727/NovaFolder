using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using NovaFolder.Core.Models;
using NovaFolder.Core;
using NovaFolder.Core.Storage;
using NovaFolder.Core.Diagnostics;

namespace NovaFolder.Services.Applications
{
    // Crea/actualiza, en el Escritorio real, un .lnk por cada carpeta de
    // NovaFolder. Apuntan al propio NovaFolder.exe con "--folder <nombre>",
    // así que al abrirlos Windows no lanza el Explorador sino el popup de
    // NovaFolder para esa carpeta (ver App.axaml.cs). Es la forma soportada
    // de tener "una carpeta más" en el Escritorio con comportamiento propio:
    // un acceso directo normal, sin tocar el ListView de íconos de explorer.exe.
    [SupportedOSPlatform("windows")]
    internal static class ShortcutService
    {
        private const string PrefijoArgumentos = "--folder ";

        // Hay que llamarlo desde un hilo STA (ver StaWorker): usa COM.
        // Recibe una copia de las carpetas, no la lista viva, porque la UI
        // puede seguir modificándola mientras esto corre en segundo plano.
        public static void Sincronizar(IReadOnlyList<AppFolder> carpetas, RutasApp rutas)
        {
            var exePath = RutaEjecutable();
            if (exePath == null) return;

            var escritorio = rutas.Escritorio;
            var vigentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool huboCambios = false;

            foreach (var carpeta in carpetas)
            {
                var ruta = Path.Combine(escritorio, SanearNombreArchivo(carpeta.Name) + ".lnk");
                vigentes.Add(ruta);
                try
                {
                    // Un ícono por carpeta, con la miniatura 2x2 de lo que tiene
                    // dentro (ver IconoCarpeta).
                    var icono = IconoCarpeta.Asegurar(carpeta, rutas.CarpetaIconos);
                    var argumentos = $"{PrefijoArgumentos}\"{carpeta.Name}\"";

                    if (EstaAlDia(ruta, exePath, argumentos, icono)) continue;

                    CrearOActualizar(ruta, exePath, argumentos, carpeta.Name, icono);
                    huboCambios = true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or ExternalException)
                {
                    Log.Advertencia($"No se pudo crear el acceso directo de '{carpeta.Name}': {ex.Message}");
                }
            }

            huboCambios |= BorrarHuerfanos(escritorio, exePath, vigentes);
            IconoCarpeta.BorrarHuerfanos(carpetas, rutas.CarpetaIconos);

            // Sin esto el Escritorio sigue mostrando el ícono que tenía
            // cacheado hasta que se reinicie el Explorador.
            if (huboCambios)
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }

        // Al desinstalar: se quitan del Escritorio todos los accesos de carpeta.
        public static void BorrarTodos(RutasApp rutas)
        {
            var exePath = RutaEjecutable();
            if (exePath != null && BorrarHuerfanos(rutas.Escritorio, exePath, new HashSet<string>()))
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }

        private static string? RutaEjecutable()
        {
            // Empaquetada (Store): el alias estable, no la ruta de WindowsApps,
            // que cambia en cada actualización.
            if (Services.Windows.PaqueteService.EsPaquete) return Services.Windows.PaqueteService.RutaAlias;
            var ruta = Environment.ProcessPath;
            return string.IsNullOrWhiteSpace(ruta) ? null : ruta;
        }

        // Reescribir los .lnk en cada arranque hacía parpadear el Escritorio
        // aunque nada hubiera cambiado. Solo se tocan si difieren.
        private static bool EstaAlDia(string rutaLnk, string exePath, string argumentos, string icono)
        {
            var info = LnkReader.Leer(rutaLnk);
            return info != null
                && string.Equals(info.Destino, exePath, StringComparison.OrdinalIgnoreCase)
                && info.Argumentos == argumentos
                && string.Equals(info.Icono, icono, StringComparison.OrdinalIgnoreCase);
        }

        // Al renombrar o eliminar una carpeta, su .lnk viejo quedaba en el
        // Escritorio apuntando a una carpeta que ya no existe. Solo se borran
        // accesos que son inequívocamente nuestros: apuntan a este mismo
        // ejecutable y llevan "--folder".
        private static bool BorrarHuerfanos(string escritorio, string exePath, HashSet<string> vigentes)
        {
            bool borrado = false;
            var nombreExe = Path.GetFileName(exePath);

            foreach (var lnk in Directory.EnumerateFiles(escritorio, "*.lnk"))
            {
                if (vigentes.Contains(lnk)) continue;

                var info = LnkReader.Leer(lnk);
                if (info?.Argumentos == null || !info.Argumentos.StartsWith(PrefijoArgumentos)) continue;
                if (!string.Equals(Path.GetFileName(info.Destino), nombreExe, StringComparison.OrdinalIgnoreCase)) continue;

                try { File.Delete(lnk); borrado = true; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Advertencia($"No se pudo borrar {lnk}: {ex.Message}"); }
            }

            return borrado;
        }

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private static string SanearNombreArchivo(string nombre)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre;
        }

        private static void CrearOActualizar(string rutaLnk, string exePath, string argumentos, string nombreCarpeta, string iconoPath)
        {
            var shellLink = (IShellLinkW)new ShellLinkCoClass();
            try
            {
                shellLink.SetPath(exePath);
                shellLink.SetArguments(argumentos);
                shellLink.SetDescription($"Carpeta NovaFolder: {nombreCarpeta}");
                shellLink.SetIconLocation(iconoPath, 0);

                var persistFile = (IPersistFile)shellLink;
                persistFile.Save(rutaLnk, false);
            }
            finally
            {
                Marshal.ReleaseComObject(shellLink);
            }
        }

        // ---- COM: creación de accesos directos (.lnk) ----

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLinkCoClass { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }
    }
}
