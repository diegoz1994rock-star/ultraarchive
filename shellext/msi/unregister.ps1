<#
  Lo ejecuta el MSI (custom action diferida, como SYSTEM) al DESINSTALAR.
  Retira la extension de shell del menu contextual moderno de Windows 11 sin dejar residuos.
  Tolerante a fallos (Return="ignore").
#>
param([string]$ShellExtDir)

$ErrorActionPreference = 'Continue'
$log = { param($m) Write-Output ("[UltraArchive shellext] " + $m) }
$PackageName = 'UltraArchive.ShellExtension'
$Subject = 'CN=UltraArchive'

# 1. Quitar el aprovisionamiento.
try {
    Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -eq $PackageName } |
        ForEach-Object {
            Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue | Out-Null
            & $log ("aprovisionamiento quitado: " + $_.PackageName)
        }
} catch { & $log ("Remove-AppxProvisionedPackage: " + $_.Exception.Message) }

# 2. Quitar el registro por usuario (todos los perfiles).
try {
    Get-AppxPackage -AllUsers -Name $PackageName -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction SilentlyContinue
        & $log ("registro quitado: " + $_.PackageFullName)
    }
} catch { & $log ("Remove-AppxPackage: " + $_.Exception.Message) }

# 3. Quitar el certificado de firma de los almacenes de maquina.
foreach ($store in @('Root', 'TrustedPeople')) {
    try {
        $s = [System.Security.Cryptography.X509Certificates.X509Store]::new($store, 'LocalMachine')
        $s.Open('ReadWrite')
        foreach ($c in @($s.Certificates)) { if ($c.Subject -eq $Subject) { $s.Remove($c) } }
        $s.Close()
        & $log ("certificado retirado de LocalMachine\" + $store)
    } catch { & $log ("no se pudo limpiar LocalMachine\" + $store + ": " + $_.Exception.Message) }
}

# 4. Quitar las exclusiones de Defender que anadio register.ps1.
try {
    Get-ChildItem 'C:\Program Files\WindowsApps' -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'UltraArchive.ShellExtension_*' } |
        ForEach-Object { Remove-MpPreference -ExclusionPath $_.FullName -ErrorAction SilentlyContinue }
    Remove-MpPreference -ThreatIDDefaultAction_Ids 2147969239 -ErrorAction SilentlyContinue
    & $log "exclusiones de Defender retiradas"
} catch { & $log ("Remove-MpPreference: " + $_.Exception.Message) }

Get-Process -Name explorer -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
& $log "hecho."
exit 0
