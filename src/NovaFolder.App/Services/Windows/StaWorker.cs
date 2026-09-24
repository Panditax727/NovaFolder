using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NovaFolder.Services.Windows
{
    // Un hilo propio, en STA, para todo lo que toca el shell de Windows por
    // COM (extraer íconos, componer el ícono de carpeta, crear .lnk).
    //
    // Por qué no Task.Run: los hilos del pool son MTA, y varias extensiones
    // del shell que sirven íconos (OneDrive, Steam, algunos antivirus) solo
    // funcionan en STA. Y por qué no el hilo de la UI, que sí es STA: extraer
    // 30 íconos a 256 px ahí congelaba la ventana casi un segundo al arrancar.
    public sealed class StaWorker
    {
        public static StaWorker Compartido { get; } = new("NovaFolder.Shell");

        private readonly BlockingCollection<Action> _cola = new();

        private StaWorker(string nombre)
        {
            var hilo = new Thread(Bucle) { IsBackground = true, Name = nombre };
            if (OperatingSystem.IsWindows()) hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
        }

        public Task<T> Ejecutar<T>(Func<T> trabajo)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _cola.Add(() =>
            {
                try { tcs.SetResult(trabajo()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        public Task Ejecutar(Action trabajo) => Ejecutar(() => { trabajo(); return true; });

        private void Bucle()
        {
            foreach (var trabajo in _cola.GetConsumingEnumerable())
                trabajo();
        }
    }
}
