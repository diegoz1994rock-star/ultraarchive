# `shellext/` — Menú contextual moderno de Windows 11 (`IExplorerCommand` + MSIX)

**No forma parte de `UltraArchive.sln`** (igual que `installer/`): necesita `win-x64` y el linker
de MSVC (Native AOT).

## Qué es

El submenú **"Ultra Archive"** que aparece en el **menú principal** del clic derecho de Windows 11
(no en "Mostrar más opciones"), con las mismas opciones que el submenú clásico:

- **Compresión** (siempre): Comprimir…, Comprimir aquí, Comprimir en "…​.zip", Comprimir en
  "…​.7z", Comprimir y dividir…
- **Extracción** (solo sobre `.zip .7z .rar .tar .gz .tgz .bz2 .iso`): Abrir con Ultra Archive,
  Extraer ficheros…, Extraer aquí, Extraer en "carpeta\".

Windows 11 solo muestra en el menú principal las extensiones **COM** (`IExplorerCommand`) con
**identidad de paquete** (MSIX). Por eso el submenú clásico basado en registro
(`RegistryShellIntegrationService` / el MSI) se queda en "Mostrar más opciones". Los dos coexisten:
el clásico sigue sirviendo a Windows 10 y a "Mostrar más opciones".

## Arquitectura

| Componente | Rol |
| --- | --- |
| `UltraArchive.ShellExtension/` | DLL COM in-proc, **.NET 8 Native AOT** (sin runtime .NET en destino). Implementa `IExplorerCommand` + `IEnumExplorerCommand`. **Solo lanza `UltraArchive.exe`** con los flags CLI (`--compress`, `--extract-here`, …) que ya valida `ShellCommandLineParser`. Cero lógica de compresión/extracción propia. |
| `UltraArchive.ShellExtensionStub/` | `.exe` mínimo (`return 0;`) exigido por el AppxManifest. Nunca se ejecuta. |
| `manifest/AppxManifest.xml` | Paquete MSIX: `com:SurrogateServer` con el CLSID → la DLL; `desktop4:FileExplorerContextMenus` sobre `*` y `Directory`. |
| `manifest/dllmanifest.manifest` | Identidad MSIX embebida en la DLL (host surrogate). |

CLSID del manejador: `A99F6CA6-1436-4063-BA8A-156692A771A8` (fijo; en `AppxManifest.xml` y en
`UltraArchiveRootCommand.CLSID`).

## Instalar en este equipo

```powershell
# Todo en uno (se auto-eleva):
powershell -ExecutionPolicy Bypass -File build\shellext\setup.ps1
```

O paso a paso:

```powershell
pwsh build/shellext/build.ps1                 # compila DLL + stub  (no necesita admin)
pwsh build/shellext/create-certificate.ps1    # cert autofirmado + confianza local  (admin)
pwsh build/shellext/install.ps1               # empaqueta, firma, registra, reinicia Explorer  (admin)
```

Quitar:

```powershell
pwsh build/shellext/uninstall.ps1             # (admin)
```

## Falso positivo de Windows Defender

La DLL compilada con **.NET Native AOT** dispara un falso positivo heurístico muy conocido de
Defender: **`Trojan:Win64/Aotera.*!MTB`** ("Aotera" = AOT). Con la protección en tiempo real
activa, Defender bloquea `makeappx`/`signtool` al leer la DLL (`0x800700e1`).

`install.ps1` añade automáticamente una **exclusión de Defender** para `build/shellext/` y para la
carpeta instalada del paquete, y marca ese ThreatID como permitido. `uninstall.ps1` las retira.

Para **distribuir a terceros** esto no basta: haría falta
1. un **certificado de firma de código real** (CA pública) en vez del autofirmado, y
2. reportar el falso positivo a Microsoft (Microsoft Security Intelligence → *Submit a file*),

o reescribir la extensión en C++/WinRT (sin AOT, sin el falso positivo).

## Firma

El certificado autofirmado `CN=UltraArchive` (creado en `Cert:\CurrentUser\My`, sin `.pfx` en
disco) se copia a `LocalMachine\TrustedPeople` y `LocalMachine\Root` para que Windows confíe en el
paquete. Es válido **solo en este equipo**. `uninstall.ps1` lo elimina de los tres almacenes.

## Integración con el MSI

`build/publish.ps1` construye la extensión y deja `UltraArchive.ShellExtension.msix` +
`UltraArchive.ShellExtension.cer` en `publish/UltraArchive/shellext/`. El MSI
(`installer/Package.wxs`, componente `ModernContextMenu`) los instala en
`[INSTALLFOLDER]\shellext\` y, mediante custom actions, instala el certificado, registra/provisiona
el paquete al instalar y lo retira al desinstalar. Ver `installer/README.md`.
