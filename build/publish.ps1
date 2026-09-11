<#
.SYNOPSIS
  Publica UltraArchive en Release y ensambla una distribución portable en publish/UltraArchive/.

.DESCRIPTION
  - Compila en Release y ejecuta la suite de tests (aborta si algo falla).
  - Publica UltraArchive.App (framework-dependent, win-x64) — requiere el
    ".NET 8 Desktop Runtime" en el equipo de destino.
  - Con -SelfContained produce una versión que NO requiere runtime instalado (más pesada).
  - Copia tools/7zr.exe si está presente, THIRD-PARTY-NOTICES.txt y LICENSE.txt.
  - El resultado en publish/UltraArchive/ se puede comprimir y distribuir tal cual, o
    empaquetar con un instalador (WiX/MSIX) que consuma esa carpeta.

.EXAMPLE
  pwsh build/publish.ps1
  pwsh build/publish.ps1 -SelfContained -SkipTests
#>
[CmdletBinding()]
param(
    [switch]$SelfContained,
    [switch]$SkipTests,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repo 'publish/UltraArchive'
$sln = Join-Path $repo 'UltraArchive.sln'
$app = Join-Path $repo 'UltraArchive.App/UltraArchive.App.csproj'

Write-Host "== Compilando ($Configuration) ==" -ForegroundColor Cyan
dotnet build $sln -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "build falló" }

if (-not $SkipTests) {
    Write-Host "== Tests ==" -ForegroundColor Cyan
    dotnet test (Join-Path $repo 'UltraArchive.Tests') -c $Configuration --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "tests fallaron" }
}

# Limpieza tolerante: si el propio directorio está bloqueado (p. ej. abierto en el
# Explorador o en un editor) se vacía su contenido y se sigue; publish sobrescribe.
if (Test-Path $outDir) {
    try {
        Remove-Item $outDir -Recurse -Force -ErrorAction Stop
    } catch {
        Write-Host "  (no se pudo borrar $outDir; se vacía su contenido)" -ForegroundColor Yellow
        Get-ChildItem -LiteralPath $outDir -Force -ErrorAction SilentlyContinue |
            ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
    }
}
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

Write-Host "== Publicando UltraArchive.App ==" -ForegroundColor Cyan
$publishArgs = @(
    'publish', $app,
    '-c', $Configuration,
    '-r', 'win-x64',
    "-p:PublishDir=$outDir",
    "-p:SelfContained=$($SelfContained.IsPresent)",
    '-p:PublishSingleFile=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '--nologo'
)
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "publish falló" }

# El .csproj ya copia tools/7zr.exe, THIRD-PARTY-NOTICES.txt y LICENSE.txt cuando existen;
# nos aseguramos de que estén también aquí por si el publish no los arrastró.
foreach ($f in @('THIRD-PARTY-NOTICES.txt', 'LICENSE.txt')) {
    $src = Join-Path $repo $f
    if (Test-Path $src) { Copy-Item $src (Join-Path $outDir $f) -Force }
}
$sevenZr = Join-Path $repo 'tools/7zr.exe'
if (Test-Path $sevenZr) {
    New-Item -ItemType Directory -Path (Join-Path $outDir 'tools') -Force | Out-Null
    Copy-Item $sevenZr (Join-Path $outDir 'tools/7zr.exe') -Force
    Write-Host "  7zr.exe incluido" -ForegroundColor Green
} else {
    Write-Host "  AVISO: tools/7zr.exe no está presente → 7Z cifrado deshabilitado en la distribución." -ForegroundColor Yellow
}

# --- Extensión de shell (menú contextual moderno de Windows 11) ---
# El .msix firmado + el .cer los produce build/shellext/setup.ps1 (una vez, elevado; ver
# shellext/README.md). Si están presentes se incluyen en la distribución y el MSI los registra;
# si no, el MSI queda sin menú moderno (el submenú clásico HKLM sigue funcionando).
$shellExtMsix = Join-Path $repo 'build/shellext/out/UltraArchive.ShellExtension.msix'
$shellExtCer = Join-Path $repo 'build/shellext/out/UltraArchive.ShellExtension.cer'
if ((Test-Path $shellExtMsix) -and (Test-Path $shellExtCer)) {
    $shellExtDir = Join-Path $outDir 'shellext'
    New-Item -ItemType Directory -Path $shellExtDir -Force | Out-Null
    Copy-Item $shellExtMsix (Join-Path $shellExtDir 'UltraArchive.ShellExtension.msix') -Force
    Copy-Item $shellExtCer (Join-Path $shellExtDir 'UltraArchive.ShellExtension.cer') -Force
    Copy-Item (Join-Path $repo 'shellext/msi/register.ps1') (Join-Path $shellExtDir 'register.ps1') -Force
    Copy-Item (Join-Path $repo 'shellext/msi/unregister.ps1') (Join-Path $shellExtDir 'unregister.ps1') -Force
    Write-Host "  Extensión de shell (menú moderno Win11) incluida" -ForegroundColor Green
} else {
    Write-Host "  AVISO: build/shellext/out/*.msix no está presente → el MSI no registrará el menú" -ForegroundColor Yellow
    Write-Host "         contextual MODERNO de Windows 11 (el clásico sí). Ejecuta build/shellext/setup.ps1." -ForegroundColor Yellow
}

$exe = Join-Path $outDir 'UltraArchive.exe'
if (-not (Test-Path $exe)) { throw "no se generó UltraArchive.exe en $outDir" }

Write-Host ""
Write-Host "== Distribución lista ==" -ForegroundColor Green
Write-Host "  $outDir"
Write-Host "  Ejecutable: $exe"
Write-Host "  Integración con el Explorador: UltraArchive.exe --install-shell  /  --uninstall-shell"
