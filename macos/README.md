# `macos/` — Integración con el menú contextual de Finder (andamiaje)

## Estado: **no funcional todavía — falta una app de macOS**

UltraArchive es hoy una aplicación **WPF**, y WPF solo se ejecuta en Windows. No existe:

- un binario de macOS de UltraArchive,
- un proyecto `.app` / bundle,
- una configuración de Xcode / firma / notarización.

Sin un ejecutable de macOS **no se puede** implementar una integración real con Finder: tanto una
**Finder Sync Extension** como un **Servicio / Quick Action de Automator** necesitan una aplicación
anfitriona instalada que reciba la selección y ejecute la operación.

Por eso, y siguiendo la instrucción de *"no improvises ni rompas el programa; detente y explica la
limitación"*, aquí **no hay una implementación a medias**. Lo que hay es el andamiaje que quedará
operativo **en cuanto exista** una compilación de macOS de UltraArchive con la misma CLI que la de
Windows (`--compress`, `--compress-here`, `--compress-zip`, `--compress-7z`, `--compress-split`,
`--extract-here`, `--extract-here-flat`, `--extract-to`).

## Qué haría falta para completarlo

1. **Portar la interfaz** fuera de WPF. Opciones realistas manteniendo el núcleo actual intacto
   (`UltraArchive.Core`, `.Archives`, `.Iso`, `.Security` ya son multiplataforma y no dependen de
   Windows):
   - **Avalonia UI** — la más parecida a WPF (XAML, MVVM); el `MainViewModel`/`CompressViewModel`
     se reaprovechan casi tal cual.
   - **.NET MAUI** (mac Catalyst).
2. **Empaquetar como `UltraArchive.app`** y respetar la misma CLI de arranque
   (`UltraArchive.Shell/ShellCommandLine.cs` ya es multiplataforma: su parser no usa nada de
   Windows).
3. **Elegir el mecanismo de Finder**:
   - **Quick Actions / Servicios** (lo más simple, sin extensión con estado): los `.workflow` de
     `quick-actions/` de esta carpeta llaman a `UltraArchive.app` con la selección. `install.sh`
     los copia a `~/Library/Services`.
   - **Finder Sync Extension** (submenú "Ultra Archive" anidado, como en Windows): requiere un
     target de extensión en Xcode, no se puede generar solo con scripts.

## Contenido de esta carpeta

| Ruta | Para qué sirve hoy |
|------|--------------------|
| `bin/ultra-archive-shell` | Puente: recibe las rutas de Finder y llama a `UltraArchive.app` con la CLI correcta. Detecta si la selección es comprimible o un archivo. |
| `quick-actions/*.sh` | Un script por acción del menú (Comprimir…, Comprimir aquí, Comprimir ZIP/7Z, Comprimir y dividir…, Extraer aquí, Extraer ficheros…). Cada uno solo invoca a `ultra-archive-shell` con un verbo. |
| `quick-actions/HOW-TO.md` | Cómo convertir cada `.sh` en un Quick Action de Finder con Automator (2 min, sin Xcode). |
| `install.sh` | Copia `bin/` y los scripts a `~/Library/Application Support/UltraArchive/` y deja los Quick Actions en `~/Library/Services`. |
| `uninstall.sh` | Revierte lo anterior y refresca Finder (`/System/Library/CoreServices/pbs -flush`). |

Todo asume `UltraArchive.app` en `/Applications`. **Nada de esto se ha podido probar** (no hay app de
macOS); es una especificación ejecutable lista para cuando exista.
