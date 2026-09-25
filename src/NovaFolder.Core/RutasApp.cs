using System;
using System.IO;

namespace NovaFolder.Core
{
    // Todas las carpetas del disco que usa NovaFolder, en un solo sitio.
    //
    // Es una instancia (no constantes estáticas) para poder inyectarla: las
    // pruebas crean una apuntando a un directorio temporal y así nunca tocan
    // la configuración ni el Escritorio reales del usuario.
    public sealed class RutasApp
    {
        // escritorioComun: el Escritorio de todos los usuarios (C:\Users\Public\Desktop),
        // donde muchos instaladores dejan sus accesos. null = no se revisa.
        public RutasApp(string datos, string escritorio, string? escritorioComun = null)
        {
            if (string.IsNullOrWhiteSpace(datos)) throw new ArgumentException("Falta la carpeta de datos.", nameof(datos));
            if (string.IsNullOrWhiteSpace(escritorio)) throw new ArgumentException("Falta la carpeta del Escritorio.", nameof(escritorio));

            Datos = Path.GetFullPath(datos);
            Escritorio = Path.GetFullPath(escritorio);
            EscritorioComun = string.IsNullOrWhiteSpace(escritorioComun) ? null : Path.GetFullPath(escritorioComun);
        }

        // La configuración va al perfil del usuario, NO junto al ejecutable:
        // el instalador reemplaza la carpeta del programa en cada
        // actualización, y en Program Files ni siquiera hay permiso de escritura.
        //   Windows -> %APPDATA%\NovaFolder
        //   Linux   -> ~/.config/NovaFolder
        public static RutasApp PorDefecto() => new(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NovaFolder"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));

        public string Datos { get; }
        public string Escritorio { get; }
        public string? EscritorioComun { get; }

        public string ArchivoConfig => Path.Combine(Datos, "folders.json");
        public string CarpetaRegistros => Path.Combine(Datos, "logs");

        // Por defecto dentro de Datos. La versión de la Microsoft Store las
        // saca de ahí (ver Program): sus datos se borran al desinstalar, y
        // los accesos guardados del usuario no pueden perderse con ellos.
        public string CarpetaAccesos { get => _accesos ?? Path.Combine(Datos, "accesos"); init => _accesos = value; }
        public string CarpetaIconos { get => _iconos ?? Path.Combine(Datos, "iconos"); init => _iconos = value; }

        private readonly string? _accesos;
        private readonly string? _iconos;
    }
}
