<#
.SYNOPSIS
    Genera el paquete MSIX de NovaFolder para la Microsoft Store.

.DESCRIPTION
    1. Compila la app en Release, autocontenida (win-x64).
    2. Arma el contenido del paquete: la app, el manifiesto y las imágenes.
    3. Genera resources.pri (íconos para cada escala de pantalla) con makepri.
    4. Empaqueta con makeappx.

    No hace falta firmarlo: al subirlo a Partner Center, Microsoft lo firma.
    Resultado: artifacts\store\NovaFolder_<versión>.0_x64.msix

    Requiere el SDK de Windows 10/11 (viene con Visual Studio o se instala
    aparte; en GitHub Actions ya está en windows-latest).

.PARAMETER Version
    Versión (SemVer, p. ej. 1.2.0). Por defecto, la de Directory.Build.props.
    La Store exige 4 números con el último en 0: se convierte en 1.2.0.0.

.EXAMPLE
    .\scripts\empaquetar-store.ps1
#>
[CmdletBinding()]
param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

if (-not $Version) {
    $Version = ([xml](Get-Content 'Directory.Build.props')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}
if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
    throw "La Store solo admite versiones X.Y.Z sin sufijo (recibido '$Version')."
}
$versionPaquete = "$Version.0"

function Paso([string]$texto) { Write-Host "`n==> $texto" -ForegroundColor Cyan }
function Ejecutar([scriptblock]$comando) {
    & $comando
    if ($LASTEXITCODE -ne 0) { throw "Falló: $comando" }
}

# Herramientas del SDK de Windows: la versión más nueva instalada.
$sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
if (-not $sdk) { throw 'No se encontró makeappx.exe. Instala el SDK de Windows.' }
$makeappx = $sdk.FullName
$makepri = Join-Path $sdk.DirectoryName 'makepri.exe'

$salida = Join-Path $raiz 'artifacts\store'
$contenido = Join-Path $salida 'contenido'
$msix = Join-Path $salida "NovaFolder_${versionPaquete}_x64.msix"
Remove-Item $salida -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $contenido | Out-Null

Paso "Compilando NovaFolder $Version (win-x64, autocontenida)"
Ejecutar { dotnet publish src/NovaFolder.App/NovaFolder.App.csproj -c Release -r win-x64 --self-contained -o $contenido "-p:Version=$Version" }

Paso 'Manifiesto e imágenes'
(Get-Content 'packaging\store\Package.appxmanifest' -Raw).Replace('$VERSION$', $versionPaquete) |
    Set-Content (Join-Path $contenido 'AppxManifest.xml') -Encoding utf8
Copy-Item 'packaging\store\Assets' (Join-Path $contenido 'Assets') -Recurse
Get-ChildItem $contenido -Filter *.pdb -Recurse | Remove-Item

Paso 'Recursos (resources.pri)'
$config = Join-Path $salida 'priconfig.xml'
Ejecutar { & $makepri createconfig /cf $config /dq es-ES /o | Out-Null }
Ejecutar { & $makepri new /pr $contenido /cf $config /mn (Join-Path $contenido 'AppxManifest.xml') /of (Join-Path $contenido 'resources.pri') /o | Out-Null }

Paso 'Empaquetando MSIX'
Ejecutar { & $makeappx pack /d $contenido /p $msix /o | Out-Null }

Paso 'Listo'
Write-Host "Súbelo a Partner Center: $msix ($([math]::Round((Get-Item $msix).Length / 1MB, 1)) MB)" -ForegroundColor Green
