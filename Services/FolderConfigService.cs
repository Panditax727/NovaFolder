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

        // La configuración va al perfil del usuario, NO junto al ejecutable.
        // Antes se guardaba en AppContext.BaseDirectory, que es bin/Debug/netX:
        // un "dotnet clean" o borrar bin/ se llevaba por delante las carpetas del
        // usuario, y una vez instalado en Program Files ni siquiera hay permiso
        // de escritura ahí.
        //   Windows -> %APPDATA%\NovaFolder\folders.json
        //   Linux   -> ~/.config/NovaFolder/folders.json
        private static string ConfigDir =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NovaFolder");

        private static string ConfigPath => Path.Combine(ConfigDir, "folders.json");

        public static List<AppFolder> Cargar()
        {
            Directory.CreateDirectory(ConfigDir);
            MigrarDesdeCarpetaDelEjecutable();

            if (!File.Exists(ConfigPath))
                CrearConfigDeEjemplo();

            Dictionary<string, string[]> data;
            try
            {
                var json = File.ReadAllText(ConfigPath);
                data = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json)
                       ?? new Dictionary<string, string[]>();
            }
            catch (JsonException ex)
            {
                // Un JSON mal escrito a mano no debe impedir que la app abra:
                // se aparta el archivo roto y se arranca con el de ejemplo.
                var roto = ConfigPath + ".roto";
                try { File.Move(ConfigPath, roto, overwrite: true); } catch { /* da igual */ }
                Console.Error.WriteLine($"folders.json inválido ({ex.Message}). Se movió a {roto}.");
                CrearConfigDeEjemplo();
                data = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
                           File.ReadAllText(ConfigPath)) ?? new();
            }

            return data.Select(kv => new AppFolder
            {
                Name = kv.Key,
                Apps = (kv.Value ?? Array.Empty<string>()).Select(CrearAcceso).ToList()
            }).ToList();
        }

        public static string RutaDeConfiguracion => ConfigPath;

        private static AppShortcut CrearAcceso(string filename)
        {
            // Una ruta absoluta en el JSON se respeta tal cual; solo se asume
            // el Escritorio cuando viene un nombre suelto.
            var ruta = Path.IsPathRooted(filename) ? filename : Path.Combine(DesktopPath, filename);

            var acceso = new AppShortcut
            {
                Name = Path.GetFileNameWithoutExtension(filename),
                Path = ruta
            };

            // Si es un .lnk se lee para sacar el ejecutable real: de ahi sale un
            // icono de verdad en vez del generico con la flechita del acceso directo.
            if (LnkReader.EsLnk(ruta))
            {
                var info = LnkReader.Leer(ruta);
                if (info != null)
                {
                    acceso.TargetPath = info.Destino;
                    if (!string.IsNullOrWhiteSpace(info.Descripcion))
                        acceso.Name = info.Descripcion!;
                }
            }

            return acceso;
        }

        // Si existe un folders.json de la versión antigua junto al ejecutable,
        // se copia una sola vez al perfil para no perder lo que el usuario tenía.
        private static void MigrarDesdeCarpetaDelEjecutable()
        {
            if (File.Exists(ConfigPath)) return;

            var antiguo = Path.Combine(AppContext.BaseDirectory, "folders.json");
            if (!File.Exists(antiguo)) return;

            try
            {
                File.Copy(antiguo, ConfigPath);
                Console.Error.WriteLine($"Configuración migrada de {antiguo} a {ConfigPath}.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"No se pudo migrar la configuración antigua: {ex.Message}");
            }
        }

        // Formato del JSON: { "Juegos": ["Steam.lnk", "Minecraft.lnk"], "Utilidades": [...] }
        // Un nombre suelto se busca en el Escritorio; también se acepta una ruta completa.
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
