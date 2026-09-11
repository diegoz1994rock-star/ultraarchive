#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Quita el paquete MSIX de la extension de shell y el certificado de desarrollo.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File build\shellext\uninstall.ps1
#>
[CmdletBinding()]
param(
    [string]$Subject = 'CN=UltraArchive',
    [string]$PackageName = 'UltraArchive.ShellExtension'
)

$ErrorActionPreference = 'Continue'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

$pkg = Get-AppxPackage -AllUsers -Name $PackageName -ErrorAction SilentlyContinue
$installLocation = if ($pkg) { @($pkg)[0].InstallLocation } else { $null }
foreach ($one in @($pkg)) {
    if ($one) {
        Write-Host ("Quitando paquete: " + $one.PackageFullName)
        Remove-AppxPackage -Package $one.PackageFullName -AllUsers -ErrorAction SilentlyContinue
    }
}
if (-not $pkg) { Write-Host ("El paquete '" + $PackageName + "' no esta registrado.") }

# Provisionado (si el MSI lo hubiera provisionado para todos los usuarios).
Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue |
    Where-Object { $_.DisplayName -eq $PackageName } | ForEach-Object {
        Write-Host "Quitando aprovisionamiento..."
        Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue | Out-Null
    }

Write-Host "Quitando el certificado de desarrollo..."
Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -eq $Subject } | Remove-Item -Force -ErrorAction SilentlyContinue
foreach ($storeName in @('TrustedPeople', 'Root')) {
    try {
        $store = [System.Security.Cryptography.X509Certificates.X509Store]::new($storeName, 'LocalMachine')
        $store.Open('ReadWrite')
        foreach ($c in @($store.Certificates)) {
            if ($c.Subject -eq $Subject) { $store.Remove($c) }
        }
        $store.Close()
    } catch {
        Write-Warning ("No se pudo limpiar LocalMachine\" + $storeName + ": " + $_.Exception.Message)
    }
}

# Quitar las exclusiones de Defender que anadio install.ps1 / register.ps1.
foreach ($ex in @((Join-Path $repo 'build\shellext'), $installLocation)) {
    if ($ex) {
        try {
            Remove-MpPreference -ExclusionPath $ex -ErrorAction Stop
            Write-Host ("Exclusion de Defender quitada: " + $ex)
        } catch {
            Write-Warning ("No se pudo quitar la exclusion " + $ex + " : " + $_.Exception.Message)
        }
    }
}
Get-ChildItem 'C:\Program Files\WindowsApps' -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'UltraArchive.ShellExtension_*' } |
    ForEach-Object { Remove-MpPreference -ExclusionPath $_.FullName -ErrorAction SilentlyContinue }
try { Remove-MpPreference -ThreatIDDefaultAction_Ids 2147969239 -ErrorAction SilentlyContinue } catch {}

Write-Host "Reiniciando el Explorador..."
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) { Start-Process explorer }

Write-Host "Hecho."
