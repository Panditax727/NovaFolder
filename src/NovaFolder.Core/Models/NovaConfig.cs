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
        // AlmacenAccesos). Apagado por defecto: mover archivos del usuario
        // tiene que ser una decisión suya.
        public bool CleanDesktop { get; set; }
    }
}
