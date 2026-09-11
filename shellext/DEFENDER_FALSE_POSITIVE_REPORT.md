# Reporte de falso positivo a Microsoft — `Trojan:Win64/Aotera.LVM!MTB`

`UltraArchive.ShellExtension.dll` (compilada con **.NET 8 Native AOT**) dispara la detección
heurística `Trojan:Win64/Aotera.LVM!MTB` en Microsoft Defender. "Aotera" es la familia que Defender
usa para marcar binarios producidos por **AOT** (Ahead-Of-Time compilation) — es un falso positivo
conocido y reportado por otros proyectos .NET Native AOT, no algo específico de este binario.

Este documento es el texto listo para pegar en el formulario de Microsoft. **El envío final lo tiene
que hacer el propio usuario** (Diego / ASTRIM): hace falta iniciar sesión y no hay forma de
automatizarlo desde aquí.

## Dónde enviarlo

**Microsoft Security Intelligence — Submit a file for malware analysis**
<https://www.microsoft.com/en-us/wdsi/filesubmission>

1. Selecciona **"I am a software developer/publisher who has software or files that are incorrectly
   detected as malicious, or I would like to dispute the classification of my software."**
2. Categoría: **Software developer**.
3. Adjunta el fichero: `build/shellext/out/UltraArchive.ShellExtension.dll` (la DLL compilada, NO el
   código fuente).
4. Detección reportada: `Trojan:Win64/Aotera.LVM!MTB` (o la variante exacta que muestre tu Defender
   en ese momento — puede cambiar de sufijo, ej. `!MTB`, `!ml`).

## Texto para el campo de descripción

> This file is `UltraArchive.ShellExtension.dll`, a COM in-process server that implements
> `IExplorerCommand` for a Windows 11 File Explorer context menu extension. It is part of
> UltraArchive, an open archive-manager application for Windows.
>
> The DLL is compiled with **.NET 8 Native AOT** (`PublishAot=true`, `NativeLib=Shared`,
> `RuntimeIdentifier=win-x64`) using source-generated COM interop
> (`System.Runtime.InteropServices.Marshalling`, `[GeneratedComInterface]` / `[GeneratedComClass]`).
> No native/unmanaged code, obfuscation, packing, or third-party native libraries are used — it is
> a single self-contained managed binary produced entirely by the standard .NET 8 SDK toolchain
> (`dotnet publish`).
>
> The detection appears to be a heuristic/ML false positive on the Native AOT code-generation
> pattern itself (the "Aotera" family name suggests this), not on any specific malicious behavior.
> We have observed the same detection on a from-scratch minimal Native AOT COM DLL used only as a
> reference sample, with no application logic at all, which supports this being a generic AOT
> pattern match rather than something specific to our code.
>
> Source code for this component is available at: **<indica aquí la URL de tu repositorio cuando lo
> subas a un hosting público (GitHub/GitLab/etc.); si el repo es privado, dilo así>**.
>
> We would appreciate a review and, if confirmed as a false positive, an exclusion/allow rule so
> future builds of this component are not flagged.

## Notas para rellenar

- Si el repositorio sigue siendo local (no está en GitHub/GitLab todavía), sustituye esa frase por
  algo como: *"Source code is currently maintained privately; happy to share build instructions or
  additional samples on request."* — Microsoft no exige que el repo sea público para aceptar el
  reporte, pero ayuda a que lo revisen más rápido.
- Si Defender muestra un sufijo de detección distinto a `!MTB` (por ejemplo `!ml`), usa el que
  aparezca realmente — se puede consultar con `Get-MpThreatDetection` en PowerShell (admin) o en el
  historial de protección de la app Seguridad de Windows.
- El formulario permite adjuntar **varios ficheros**: si quieres reforzar el caso, adjunta también
  `UltraArchive.ShellExtensionStub.exe` (el stub, también AOT, que normalmente NO se marca — ayuda a
  mostrar que el patrón AOT en sí no siempre dispara la detección, así que es probablemente algo
  específico de las stubs COM generadas).
- Tras enviarlo, Microsoft suele responder por correo en **1-3 días laborables** con el resultado
  (falso positivo confirmado → se corrige en la próxima actualización de definiciones; o mantienen la
  detección y explican por qué).

## Mientras se resuelve

El proyecto ya mitiga esto localmente: `build/shellext/install.ps1` y la custom action
`shellext/msi/register.ps1` del MSI añaden una exclusión de Defender (por ruta y por ThreatID) de
forma automática al instalar. Ver `shellext/README.md`.
