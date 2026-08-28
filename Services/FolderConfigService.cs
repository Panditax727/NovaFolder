using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using android_folder_win11.Models;

namespace android_folder_win11.Services
{
    // Toda la lógica de "de dónde salen las carpetas y qué apps tienen adentro"
    // vive aquí. Si mañana quieres cargar desde otro lado (registro de Windows,
    // una base de datos, drag&drop guardado en otro formato), solo tocas este archivo.
    public static class FolderConfigService
    {
        private static readonly string DesktopPath =
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        private static string ConfigPath =>
            Path.Combine(AppContext.BaseDirectory, "folders.json");

        public static List<AppFolder> Cargar()
        {
            if (!File.Exists(ConfigPath))
            {
                CrearConfigDeEjemplo();
            }

            var json = File.ReadAllText(ConfigPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json)
                       ?? new Dictionary<string, string[]>();

            return data.Select(kv => new AppFolder
            {
                Name = kv.Key,
                Apps = kv.Value.Select(filename => new AppShortcut
                {
                    Name = Path.GetFileNameWithoutExtension(filename),
                    Path = Path.Combine(DesktopPath, filename)
                }).ToList()
            }).ToList();
        }

        // Formato del JSON: { "Juegos": ["Steam.lnk", "Minecraft.lnk"], "Utilidades": [...] }
        // Los nombres deben coincidir exactamente con archivos reales en el Escritorio.
        private static void CrearConfigDeEjemplo()
        {
            var ejemplo = new Dictionary<string, string[]>
            {
                ["Juegos"] = new[] { "Steam.lnk", "Minecraft.lnk", "Portal 2.lnk", "Half-Life.lnk" },
                ["Utilidades"] = new[] { "Calculadora.lnk", "Bloc de notas.lnk" }
            };

            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(ejemplo, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
    }
}
