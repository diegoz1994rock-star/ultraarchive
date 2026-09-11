# UltraArchive

Alternativa moderna a WinRAR/7-Zip para Windows, con identidad, código e interfaz propios.
**La hoja de ruta completa, el estado por fases, las decisiones técnicas y las limitaciones legales
están en [ROADMAP.md](ROADMAP.md)** — la fuente de verdad del proyecto. Este documento cubre solo
cómo compilar y ejecutar lo ya implementado.

## Estado: Fases 1-8 completadas — listo para release (419/419 tests, build Debug y Release sin warnings)

- Fase 2: ZIP, 7Z, TAR y GZIP de extremo a extremo (crear, listar, extraer, probar; añadir/quitar solo en ZIP).
- Fase 3: lectura/exploración/extracción/integridad de **RAR** (RAR4 y RAR5, incluidos los cifrados). No se crean archivos RAR (restricción legal del formato).
- Fase 4: creación de **ZIP protegido con AES-256** (WinZip AES, vía SharpZipLib), gestión segura de contraseñas con confirmación, y endurecimiento de la extracción (nombres de Windows peligrosos, bomba de descompresión, limpieza de ficheros parciales). El resto de formatos rechaza el cifrado de forma explícita en vez de simularlo.
- Fase 5: lectura, exploración y extracción de imágenes **ISO9660** (Level 1/2, **Joliet**, **Rock Ridge**) vía `LTRData.DiscUtils.Iso9660` (MIT). Sin creación de ISO. Sin UDF (se rechaza con mensaje claro).
- Fase 6A: **7Z cifrado al crear** vía `7zr.exe` (LZMA SDK 26.02, dominio público, bundleado + verificado por SHA-256).
- Fase 6B: **integración con el Explorador de Windows** (HKCU, sin admin) + CLI; **instancia única**.
- Fase 6C: submenú contextual **"Ultra Archive"** en el Explorador — *Comprimir… / Comprimir aquí / como .ZIP / como .7Z / Comprimir y dividir…* sobre archivos y carpetas (con selección múltiple), y *Abrir / Extraer aquí / Extraer ficheros… / Extraer a subcarpeta* sobre archivos comprimidos. Reutiliza las mismas funciones de compresión/extracción; el MSI lo registra para todos los usuarios (HKLM) y lo limpia al desinstalar. macOS: andamiaje en `macos/` + explicación de por qué no es posible aún (app WPF = solo Windows).
- Fase 8: distribución portable (`build/publish.ps1`), paquete `.zip` verificado (`build/package.ps1`), instalador WiX v5 (`installer/`, se compila si el WiX Toolset está disponible).
- Extras: **diálogo interactivo de conflicto de ficheros** al extraer (Sobrescribir / Omitir / Conservar ambos / Cancelar + "aplicar a todos").

Lo que YA funciona de verdad:

- Solución `.NET 8` con arquitectura modular (`Core`, `Archives`, `Iso`, `Security`, `Interop`, `Shell`, `App`, `Tests`).
- Modelos e interfaces del dominio (`IArchiveReader`, `IArchiveWriter`, `IMutableArchiveWriter`, `IArchiveEngineFactory`, `IArchiveFormatDetector`, `IPasswordProvider`).
- Detección real de formato por firma de bytes (ZIP, 7Z, RAR, GZIP, BZip2, TAR, ISO9660), con fallback a extensión (Fase 1).
- **Motores reales de ZIP, 7Z, TAR y GZIP** (`UltraArchive.Archives`, sobre SharpCompress 0.50.4 —y **SharpZipLib 1.4.2** para el ZIP cifrado—), registrados en el composition root (`App.xaml.cs`) y resueltos por `IArchiveEngineFactory` sin que el resto de la app conozca la librería subyacente:
  - **ZIP**: lectura, extracción, comprobación de integridad (CRC32), creación (con o sin **cifrado AES-256**) y el único formato **mutable** (añadir/quitar entradas de un ZIP no cifrado sin recrearlo entrada por entrada desde la UI).
  - **7Z**: lectura/extracción/integridad y creación (LZMA2). Solo streaming: SharpCompress no permite editar un 7Z existente, así que no implementa añadir/quitar.
  - **TAR**: lectura/extracción/integridad y creación, cabeceras en formato **USTAR** explícito (necesario para que la firma "ustar" en el offset 257 sea detectable, tanto por `ArchiveFormatDetector` como por el propio `GZipArchiveReader`).
  - **GZIP**: archivo suelto (`.gz`) o TAR.GZ combinado (`.tar.gz`) cuando hay varios orígenes, igual que `tar czf`.
  - **RAR** (Fase 3, solo lectura): abrir, listar, extraer (con protección Zip Slip) y comprobar integridad (CRC32) de RAR4 y RAR5. Los RAR con datos o cabecera cifrados piden la contraseña vía `IPasswordProvider` igual que ZIP/7Z. `ArchiveEngineFactory` no registra escritor de RAR y `CreateWriter(Rar)` lo explica al usuario.
