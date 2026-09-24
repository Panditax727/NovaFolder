using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NovaFolder.Services.Windows
{
    // Orden Z de las ventanas de NovaFolder respecto al resto del sistema.
    //
    // El widget se queda como top-level normal (Avalonia la dibuja sin
    // problemas) y se manda sola al fondo del z-order del Escritorio cada
    // tanto. Cualquier ventana que uses la tapa, y vuelve a quedar a la
    // vista en cuanto la cierras, minimizas todo, o muestras el Escritorio
    // (Win+D).
    //
    // No reparenta la ventana como hija de Progman/WorkerW (el truco real
    // de Fences/Wallpaper Engine) a propósito: se probó, y aunque el
    // reparentado en sí funciona, Avalonia deja de pintar en cuanto la
    // ventana pasa a ser hija de otra (limitación del motor de render, no
    // del truco — WPF tiene la misma).
    [SupportedOSPlatform("windows")]
    internal static class DesktopIntegrationService
    {
        private static readonly IntPtr HWND_BOTTOM = new(1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        // OJO: el widget y los popups comparten la ventana dueña oculta que
        // Avalonia crea para ShowInTaskbar="False". Mandar el widget al
        // fondo arrastra a todo el grupo y les quita el TOPMOST a los
        // popups (comprobado con GetWindowLong). No llamar a esto mientras
        // haya un popup o menú abierto: ver MainWindow.EnviarAlFondo.
        public static void MantenerAlFondo(IntPtr hwnd) =>
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
    }
}
