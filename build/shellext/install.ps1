#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Empaqueta la extensión de shell en un MSIX firmado y lo registra en Windows.

.DESCRIPTION
  1. Ensambla un staging dir: AppxManifest.xml + Assets + UltraArchive.ShellExtension.dll + stub.
  2. makeappx pack -> build/shellext/out/UltraArchive.ShellExtension.msix
  3. signtool sign con el certificado 'CN=UltraArchive' de Cert:\CurrentUser\My (por thumbprint).
  4. Quita cualquier registro previo y hace Add-AppxPackage.
  5. Reinicia el Explorador para que cargue el manejador.

  Requiere: build.ps1 y create-certificate.ps1 ya ejecutados.

.EXAMPLE
  pwsh build/shellext/install.ps1
#>
[CmdletBinding()]
param(
    [string]$Subject = 'CN=UltraArchive',
    [string]$PackageName = 'UltraArchive.ShellExtension'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$outDir = Join-Path $repo 'build/shellext/out'
$manifestDir = Join-Path $repo 'shellext/manifest'
$stagingDir = Join-Path $repo 'build/shellext/staging'
$msixPath = Join-Path $outDir 'UltraArchive.ShellExtension.msix'

# --- Falso positivo de Windows Defender (Trojan:Win64/Aotera.*!MTB) ---
# La DLL compilada con .NET Native AOT dispara un falso positivo heurístico muy conocido de
# Defender ("Aotera" = AOT). Con la protección en tiempo real activa, Defender bloquea makeappx y
# signtool al leer la DLL. Se añaden exclusiones para la carpeta de compilación (necesaria para
# empaquetar) y para el propio detección. Reversible con uninstall.ps1 / Remove-MpPreference.
try {
    Add-MpPreference -ExclusionPath (Join-Path $repo 'build/shellext') -ErrorAction Stop
    Add-MpPreference -ThreatIDDefaultAction_Ids 2147969239 -ThreatIDDefaultAction_Actions Allow -ErrorAction SilentlyContinue
    Write-Host "Exclusión de Defender añadida para build/shellext (falso positivo AOT)." -ForegroundColor Yellow
} catch {
    Write-Warning "No se pudo añadir la exclusión de Defender: $($_.Exception.Message)"
    Write-Warning "Si makeappx/signtool fallan con 0x800700e1, añade manualmente: Add-MpPreference -ExclusionPath '$repo\build\shellext'"
}

$dll = Join-Path $outDir 'UltraArchive.ShellExtension.dll'
$stub = Join-Path $outDir 'UltraArchive.ShellExtensionStub.exe'
foreach ($f in @($dll, $stub)) {
    if (-not (Test-Path $f)) { throw "$f no existe. Ejecuta build/shellext/build.ps1 primero." }
}

$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Subject } | Select-Object -First 1
if (-not $cert) { throw "Certificado '$Subject' no encontrado. Ejecuta build/shellext/create-certificate.ps1 primero." }

# --- Herramientas del Windows SDK ---
$kits = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' -Name KitsRoot10).KitsRoot10
$sdkBin = Get-ChildItem (Join-Path $kits 'bin') -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName 'x64\makeappx.exe') } |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $sdkBin) { throw "No se encontró makeappx.exe en el Windows SDK." }
$makeappx = Join-Path $sdkBin.FullName 'x64\makeappx.exe'
$signtool = Join-Path $sdkBin.FullName 'x64\signtool.exe'
Write-Host "SDK: $($sdkBin.FullName)"

# --- Staging ---
if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
$stagingAssets = Join-Path $stagingDir 'Assets'
New-Item -ItemType Directory -Path $stagingAssets -Force | Out-Null
Copy-Item (Join-Path $manifestDir 'AppxManifest.xml') (Join-Path $stagingDir 'AppxManifest.xml') -Force
Get-ChildItem (Join-Path $manifestDir 'Assets') -File |
    ForEach-Object { Copy-Item $_.FullName (Join-Path $stagingAssets $_.Name) -Force }
Copy-Item $dll (Join-Path $stagingDir 'UltraArchive.ShellExtension.dll') -Force
Copy-Item $stub (Join-Path $stagingDir 'UltraArchive.ShellExtensionStub.exe') -Force

# --- Empaquetar ---
if (Test-Path $msixPath) { Remove-Item $msixPath -Force }
Write-Host "Empaquetando MSIX..."
& $makeappx pack /o /d $stagingDir /nv /p $msixPath
if ($LASTEXITCODE -ne 0) { throw "makeappx falló ($LASTEXITCODE)" }

# --- Firmar ---
Write-Host "Firmando con $($cert.Thumbprint)..."
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $msixPath
if ($LASTEXITCODE -ne 0) { throw "signtool falló ($LASTEXITCODE)" }

# --- Registrar ---
$existing = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Quitando registro previo..."
    Remove-AppxPackage -Package $existing.PackageFullName
    Start-Sleep -Seconds 1
}
Write-Host "Registrando el paquete..."
Add-AppxPackage -Path $msixPath

# Excluir la ubicación instalada del paquete (mismo falso positivo AOT sobre la DLL en WindowsApps).
$installed = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
if ($installed -and $installed.InstallLocation) {
    try {
        Add-MpPreference -ExclusionPath $installed.InstallLocation -ErrorAction Stop
        Write-Host "Exclusión de Defender añadida para $($installed.InstallLocation)." -ForegroundColor Yellow
    } catch {
        Write-Warning "No se pudo excluir $($installed.InstallLocation): $($_.Exception.Message)"
    }
}

Write-Host "Reiniciando el Explorador..."
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) { Start-Process explorer }

Write-Host ""
Write-Host "== Instalado ==" -ForegroundColor Green
Write-Host "  Clic derecho sobre un archivo o carpeta -> 'Ultra Archive' en el menú principal."
Write-Host "  Quitar: pwsh build/shellext/uninstall.ps1"
