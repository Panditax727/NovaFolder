# Política de firma de código

> **Estado:** solicitud a SignPath Foundation pendiente. Hasta que se apruebe, el instalador de GitHub se publica **sin firmar**. La versión de la Microsoft Store la firma Microsoft.

Una vez aprobada:

_Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/)._

## Qué se firma

- `NovaFolder-win-Setup.exe`: el instalador publicado en [GitHub Releases](https://github.com/Panditax727/NovaFolder/releases).
- `NovaFolder.exe`: el ejecutable de la aplicación.

Solo se firman archivos compilados por **GitHub Actions** (workflow `Release`) a partir del código fuente de este repositorio. Nunca se firman archivos compilados en un equipo personal.

## Roles

| Rol | Quién |
|---|---|
| Autor y committer | [Panditax727](https://github.com/Panditax727) |
| Revisor | [Panditax727](https://github.com/Panditax727) |
| Aprobador de cada firma | [Panditax727](https://github.com/Panditax727) |

Cada publicación requiere la aprobación manual del Environment `produccion` en GitHub Actions.

## Privacidad

NovaFolder no recopila ni envía datos personales. Ver la [política de privacidad](PRIVACIDAD.md).

## Verificar una descarga

Cada Release incluye `SHA256SUMS.txt` con el hash de cada archivo. En PowerShell:

```powershell
Get-FileHash .\NovaFolder-win-Setup.exe -Algorithm SHA256
```

El resultado debe coincidir con la línea de ese archivo en `SHA256SUMS.txt`.
