# NovaFolder 📁✨

[![CI](https://github.com/Panditax727/NovaFolder/actions/workflows/ci.yml/badge.svg)](https://github.com/Panditax727/NovaFolder/actions/workflows/ci.yml)

Organiza los accesos directos, juegos y archivos del Escritorio de Windows en carpetas que se abren con un clic, al estilo Android, sin pasar por el Explorador.

- **¿Quieres usarlo?** → [Microsoft Store](https://apps.microsoft.com/detail/9P3KG5PCB5HD) · [Guía de instalación](docs/INSTALACION.md)
- **¿Vas a publicar una versión?** → [Guía de publicación](docs/PUBLICAR.md)
- [Privacidad](docs/PRIVACIDAD.md) · [Firma de código](docs/FIRMA-DE-CODIGO.md) · [Seguridad](SECURITY.md) · Licencia [MIT](LICENSE)
- **¿Qué cambió?** → [CHANGELOG](CHANGELOG.md)

---

## Estructura del proyecto

```text
NovaFolder/
├── src/
│   ├── NovaFolder.Core/          Lógica sin interfaz (se prueba de forma aislada)
│   │   ├── Models/               AppFolder, AppShortcut, NovaConfig
│   │   ├── Storage/              FolderStore (CRUD), ConfigRepository (JSON), AlmacenAccesos, LnkReader
│   │   ├── Organization/         Clasificador (sugiere carpeta) y EscritorioScanner (qué está suelto)
│   │   ├── Validation/           Validador + Limites: reglas de nombres, rutas y tamaños
│   │   ├── Errors/               NovaFolderException y derivadas
│   │   ├── Diagnostics/          Log: registro diario en archivo
│   │   └── RutasApp.cs           Todas las rutas del disco, inyectables
│   │
│   └── NovaFolder.App/           Aplicación de escritorio (Avalonia, solo Windows)
│       ├── Program.cs            Arranque: Velopack, registro, errores globales, instancia única
│       ├── App.axaml(.cs)        Raíz de composición, bandeja y estilos centralizados
│       ├── Views/                MainWindow (ventana de la app), WidgetWindow (widget flotante),
│       │   │                     FolderPopupWindow (carpeta abierta estilo Android),
│       │   │                     BienvenidaWindow (guía), NotificacionWindow (avisos junto al reloj)
│       │   ├── Paginas/          Carpetas, Ordenar Escritorio, Ajustes, Ayuda
│       │   └── Principal/        ServiciosApp (lo que reciben las páginas) e IAvisador
│       ├── Controls/             VistaCarpeta (rejilla de una carpeta, compartida), FolderTile,
│       │                         AppTile, Interacciones (clic, teclado, arrastrar)
│       └── Services/
│           ├── Applications/     Lanzar apps, accesos del Escritorio, ícono de carpeta
│           ├── Windows/          Íconos del shell, hilo STA, instancia única, autoinicio, orden Z
│           └── Updates/          Actualizaciones automáticas (Velopack)
│
├── tests/NovaFolder.Core.Tests/  Pruebas xUnit de Core
├── scripts/
│   ├── publicar.ps1              Pruebas + instalador Setup.exe (GitHub Releases)
│   └── empaquetar-store.ps1      Paquete MSIX para la Microsoft Store
├── packaging/store/              Manifiesto e imágenes del paquete de la Store
├── assets/                       Ícono e imagen del instalador
├── docs/                         Documentación para usuarios
└── .github/workflows/            CI (pruebas) y Release (instalador + actualizaciones)
```

### Principios

- **Capas.** `Core` no sabe nada de ventanas ni de Windows. `App` depende de `Core`, nunca al revés.
- **Un solo dueño del estado.** Todo cambio pasa por `FolderStore`, que valida, guarda y avisa con `Changed`. Las ventanas solo escuchan y se redibujan.
- **Errores con dos categorías.**
  - `NovaFolderException` y sus derivadas (`ValidacionException`, `CarpetaNoEncontradaException`, `ConfiguracionException`) son errores esperables, con un mensaje pensado para el usuario. La interfaz los muestra en la barra de avisos.
  - Cualquier otra excepción es un fallo del programa. La recogen los manejadores globales (`Program`, `App.AlFallarInterfaz`), que la registran en `logs/` y la app sigue funcionando.
- **La interfaz nunca espera al shell.** Íconos, accesos del Escritorio y COM corren en un hilo STA de fondo (`StaWorker`).

### API de carpetas (`FolderStore`)

| | Carpetas | Elementos |
|---|---|---|
| **Crear** | `CrearCarpeta(nombre?, rutas?)`, `CrearCarpetaMoviendo` | `AgregarElementos(carpeta, rutas, indice?)` → `ResultadoAgregar` |
| **Leer** | `Folders`, `Buscar(nombre)`, `Obtener(nombre)` | `carpeta.Apps` |
| **Actualizar** | `RenombrarCarpeta` | `MoverElemento`, `ReordenarElemento` |
| **Eliminar** | `EliminarCarpeta` → índice, `RestaurarCarpeta` (deshacer) | `QuitarElemento`, `QuitarElementos`, `SacarAlEscritorio` |

### Seguridad

- Sin permisos de administrador (`asInvoker`). Solo escribe en el perfil del usuario.
- La configuración se valida al cargarla: nombres válidos para Windows, sin duplicados, y límites de tamaño (5 MB), de carpetas (100) y de elementos (500 por carpeta). Un JSON corrupto se aparta como `.roto` y la app arranca igual.
- El canal entre instancias (`SingleInstanceService`) usa un named pipe con `CurrentUserOnly` y limita el tamaño de los mensajes.
- Al arrastrar un elemento fuera de la app, nunca se mueve el `.exe` de un programa ni una carpeta que no esté en el Escritorio (solo se crea un acceso directo); los documentos se copian.
- Solo se borran o mueven archivos que NovaFolder creó: sus `.lnk` del Escritorio (identificados por `--folder`), su caché de íconos y su almacén de accesos.
- Las actualizaciones no llevan ningún token en el ejecutable. Velopack verifica el hash de cada paquete descargado.

### Datos del usuario

`%APPDATA%\NovaFolder\`:

| Ruta | Contenido |
|---|---|
| `folders.json` | Configuración. Se puede editar a mano y se recarga al guardar |
| `accesos\` | Accesos directos sacados del Escritorio (opción «Quitar del Escritorio al agregar») |
| `iconos\` | Íconos generados para las carpetas del Escritorio |
| `logs\` | Registro diario; se guardan 14 días |

---

## Desarrollo

Requisitos: **.NET SDK 10** y Windows 10/11.

```powershell
dotnet build NovaFolder.slnx                         # compilar todo
dotnet test --solution NovaFolder.slnx               # pruebas
dotnet run --project src/NovaFolder.App              # ejecutar
```

La compilación trata los avisos como errores (`Directory.Build.props`). Las versiones de los paquetes están en `Directory.Packages.props`.

Una copia ejecutada con `dotnet run` no se actualiza sola. Solo lo hace la instalada con el Setup.

## Publicar una versión

1. Anota los cambios en `CHANGELOG.md` y sube `<Version>` en `Directory.Build.props` (SemVer).
2. Haz commit y crea el tag con el mismo número:
   ```powershell
   git tag v1.1.0
   git push origin main v1.1.0
   ```
3. El workflow **Release** ejecuta las pruebas, genera el instalador y lo publica en GitHub Releases. Las copias instaladas se actualizan solas.

Para generar el instalador en local, sin publicar: `.\scripts\publicar.ps1` (el resultado queda en `artifacts\releases\`).

> ⚠️ Las actualizaciones automáticas descargan desde GitHub Releases, así que **el repositorio (o uno solo para releases) tiene que ser público**. La URL está en `UpdateService.RepositorioGitHub`.
