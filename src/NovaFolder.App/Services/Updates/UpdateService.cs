using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NovaFolder.Core.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace NovaFolder.Services.Updates
{
    // Actualizaciones automáticas con Velopack desde GitHub Releases.
    //
    // Flujo: cada pocas horas se consulta si hay versión nueva; si la hay se
    // descarga en segundo plano (Velopack verifica el hash de cada paquete)
    // y se avisa con ActualizacionLista. Se instala sola en el siguiente
    // arranque, o al momento si el usuario elige "Reiniciar".
    //
    // Seguridad: la fuente es un repositorio PÚBLICO y la app no lleva
    // ningún token. Con un repo privado habría que incrustar un token en
    // el ejecutable, y cualquiera podría extraerlo: por eso no se admite.
    public sealed class UpdateService : IDisposable
    {
        // Único sitio donde se configura de dónde salen las versiones.
        public const string RepositorioGitHub = "https://github.com/Panditax727/NovaFolder";

        // Ficha de NovaFolder en la Microsoft Store (no es un dato secreto).
        public const string IdTienda = "9P3KG5PCB5HD";
        public const string EnlaceTienda = "ms-windows-store://pdp/?productid=" + IdTienda;

        private static readonly TimeSpan PrimeraComprobacion = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

        // null si Velopack no pudo iniciarse (p. ej. no se llamó a
        // VelopackApp.Run): la app funciona igual, solo sin actualizaciones.
        private readonly UpdateManager? _manager;
        private readonly SemaphoreSlim _enCurso = new(1, 1);
        private Timer? _temporizador;
        private UpdateInfo? _lista;

        // Llega en un hilo de fondo con la versión descargada.
        public event Action<string>? ActualizacionLista;

        public UpdateService()
        {
            // En la versión de la Store, las actualizaciones las hace la Store.
            if (DeLaTienda) return;
            try
            {
                _manager = new UpdateManager(new GithubSource(RepositorioGitHub, accessToken: null, prerelease: false));
            }
            catch (InvalidOperationException ex)
            {
                Log.Advertencia($"Actualizaciones desactivadas: {ex.Message}");
            }
        }

        // Solo una copia instalada con el Setup puede actualizarse; ejecutada
        // desde Visual Studio o "dotnet run" no hay nada que actualizar.
        public bool EstaInstalada => _manager?.IsInstalled == true;

        public static bool DeLaTienda => Services.Windows.PaqueteService.EsPaquete;

        public string VersionActual =>
            DeLaTienda ? Services.Windows.PaqueteService.Version ?? "?"
            : EstaInstalada && _manager!.CurrentVersion != null
                ? _manager.CurrentVersion.ToString()
                : (Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?") + " (desarrollo)";

        public string? VersionLista => _lista?.TargetFullRelease.Version.ToString();

        public void Iniciar()
        {
            if (DeLaTienda)
            {
                Log.Info("Versión de la Microsoft Store: la actualiza la Store.");
                return;
            }
            if (!EstaInstalada)
            {
                Log.Info("Copia de desarrollo: las actualizaciones automáticas están desactivadas.");
                return;
            }
            _temporizador = new Timer(_ => _ = BuscarAsync(), null, PrimeraComprobacion, Intervalo);
        }

        // Devuelve un texto para mostrar si la comprobación la pidió el usuario.
        public async Task<string> BuscarAsync()
        {
            if (DeLaTienda) return "Microsoft Store mantiene NovaFolder actualizado.";
            if (!EstaInstalada) return "Esta copia no se instaló con el instalador: no se actualiza sola.";
            if (_lista != null) return $"La versión {VersionLista} ya está descargada. Reinicia para instalarla.";
            if (!await _enCurso.WaitAsync(0).ConfigureAwait(false)) return "Ya se está buscando una actualización…";

            try
            {
                var info = await _manager!.CheckForUpdatesAsync().ConfigureAwait(false);
                if (info == null) return $"Tienes la última versión ({VersionActual}).";

                Log.Info($"Descargando la versión {info.TargetFullRelease.Version}…");
                await _manager.DownloadUpdatesAsync(info).ConfigureAwait(false);
                _lista = info;
                Log.Info($"Versión {VersionLista} lista para instalar.");
                ActualizacionLista?.Invoke(VersionLista!);
                return $"Versión {VersionLista} descargada.";
            }
            catch (Exception ex)
            {
                // Sin internet, GitHub caído, repo privado...: no es un error
                // del usuario ni debe molestarle. Se reintenta en el siguiente ciclo.
                Log.Advertencia($"No se pudo comprobar si hay actualizaciones: {ex.Message}");
                return "No se pudo comprobar si hay actualizaciones. Revisa tu conexión.";
            }
            finally
            {
                _enCurso.Release();
            }
        }

        public void ReiniciarEInstalar()
        {
            if (_lista == null) return;
            Log.Info($"Reiniciando para instalar la versión {VersionLista}.");
            _manager?.ApplyUpdatesAndRestart(_lista.TargetFullRelease);
        }

        public void Dispose()
        {
            _temporizador?.Dispose();
            _enCurso.Dispose();
        }
    }
}
