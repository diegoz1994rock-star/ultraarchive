<#
.SYNOPSIS
  Compila la extensión de shell de UltraArchive (menú contextual moderno de Windows 11).

.DESCRIPTION
  - UltraArchive.ShellExtension  -> DLL COM nativa (.NET 8 Native AOT), sin runtime .NET en destino.
  - UltraArchive.ShellExtensionStub -> ejecutable stub exigido por el AppxManifest (nunca se lanza).
  Salida: build/shellext/out/ (UltraArchive.ShellExtension.dll + UltraArchive.ShellExtensionStub.exe).

  Native AOT enlaza con el linker de MSVC: requiere Visual Studio / Build Tools con la carga
  "Desarrollo para el escritorio con C++". El ILCompiler llama a vswhere.exe, que no está en el PATH
  por defecto — este script lo añade.

.EXAMPLE
  pwsh build/shellext/build.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$outDir = Join-Path $repo 'build/shellext/out'
$dllProj = Join-Path $repo 'shellext/UltraArchive.ShellExtension/UltraArchive.ShellExtension.csproj'
$stubProj = Join-Path $repo 'shellext/UltraArchive.ShellExtensionStub/UltraArchive.ShellExtensionStub.csproj'

# vswhere.exe para que Native AOT localice el linker de MSVC.
if (-not (Get-Command vswhere.exe -ErrorAction SilentlyContinue)) {
    $installerDir = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
    if (Test-Path (Join-Path $installerDir 'vswhere.exe')) {
        $env:PATH = "$installerDir;$env:PATH"
    } else {
        Write-Warning "vswhere.exe no encontrado. Native AOT necesita Visual Studio con la carga de C++ (Desktop development with C++). La compilación puede fallar."
    }
}

# Solo se limpian los artefactos de compilación; se conservan el .cer y el .msix ya firmados
# (los produce create-certificate.ps1 / install.ps1 y los consume build/publish.ps1).
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
Get-ChildItem $outDir -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in '.dll', '.exe', '.pdb', '.lib', '.exp' } |
    Remove-Item -Force

Write-Host "== Compilando UltraArchive.ShellExtension.dll (Native AOT, $Configuration) ==" -ForegroundColor Cyan
dotnet publish $dllProj -c $Configuration -r win-x64 "-p:PublishDir=$outDir/" --nologo
if ($LASTEXITCODE -ne 0) { throw "falló la compilación de la DLL" }

Write-Host "== Compilando UltraArchive.ShellExtensionStub.exe ==" -ForegroundColor Cyan
dotnet publish $stubProj -c $Configuration -r win-x64 "-p:PublishDir=$outDir/" --nologo
if ($LASTEXITCODE -ne 0) { throw "falló la compilación del stub" }

$dll = Join-Path $outDir 'UltraArchive.ShellExtension.dll'
$stub = Join-Path $outDir 'UltraArchive.ShellExtensionStub.exe'
foreach ($f in @($dll, $stub)) {
    if (-not (Test-Path $f)) { throw "no se generó $f" }
}

# Los .pdb no van al paquete.
Get-ChildItem $outDir -Filter *.pdb | Remove-Item -Force

Write-Host ""
Write-Host "== Extensión compilada ==" -ForegroundColor Green
Write-Host "  $dll"
Write-Host "  $stub"
Write-Host "  Siguiente: pwsh build/shellext/create-certificate.ps1  (una vez, admin)"
Write-Host "             pwsh build/shellext/install.ps1              (admin)"
