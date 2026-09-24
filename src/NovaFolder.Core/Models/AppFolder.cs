using System.Collections.Generic;

namespace NovaFolder.Core.Models
{
    // Representa una carpeta que agrupa varias apps (ej. "Juegos", "Utilidades")
    public class AppFolder
    {
        public string Name { get; set; } = "";
        public List<AppShortcut> Apps { get; set; } = new();
    }
}
