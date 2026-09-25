using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using NovaFolder.Core.Diagnostics;
using NovaFolder.Core.Errors;
using NovaFolder.Core.Models;
using NovaFolder.Core.Validation;

namespace NovaFolder.Core.Storage
{
    // Lectura y escritura de folders.json. Solo sabe de disco y de formato:
    // quién cambia las carpetas y cuándo se guarda lo decide FolderStore.
    //
    // Formato actual (v2):
    //   {
    //     "version": 2,
    //     "startWithWindows": true,
    //     "cleanDesktop": false,
    //     "window": { "x": 40, "y": 40 },
    //     "folders": [ { "name": "Juegos", "items": ["C:\\...\\Steam.lnk"] } ]
    //   }
    //
    // Se sigue aceptando el formato antiguo { "Juegos": ["Steam.lnk", ...] }
    // y se reescribe como v2 la primera vez que se guarde.
    public sealed class ConfigRepository
    {
        private readonly RutasApp _rutas;

        private static readonly JsonSerializerOptions Opciones = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public ConfigRepository(RutasApp rutas) => _rutas = rutas;

        public string RutaArchivo => _rutas.ArchivoConfig;

        // Nunca lanza: si el archivo está roto se aparta (para no perderlo)
        // y se arranca con una configuración limpia. Una configuración mala
        // no puede impedir que la app abra.
        public NovaConfig Cargar()
        {
            try
            {
                Directory.CreateDirectory(_rutas.Datos);
                if (!File.Exists(RutaArchivo)) return ConfigInicial();

                if (new FileInfo(RutaArchivo).Length > Limites.TamanoMaxConfig)
                    throw new ConfiguracionException("folders.json es demasiado grande.");

                return Interpretar(File.ReadAllText(RutaArchivo), _rutas.Escritorio);
            }
            catch (Exception ex) when (ex is JsonException or ConfiguracionException)
            {
                var roto = RutaArchivo + ".roto";
                try { File.Move(RutaArchivo, roto, overwrite: true); }
                catch (Exception mover) when (mover is IOException or UnauthorizedAccessException) { /* se queda donde está */ }

                Log.Error($"folders.json inválido; se apartó como {roto} y se empieza de cero.", ex);
                return ConfigInicial();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Error("No se pudo leer folders.json; se empieza con una configuración vacía.", ex);
                return ConfigInicial();
            }
        }

        // Devuelve el texto exacto que se escribió: FolderStore lo compara con
        // lo que lee el vigilante de archivos para ignorar sus propios guardados.
        public string Guardar(NovaConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            var dto = new ConfigDto
            {
                Version = 2,
                StartWithWindows = config.StartWithWindows,
                CleanDesktop = config.CleanDesktop,
                ShowWidget = config.ShowWidget,
                DesktopFolders = config.DesktopFolders,
                WelcomeSeen = config.WelcomeSeen,
                TrayHintShown = config.TrayHintShown,
                Window = config.WindowX is int x && config.WindowY is int y ? new VentanaDto { X = x, Y = y } : null,
                Folders = config.Folders.Select(f => new CarpetaDto
                {
                    Name = f.Name,
                    Items = f.Apps.Select(a => a.Path).ToList()
                }).ToList()
            };

            var json = JsonSerializer.Serialize(dto, Opciones);

            try
            {
                Directory.CreateDirectory(_rutas.Datos);
                // Escribir a un temporal y renombrar: si el proceso muere a
                // mitad de escritura, el folders.json anterior sigue intacto.
                var temporal = RutaArchivo + ".tmp";
                File.WriteAllText(temporal, json);
                File.Move(temporal, RutaArchivo, overwrite: true);
                return json;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new ConfiguracionException($"No se pudo guardar la configuración: {ex.Message}", ex);
            }
        }

        // Texto -> configuración. Lanza JsonException si no es un JSON válido.
        // Las entradas inválidas (nombres vacíos o repetidos, carpetas de más)
        // se descartan con un aviso en el registro en vez de rechazar el
        // archivo entero: es lo que alguien espera al editarlo a mano.
        public static NovaConfig Interpretar(string json, string escritorio)
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("La raíz tiene que ser un objeto.");

            var esV2 = doc.RootElement.TryGetProperty("folders", out var f) && f.ValueKind == JsonValueKind.Array;
            var config = esV2
                ? DesdeV2(doc.RootElement.Deserialize<ConfigDto>(Opciones) ?? new ConfigDto())
                : DesdeFormatoAntiguo(doc.RootElement);

