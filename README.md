# NovaFolder 📁✨

**NovaFolder** es una aplicación de escritorio nativa para Windows 11 desarrollada en C# y Avalonia UI, diseñada para organizar accesos directos, aplicaciones y archivos en carpetas flotantes e interactivas inspiradas en la interfaz de Android.

---

## Características
* **Estilo Android en Desktop:** Agrupa aplicaciones y archivos en carpetas compactas, desplegables y personalizables.
* **Integración con Windows 11:** Interfaz adaptada al lenguaje visual Fluent Design.
* **Arquitectura Modular:** Separación clara entre modelos, servicios del sistema y controles de usuario reutilizables.
* **Ligero y Rápido:** Construcción nativa sobre .NET y Avalonia UI (soporte SkiaSharp/HarfBuzzSharp).

---

## Arquitectura y Estructura del Proyecto

```text
NovaFolder/
├── Models/
│   ├── AppShortcut.cs          → Representa una app individual (nombre + ruta)
│   └── AppFolder.cs            → Contenedor que agrupa varias AppShortcut
├── Services/
│   ├── FolderConfigService.cs  → Gestiona folders.json y resuelve rutas del Escritorio
│   ├── IconService.cs          → Extrae íconos reales (Windows) o fallback (Linux/error)
│   └── AppLauncherService.cs   → Ejecuta una app dada su ruta de sistema
├── Controls/
│   ├── FolderControl.axaml     → Contenedor visual del widget de carpeta
│   └── FolderControl.axaml.cs  → Lógica visual: preview 2x2, expandir/colapsar y eventos
├── MainWindow.axaml
└── MainWindow.axaml.cs         → Carga la configuración e instancia los FolderControl

cat << 'EOF' >> README.md

### Principios de diseño aplicados

* **Models:** Clases puras de datos sin lógica de presentación. Permiten extender atributos (como colores o temas por carpeta) sin romper la estructura principal.
* **Services:** Módulos estáticos y sin estado dedicados a tareas concretas (lectura de config, extracción de íconos de accesos directos y ejecución de procesos).
* **Controls (`FolderControl`):** Componente de UI completamente encapsulado. La ventana principal desconoce la renderización interna de la carpeta y solo administra sus instancias.
* **MainWindow:** Muestra la vista principal actuando únicamente como contenedor de la lista de carpetas cargadas.

---

## 💻 Desarrollo y Compilación

### Requisitos
* .NET SDK 8.0 o superior

### Dependencias e inicio local
```bash
# Agregar dependencia para el manejo de íconos en Windows
dotnet add package System.Drawing.Common

# Restaurar paquetes y ejecutar
dotnet restore
dotnet run
