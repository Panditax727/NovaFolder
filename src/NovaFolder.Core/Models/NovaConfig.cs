using System.Collections.Generic;

namespace NovaFolder.Core.Models
{
    // Todo lo que NovaFolder recuerda entre arranques. Es lo que se guarda
    // en folders.json (ver FolderConfigService para el formato en disco).
    public class NovaConfig
    {
        public List<AppFolder> Folders { get; set; } = new();

        // Dónde dejó el usuario el widget la última vez. null = esquina por defecto.
        public int? WindowX { get; set; }
        public int? WindowY { get; set; }

        public bool StartWithWindows { get; set; } = true;

        // Al agregar un acceso directo del Escritorio, sacarlo de ahí (ver
        // AlmacenAccesos). Es el objetivo de la app, pero mover archivos del
        // usuario tiene que ser decisión suya: se confirma en la bienvenida.
        public bool CleanDesktop { get; set; }

        // Widget flotante sobre el Escritorio. Es un complemento opcional: la
        // app principal es su ventana. Apagado en instalaciones nuevas.
        public bool ShowWidget { get; set; } = true;

        // Un acceso por carpeta en el Escritorio, para abrirlas desde ahí.
        public bool DesktopFolders { get; set; } = true;

        // Ya vio la bienvenida guiada (se muestra una sola vez).
        public bool WelcomeSeen { get; set; }

        // Ya se le explicó que al ocultar el widget la app sigue en la bandeja.
        public bool TrayHintShown { get; set; }
    }
}
