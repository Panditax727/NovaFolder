namespace android_folder_win11.Models
{
    // Representa una sola app/acceso directo: nombre a mostrar + ruta real en disco
    public class AppShortcut
    {
        public string Name { get; set; } = "";

        // Ruta del archivo tal como aparece en la configuración: normalmente el .lnk
        public string Path { get; set; } = "";

        // Ruta del ejecutable de destino, resuelta leyendo el .lnk.
        // Es de donde conviene sacar el icono: el de un .lnk es el genérico con
        // la flechita, el del .exe es el icono de verdad de la aplicación.
        public string? TargetPath { get; set; }

        // De dónde sacar el icono, en orden de preferencia.
        public string IconSource =>
            !string.IsNullOrWhiteSpace(TargetPath) ? TargetPath! : Path;
    }
}