- **Motor de imágenes ISO9660** (`UltraArchive.Iso`, sobre `LTRData.DiscUtils.Iso9660` 1.0.88, MIT — módulo independiente que **no** referencia `UltraArchive.Archives`):
  - **ISO** (Fase 5, solo lectura): abrir, explorar (carpetas y ficheros con tamaño/fecha), extraer (total, selectiva, con política de colisión) y comprobar (lectura estructural: ISO9660 no lleva CRC por fichero). Soporta **ISO9660 Level 1/2**, **Joliet** (nombres Unicode, se prefiere automáticamente) y **Rock Ridge** (nombres POSIX; los permisos POSIX **no** se aplican en Windows).
  - `IsoVolumeProbe` (parser propio, sin DiscUtils) decide antes de abrir si es una ISO9660 válida, si trae Joliet y si es **UDF** — en cuyo caso se rechaza con `UnsupportedFormatException`. No soporta: creación de ISO, UDF, un único fichero > 4 GiB (multi-extent), imágenes raw 2352.
  - Se abre por el botón **"Abrir"** normal o por el botón **"ISO"** (atajo con selector filtrado a `*.iso`).
- Protección Zip Slip / path traversal (`UltraArchive.Security.PathSecurity`) aplicada de forma uniforme por los seis lectores (ZIP, 7Z, TAR, GZIP, RAR, ISO): una entrada que intenta escapar del destino, o con un nombre no válido en Windows (caracteres prohibidos, dispositivos reservados `CON`/`COM1`..., punto/espacio final), se bloquea y se reporta sin abortar el resto de la extracción.
  - `UltraArchive.Security.DecompressionGuard`: antes de extraer ZIP/7Z/RAR/TAR se comprueba el ratio y el tamaño declarados; un archivo que parece una bomba de descompresión aborta con error. (ISO9660 no comprime, así que no aplica.)
  - Limpieza de ficheros parciales: si la copia de una entrada falla o se cancela a mitad, se borra el fichero parcial en vez de dejar datos incompletos en disco (`EntryFileWriter` en Archives; `IsoEntryExtractor` en Iso — copia propia para no acoplar los módulos).
  - Contraseñas: nunca se intenta romper una contraseña desconocida; si una operación encuentra contenido cifrado sin contraseña válida, la UI la pide vía `IPasswordProvider` (diálogo WPF con `PasswordBox`, nunca un TextBox) y reintenta. Al **crear** un ZIP protegido, el diálogo "Comprimir" exige confirmar la contraseña. Las contraseñas solo viven en memoria (sesión del archivo, o mientras la ventana "Comprimir" está abierta); nunca se persisten a disco ni se registran en logs.
