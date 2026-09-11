# `installer/` — MSI de UltraArchive (WiX v5)

**No forma parte de `UltraArchive.sln`.** Se compila aparte; el SDK `WixToolset.Sdk 5.0.2` y
`WixToolset.Heat` se restauran de NuGet automáticamente al construir este `.wixproj`.

## Cómo generar el MSI oficial

```powershell
# 1. Publicar la distribución Release AUTOCONTENIDA (incluye el runtime .NET 8 + tools/7zr.exe).
pwsh build/publish.ps1 -SelfContained

# 2. Compilar el MSI a partir de publish/UltraArchive/.
dotnet build installer/UltraArchive.Installer.wixproj -c Release -p:ProductVersion=1.0.0
#    -> installer/bin/x64/Release/UltraArchive-1.0.0.msi
```

El MSI es de **64 bits** (el payload es `win-x64` autocontenido) e instala en el Program Files de
64 bits. Como es autocontenido, la PC de destino **no necesita** tener instalado ningún runtime de
.NET: basta con copiar el `.msi` y ejecutarlo.

> Si se prefiere un MSI pequeño (~10 MB) que dependa del **".NET 8 Desktop Runtime (x64)"** ya
> instalado en el destino, usar `pwsh build/publish.ps1` (sin `-SelfContained`) en el paso 1.

## Qué hace el MSI

- Instala el contenido de `publish/UltraArchive/` en `%ProgramFiles%\UltraArchive` — la app, el
  runtime .NET 8 autocontenido y `tools/7zr.exe` (para 7Z cifrado/dividido).
- Crea un acceso directo **UltraArchive** en el menú Inicio.
- Registra la entrada de **Agregar o quitar programas** con icono, `InstallLocation` y sin opción
  de "Modificar/Reparar".
- Soporta **actualización mayor** (`MajorUpgrade`): instalar una versión nueva reemplaza la anterior.
- **Registra el submenú "Ultra Archive" del menú contextual del Explorador para todos los usuarios**
  (`HKLM\Software\Classes`), de forma **declarativa** (componente `ContextMenuIntegration` en
  `Package.wxs`): submenú desplegable sobre archivos y carpetas (Comprimir… / Comprimir aquí / como
  .ZIP / como .7Z / Comprimir y dividir…), verbos de extracción sobre archivos comprimidos
  (`AppliesTo` filtra por extensión) y "Abrir con UltraArchive". Los comandos apuntan a
  `[INSTALLFOLDER]UltraArchive.exe` (resuelto en la máquina de destino, sin rutas del PC de
  desarrollo). Windows Installer **añade estas claves al instalar y las elimina íntegras al
  desinstalar** — sin lanzar procesos, con rollback correcto.
  - Es la misma estructura que crea `RegistryShellIntegrationService` para el modo por-usuario; el
    MSI la pone en `HKLM` en vez de `HKCU`.

## Qué NO hace (a propósito)

- **No** cambia la asociación **predeterminada** de ningún tipo de archivo (doble-clic). Solo añade
  "Abrir con UltraArchive" como candidato y el submenú contextual.
- Un usuario concreto que use la versión **portable** (sin MSI) puede activar el menú **solo para su
  cuenta** con `UltraArchive.exe --install-shell` (reversible con `--uninstall-shell`, sin admin).
- **No** instala servicios, tareas programadas ni componentes COM (el menú es registro puro).

## Prueba de instalación / desinstalación (verificada)

```powershell
msiexec /i installer\bin\x64\Release\UltraArchive-1.0.0.msi /qn /l*v install.log   # instala (pide UAC)
msiexec /x installer\bin\x64\Release\UltraArchive-1.0.0.msi /qn /l*v uninstall.log  # desinstala
```

Comprobado en este repo: instala en `C:\Program Files\UltraArchive` (280 ficheros, sin carpeta
duplicada), crea el acceso directo, escribe el submenú en `HKLM\Software\Classes\*\shell\UltraArchive`
y `Directory\shell\UltraArchive`, y el `.exe` instalado ejecuta los verbos del menú
(`--compress-zip` produce un ZIP válido). La desinstalación deja `Program Files`, el menú Inicio,
"Agregar o quitar programas" y **todas** las claves del menú contextual sin residuo.

## Alternativa sin instalador

`publish/UltraArchive/` es una distribución **portable** funcional: copiar la carpeta y ejecutar
`UltraArchive.exe`. `pwsh build/package.ps1 -SelfContained` la comprime en
`publish/UltraArchive-<versión>-selfcontained.zip` con su SHA-256.
