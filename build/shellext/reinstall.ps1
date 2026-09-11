<#
.SYNOPSIS
  Reinstala UltraArchive desde el MSI recien compilado (prueba de la Fase 2 de principio a fin).

.DESCRIPTION
  Se auto-eleva (UAC) y ejecuta, dejando logs en <repo>\_reinstall\ :
    1. Quita el paquete MSIX del menu moderno (si esta) - provisionado y por usuario.
    2. Desinstala la version actual de UltraArchive por ProductCode.
    3. Instala installer\bin\x64\Release\UltraArchive-1.0.0.msi de forma INTERACTIVA (asistente
       con marca ASTRIM). Su custom action register.ps1 confia en el certificado, aprovisiona el
       paquete y lo registra para la sesion actual.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File build\shellext\reinstall.ps1
#>
[CmdletBinding()]
param()

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$msi = Join-Path $repoRoot 'installer\bin\x64\Release\UltraArchive-1.0.0.msi'
$logDir = Join-Path $repoRoot '_reinstall'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Elevando (UAC)..." -ForegroundColor Yellow
    Start-Process powershell.exe -Verb RunAs -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`""
    )
    return
}

New-Item -ItemType Directory -Path $logDir -Force | Out-Null
Start-Transcript -Path (Join-Path $logDir 'reinstall.txt') -Force | Out-Null

try {
    Write-Host "MSI: $msi"
    if (-not (Test-Path $msi)) { throw "No existe el MSI. Reconstruyelo primero." }

    # 1. Quitar el paquete MSIX del menu moderno.
    Write-Host "== [1/3] Quitando el paquete MSIX del menu moderno ==" -ForegroundColor Cyan
    Get-AppxPackage -AllUsers -Name 'UltraArchive.ShellExtension' -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "  Remove-AppxPackage $($_.PackageFullName)"
        Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction SilentlyContinue
    }
    Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -eq 'UltraArchive.ShellExtension' } | ForEach-Object {
            Write-Host "  Remove-AppxProvisionedPackage $($_.PackageName)"
            Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue | Out-Null
        }

    # 2. Desinstalar UltraArchive actual.
    Write-Host "== [2/3] Desinstalando UltraArchive actual ==" -ForegroundColor Cyan
    $codes = @()
    foreach ($root in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
                        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')) {
        Get-ChildItem $root -ErrorAction SilentlyContinue | ForEach-Object {
            $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
            if ($p.DisplayName -eq 'UltraArchive' -and $_.PSChildName -match '^\{.+\}$') {
                $codes += $_.PSChildName
            }
        }
    }
    if ($codes.Count -eq 0) { Write-Host "  (no habia ninguna version instalada)" }
    foreach ($code in ($codes | Select-Object -Unique)) {
        Write-Host "  msiexec /x $code"
        $u = Join-Path $logDir ("uninstall-" + $code.Trim('{}') + ".log")
        Start-Process msiexec.exe -Wait -ArgumentList @('/x', $code, '/qb', '/norestart', '/l*v', "`"$u`"")
    }

    # 3. Instalar el MSI nuevo INTERACTIVO (asistente ASTRIM: pulsa Siguiente / Instalar).
    Write-Host "== [3/3] Instalando - sigue el asistente (Siguiente / Instalar) ==" -ForegroundColor Cyan
    $ilog = Join-Path $logDir 'install.log'
    $proc = Start-Process msiexec.exe -Wait -PassThru -ArgumentList @('/i', "`"$msi`"", '/norestart', '/l*v', "`"$ilog`"")
    $installExit = $proc.ExitCode

    Write-Host ""
    Write-Host "== Comprobacion ==" -ForegroundColor Green
    Write-Host "  msiexec exit: $installExit"
    $exe = 'C:\Program Files\UltraArchive\UltraArchive.exe'
    if (Test-Path $exe) {
        Write-Host ("  " + $exe + "  (" + (Get-Item $exe).LastWriteTime + ")")
    } else {
        Write-Host "  FALTA $exe"
    }
    Get-AppxPackage -Name 'UltraArchive.ShellExtension' -ErrorAction SilentlyContinue |
        Format-List Name, Version, Status, InstallLocation
    Write-Host "  Logs en: $logDir"
}
catch {
    Write-Host ("ERROR: " + $_.Exception.Message) -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
}
finally {
    Stop-Transcript | Out-Null
    Write-Host ""
    Write-Host "(Enter para cerrar.)"
    [void][System.Console]::ReadLine()
}
