<#
.SYNOPSIS
    Genera el instalador de NovaFolder (Setup.exe) y el paquete portable.

.DESCRIPTION
    1. Ejecuta las pruebas (si fallan, no se publica nada).
    2. Compila la app en Release, autocontenida (el usuario no necesita
       instalar .NET).
    3. Empaqueta con Velopack: Setup.exe, versión portable y los paquetes
       que usan las actualizaciones automáticas.

    Resultado en artifacts\releases\:
      NovaFolder-win-Setup.exe      <- lo que se le entrega al usuario
      NovaFolder-win-Portable.zip   <- sin instalar (no se actualiza sola)
      *.nupkg, releases.win.json    <- para GitHub Releases (actualizaciones)

.PARAMETER Version
    Versión a publicar (SemVer, p. ej. 1.2.0). Por defecto, la de
    Directory.Build.props.

.PARAMETER ConservarAnteriores
    No borra artifacts\releases antes de empaquetar. Lo usa el workflow de
    Release, que descarga ahí las versiones ya publicadas para generar
    actualizaciones delta. En local no hace falta.

.EXAMPLE
    .\scripts\publicar.ps1
    .\scripts\publicar.ps1 -Version 1.1.0
#>
[CmdletBinding()]
param(
    [string]$Version,
    [switch]$ConservarAnteriores
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

if (-not $Version) {
    $Version = ([xml](Get-Content 'Directory.Build.props')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "Versión inválida '$Version'. Usa SemVer: 1.2.3 o 1.2.3-beta.1"
}

$publish = Join-Path $raiz 'artifacts\publish'
$releases = Join-Path $raiz 'artifacts\releases'
Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue
# Velopack no deja empaquetar una versión igual o menor que otra que ya esté
# en la carpeta de salida: en local se empieza siempre de cero.
if (-not $ConservarAnteriores) { Remove-Item $releases -Recurse -Force -ErrorAction SilentlyContinue }

function Paso([string]$texto) { Write-Host "`n==> $texto" -ForegroundColor Cyan }
function Ejecutar([scriptblock]$comando) {
    & $comando
    if ($LASTEXITCODE -ne 0) { throw "Falló: $comando" }
}

Paso "Restaurando herramientas"
Ejecutar { dotnet tool restore }

Paso "Pruebas"
Ejecutar { dotnet test --solution NovaFolder.slnx -c Release }

Paso "Compilando NovaFolder $Version (win-x64, autocontenida)"
Ejecutar { dotnet publish src/NovaFolder.App/NovaFolder.App.csproj -c Release -r win-x64 --self-contained -o $publish "-p:Version=$Version" }

Paso "Empaquetando instalador"
Ejecutar {
    dotnet vpk pack `
        --packId NovaFolder `
        --packVersion $Version `
        --packDir $publish `
        --mainExe NovaFolder.exe `
        --packTitle NovaFolder `
        --packAuthors Panditax727 `
        --icon assets/NovaFolder.ico `
        --splashImage assets/instalador-splash.png `
        --shortcuts StartMenuRoot `
        --outputDir $releases
}

Paso "Listo"
Get-ChildItem $releases | Format-Table Name, @{ n = 'Tamaño (MB)'; e = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize
Write-Host "Entrega este archivo: $(Join-Path $releases 'NovaFolder-win-Setup.exe')" -ForegroundColor Green
