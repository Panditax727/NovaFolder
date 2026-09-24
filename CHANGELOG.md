# Cambios

Todas las versiones de NovaFolder. El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/)
y las versiones, [SemVer](https://semver.org/lang/es/): `MAYOR.MENOR.PARCHE`.

## [Sin publicar]

## [1.0.0] - 2026-09-24

Primera versión instalable.

### Añadido
- Instalador de un clic (`NovaFolder-win-Setup.exe`), sin permisos de administrador y sin tener que instalar .NET.
- Actualizaciones automáticas desde GitHub Releases.
- Gestionar carpetas sin editar archivos: crear, renombrar, eliminar (con **Deshacer**), agregar, quitar, mover y reordenar elementos.
- Arrastrar y soltar: sobre las carpetas del widget, sobre sus íconos en el Escritorio y dentro de una carpeta abierta.
- Buscador en carpetas con 9 o más elementos.
- Opción «Quitar del Escritorio al agregar» para dejar el Escritorio limpio.
- Registro de errores en `%APPDATA%\NovaFolder\logs`.
- Ícono propio de la aplicación.

### Cambiado
- Una sola instancia: abrir una carpeta desde el Escritorio es inmediato.
- Los íconos se cargan en segundo plano; la interfaz no se congela.
- `folders.json` pasa al formato v2. El formato anterior se migra solo.

### Corregido
- El popup de una carpeta podía quedar detrás de otras ventanas.
- Los accesos directos de carpetas eliminadas o renombradas quedaban en el Escritorio.
