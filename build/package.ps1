<#
.SYNOPSIS
  Empaqueta la distribución portable de UltraArchive en un único .zip listo para publicar.

.DESCRIPTION
  - Ejecuta build/publish.ps1 (compila + tests + publish a publish/UltraArchive/).
  - Comprime publish/UltraArchive/ en publish/UltraArchive-<version>.zip.
  - Verifica que el zip contiene UltraArchive.exe, THIRD-PARTY-NOTICES.txt, LICENSE.txt
    y (si se incorporó) tools/7zr.exe, y que NO contiene .pdb ni ficheros temporales.
  - Imprime el SHA-256 del zip para publicarlo junto a la descarga.

.EXAMPLE
  pwsh build/package.ps1
  pwsh build/package.ps1 -SelfContained
#>
[CmdletBinding()]
param(
    [switch]$SelfContained,
    [switch]$SkipTests,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$distDir = Join-Path $repo 'publish/UltraArchive'

# 1. Publicar.
$publishScript = Join-Path $PSScriptRoot 'publish.ps1'
& $publishScript -Configuration $Configuration -SelfContained:$SelfContained -SkipTests:$SkipTests
if ($LASTEXITCODE -ne 0) { throw "publish.ps1 falló" }

# 2. Versión (de Directory.Build.props).
[xml]$props = Get-Content (Join-Path $repo 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ }) | Select-Object -First 1
if (-not $version) { $version = '1.0.0' }

$suffix = if ($SelfContained) { '-selfcontained' } else { '' }
$zipPath = Join-Path $repo "publish/UltraArchive-$version$suffix.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

# 3. Comprimir.
Write-Host "== Comprimiendo $distDir ==" -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($distDir, $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal, $false)

# 4. Verificar contenido.
$entries = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $names = $entries.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }
} finally {
    $entries.Dispose()
}

$required = @('UltraArchive.exe', 'THIRD-PARTY-NOTICES.txt', 'LICENSE.txt')
foreach ($r in $required) {
    if ($names -notcontains $r) { throw "el paquete no contiene '$r'" }
}
$junk = $names | Where-Object { $_ -like '*.pdb' -or $_ -like '*.uatmp-*' -or $_ -like '*uarsp-*' }
if ($junk) { throw "el paquete contiene ficheros no deseados:`n$($junk -join "`n")" }

$has7zr = $names -contains 'tools/7zr.exe'
$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash

Write-Host ""
Write-Host "== Paquete listo ==" -ForegroundColor Green
Write-Host "  $zipPath"
Write-Host "  Ficheros:      $($names.Count)"
Write-Host "  tools/7zr.exe: $(if ($has7zr) { 'incluido' } else { 'NO (7Z cifrado deshabilitado)' })"
Write-Host "  SHA-256:       $hash"
