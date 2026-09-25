# Instalar NovaFolder

NovaFolder ordena tu Escritorio: agrupa accesos directos, juegos y archivos en carpetas que se abren con un clic, como en el móvil.

## Requisitos

- Windows 10 (1809 o posterior) o Windows 11, de 64 bits.
- No necesitas instalar nada más: el instalador ya trae todo lo necesario.
- No hacen falta permisos de administrador.

## Instalación recomendada: Microsoft Store

Busca **NovaFolder** en la Microsoft Store, o abre [su ficha](https://apps.microsoft.com/detail/9P3KG5PCB5HD), y pulsa **Obtener**. Sin avisos de seguridad, y la Store lo mantiene actualizado.

## Instalación desde GitHub (alternativa)

1. Descarga **`NovaFolder-win-Setup.exe`** desde la página de [versiones](https://github.com/Panditax727/NovaFolder/releases/latest).
2. Haz doble clic en el archivo descargado.
3. Si Windows muestra **«Windows protegió su PC»**, pulsa **Más información → Ejecutar de todas formas**.
   Aparece porque este instalador no está firmado. La versión de la Microsoft Store sí lo está y no muestra el aviso.
4. En unos segundos se abre la **ventana de NovaFolder** con una guía de 4 pasos. Al final eliges si quieres limpiar el Escritorio y abrir NovaFolder al encender el equipo.

Después lo encuentras en el **menú Inicio** como cualquier otro programa.

## La ventana de NovaFolder

| Sección | Para qué sirve |
|---|---|
| **Carpetas** | Ver y gestionar tus carpetas: crear, renombrar, eliminar, agregar, ordenar, buscar |
| **Ordenar Escritorio** | Revisa lo que está suelto en tu Escritorio, sugiere una carpeta para cada cosa y lo ordena todo con un clic |
| **Ajustes** | Limpiar el Escritorio, carpetas en el Escritorio, widget flotante, inicio con Windows y actualizaciones |
| **Ayuda** | Todos los gestos y preguntas frecuentes |

Al **cerrar la ventana**, NovaFolder sigue funcionando junto al reloj, para que las carpetas del Escritorio se abran al instante. Un clic en su ícono morado (si no lo ves, pulsa la flecha **^**) vuelve a abrir la ventana. Para cerrarlo del todo: **Ajustes → Salir de NovaFolder**.

## Primeros pasos

| Para… | Haz esto |
|---|---|
| Ordenar todo el Escritorio de una vez | **Ordenar Escritorio** → revisa las sugerencias → **Ordenar** |
| Crear una carpeta | **Carpetas** → **Nueva carpeta** |
| Meter algo en una carpeta | Arrastra su acceso directo sobre la carpeta (en la lista o en su ícono del Escritorio) |
| Abrir un juego o una app | Clic sobre él |
| Ordenar o mover | Arrastra el elemento a otra posición, o sobre otra carpeta de la lista |
| Sacar algo de una carpeta | Arrástralo al Escritorio, o clic derecho → **Devolver al Escritorio** |
| Renombrar o eliminar una carpeta | Botones junto a su nombre, o clic derecho sobre ella |
| Deshacer | Botón **Deshacer** en el aviso que aparece abajo |

Cada carpeta aparece también en tu Escritorio con una miniatura de lo que contiene: doble clic para verla sin abrir la ventana.

## Actualizaciones

NovaFolder se actualiza solo. Descarga la versión nueva en segundo plano y la instala la próxima vez que se inicia, o al momento con **Reiniciar y actualizar**.

## Desinstalar

Cualquiera de estas tres formas:

- Dentro de NovaFolder: **Ajustes → Desinstalar NovaFolder**.
- **Configuración de Windows → Aplicaciones → Aplicaciones instaladas → NovaFolder → Desinstalar.**
- Clic derecho sobre NovaFolder en el menú Inicio → **Desinstalar**.

Si lo instalaste desde la Microsoft Store, los accesos directos que NovaFolder había guardado quedan en **Documentos\NovaFolder\Accesos guardados**: puedes devolverlos al Escritorio desde ahí.

- Se quitan el inicio automático y las carpetas de NovaFolder del Escritorio.
- Los accesos directos que NovaFolder había guardado **vuelven a tu Escritorio**: no pierdes nada.
- Tu configuración queda en `%APPDATA%\NovaFolder` por si lo vuelves a instalar; puedes borrarla.

## Si algo falla

**Ajustes → Configuración y registros → Abrir carpeta**. Dentro de `logs` está el registro del día; adjúntalo si reportas un problema en [GitHub Issues](https://github.com/Panditax727/NovaFolder/issues).
