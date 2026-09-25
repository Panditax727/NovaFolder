# Cambios

Todas las versiones de NovaFolder. El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/)
y las versiones, [SemVer](https://semver.org/lang/es/): `MAYOR.MENOR.PARCHE`.

## [Sin publicar]

## [1.1.0] - sin publicar

### Añadido
- **Microsoft Store**: paquete MSIX firmado por Microsoft (sin aviso de SmartScreen), con inicio con Windows mediante tarea de inicio y envío automático desde GitHub Actions (Environment `produccion`).
- **Desinstalar desde la app**: Ajustes → Desinstalar NovaFolder.
- Seguridad del repositorio: revisión automática de secretos, Dependabot y política de reporte de vulnerabilidades.

## [1.0.0] - 2026-09-25

Primera versión instalable.

### Añadido
- **Ventana principal** de aplicación (barra de tareas, Alt+Tab, menú Inicio) con cuatro secciones: Carpetas, Ordenar Escritorio, Ajustes y Ayuda. Se abre al instalar y al lanzar NovaFolder; al cerrarla sigue en la bandeja.
- **Ordenar Escritorio**: revisa lo que está suelto (también en el Escritorio común), sugiere una carpeta para cada cosa (Juegos, Apps, Documentos, Imágenes, Carpetas, Enlaces web…) y lo ordena todo con un clic, con Deshacer.
- Opción «Carpetas en el Escritorio» para crear o quitar los accesos de carpeta del Escritorio.
- Instalador de un clic (`NovaFolder-win-Setup.exe`), sin permisos de administrador y sin tener que instalar .NET.
- Actualizaciones automáticas desde GitHub Releases.
- Gestionar carpetas sin editar archivos: crear, renombrar, eliminar (con **Deshacer**), agregar, quitar, mover y reordenar elementos.
- Arrastrar y soltar: sobre las carpetas del widget, sobre sus íconos en el Escritorio y dentro de una carpeta abierta.
- Buscador en carpetas con 9 o más elementos.
- «Limpiar el Escritorio»: lo que metes en una carpeta sale del Escritorio y vuelve si lo quitas. Viene activado en instalaciones nuevas, y se ofrece guardar los accesos que ya estaban en carpetas.
- Sacar elementos de una carpeta arrastrándolos al Escritorio o al Explorador, o con «Devolver al Escritorio».
- Bienvenida guiada de 4 pasos la primera vez; se puede volver a ver desde Ayuda.
- Avisos junto a la bandeja cuando la ventana está cerrada (por ejemplo, cuando hay una actualización).
- Registro de errores en `%APPDATA%\NovaFolder\logs`.
- Ícono propio de la aplicación.

### Cambiado
- El widget flotante pasa a ser opcional (Ajustes); apagado en instalaciones nuevas.
- Al iniciar con Windows, NovaFolder arranca discreto en la bandeja, sin abrir la ventana.
- Una sola instancia: abrir una carpeta desde el Escritorio es inmediato.
- Los íconos se cargan en segundo plano; la interfaz no se congela.
- `folders.json` pasa al formato v2. El formato anterior se migra solo.

### Corregido
- El popup de una carpeta podía quedar detrás de otras ventanas.
- Los accesos directos de carpetas eliminadas o renombradas quedaban en el Escritorio.
