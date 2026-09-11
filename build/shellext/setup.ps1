<#
.SYNOPSIS
  Instala el menú contextual moderno de UltraArchive en ESTE equipo, de principio a fin.

.DESCRIPTION
  Se auto-eleva (UAC) y ejecuta en orden:
    1. build.ps1              — compila la DLL COM (Native AOT) + el stub.
    2. create-certificate.ps1 — certificado autofirmado 'CN=UltraArchive' + confianza local.
    3. install.ps1            — exclusión de Defender (falso positivo AOT), empaqueta+firma el
                                MSIX, lo registra y reinicia el Explorador.

  Para quitarlo todo:  build/shellext/uninstall.ps1  (como administrador)

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File build\shellext\setup.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Elevando (UAC)..." -ForegroundColor Yellow
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`""
    )
    return
}

$here = $PSScriptRoot

# La exclusión de Defender debe ir ANTES de compilar: la DLL Native AOT dispara un falso positivo
# heurístico ("Aotera" = AOT) y Defender la pondría en cuarentena nada más generarla.
try {
    Add-MpPreference -ExclusionPath $here -ErrorAction Stop
    Add-MpPreference -ExclusionPath (Join-Path $here 'out') -ErrorAction SilentlyContinue
    Add-MpPreference -ExclusionPath (Join-Path $here 'staging') -ErrorAction SilentlyContinue
    Add-MpPreference -ThreatIDDefaultAction_Ids 2147969239 -ThreatIDDefaultAction_Actions Allow -ErrorAction SilentlyContinue
    Write-Host "Exclusión de Defender añadida para $here (falso positivo AOT conocido)." -ForegroundColor Yellow
} catch {
    Write-Warning "No se pudo añadir la exclusión de Defender: $($_.Exception.Message)"
}

Write-Host "== [1/3] Compilando la extensión ==" -ForegroundColor Cyan
& (Join-Path $here 'build.ps1')
if ($LASTEXITCODE) { throw "build.ps1 falló" }

Write-Host "== [2/3] Certificado de firma ==" -ForegroundColor Cyan
& (Join-Path $here 'create-certificate.ps1')

Write-Host "== [3/3] Empaquetar + registrar ==" -ForegroundColor Cyan
& (Join-Path $here 'install.ps1')

Write-Host ""
Write-Host "Todo listo. Clic derecho sobre un documento -> 'Ultra Archive' en el menú principal." -ForegroundColor Green
Write-Host "(Esta ventana se cerrará al pulsar Enter.)"
[void][System.Console]::ReadLine()
