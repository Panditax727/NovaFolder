namespace android_folder_win11.Models
{
    // Representa una sola app/acceso directo: nombre a mostrar + ruta real en disco
    public class AppShortcut
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
    }
}
