namespace NovaFolder.Core.Models
{
    // Representa una sola app/acceso directo: nombre a mostrar + ruta real en disco
    public class AppShortcut
    {
        public string Name { get; set; } = "";

        // Ruta absoluta del elemento: un .lnk, un .exe, un documento o una carpeta.
        public string Path { get; set; } = "";

        // Ruta del ejecutable de destino, resuelta leyendo el .lnk.
        // Es de donde conviene sacar el icono: el de un .lnk es el genérico con
        // la flechita, el del .exe es el icono de verdad de la aplicación.
        public string? TargetPath { get; set; }

        // Archivo del que el propio .lnk dice que hay que sacar el icono
        // (su campo IconLocation), ya resuelto y comprobado.
        public string? IconPath { get; set; }

        // De dónde sacar el icono, en orden de preferencia.
        //
        // IconPath va primero porque es lo que usa el propio Windows, y en
        // varios instaladores es la ÚNICA fuente correcta: Discord, por
        // ejemplo, apunta su acceso directo a Update.exe (el stub del
        // actualizador, con un icono genérico) y guarda el logo de verdad
        // aparte en app.ico. Quedarse con TargetPath mostraba ese icono
        // equivocado.
        public string IconSource =>
            !string.IsNullOrWhiteSpace(IconPath) ? IconPath!
            : !string.IsNullOrWhiteSpace(TargetPath) ? TargetPath!
            : Path;
    }
}
