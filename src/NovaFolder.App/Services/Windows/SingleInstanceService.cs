using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NovaFolder.Core.Diagnostics;

namespace NovaFolder.Services.Windows
{
    // Garantiza que haya un solo NovaFolder corriendo por usuario.
    //
    // Antes, cada doble clic en una carpeta del Escritorio arrancaba un
    // proceso nuevo de Avalonia solo para mostrar el popup: casi un segundo
    // de espera y otra copia entera de la app en memoria. Ahora el segundo
    // proceso le pasa sus argumentos al primero por un named pipe y termina
    // al instante; el popup lo abre la instancia que ya estaba en marcha.
    public sealed class SingleInstanceService : IDisposable
    {
        private readonly Mutex _mutex;
        private readonly string _nombrePipe;
        private readonly CancellationTokenSource _cancelar = new();

        public bool EsPrimera { get; }

        // Llega en un hilo de fondo: quien lo escuche tiene que pasar al de la UI.
        public event Action<string[]>? ArgumentosRecibidos;

        public SingleInstanceService(string nombre)
        {
            // "Local\" = uno por sesión de usuario, no global a la máquina.
            var id = $"{nombre}-{Environment.UserName}";
            _mutex = new Mutex(true, @"Local\" + id, out var creado);
            EsPrimera = creado;
            _nombrePipe = id;
        }

        // Instancia secundaria: entrega los argumentos y devuelve si llegaron.
        public bool EnviarAPrimera(string[] args)
        {
            try
            {
                // La instancia secundaria la lanzó el usuario (doble clic), así
                // que tiene derecho a poner ventanas en primer plano; se lo cede
                // a la primera. Sin esto Windows deja el popup detrás de todo
                // y parpadeando en la barra de tareas.
                if (OperatingSystem.IsWindows()) AllowSetForegroundWindow(ASFW_ANY);

                using var cliente = new NamedPipeClientStream(".", _nombrePipe, PipeDirection.Out, PipeOptions.CurrentUserOnly);
                cliente.Connect(2000);
                using var escritor = new StreamWriter(cliente);
                // Un argumento por línea: los nombres de carpeta pueden tener
                // espacios, pero nunca saltos de línea.
                escritor.Write(string.Join("\n", args));
                escritor.Flush();
                return true;
            }
            catch (Exception ex)
            {
                Log.Advertencia($"No se pudo contactar con la instancia abierta: {ex.Message}");
                return false;
            }
        }

        // Instancia primaria: atiende a las siguientes, una tras otra.
        public void Escuchar()
        {
            if (!EsPrimera) return;
            _ = Task.Run(async () =>
            {
                while (!_cancelar.IsCancellationRequested)
                {
                    try
                    {
                        // CurrentUserOnly: solo procesos de este mismo usuario
                        // pueden conectarse. Sin esto, otro usuario de la
                        // máquina podría pedirle a NovaFolder que agregue rutas.
                        using var servidor = new NamedPipeServerStream(_nombrePipe, PipeDirection.In, 1,
                            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                        await servidor.WaitForConnectionAsync(_cancelar.Token);

                        using var lector = new StreamReader(servidor);
                        var texto = await LeerAcotadoAsync(lector, _cancelar.Token);
                        if (texto == null)
                        {
                            Log.Advertencia("Mensaje entre instancias demasiado grande: se descarta.");
                            continue;
                        }
                        var args = texto.Length == 0 ? Array.Empty<string>() : texto.Split('\n');
                        ArgumentosRecibidos?.Invoke(args);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        Log.Advertencia($"Error en el canal entre instancias: {ex.Message}");
                        await Task.Delay(500);
                    }
                }
            });
        }

        // Los argumentos de un lanzamiento normal ocupan poco: se leen como
        // máximo 256 KB (unas cuantas decenas de rutas largas) para que nada
        // pueda hacer que la instancia principal reserve memoria sin límite.
        private const int MaxCaracteresMensaje = 256 * 1024;

        private static async Task<string?> LeerAcotadoAsync(StreamReader lector, CancellationToken token)
        {
            var buffer = new char[4096];
            var texto = new StringBuilder();
            int leidos;
            while ((leidos = await lector.ReadAsync(buffer.AsMemory(), token)) > 0)
            {
                texto.Append(buffer, 0, leidos);
                if (texto.Length > MaxCaracteresMensaje) return null;
            }
            return texto.ToString();
        }

        public void Dispose()
        {
            _cancelar.Cancel();
            _cancelar.Dispose();
            if (EsPrimera)
            {
                try { _mutex.ReleaseMutex(); }
                catch (ApplicationException) { /* ya liberado */ }
            }
            _mutex.Dispose();
        }

        private const int ASFW_ANY = -1;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);
    }
}