- Ventana principal WPF (Fluent, WPF-UI) con los 8 botones principales conectados a los motores reales: **Abrir** (detecta formato + explora contenido, incluidas imágenes ISO), **Extraer** (a carpeta elegida, con Zip Slip), **Comprimir** (ventana dedicada: orígenes, formato, nivel, opciones), **Probar** (integridad CRC32), **Añadir**/**Eliminar** (solo ZIP, por ser el único formato mutable), **Contraseña** (recordarla para la sesión), **ISO** (atajo para abrir una imagen `.iso`). RAR e ISO admiten Abrir/Extraer/Probar pero no Comprimir/Añadir/Eliminar.
- Progreso determinista (`IProgress<OperationProgress>`) con barra de progreso y porcentaje real cuando el motor lo reporta, `CancellationToken` de extremo a extremo y botón "Cancelar" visible durante cualquier operación larga, sin bloquear la interfaz (todo el trabajo pesado corre en `Task.Run`/IO asíncrono).
- **Árbol de carpetas** (panel izquierdo): al abrir un archivo se construye su jerarquía real de carpetas (`ArchiveFolderTree`, lógica pura; deduce carpetas de las rutas aunque el formato no las liste). Clic en una carpeta del árbol —o doble clic en una fila de tipo carpeta— muestra en la tabla las subcarpetas y ficheros de esa carpeta. Funciona con todos los formatos legibles. Extraer sigue extrayendo el archivo completo; Eliminar opera sobre las filas seleccionadas.
- **Comprimir → Formato** muestra ZIP / 7Z / TAR / GZIP creables y **RAR desactivado** con el motivo ("solo lectura: restricción legal del formato propietario"). No hay ni habrá escritor RAR.
- **Diálogo de conflicto de ficheros** al extraer: si el destino ya tiene un fichero con ese nombre, se pregunta (Sobrescribir / Omitir / Conservar ambos / Cancelar) con opción "aplicar a los siguientes conflictos". La decisión la centraliza `UltraArchive.Core.Services.CollisionResolver`; las políticas no interactivas (Overwrite/Skip/RenameAutomatically) siguen disponibles por código. Las entradas omitidas se reportan en `ExtractionResult.SkippedEntries`.
- **Instancia única**: lanzar UltraArchive con una segunda ventana (p. ej. "Abrir con UltraArchive" desde el Explorador teniendo la app abierta) reenvía los argumentos a la instancia en marcha (named pipe local) y la trae al frente, en vez de abrir otra ventana. Cualquier fallo → arranque normal.
- **Identidad visual propia**: tema oscuro fijo (azul marino / grafito, acentos azul-cian sobrios), tarjetas con borde y sombra suave, esquinas redondeadas, tabla y árbol con selección en píldora. Toda la capa visual vive en `UltraArchive.App/Styles/Theme.xaml` (se mezcla sobre WPF-UI); no toca ninguna lógica. Icono del `.exe`/ventanas en `UltraArchive.App/Assets/UltraArchive.ico` y logotipo junto al nombre en `Assets/UltraArchive-logo.png`.
- **7Z cifrado al crear** (Fase 6A): sin contraseña → motor gestionado (SharpCompress, LZMA2); con contraseña → `7zr.exe` (LZMA SDK 26.02, dominio público, bundleado y verificado por SHA-256 en `tools/7zr.exe`) con AES-256 y cabecera oculta (`-mhe=on`). `RoutingSevenZipWriter` publica por rename atómico solo si 7zr terminó con éxito; en fallo/cancelación no toca el destino y borra el temporal. La contraseña nunca se registra.
- **Integración con el Explorador de Windows** (Fases 6B–6C): submenú **"Ultra Archive"** en el menú
  contextual de archivos y carpetas, sin extensión COM.
  - Sobre archivos/carpetas: *Comprimir…*, *Comprimir aquí*, *Comprimir como .ZIP*, *Comprimir como
    .7Z*, *Comprimir y dividir…* — con **selección múltiple** (varios elementos → un archivo).
  - Sobre archivos comprimidos (`.zip .7z .rar .tar .gz .tgz .bz2 .iso`, filtrado con `AppliesTo`):
    *Abrir con Ultra Archive*, *Extraer ficheros…*, *Extraer aquí*, *Extraer a subcarpeta*.
  - Reutiliza la ventana "Comprimir" y las rutas de extracción existentes (sin lógica duplicada).
    RAR sigue siendo solo lectura. CLI: `UltraArchive.exe "<archivo>"`, `--extract-here[-flat]`,
    `--extract-to`, `--compress[-here|-zip|-7z|-split]` (multi-ruta), `--install-shell[-allusers]`,
    `--uninstall-shell[-allusers]`.
  - El botón "Explorador" de la app instala/quita el submenú en **`HKCU`** (sin admin); el **MSI** lo
    registra en **`HKLM`** para todos los usuarios y lo elimina limpiamente al desinstalar.
  - **macOS**: no disponible todavía (la app es WPF = solo Windows). `macos/` incluye el andamiaje
    (puente + Quick Actions) y explica qué haría falta.
- 419 tests en verde (unitarios + integración real en disco), incluido el **E2E real de 7Z cifrado y
  dividido con `7zr.exe`** y el round-trip real de integración con el Explorador: crear→listar→
  extraer→verificar byte a byte, integridad, Zip Slip con un ZIP hostil de otra herramienta, mutación
  ZIP, RAR5 con fixtures reales, crear/leer ZIP AES-256 con contraseña correcta/incorrecta/ausente,
  rechazo de cifrado donde no se soporta, nombres de Windows peligrosos, bomba de descompresión,
  limpieza de ficheros parciales, validación del diálogo "Comprimir", parser de la CLI del menú
  contextual, alta/baja del submenú en el registro (idempotente, sin tocar entradas ajenas, limpia el
  esquema antiguo), y **lectura/exploración/extracción de ISO9660 y Joliet (incl. corrupta, truncada,
  UDF rechazada, path traversal y cancelación)** con fixtures generados en el propio test.

Lo que **no** hace (a propósito): **creación de ISO**, **UDF**, división en volúmenes, **creación de RAR** (restricción legal permanente). Detalle completo en [ROADMAP.md](ROADMAP.md).

### Nota técnica para quien continúe el desarrollo

- `SharpCompress.Archives.IWritableArchive.AddEntry` (0.50.4) **no tiene** sobrecarga de ruta
  `(string key, string filePath)`: solo acepta un `Stream`. Abre con `File.OpenRead(...)` y
  `closeStream: true`.
- **SharpCompress 0.50.4 no cifra al escribir** (ningún formato): solo descifra al leer. Por eso el
  ZIP cifrado se crea con **SharpZipLib** (`ZipOutputStream` + `ZipEntry.AESKeySize = 256`) y el 7Z
  cifrado con el binario externo **`7zr.exe`** (`UltraArchive.Interop`, Fase 6A).
- SharpCompress no señala "contraseña incorrecta" de forma única: lanza
  `SharpCompress.Common.CryptographicException` **o** `InvalidFormatException("bad password")` según
  formato. El helper `UltraArchive.Archives.Common.PasswordErrors` reconoce ambos.
- Antes de crear cualquier archivo, los escritores llaman a `EncryptionSupport.Validate(options)`:
  si se pidió un cifrado no soportado, lanza y **no** se crea nada.
- **ISO (Fase 5)**: `UltraArchive.Iso` usa `LTRData.DiscUtils.Iso9660` (fork MIT activo; `CDReader`
  no cierra el `Stream`, hay que cerrarlo a mano). `CDReader(stream, joliet, hideVersions:true)`: el
  flag `joliet` lo decide `IsoVolumeProbe` (parser propio de los Volume Descriptors) — con `joliet:true`
  cuando hay SVD Joliet, con `joliet:false` en otro caso para permitir Rock Ridge. **Todo nombre que
  devuelve DiscUtils pasa por `PathSecurity` antes de escribir** (Joliet/Rock Ridge admiten nombres
  ilegales en Windows). `CDBuilder` (creación) NO se usa en producción, solo para generar fixtures de
  test. DiscUtils no lee multi-extent ni UDF.

## Requisitos

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (instalado automáticamente en este entorno vía `winget install Microsoft.DotNet.SDK.8`)

## Compilar

```powershell
dotnet build UltraArchive.sln
```

## Ejecutar

```powershell
dotnet run --project UltraArchive.App
```

## Pruebas

```powershell
dotnet test UltraArchive.Tests
```

## Estructura del repositorio

```text
UltraArchive.sln
├── UltraArchive.Core        Modelos, interfaces, excepciones, detección de formatos (sin dependencias de UI)
├── UltraArchive.Archives    Motores ZIP/7Z/TAR/GZIP (Fase 2), RAR de solo lectura (Fase 3), ZIP AES-256 al crear (Fase 4, SharpZipLib)
├── UltraArchive.Iso         Motor de imágenes ISO9660 de solo lectura (Fase 5, LTRData.DiscUtils) — módulo independiente
├── UltraArchive.Security    PathSecurity (Zip Slip + nombres de Windows), DecompressionGuard (bomba de descompresión)
├── UltraArchive.Interop     Wrappers de procesos externos (7za.exe, WinRAR opcional) (Fase 6)
├── UltraArchive.Shell       Asociaciones de archivo y menú contextual de Windows (Fase 6)
├── UltraArchive.App         Aplicación WPF (Views/ViewModels/Commands/Converters/Resources/Services)
└── UltraArchive.Tests       Pruebas unitarias e integración (xUnit): Core, Security, Archives y ViewModels
```
