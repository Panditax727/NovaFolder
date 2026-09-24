# Instalar NovaFolder

NovaFolder organiza tus accesos directos, juegos y archivos en carpetas que se abren con un clic, como en Android.

## Requisitos

- Windows 10 (1809 o posterior) o Windows 11, de 64 bits.
- No necesitas instalar nada más: el instalador ya trae todo lo necesario.
- No hacen falta permisos de administrador.

## Instalación (1 minuto)

1. Descarga **`NovaFolder-win-Setup.exe`** desde la página de [versiones](https://github.com/Panditax727/NovaFolder/releases/latest).
2. Haz doble clic en el archivo descargado.
3. Si Windows muestra **«Windows protegió su PC»**, pulsa **Más información → Ejecutar de todas formas**.
   Aparece porque el instalador aún no tiene firma digital de pago, no porque sea peligroso.
4. Espera unos segundos: NovaFolder se instala y se abre solo con una **bienvenida de 4 pasos** que explica cómo usarlo. Al final eliges si quieres limpiar el Escritorio y abrir NovaFolder al encender el equipo.

Al terminar verás:

- El **widget** de NovaFolder en la esquina superior izquierda del Escritorio (puedes moverlo arrastrando su barra superior).
- El ícono **morado** de NovaFolder en la bandeja del sistema, junto al reloj. Si no lo ves, está en la flecha **^**. Un clic en él abre el **panel de NovaFolder**: tus carpetas, los ajustes y las actualizaciones.
- **NovaFolder** en el menú Inicio.

## Primeros pasos

| Para… | Haz esto |
|---|---|
| Crear una carpeta | Pulsa **+** en el widget |
| Meter un juego o una app | Arrastra su acceso directo sobre la carpeta (en el widget o en el Escritorio) |
| Abrir una carpeta | Clic en ella |
| Abrir un juego o una app | Clic sobre él dentro de la carpeta |
| Ordenar | Arrastra los elementos dentro de la carpeta abierta |
| Mover a otra carpeta | Arrastra el elemento sobre otra carpeta del widget |
| Sacar algo de una carpeta | Arrástralo al Escritorio, o clic derecho → **Devolver al Escritorio** |
| Renombrar o eliminar | Clic derecho sobre la carpeta o el elemento |
| Dejar el Escritorio limpio | Panel de la bandeja → **Limpiar el Escritorio** |
| Ocultar el widget | **⋯** → Ocultar widget. NovaFolder sigue en la bandeja |
| Volver a ver la guía | Botón **?** del widget o del panel de la bandeja |

Cada carpeta también aparece en tu Escritorio con una miniatura de lo que contiene. Puedes soltar accesos directos directamente sobre ella.

## Actualizaciones

NovaFolder se actualiza solo. Cuando hay una versión nueva, la descarga en segundo plano y la instala la próxima vez que se inicia. Si quieres instalarla en el momento, pulsa **Reiniciar** en el aviso del widget.

Para comprobarlo a mano: panel de la bandeja → **Actualizar**.

## Desinstalar

**Configuración de Windows → Aplicaciones → Aplicaciones instaladas → NovaFolder → Desinstalar.**

Al desinstalar:

- se quitan el autoinicio y las carpetas de NovaFolder del Escritorio;
- los accesos directos que NovaFolder había guardado **vuelven a tu Escritorio**, así que no pierdes nada;
- tu configuración se conserva en `%APPDATA%\NovaFolder`, por si vuelves a instalarlo. Puedes borrar esa carpeta si no lo vas a hacer.

## Si algo falla

- Panel de la bandeja → **Datos**. Dentro de `logs` está el registro del día; adjúntalo si reportas un problema.
- Reporta problemas en [GitHub Issues](https://github.com/Panditax727/NovaFolder/issues).
