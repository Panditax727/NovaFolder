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

        private static readonly TimeSpan PrimeraComprobacion = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

        private readonly UpdateManager _manager;
        private readonly SemaphoreSlim _enCurso = new(1, 1);
        private Timer? _temporizador;
        private UpdateInfo? _lista;

        // Llega en un hilo de fondo con la versión descargada.
        public event Action<string>? ActualizacionLista;

        public UpdateService()
        {
            _manager = new UpdateManager(new GithubSource(RepositorioGitHub, accessToken: null, prerelease: false));
        }

        // Solo una copia instalada con el Setup puede actualizarse; ejecutada
        // desde Visual Studio o "dotnet run" no hay nada que actualizar.
        public bool EstaInstalada => _manager.IsInstalled;

        public string VersionActual =>
            _manager.IsInstalled && _manager.CurrentVersion != null
                ? _manager.CurrentVersion.ToString()
                : (Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?") + " (desarrollo)";

        public string? VersionLista => _lista?.TargetFullRelease.Version.ToString();

        public void Iniciar()
        {
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
            if (!EstaInstalada) return "Esta copia no se instaló con el instalador: no se actualiza sola.";
            if (_lista != null) return $"La versión {VersionLista} ya está descargada. Reinicia para instalarla.";
            if (!await _enCurso.WaitAsync(0).ConfigureAwait(false)) return "Ya se está buscando una actualización…";

            try
            {
                var info = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
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
            _manager.ApplyUpdatesAndRestart(_lista.TargetFullRelease);
        }

        public void Dispose()
        {
            _temporizador?.Dispose();
            _enCurso.Dispose();
        }
    }
}