            config.Folders = Sanear(config.Folders, escritorio);
            return config;
        }

        private static NovaConfig DesdeV2(ConfigDto dto) => new()
        {
            StartWithWindows = dto.StartWithWindows ?? true,
            CleanDesktop = dto.CleanDesktop ?? false,
            ShowWidget = dto.ShowWidget ?? true,
            DesktopFolders = dto.DesktopFolders ?? true,
            WelcomeSeen = dto.WelcomeSeen ?? false,
            TrayHintShown = dto.TrayHintShown ?? false,
            WindowX = dto.Window?.X,
            WindowY = dto.Window?.Y,
            Folders = (dto.Folders ?? new())
                .Select(c => Borrador(c.Name, c.Items ?? new()))
                .ToList()
        };

        private static NovaConfig DesdeFormatoAntiguo(JsonElement raiz) => new()
        {
            Folders = raiz.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.Array)
                .Select(p => Borrador(p.Name, p.Value.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList()))
                .ToList()
        };

        // Carpeta tal como viene del archivo, antes de validar. Los elementos
        // guardan la ruta en bruto en Path; Sanear la resuelve.
        private static AppFolder Borrador(string? nombre, List<string> items) => new()
        {
            Name = nombre ?? "",
            Apps = items.Select(i => new AppShortcut { Path = i ?? "" }).ToList()
        };

        private static List<AppFolder> Sanear(List<AppFolder> borradores, string escritorio)
        {
            var resultado = new List<AppFolder>();
            var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var borrador in borradores)
            {
                string nombre;
                try { nombre = Validador.NombreCarpeta(borrador.Name); }
                catch (ValidacionException ex)
                {
                    Log.Advertencia($"Carpeta «{borrador.Name}» ignorada: {ex.Message}");
                    continue;
                }

                if (!nombres.Add(nombre))
                {
                    Log.Advertencia($"Carpeta «{nombre}» repetida en folders.json: se ignora la segunda.");
                    continue;
                }
                if (resultado.Count >= Limites.MaxCarpetas)
                {
                    Log.Advertencia($"Más de {Limites.MaxCarpetas} carpetas en folders.json: se ignoran las restantes.");
                    break;
                }

                // Los elementos NO se validan contra el disco: un acceso cuyo
                // programa se desinstaló sigue en la carpeta (marcado como
                // roto) para que el usuario decida qué hacer con él.
                resultado.Add(new AppFolder
                {
                    Name = nombre,
                    Apps = borrador.Apps
                        .Where(a => !string.IsNullOrWhiteSpace(a.Path))
                        .DistinctBy(a => a.Path, StringComparer.OrdinalIgnoreCase)
                        .Take(Limites.MaxElementosPorCarpeta)
                        .Select(a => ShortcutResolver.Crear(a.Path, escritorio))
                        .ToList()
                });
            }

            return resultado;
        }

        // Primera ejecución: una carpeta vacía que invita a arrastrar cosas
        // dentro, y el Escritorio limpio por defecto (la bienvenida deja
        // desactivarlo antes de que se mueva nada).
        private static NovaConfig ConfigInicial() => new()
        {
            CleanDesktop = true,
            ShowWidget = false,
            Folders = { new AppFolder { Name = "Favoritos" } }
        };

        // ---- forma exacta del JSON en disco ----

        private sealed class ConfigDto
        {
            [JsonPropertyName("version")] public int Version { get; set; }
            [JsonPropertyName("startWithWindows")] public bool? StartWithWindows { get; set; }
            [JsonPropertyName("cleanDesktop")] public bool? CleanDesktop { get; set; }
            [JsonPropertyName("showWidget")] public bool? ShowWidget { get; set; }
            [JsonPropertyName("desktopFolders")] public bool? DesktopFolders { get; set; }
            [JsonPropertyName("welcomeSeen")] public bool? WelcomeSeen { get; set; }
            [JsonPropertyName("trayHintShown")] public bool? TrayHintShown { get; set; }
            [JsonPropertyName("window")] public VentanaDto? Window { get; set; }
            [JsonPropertyName("folders")] public List<CarpetaDto>? Folders { get; set; }
        }

        private sealed class VentanaDto
        {
            [JsonPropertyName("x")] public int X { get; set; }
            [JsonPropertyName("y")] public int Y { get; set; }
        }

        private sealed class CarpetaDto
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("items")] public List<string>? Items { get; set; }
        }
    }
}
