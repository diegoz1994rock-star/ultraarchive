#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Crea el certificado autofirmado de desarrollo para firmar el paquete MSIX de la extensión de shell.

.DESCRIPTION
  El "sujeto" DEBE coincidir con el Publisher del AppxManifest: CN=UltraArchive.
  - Clave privada: se queda en Cert:\CurrentUser\My (nunca se escribe un .pfx a disco).
  - Parte pública: se añade a LocalMachine\TrustedPeople Y LocalMachine\Root para que Windows
    confíe en el paquete al registrarlo (el menú moderno EXIGE paquete firmado y de confianza).

  Uso propio / un solo equipo. Para distribuir a terceros hace falta un certificado de firma de
  código real (CA pública); ver shellext/README.md.

.EXAMPLE
  pwsh build/shellext/create-certificate.ps1
#>
[CmdletBinding()]
param(
    [string]$Subject = 'CN=UltraArchive'
)

$ErrorActionPreference = 'Stop'

$existing = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Subject }
if ($existing) {
    Write-Host "Ya existe un certificado '$Subject' ($($existing.Thumbprint)). Se elimina y se recrea."
    $existing | Remove-Item -Force
}

Write-Host "Creando certificado autofirmado: $Subject"
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Subject `
    -KeyUsage DigitalSignature `
    -FriendlyName 'UltraArchive Shell Extension Dev Certificate' `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

Write-Host "Thumbprint: $($cert.Thumbprint)"

$public = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($cert.RawData)
foreach ($storeName in @('TrustedPeople', 'Root')) {
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new($storeName, 'LocalMachine')
    $store.Open('ReadWrite')
    $store.Add($public)
    $store.Close()
    Write-Host "  Confianza añadida en LocalMachine\$storeName"
}

# Exporta SOLO la parte pública (para que el MSI la instale en otros equipos si hiciera falta).
$cerPath = Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path) 'build/shellext/out/UltraArchive.ShellExtension.cer'
New-Item -ItemType Directory -Path (Split-Path $cerPath) -Force | Out-Null
[System.IO.File]::WriteAllBytes($cerPath, $public.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))
Write-Host "Certificado público exportado: $cerPath"

Write-Host ""
Write-Host "Listo. Ahora: pwsh build/shellext/install.ps1 (admin)"
