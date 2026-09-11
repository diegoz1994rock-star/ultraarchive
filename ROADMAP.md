# UltraArchive — Hoja de ruta y estado del proyecto

> Documento **fuente de verdad** del proyecto. No debe depender de la memoria de ninguna conversación.
> Última actualización: 2026-09-03. **Fases 1-8 completadas.** La Fase 7 se cerró sin nuevo alcance.
> El binario oficial `7zr.exe` (LZMA SDK 26.02, x86, dominio público) ya está **incorporado y
> verificado** (`tools/7zr.exe`, SHA-256 fijado); el **E2E real de 7zr se ejecuta y pasa**.
> Estado: **419/419 tests en verde** (incluye el E2E real de 7Z cifrado), build Debug y Release
> sin warnings ni errores. **No quedan bloqueos.**
>
> Cerrados también todos los pendientes menores: **diálogo interactivo de colisión** (`Ask` con
> Sobrescribir / Omitir / Conservar ambos / Cancelar + "aplicar a todos"), **instancia única**
> (una segunda instancia reenvía sus argumentos por named pipe local y no abre otra ventana),
> **`build/package.ps1`** (empaqueta la distribución en un `.zip` verificado con su SHA-256) y
> **`installer/`** (proyecto WiX v5 listo para generar el MSI cuando el WiX Toolset esté disponible).
> La carpeta `publish/UltraArchive/` es una distribución portable funcional.

UltraArchive es una alternativa moderna a WinRAR / 7-Zip para Windows, con identidad, código e
interfaz propios. Solución **.NET 8** + **WPF** (Fluent, WPF-UI), arquitectura modular con inyección
de dependencias. El dominio (`UltraArchive.Core`) no conoce ninguna librería de compresión concreta:
los motores se resuelven por `IArchiveEngineFactory` y se registran en el composition root
(`UltraArchive.App/App.xaml.cs`).

---

## 1. Principios de diseño (invariantes del proyecto)

1. **Nunca criptografía propia.** Todo el cifrado se apoya en `System.Security.Cryptography` o en el
   cifrado nativo del formato, siempre a través de una librería reconocida y mantenida. No se
   implementa ni el contenedor de cifrado de ningún formato a mano.
2. **Nunca romper contraseñas.** Si falta la contraseña o es incorrecta, la UI la pide
   (`IPasswordProvider` → diálogo WPF con `PasswordBox`, nunca un `TextBox`) y reintenta.
3. **Las contraseñas nunca se persisten ni se registran.** Solo viven en memoria durante la sesión
   del archivo (`MainViewModel._currentPassword`) y se descartan al cerrar el archivo o la app.
   No aparecen en logs, mensajes de error, títulos de ventana ni nombres de archivo temporales.
4. **Nunca simular una capacidad de forma insegura.** Si un formato/librería no soporta cifrado de
   cierta manera, la operación **falla con un mensaje claro**; no se genera un archivo sin cifrar
   cuando el usuario pidió cifrado.
5. **Seguridad de extracción por defecto.** Ninguna entrada puede escribir fuera de la carpeta de
   destino (Zip Slip / path traversal), ni con nombres inválidos de Windows, ni agotar el disco
   (bomba de descompresión). Las entradas bloqueadas se reportan, no se ocultan.
6. **UI siempre responsive.** Todo el trabajo pesado en `Task.Run` / IO asíncrono, con
   `IProgress<OperationProgress>` y `CancellationToken` de extremo a extremo y botón "Cancelar".
7. **No se rehace lo que ya funciona.** Cada fase mantiene verdes todos los tests de las anteriores.

---

## 2. Librerías y licencias

| Librería | Versión | Licencia | Uso en el proyecto |
| --- | --- | --- | --- |
| [SharpCompress](https://github.com/adamhathcock/sharpcompress) | 0.50.4 | MIT | Lectura de ZIP/7Z/TAR/GZIP/RAR; creación de ZIP (plano) / 7Z / TAR / GZIP; edición in situ de ZIP. **No** crea archivos cifrados (limitación de la librería). |
| [SharpZipLib](https://github.com/icsharpcode/SharpZipLib) | 1.4.2 | MIT | Creación de ZIP cifrado con **WinZip AES-256** (`ZipOutputStream` + `ZipEntry.AESKeySize = 256`). Introducida en la Fase 4. |
| [LTRData.DiscUtils.Iso9660](https://github.com/LTRData/DiscUtils) | 1.0.88 | MIT | Lectura de imágenes **ISO9660** (Level 1/2), **Joliet** y **Rock Ridge**. 100% código gestionado, sin nativo. Introducida en la Fase 5. Arrastra `LTRData.DiscUtils.Core` y `LTRData.DiscUtils.Streams` (ambos 1.0.88, MIT). |
| System.IO.Hashing | 10.0.11 | MIT | CRC32 para comprobación de integridad. |
| System.Security.Cryptography | (BCL .NET 8) | MIT / .NET Library License | Primitivas criptográficas subyacentes (AES, PBKDF2, HMAC). Usadas **a través de** SharpZipLib / SharpCompress, no directamente. |
| Microsoft.Extensions.DependencyInjection | 8.0.0 | MIT | Composition root. |
| WPF-UI | 4.3.0 | MIT | Interfaz Fluent. |
| `Microsoft.Win32.Registry` | (BCL `net8.0-windows`) | MIT | Integración con el Explorador. HKCU (botón "Explorador" / `--install-shell`) o HKLM (MSI / `--install-shell-allusers`). Fases 6B–6C. |
| **`7zr.exe`** (LZMA SDK) | 26.02 (x86) | **Dominio público** | Binario externo para **crear 7Z cifrado**. **Incorporado y verificado** en `tools/7zr.exe` (SHA-256 `56B8CC9F…ACD72` fijado en `SevenZipToolReference`). Banner: `7-Zip (r) 26.02 (x86) : Igor Pavlov : Public domain : 2026-06-25`. |
| xUnit | 2.5.3 | Apache-2.0 | Tests (no se redistribuye). |

### Limitaciones legales

- **RAR:** el algoritmo de compresión RAR es propietario y su licencia (`UnRAR license`) prohíbe
  expresamente usar el código para **crear** un compresor compatible. La **descompresión** es libre:
  SharpCompress incluye un port administrado del descompresor. → UltraArchive **lee** RAR pero
  **nunca lo crea ni lo modifica**, y no lo hará mientras no exista una solución técnica y legalmente
  adecuada.
- **7-Zip completo (`7z.exe` / `7z.dll` / `7za.exe`):** LGPL + restricción unRAR + BSD. **NO se usa.**
  Para la creación de 7Z cifrado se usa **`7zr.exe` del LZMA SDK (dominio público)**, que solo maneja
  el formato 7z y **no** contiene código unRAR. Cero obligaciones de redistribución (aun así se
  acredita en `THIRD-PARTY-NOTICES.txt`). El wrapper managed `SharpSevenZip` se **descartó** por ser
  GPL-3 (incompatible con distribución cerrada).
- **ISO9660 / DiscUtils:** MIT, sin restricciones. La creación de ISO booteables/híbridas y la
  lectura UDF quedan fuera de alcance por decisión de producto, no por licencia.

---

## 3. Formatos: matriz de capacidades

Estado **real** según las librerías usadas. No refleja lo que "debería" poder hacerse, sino lo que
el código hace hoy sin inventar capacidades.

### Lectura / extracción

| Formato | Abrir / listar | Extraer | Integridad | Extraer protegido | Notas |
| --- | :-: | :-: | :-: | :-: | --- |
| ZIP | ✅ | ✅ | ✅ CRC32 | ✅ ZipCrypto y WinZip **AES-256** | SharpCompress descifra ambos al leer. |
| 7Z | ✅ | ✅ | ✅ CRC32 | ✅ **AES-256**, incluida cabecera/índice cifrado | Extracción secuencial (formato solid). |
| TAR | ✅ | ✅ | ✅ | — (el formato no cifra) | |
| GZIP | ✅ | ✅ | ✅ | — (el formato no cifra) | `.gz` suelto o `.tar.gz`. |
| RAR | ✅ | ✅ (Zip Slip aplicado) | ✅ CRC32 (RAR sin cifrar); en **RAR5 cifrado** el archivo guarda un MAC con clave, no un CRC32 → la integridad se valida porque SharpCompress descifra el flujo entero sin error | ✅ datos y cabecera cifrados (`-p` / `-hp`) | RAR4 y RAR5. Solo lectura. |
| ISO | ✅ | ✅ (Zip Slip + nombres Windows aplicados) | ⚠️ estructural (ISO9660 no lleva checksum por fichero) | — (ISO9660 no cifra) | ISO9660 L1/L2, **Joliet**, **Rock Ridge** (nombres). Solo lectura. |

### ISO9660 — compatibilidad detallada (Fase 5)

| Característica | Estado | Nota |
| --- | :-: | --- |
| ISO9660 Level 1 (nombres 8.3) | ✅ | |
| ISO9660 Level 2 (nombres ≤31) | ✅ | |
| **Joliet** (nombres Unicode) | ✅ | Se prefiere automáticamente si la imagen trae un SVD Joliet (detección propia en `IsoVolumeProbe`) |
| **Rock Ridge** (nombres POSIX largos, metadatos) | ✅ lectura | Los **permisos POSIX se leen pero NUNCA se aplican** al extraer en Windows |
| **UDF** | ❌ | Motor separado. Una imagen UDF pura se rechaza con `UnsupportedFormatException` y mensaje claro. ISO híbrida UDF+ISO9660 → se lee la capa ISO9660 |
| Imagen total > 4 GB (muchos ficheros) | ✅ | Offsets de 64 bits |
| Un único fichero > 4 GiB dentro de ISO9660 (**multi-extent**) | ❌ | DiscUtils no parsea el flag "not final"; es un caso raro (para eso existe UDF) |
| ISO booteable / híbrida (El Torito, isohybrid) | ⚠️ | Se lee el sistema de archivos ISO9660 con normalidad; el catálogo de arranque y la MBR **no** se exponen como ficheros (correcto) |
| Imagen "raw" 2352 bytes/sector (`.bin` renombrado) | ❌ | Sin `CD001` en el offset esperado → `ArchiveCorruptedException` (con test) |
| **Creación de ISO** | ❌ | Fuera del alcance de la Fase 5. Podría evaluarse como fase menor futura (solo ISO de datos, no booteable) |

### Creación

| Formato | Crear | Contraseña al crear | AES-256 al crear | Cifrar nombres | Dividir en volúmenes | Añadir/Quitar in situ |
| --- | :-: | :-: | :-: | :-: | :-: | :-: |
| ZIP | ✅ (SharpZipLib) | ✅ | ✅ **WinZip AES-256** | ❌ — WinZip AES **no cifra el índice central**; limitación del formato, no se simula | ❌ (ni SharpZipLib ni SharpCompress escriben ZIP spanned) | ✅ solo si **no** está cifrado |
| 7Z sin contraseña | ✅ LZMA2 (SharpCompress); si se pide división → `7zr.exe` | — | — | — | ✅ **`7zr.exe -v<n>b`** → `.7z.001/.002…` | ❌ (7z no es editable en streaming) |
| 7Z con contraseña | ✅ vía **`7zr.exe`** *(binario verificado en `tools/7zr.exe`)* | ✅ `-p` | ✅ **AES-256** | ✅ **`-mhe=on`** (siempre, oculta la lista de nombres) | ✅ combinable con contraseña | ❌ |
| TAR | ✅ | ❌ el formato TAR no tiene cifrado | ❌ | ❌ | ❌ | ❌ |
| GZIP | ✅ | ❌ el formato GZIP no tiene cifrado | ❌ | ❌ | ❌ | ❌ |
| RAR | ❌ **nunca** (restricción legal — ver abajo) | — | — | — | ❌ | ❌ |
| ISO | ❌ fuera de alcance (Fase 5) | — | — | — | ❌ | ❌ |

**Comportamiento cuando se pide una capacidad no soportada:** el escritor lanza
`EncryptionNotSupportedException` (categoría `ArchiveErrorCategory.UnsupportedFormat`) con un mensaje
que explica la limitación y sugiere una alternativa. **Nunca** se produce un archivo sin cifrar
cuando se solicitó cifrado.

**RAR en el diálogo "Comprimir":** RAR **sí** aparece en el desplegable de formato, pero
**desactivado** (`CompressFormatOption.CanCreate == false`) con la etiqueta
*"RAR — solo lectura (no se puede crear: restricción legal del formato)"*. Se muestra desactivado en
vez de ocultarlo para que el usuario entienda por qué no puede elegirlo. No existe ni existirá un
escritor RAR: el algoritmo RAR es propietario y la licencia UnRAR prohíbe expresamente usar su código
para crear un compresor compatible; ninguna librería libre (SharpCompress incluido) trae un escritor
RAR y el wrapper GPL-3 `SharpSevenZip`/`SharpCompress`-RAR-write no existe. Defensa en profundidad:
`ValidateBeforeStart()` → `FormatNotCreatable` y, aun así, `ArchiveEngineFactory.CreateWriter(Rar)`
lanza `UnsupportedFormatException` con el motivo legal. **RAR sigue funcionando para abrir / listar /
extraer / comprobar** (RAR4 y RAR5, con y sin contraseña).

**Enrutado del 7Z (`RoutingSevenZipWriter`, Fase 6A):** sin contraseña → motor gestionado
(SharpCompress); con contraseña → `SevenZipCli` → `7zr.exe`, que escribe un `.uatmp-<guid>.7z` en la
carpeta de destino; el writer lo valida (existe, > 0 bytes, es el temporal esperado) y lo **mueve**
(rename atómico, mismo volumen) al destino final solo si 7zr terminó con éxito. En fallo / cancelación
/ watchdog / excepción, el destino final **no se toca** y el temporal se borra. La disponibilidad la
decide `ISevenZipCapability` (→ `SevenZipLocator` = `Available`).

---

## 4. Fases

### FASE 1 — Núcleo y arquitectura — ✅ COMPLETADA

**Objetivo:** cimientos del proyecto sin dependencia de UI.

- Solución modular: `Core`, `Archives`, `Iso`, `Security`, `Interop`, `Shell`, `App`, `Tests`.
- Modelos e interfaces de dominio (`IArchiveReader`, `IArchiveWriter`, `IMutableArchiveWriter`,
  `IArchiveEngineFactory`, `IArchiveFormatDetector`, `IPasswordProvider`).
- Detección de formato por **firma de bytes** (magic numbers) con fallback a extensión: ZIP, 7Z,
  RAR4/RAR5, GZIP, BZip2, TAR (ustar), ISO9660.
- Ventana principal WPF base (tema del sistema, layout, 8 botones).

### FASE 2 — Motores ZIP / 7Z / TAR / GZIP — ✅ COMPLETADA

**Objetivo:** compresión/descompresión real de extremo a extremo para los formatos abiertos.

- **ZIP:** leer, extraer, integridad CRC32, crear, y **añadir/quitar** entradas de un ZIP existente
  (único formato mutable).
- **7Z:** leer, extraer, integridad, crear (LZMA2). Sin edición in situ.
- **TAR:** leer, extraer, integridad, crear (cabeceras USTAR explícitas).
- **GZIP:** `.gz` suelto o `.tar.gz` combinado (como `tar czf`).
- Protección **Zip Slip / path traversal** (`UltraArchive.Security.PathSecurity`) aplicada
  uniformemente por los cuatro lectores (adelantada desde la Fase 4).
- Progreso determinista, `CancellationToken` de extremo a extremo, cancelación sin bloquear la UI.
- Contraseñas de **lectura** vía `IPasswordProvider` (solo memoria de sesión).
- **44 tests** en verde.

### FASE 3 — RAR de solo lectura — ✅ COMPLETADA (2026-09-02)

**Objetivo:** abrir y extraer archivos RAR sin capacidad de creación (restricción legal).

- Motor `RarArchiveReader` (SharpCompress): abrir, listar, extraer (con Zip Slip), integridad CRC32.
  RAR4 y RAR5.
- Contraseñas de RAR: datos cifrados (`-p`) y cabecera cifrada (`-hp`) → `InvalidPasswordException`
  → la UI pide la contraseña y reintenta.
- Sin escritor registrado. `ArchiveEngineFactory.CreateWriter(Rar)` explica la restricción legal.
- Corrección incluida: `ArchiveEntryMapper` tragaba mal el CRC de entradas de carpeta en RAR
  (SharpCompress lanza `ArgumentNullException`); y `MainViewModel` mostraba un mensaje de estado
  equivocado al abrir un formato que sí sabía leer.
- **RAR NO tendrá creación ni modificación** mientras no exista una solución técnica y legalmente
  adecuada. Esta decisión es permanente salvo cambio legal del formato.
- **9 tests nuevos** (fixtures RAR5 reales incrustados en Base64, generados con WinRAR). Total: **53/53**.

### FASE 4 — Seguridad y cifrado AES-256 al crear — ✅ COMPLETADA (2026-09-02)

**Objetivo:** permitir crear archivos protegidos con contraseña y cifrado fuerte donde el formato lo
soporte, y endurecer la extracción frente a entradas maliciosas — **sin simular nunca** una capacidad
que la librería no tiene.

**Terminado:**

- **Creación de ZIP con WinZip AES-256** (`ZipArchiveWriter.CreateAsync`, ahora sobre **SharpZipLib**;
  SharpCompress solo descifra al leer, no cifra al escribir). Sin contraseña, produce un ZIP Deflate
  normal con escritura en streaming y progreso por entrada.
- **`EncryptionSupport`** (nuevo, en `UltraArchive.Core`): única fuente de verdad de la matriz de
  capacidades (§3). `Validate(options)` se llama al principio de **todos** los escritores y lanza
  `EncryptionNotSupportedException` (sin tocar disco) si se pide:
  - contraseña/AES-256 en 7Z, TAR, GZIP o RAR;
  - `EncryptFileNames` en cualquier formato;
  - `ZipCrypto` al crear (solo lectura por compatibilidad);
  - estado incoherente (contraseña sin método de cifrado o viceversa).
- **Diálogo "Comprimir"**: casilla "Proteger con contraseña (AES-256)", campos de contraseña +
  **confirmación**, ambos `PasswordBox`. Solo se muestran para ZIP; para el resto de formatos aparece
  una nota explicando la limitación. La validación previa (`CompressViewModel.ValidateBeforeStart`,
  método puro y testeable) rechaza contraseña vacía o que no coincide con su confirmación.
- **Contraseñas seguras**: nunca en logs ni en texto persistido. En la ventana "Comprimir" solo viven
  en memoria del ViewModel mientras está abierta; se borran (`ClearSensitiveData`) al terminar con
  éxito y al cerrar la ventana (que además vacía los `PasswordBox`). En la ventana principal siguen
  como memoria de sesión por archivo (`MainViewModel._currentPassword`).
- **`ZipArchiveReader`**: `IsPasswordProtected` mira `entry.IsEncrypted` (el flag de archivo es poco
  fiable en ZIP AES). Nuevo helper `PasswordErrors` traduce a `InvalidPasswordException` tanto
  `SharpCompress.Common.CryptographicException` como `InvalidFormatException("bad password")`.
  Aplicado también a los lectores de 7Z y RAR para uniformar el manejo.
- **`PathSecurity` endurecido**: además del control Zip Slip, rechaza (`UnsafeEntryNameException`,
  subtipo de `PathTraversalException`, así los lectores ya lo reportan como "bloqueada") nombres con
  caracteres inválidos de Windows (`< > : " | ? *` y control chars), nombres de dispositivo
  reservados (`CON`, `PRN`, `AUX`, `NUL`, `COM1..9`, `LPT1..9`) y segmentos que terminan en punto o
  espacio.
- **`DecompressionGuard`** (nuevo, en `UltraArchive.Security`): antes de extraer, comprueba el ratio
  (descomprimido/comprimido declarados) y el tamaño total; aborta con `DecompressionBombException` si
  parece una bomba (>1000× y >256 MiB). Cableado en los lectores de ZIP, 7Z, RAR y TAR (donde los
  tamaños de cabecera son fiables).
- **`EntryFileWriter`** (nuevo helper compartido): toda extracción escribe a través de él; si la
  copia de una entrada falla o se cancela a mitad, **borra el fichero parcial** en vez de dejar datos
  incompletos en disco. Aplicado a los 5 lectores.
- **Añadir/Quitar sobre un ZIP cifrado**: bloqueado (`ZipArchiveWriter.EnsureNotEncrypted`) — reescribir
  con SharpCompress lo dejaría sin protección.

**Criptografía:** no se implementa nada propio. El cifrado ZIP lo hace SharpZipLib (WinZip AES:
AES-256 en modo CTR, clave derivada con PBKDF2/HMAC-SHA1, autenticación HMAC-SHA1), sobre las
primitivas de `System.Security.Cryptography`.

**Limitaciones conocidas de la fase (documentadas, no ocultas):**

- 7Z cifrado al crear: no disponible con SharpCompress. **Resuelto en la Fase 6A** (vía `7zr.exe`).
- Cifrado de nombres de archivo en ZIP: imposible (WinZip AES deja el índice central en claro). En 7Z
  sí, vía `7zr.exe -mhe=on` (Fase 6A).
- `DecompressionGuard` no cubría `.tar.gz`. **Resuelto en la Fase 7** (`DecompressionGuard.RatioGuard`
  incremental + comprobación del tamaño declarado por entrada TAR).
- Add/Delete de un ZIP cifrado: bloqueado (no soportado), en vez de re-cifrar.

**Tests:** 52 nuevos (ver §5). Total **105/105** en verde.

### FASE 5 — Motor de imágenes ISO9660 — ✅ COMPLETADA (2026-09-03)

**Objetivo:** leer, explorar y extraer imágenes ISO. Sin creación.

**Terminado:**

- **`UltraArchive.Iso`** (antes proyecto vacío): ahora depende de `Core`, `Security` y
  `LTRData.DiscUtils.Iso9660` 1.0.88. **Nunca lo referencia `UltraArchive.Archives`** — separación de
  módulos respetada. Helpers de extracción propios (`IsoEntryExtractor`), sin tocar `Archives`.
- **`IsoVolumeProbe`**: parser propio (independiente de DiscUtils) de los Volume Descriptors (sector
  16+). Decide si es ISO9660 válida, si trae árbol **Joliet** (→ se abre con nombres Unicode) y si
  trae la secuencia de reconocimiento **UDF** (→ se rechaza). Tolera imágenes truncadas o basura.
  Expone `IsoVolumeInfo` (etiqueta, fecha, joliet/udf) — estructura lista para mostrarse en la UI en
  una fase posterior, sin jerarquía de interfaces paralela.
- **`IsoArchiveReader : IArchiveReader`** (misma abstracción que el resto de formatos), registrado en
  el composition root como los demás. Abrir / listar (carpetas y ficheros con tamaño y fecha) /
  extraer (total, selectiva, con `CollisionPolicy`) / comprobar (lectura estructural: ISO9660 no
  lleva CRC por fichero). Traduce las excepciones de DiscUtils (`InvalidFileSystemException`,
  `IOException`, `EndOfStreamException`, `InvalidDataException`) a `ArchiveCorruptedException`, y una
  imagen UDF pura a `UnsupportedFormatException`.
- **Seguridad**: cada nombre que devuelve DiscUtils pasa por `PathSecurity.ResolveSafeDestinationPath`
  **antes** de escribir (path traversal, rutas absolutas, nombres inválidos de Windows, dispositivos
  reservados). Las entradas bloqueadas se reportan en `ExtractionResult.BlockedEntries`. Fichero
  parcial borrado si la copia falla o se cancela. `CancellationToken` de extremo a extremo. Los
  permisos POSIX de Rock Ridge **no se aplican** en Windows. No se registra información sensible.
- **UI**: la ISO se abre por el botón **"Abrir"** normal (el detector ya la reconocía) **y** por el
  botón **"ISO"**, ahora reconvertido en atajo con selector filtrado a `*.iso`
  (`IFileDialogService.ShowOpenIsoDialog`). Mensajes en español e inglés.

**Limitaciones conocidas (documentadas, no ocultas):** ver la tabla "ISO9660 — compatibilidad
detallada" en §3. En resumen: sin UDF, sin multi-extent (fichero único > 4 GiB), sin creación de ISO,
sin imágenes raw 2352.

**Tests:** 24 nuevos (6 de `IsoVolumeProbe` + 18 de motor). Total **129/129** en verde.

### FASE 6A — Interop 7zr (7Z cifrado) — ✅ COMPLETADA

**Objetivo:** crear 7Z cifrado (contenido AES-256 + cabecera) sin implementar criptografía propia ni
usar una librería de licencia incompatible.

**Terminado (5 pasos):**

1. **`SevenZipLocator`** — localiza `<app>/tools/7zr.exe` (ruta **fija**, nunca PATH), verifica
   **SHA-256** y banner/versión. Estados: `Available` / `NotFound` / `NotConfigured` / `HashMismatch`
   / `InvalidExecutable` / `IncompatibleVersion`. `ExpectedSha256` vacío ⇒ `NotConfigured` (nunca
   ejecuta el binario).
2. **`SevenZipArgumentBuilder`** — `ProcessStartInfo.ArgumentList` token a token (jamás una línea
   concatenada). Rutas de origen por **response file** (`@listfile` UTF-8, nombre `uarsp-<guid>.txt`,
   sin contraseña, `Dispose()` lo borra). `SevenZipPathGuard` rechaza rutas con prefijo `-`/`@`, o con
   CR/LF/NUL, o no absolutas (sustituto de `--`, que 7-Zip hace incompatible con `@listfile`).
   Contraseña solo en `-p<...>`; `SevenZipArgRedactor` la enmascara en cualquier log/diagnóstico.
3. **`SevenZipCli` + `SevenZipProcessRunner`** — `UseShellExecute=false`, `CreateNoWindow=true`,
   stdin cerrado, stdout/stderr drenados **concurrentemente**. Progreso por `-bsp1` (monotónico,
   0-100). **Watchdog de inactividad** (10 min sin progreso ⇒ mata; no es un timeout global).
   Cancelación ⇒ `Kill(entireProcessTree:true)`. Códigos de salida: 0→Success, 1→Warning, 2/7/8/
   desconocido→Failed, 255→Cancelled. `.uatmp-<guid>.7z` en la carpeta del destino; el final no se
   toca durante la ejecución; el response file se borra **siempre**.
4. **`RoutingSevenZipWriter` + `ISevenZipCapability` + `EncryptionSupport` dinámico** — sin contraseña
   → motor gestionado; con contraseña → `SevenZipCli` (solo si `Available`). Publicación por rename
   atómico tras validar el temporal. `CompressViewModel` muestra la opción de contraseña para 7Z
   **solo** si `ISevenZipCapability` lo permite. Registrado en `App.xaml.cs`.
5. **Cierre** — build Debug + Release en verde, docs, este ROADMAP.

**Cerrado (Fase 8):** el binario oficial `7zr.exe` (LZMA SDK 26.02, x86, dominio público) está
incorporado en `tools/7zr.exe` y su `ExpectedSha256` fijado. `SevenZipLocator` devuelve `Available`
(versión 26.2 ≥ 19.0). El **E2E real** (`SevenZipEncryptedE2ETests.Crear_Y_Extraer_SevenZipCifrado_Real`)
crea un 7Z cifrado con cabecera oculta (`-mhe=on`) vía `7zr.exe` y lo vuelve a extraer con el motor
gestionado: contraseña correcta → OK; contraseña incorrecta → fallo controlado sin filtrar la
contraseña. El resto sigue cubierto además con dobles (`FakeSevenZipProcessRunner`).

### FASE 6B — Integración con el Explorador de Windows — ✅ COMPLETADA

**Objetivo:** "Abrir con UltraArchive" y verbos de menú contextual, sin extensión nativa ni permisos
de administrador.

**Terminado:**

- **`RegistryShellIntegrationService`** — escribe **solo bajo `HKCU\Software\Classes`**: `Applications\
  UltraArchive.exe` (+ `SupportedTypes`, `FriendlyAppName`), `<.ext>\OpenWithList\UltraArchive.exe`
  para 8 extensiones (`.zip .7z .rar .tar .gz .tgz .bz2 .iso`), y verbos
  `SystemFileAssociations\<.ext>\shell\UltraArchive.ExtractHere|ExtractTo\command` (independientes del
  programa predeterminado). `Install`/`Uninstall` idempotentes; `Uninstall` borra **exactamente** lo
  que crea `Install` y **nada ajeno** (verificado con test). **Nunca** cambia la app predeterminada,
  ni HKLM, ni servicios, ni tareas, ni DLLs de Explorer, ni `IContextMenu`/`IExplorerCommand`.
- **CLI**: `UltraArchive.exe "<archivo>"` (abrir), `--extract-here "<archivo>"`, `--extract-to
  "<archivo>"`, `--install-shell`, `--uninstall-shell`. `ShellCommandLineParser` valida forma y número
  de argumentos; entradas mal formadas → arranque normal. **No** se usa `cmd.exe` ni PowerShell; los
  argumentos llegan ya separados por el SO y el parser los revalida.
- **UI**: botón "Explorador" en la barra principal (instalar/quitar con confirmación).

### FASE 6C — Menú contextual "Ultra Archive" (comprimir + extraer) — ✅ COMPLETADA

**Objetivo:** experiencia equivalente a WinRAR desde el menú contextual, reutilizando **al 100%**
las funciones de compresión/descompresión existentes y sin tocar la interfaz principal.

**Terminado:**

- **`RegistryShellIntegrationService` reescrito** — un único submenú desplegable **"Ultra Archive"**
  bajo `*\shell\UltraArchive` (archivos) y `Directory\shell\UltraArchive` (carpetas), vía
  `SubCommands=""` + subclave `\shell` anidada (fly-out nativo de Explorer, sin COM). Sub-verbos:
  - **Compresión** (siempre visibles, `MultiSelectModel="Player"` → un proceso con toda la
    selección): *Comprimir…* (`--compress`), *Comprimir aquí* (`--compress-here`), *Comprimir como
    .ZIP* (`--compress-zip`), *Comprimir como .7Z* (`--compress-7z`), *Comprimir y dividir…*
    (`--compress-split`).
  - **Extracción** (solo en el menú de archivos, con `AppliesTo` = lista de extensiones que
    UltraArchive sabe abrir → se ocultan sobre lo que no es un archivo): *Abrir con Ultra Archive*,
    *Extraer ficheros…* (`--extract-to`), *Extraer aquí* (`--extract-here-flat`, carpeta actual),
    *Extraer a subcarpeta con el nombre del archivo* (`--extract-here`).
  - `Uninstall` borra el submenú nuevo **y** los verbos planos del esquema 6B (compatibilidad al
    actualizar). Sigue sin tocar HKLM salvo cuando se invoca explícitamente para "todos los usuarios".
- **CLI nueva** (en `ShellCommandLine.cs`): verbos de compresión multi-ruta (recogen toda la
  selección del Explorador, cualquier ruta mal formada anula la operación) y
  `--install-shell-allusers` / `--uninstall-shell-allusers` (HKLM; disponibles para uso manual).
  Todo revalidado por `ShellCommandLineParser`.
- **Reutilización total**: los verbos de compresión abren `CompressWindow`/`CompressViewModel` con
  los orígenes precargados (`InitializeFromShell`). *Comprimir aquí/ZIP/7Z* calculan la ruta de
  salida junto al primer origen (evitando sobrescritura, `UniquePath`) y lanzan el **mismo**
  `StartCommand` que el botón "Comprimir"; al terminar, la ventana se cierra sola. Los verbos de
  extracción pasan por `RunStartupCommandAsync` → `OpenPathAsync` + `ExtractToPathAsync` existentes.
  **No hay una segunda ruta de compresión/extracción.**
- **Instalador**: `Package.wxs` escribe el submenú en `HKLM\Software\Classes` de forma **declarativa**
  (componente `ContextMenuIntegration`, comandos con `[INSTALLFOLDER]UltraArchive.exe`). Windows
  Installer lo añade al instalar y lo elimina al desinstalar (rollback correcto, sin lanzar
  procesos). MSI de 64 bits, autocontenido (~57 MiB), probado install/uninstall de principio a fin.
  Ver `installer/README.md`.
- **RAR**: sigue siendo solo lectura/extracción. El submenú no ofrece "crear RAR" en ningún sitio.
- **macOS**: **no implementable hoy** — la app es WPF (solo Windows); no hay binario, bundle ni
  proyecto de macOS. `macos/` contiene el andamiaje (puente `ultra-archive-shell`, scripts por
  acción, `install.sh`/`uninstall.sh` que generan Quick Actions) y un README que explica la
  limitación y qué haría falta (portar la UI a Avalonia/MAUI y empaquetar `UltraArchive.app`).

**Instancia única:** ✅ implementada (`UltraArchive.App/Services/SingleInstance.cs`). La primera
instancia toma un `Mutex` (`Local\UltraArchive.SingleInstance.Mutex`) y escucha en un named pipe
local (`UltraArchive.SingleInstance.Pipe`); una segunda instancia le reenvía sus argumentos (que
vuelven a pasar por `ShellCommandLineParser`, misma validación) y se cierra sin abrir otra ventana.
La primera trae su ventana al frente y ejecuta el verbo recibido. Cualquier fallo de mutex/pipe →
arranque normal (mejor dos ventanas que ninguna). Nada persistente.

### FASE 7 — (reserva) — ✅ CERRADA SIN NUEVO ALCANCE

La Fase 7 estaba **reservada** como desbordamiento de la Fase 6 ("interop externo avanzado: colas de
trabajo, detección de binarios, sandboxing de procesos externos"). **No se materializó ninguno de esos
supuestos**: la Fase 6 absorbió limpiamente la detección de binarios (`SevenZipLocator`) y el
endurecimiento del proceso (`SevenZipProcessRunner` sin shell, kill-tree, watchdog, validación de
argumentos), y UltraArchive es una app interactiva de una operación a la vez (no hay lote → no hay
cola). Inventar features de cola/sandbox habría violado el principio de "no inventar alcance".

**Cerrada con un paso de endurecimiento concreto** (no scope nuevo, sino cerrar un hueco documentado):

- **`DecompressionGuard.RatioGuard`** — control de bomba de descompresión **incremental** para
  `.tar.gz` y `.gz` (que no exponen el tamaño total por adelantado): se acumulan los bytes realmente
  descomprimidos y se aborta si el ratio supera 1000× una vez pasado el umbral de 256 MiB. Se añade
  también la comprobación del tamaño declarado por entrada TAR. Cableado en `GZipArchiveReader`.
- **Revisión de seguridad completa** (ver §6): sin TODO/FIXME/HACK/`NotImplementedException` en
  producción, sin `cmd`/PowerShell/`UseShellExecute`, sin búsqueda de ejecutables en PATH, sin rutas
  de desarrollo hardcodeadas, sin fugas de contraseña, `PathSecurity` en los 6 lectores.

### FASE 8 — Empaquetado y distribución — ✅ COMPLETADA

**Terminado:**

- `Directory.Build.props` con **versión 1.0.0** común a toda la solución.
- `UltraArchive.App` → `AssemblyName = UltraArchive` ⇒ el ejecutable es **`UltraArchive.exe`**.
  `app.manifest` (`asInvoker` — nunca administrador —, DPI PerMonitorV2, `longPathAware`).
  `SatelliteResourceLanguages = es;en`.
- El `.csproj` copia `tools/7zr.exe` a `<salida>/tools/7zr.exe` **cuando el fichero está presente**;
  siempre copia `THIRD-PARTY-NOTICES.txt` y `LICENSE.txt` junto al ejecutable.
- **`build/publish.ps1`** — compila + testea + `dotnet publish` (framework-dependent o
  `-SelfContained`) a **`publish/UltraArchive/`**: una distribución portable que arranca sin depender
  del PATH del desarrollador (verificado). `LICENSE.txt` (proprietario por defecto, ver nota) y
  `tools/README.md` (instrucciones exactas para incorporar `7zr.exe` + fijar el hash).

**7zr.exe — hecho:** descargado `lzma2602.7z` de <https://www.7-zip.org/a/lzma2602.7z>
(SHA-256 `2878C85F…AA9357`), extraído `bin/7zr.exe` (x86, 602 112 bytes,
SHA-256 `56B8CC9F4971CEF253644FAFE54063ED7FDCA551D4DEE0F8C6BAA81B855ACD72`), colocado en
`tools/7zr.exe`, hash fijado en `SevenZipToolReference.ExpectedSha256` y registrado en
`THIRD-PARTY-NOTICES.txt`. Banner verificado. El `.csproj` lo copia a `<salida>/tools/7zr.exe` y
`publish/UltraArchive/tools/7zr.exe` lo contiene.

- **`build/package.ps1`** — llama a `publish.ps1`, comprime `publish/UltraArchive/` en
  `publish/UltraArchive-<versión>[-selfcontained].zip`, verifica que el paquete lleva
  `UltraArchive.exe`, `THIRD-PARTY-NOTICES.txt`, `LICENSE.txt` y `tools/7zr.exe` y que **no** lleva
  `.pdb` ni temporales, e imprime el SHA-256 del zip.
- **`installer/`** — proyecto **WiX v5** (`UltraArchive.Installer.wixproj` + `Package.wxs`), fuera de
  `UltraArchive.sln`. **MSI oficial COMPILADO Y PROBADO** (WiX SDK 5.0.2 se restaura de NuGet):
  `pwsh build/publish.ps1 -SelfContained` + `dotnet build installer/UltraArchive.Installer.wixproj -c
  Release -p:ProductVersion=1.0.0` → `installer/bin/x64/Release/UltraArchive-1.0.0.msi` (~57 MiB,
  **autocontenido**: incluye el runtime .NET 8, no necesita nada preinstalado en el destino).
  - MSI de 64 bits, instala en `%ProgramFiles%\UltraArchive` (280 ficheros, `SuppressRootDirectory`
    evita la subcarpeta duplicada), acceso directo en Inicio, entrada de Agregar/Quitar programas
    (`ARPINSTALLLOCATION`, `ARPNOMODIFY`), `MajorUpgrade`.
  - **Asistente de instalación con marca ASTRIM** (`WixToolset.UI.wixext`): `WixUI_InstallDir`
    (Bienvenida → Licencia → Carpeta → *Instalar* → Progreso → Finalizado). `WixUIDialogBmp`
    (493×312, PNG) = panel izquierdo con la imagen de ASTRIM sobre banda verde en Bienvenida/
    Finalizado; `WixUIBannerBmp` (493×58) = cabecera con el logotipo + "ASTRIM" en las pantallas
    intermedias; `WixUILicenseRtf` = `installer/Assets/License.rtf`. Imágenes en `installer/Assets/`,
    generadas de `Pictures/ASTRIM/{Diego Logo Astrim,logo}.png`. Para verlo hay que instalar
    **sin `/qb`** (`build/shellext/reinstall.ps1` ya lanza el MSI interactivo).
    **En español**: `Package Language="3082"` (`Package.wxs`) + `<Cultures>es-ES</Cultures>`
    (`.wixproj`) — WixUI trae localización `es-ES` de fábrica pero hacen falta AMBAS propiedades o
    cae a inglés; verificado leyendo la tabla `Control` del MSI compilado, no solo mirando el
    asistente. `Manufacturer="ASTRIM"` (antes "UltraArchive"); sin `ARPHELPLINK` (se añadirá una URL
    real cuando exista).
  - **Registra el submenú contextual "Ultra Archive" en `HKLM\Software\Classes`** de forma
    declarativa (componente `ContextMenuIntegration`): misma estructura que
    `RegistryShellIntegrationService` pero puesta por el instalador; Windows Installer la elimina
    íntegra al desinstalar. Verificado: install → claves + ficheros + acceso directo presentes, el
    `.exe` instalado ejecuta `--compress-zip` OK; uninstall → cero residuos (ficheros, Inicio, ARP,
    y las 4 raíces del menú contextual).
  - **No** cambia la asociación predeterminada de ningún tipo. La versión portable puede activar el
    menú solo para el usuario con `--install-shell` (HKCU, sin admin).

La carpeta `publish/UltraArchive/` **es** una distribución portable funcional por sí sola.

### Explorador de carpetas del archivo (panel izquierdo)

Antes había un **placeholder** ("El árbol de carpetas… estará disponible a partir de la Fase 2…")
que nunca se sustituyó: solo existía la tabla plana. Ahora:

- **`ArchiveFolderTree`** (`UltraArchive.App/Services`, lógica pura) construye el árbol de carpetas a
  partir de la lista plana de entradas del archivo abierto — deduce carpetas tanto de las entradas de
  tipo carpeta como de la ruta padre de cada fichero (hay formatos, p. ej. ZIP de `Compress-Archive`,
  que no listan carpetas). Normaliza `\`↔`/`.
- **`ArchiveTreeNode`** — nodo de carpeta (`Name`, `FullPath`, `Children`, `IsExpanded`, `IsSelected`).
- **`MainViewModel`** expone `FolderRoots` (raíz del árbol) y recalcula `Entries` (la tabla) con las
  **filas de la carpeta seleccionada**: subcarpetas primero, luego ficheros. `SelectFolder()` (clic en
  el árbol) y `NavigateInto()` (doble clic en una fila de carpeta) navegan; el árbol expande los
  ancestros. Se reconstruye al abrir un archivo y tras Añadir/Eliminar (conservando la carpeta
  abierta si sigue existiendo).
- **UI**: `TreeView` con `HierarchicalDataTemplate` + `GridSplitter`; el `DataGrid` sigue enlazado a
  `Entries`, así que Extraer / Probar / Eliminar / selección **no cambian** (Extraer sigue extrayendo
  todo el archivo; Eliminar opera sobre las filas seleccionadas). Funciona con todos los formatos
  legibles (ZIP/7Z/TAR/GZIP/RAR/ISO). El placeholder se ha eliminado.

### Identidad visual (tema oscuro "gestor premium")

Rediseño **solo de capa visual** (ninguna lógica, comando, binding, x:Name ni handler cambió):

- **`UltraArchive.App/Styles/Theme.xaml`** — `ResourceDictionary` mezclado el último en `App.xaml`
  (sobre WPF-UI). Paleta azul marino / grafito con acentos azul-cian sobrios (`UA.Color.*` /
  `UA.Brush.*`), degradado de ventana, sombras suaves, esquinas redondeadas. Sobrescribe ~50 claves
  de brush/corner de WPF-UI (`ControlFillColorDefaultBrush`, `AccentFillColorDefaultBrush`,
  `TextFillColor*`, `SubtleFillColor*`, `ControlElevationBorderBrush`, `ControlCornerRadius`…) para
  que botones, cajas de texto, combos, checkbox y titlebar adopten la identidad sin re-templatizar.
  Plantillas propias para lo que WPF-UI no cubre bien: `DataGrid` + cabecera/fila/celda (selección en
  píldora azul, hover sutil, sin líneas), `TreeView`/`TreeViewItem` (chevron, píldora de selección),
  `ProgressBar` (degradado azul→cian), `ScrollBar` fina, `GridSplitter`. Estilos `UA.Card`,
  `UA.Toolbar`, `UA.StatusBar`, `UA.SectionHeader`, `UA.BrandMark`, `UA.StatusChip`.
- **`App.xaml`** fija `Theme="Dark"`; **`App.xaml.cs`** aplica `ApplicationTheme.Dark` (identidad
  propia, no sigue el tema del SO). El botón "tema claro/oscuro" y su comando siguen existiendo.
- **Ventanas** (`MainWindow`, `CompressWindow`, `PasswordDialog`, `CollisionDialog`): fondo oscuro
  explícito, barra de herramientas como tarjeta con la marca **UltraArchive** (logotipo + "Ultra" +
  "Archive" en cian), paneles como tarjetas con borde y sombra, barra de estado como chip.
  Verificadas por carga headless (parse + measure + arrange sin errores) además de a ojo.
- **Icono / logotipo** — `UltraArchive.App/Assets/UltraArchive.ico` (7 tamaños 16–256, generado desde
  la imagen de marca) fijado como `<ApplicationIcon>` (icono del `.exe`, barra de tareas, Alt-Tab) y
  como `Window.Icon` de las 4 ventanas. `Assets/UltraArchive-logo.png` (256×256) se muestra junto al
  texto "UltraArchive" en la barra de herramientas. La integración con el Explorador ya usaba
  `"<exe>",0`, así que los verbos del menú contextual toman el icono automáticamente.

### Compresión dividida en volúmenes (7Z)

Añadido a "Comprimir" sin tocar el comportamiento normal (con "No dividir" el flujo es idéntico).

- **Backend: 7Z vía `7zr.exe -v<n>b`** (switch nativo de 7-Zip). Es el **único** formato que soporta
  volúmenes con las librerías actuales: SharpZipLib y SharpCompress **no** escriben ZIP/TAR/GZIP
  spanned. `SplitSupport.SupportsSplitOnCreate` = solo `SevenZip`; el resto → `SplitNotSupportedException`.
  RAR sin cambios.
- **Enrutado:** `RoutingSevenZipWriter` pasa por `7zr.exe` cuando hay contraseña **o** división
  (antes solo con contraseña); sin ninguna de las dos, motor gestionado como siempre. `SevenZipRequest`
  admite `VolumeSizeBytes`; `SevenZipArgumentBuilder` añade `-v<n>b` (y `-p`/`-mhe=on` solo si hay
  contraseña). `SevenZipCli` valida la primera parte (`.uatmp-*.7z.001`) y limpia todo el conjunto
  (incluidos los `.NNN.tmp` de 7-Zip) en fallo/cancelación, con reintentos.
- **Publicación atómica:** el destino no se toca hasta validar TODAS las partes temporales
  (secuencia 1..N, no vacías); entonces se mueven a `<destino>.001`, `.002`… (nomenclatura estándar).
- **Detección + extracción:** `ArchiveFormatDetector` reconoce `X.7z.NNN`. `SevenZipArchiveReader`,
  al recibir cualquier parte, reúne el conjunto de la carpeta con `VolumeSetInspector`, exige que esté
  **completo** (si falta una parte → `IncompleteVolumeSetException` con lista de encontradas/faltantes;
  **nunca** descomprime a medias) y lo presenta como un stream contiguo (`MultiVolumeReadStream`,
  read-only seekable, sin copiar a disco ni RAM) que se pasa al lector 7Z existente. Toda la lógica de
  extracción / colisiones / seguridad / progreso se reutiliza tal cual.
- **Verificación:** tras crear un 7Z dividido, la UI abre el resultado por la 1ª parte y ejecuta
  `TestIntegrityAsync` (CRC por fichero que guarda el propio 7z), mostrando el resumen
  (tamaño, partes, "✓ Verificación correcta").
- **UI:** en "Comprimir", desplegable **"Dividir archivo"** (No dividir / 100 MB … 20 GB /
  Personalizado… / Número de partes…). "Personalizado" → tamaño + unidad MB/GB. "Número de partes" →
  cantidad (se reparte `total_origen / N`, `SplitCalculator`). Desactivado y con nota si el formato no
  es 7Z o no hay `7zr.exe`. Reutiliza la barra de progreso.
- **Archivos > RAM:** streaming de extremo a extremo (7zr por bloques al crear; `MultiVolumeReadStream`
  al leer). Nada se carga entero en memoria ni se duplica en disco.

**Archivos nuevos:** `Core/Models/SplitMethod.cs`, `Core/Services/{SplitCalculator, VolumeSetInspector,
SplitSupport, SourcePathSize}.cs`, `Core/Exceptions/{SplitNotSupportedException,
IncompleteVolumeSetException}.cs`, `Archives/Common/MultiVolumeReadStream.cs`, `App/ViewModels`
(clase `SplitChoice`). **Modificados:** `ArchiveFormatDetector`, `SevenZipArchiveReader`,
los 4 escritores (guard `SplitSupport.Validate`), `SevenZipRequest`, `SevenZipArgumentBuilder`,
`SevenZipCli`, `RoutingSevenZipWriter`, `CompressViewModel`, `CompressWindow.xaml`, `Strings.*`.
**Ninguna API pública renombrada; ninguna librería cambiada.**

### Menú contextual moderno de Windows 11 — `IExplorerCommand` + paquete MSIX

**Objetivo:** que el submenú **"Ultra Archive"** aparezca en el **menú principal** del clic derecho de
Windows 11 (como WinRAR 7.x), no solo bajo *"Mostrar más opciones"*.

**Por qué es alcance nuevo:** Windows 11 **solo** promociona al menú principal las extensiones **COM**
(`IExplorerCommand`) que tienen **identidad de paquete** (MSIX). El submenú clásico basado en
registro (`RegistryShellIntegrationService` / componente `ContextMenuIntegration` del MSI) se queda
en el menú heredado. El §1 decía *"Nunca `IContextMenu`/`IExplorerCommand`"*; **esta fase es la
excepción, y única**: el manejador COM **no contiene lógica de negocio** — solo lanza
`UltraArchive.exe` con los mismos flags CLI (`--compress`, `--extract-here`, …) que valida
`ShellCommandLineParser`. Cero segunda ruta de compresión/extracción. Los dos menús **coexisten**
(el clásico sirve a Windows 10 y a *"Mostrar más opciones"*; no se duplican porque el clásico no
sale en el menú principal).

**`shellext/` (fuera de `UltraArchive.sln`, como `installer/`):**

- **`UltraArchive.ShellExtension`** — DLL COM in-proc, **.NET 8 Native AOT** (sin runtime .NET en
  destino). Interfaces COM (`IExplorerCommand`, `IEnumExplorerCommand`, `IShellItem(Array)`,
  `IClassFactory`) **definidas a mano** con `[GeneratedComInterface]`/`[GeneratedComClass]` +
  `StrategyBasedComWrappers` — **sin NuGet nuevo**. `UltraArchiveRootCommand` (CLSID fijo
  `A99F6CA6-1436-4063-BA8A-156692A771A8`) es un desplegable; los sub-verbos (`SubCommands/`) reúnen
  toda la selección (`IShellItemArray`) y hacen **un** `Process.Start` con `ArgumentList` →
  equivale a `MultiSelectModel="Player"`. Los verbos de extracción se ocultan
  (`ECS_HIDDEN`) si la selección no tiene una de las 8 extensiones reconocidas.
  `UltraArchiveShellLocator` resuelve `UltraArchive.exe` por la entrada de Uninstall del MSI
  (HKLM) o `%ProgramFiles%\UltraArchive\`.
- **`UltraArchive.ShellExtensionStub`** — `.exe` mínimo (`return 0;`) que exige el AppxManifest.
- **`manifest/AppxManifest.xml`** — paquete MSIX: `com:SurrogateServer` con el CLSID → la DLL;
  `desktop4:FileExplorerContextMenus` sobre `*` y `Directory`. `dllmanifest.manifest` (identidad
  embebida en la DLL).
- **`build/shellext/{build,create-certificate,install,uninstall,setup,reinstall}.ps1`** — compilan
  la DLL, crean el certificado autofirmado `CN=UltraArchive` (confianza local, sin `.pfx`),
  empaquetan + firman el `.msix` (`makeappx`/`signtool`) y lo registran (`Add-AppxPackage`).
  `setup.ps1` hace todo con auto-elevación; `reinstall.ps1` reinstala UltraArchive completo desde el
  MSI (prueba de la Fase 2, logs en `_reinstall/`). Identidad del paquete: **1.0.1.0** (bump para que
  `Add-AppxPackage` trate el repaquetado como actualización y no como conflicto de misma versión).

**Falso positivo de Defender:** la DLL Native AOT dispara `Trojan:Win64/Aotera.*!MTB` (heurística
ML sobre binarios **AOT**). Los scripts añaden una **exclusión de Defender** para `build/shellext/`
y para la carpeta instalada del paquete, y marcan ese ThreatID como permitido; `uninstall.ps1` /
`unregister.ps1` las retiran. **Para distribuir a terceros** haría falta un certificado de firma de
código real + reportar el FP a Microsoft (o reescribir la extensión en C++/WinRT).

**Integración con el MSI:**

- `build/publish.ps1` copia `build/shellext/out/*.msix` + `.cer` + `shellext/msi/{register,
  unregister}.ps1` a `publish/UltraArchive/shellext/` (si el `.msix` no existe, avisa y sigue —
  patrón de `7zr.exe`). `HarvestDirectory` los mete en el MSI.
- `installer/Package.wxs` — custom actions **diferidas, no-impersonadas** (`WixSilentExec64` de
  `WixToolset.Util.wixext`): `UltraArchiveRegisterShellExt` (tras `InstallFiles`, `NOT REMOVE`) →
  `register.ps1` (confía en el certificado, `Add-AppxProvisionedPackage`, exclusión de Defender,
  registro para sesiones abiertas); `UltraArchiveUnregisterShellExt` (antes de `RemoveFiles`,
  `REMOVE="ALL"`) → `unregister.ps1` (revierte todo). `Return="ignore"` — un fallo no rompe el MSI
  ni el submenú clásico.
- `RegistryShellIntegrationService`, `UltraArchive.App` y `UltraArchive.Shell` **sin cambios**.

**Archivos nuevos:** `shellext/` (≈20 ficheros), `build/shellext/*.ps1`, `shellext/msi/{register,
unregister}.ps1`, `shellext/README.md`. **Modificados:** `installer/Package.wxs`,
`installer/UltraArchive.Installer.wixproj` (`WixToolset.Util.wixext`), `build/publish.ps1`.
**Ninguna librería añadida a la solución; ninguna API pública cambiada; los 419 tests siguen en
verde (los proyectos nuevos están fuera de `UltraArchive.sln`).**

### Ventana compacta de progreso para la extracción desde el Explorador (estilo WinRAR)

Los verbos de **extracción** del menú contextual (`--extract-here`, `--extract-here-flat`,
`--extract-to`) ya no abren la ventana principal: abren una **ventana compacta de progreso**
(`ShellProgressWindow`, ~460×160) con el fichero actual + %, barra de llenado (degradado azul→cian),
tiempo transcurrido y botones **Segundo plano** (minimiza) y **Cancelar**
(`MainViewModel.CancelOperationCommand`, el `CancellationToken` que ya existía). Al terminar muestra
el resultado ~1,4 s y la aplicación **se cierra sola**. Si una segunda instancia reenvía otro
comando por el named pipe mientras tanto, se muestra la ventana principal y el proceso sigue vivo.

Reutiliza **íntegramente** `MainViewModel` (misma ruta de extracción `ExtractToPathAsync` →
`WithPasswordRetryAsync` → motores; diálogos de contraseña y colisión con
`DialogOwner.Pick()`, que elige una ventana **visible** en vez de la principal oculta). Los verbos
de **compresión** siguen abriendo `CompressWindow` (que ya tenía su propia barra y se cierra sola).

**Archivos nuevos:** `App/Views/ShellProgressWindow.xaml(.cs)`, `App/Services/DialogOwner.cs`.
**Modificados:** `App/App.xaml.cs` (rama de extracción sin ventana principal), `App/ViewModels/
MainViewModel.cs` (`ProgressElapsedText`, evento `ShellOperationFinished`, `RunStartupCommandAsync`
con `try/finally`), `App/Services/Wpf{PasswordProvider,CollisionPrompt}.cs`, `Strings*.resx`.
**419/419 tests en verde.**

### Marca ASTRIM: barra de estado y diálogo "Acerca de"

- **Barra de estado** de `MainWindow` (esquina inferior derecha): logo `App/Assets/ASTRIM-logo.png`
  (96×96 en disco, mostrado a 18px en un tile redondeado con borde) + texto "ASTRIM" en
  `UA.Brush.TextMuted`, tras un separador vertical. Pinned a la derecha (`DockPanel.Dock="Right"`,
  primer hijo → queda pegado al borde aunque aparezcan la barra de progreso o "Cancelar").
- **Diálogo "Acerca de"** (`App/Views/AboutDialog.xaml`, botón ℹ️ nuevo en la barra de herramientas,
  junto al de tema): logo de UltraArchive, marca "UltraArchive", versión (leída en runtime de
  `Assembly.GetExecutingAssembly().GetName().Version` — nunca hardcodeada), descripción corta, logo
  de ASTRIM + "Desarrollado por ASTRIM" + copyright, botón Cerrar. Sin ViewModel propio (contenido
  estático); `MainViewModel.AboutCommand` (`RelayCommand`) lo abre con
  `Owner = Application.Current?.MainWindow`.
- Cadenas nuevas (`Strings.resx`/`.en.resx`/`Designer.cs`): `ButtonAbout(Tooltip)`, `ButtonClose`,
  `AboutWindowTitle`, `AboutDescription`, `AboutDevelopedBy`, `AboutVersion`, `AboutCopyright`.

### Tests de `shellext/` (lógica pura, fuera de `UltraArchive.sln`)

La DLL de la extensión de shell es **Native AOT** y no puede alojar xUnit, así que
`shellext/UltraArchive.ShellExtension.Tests` (net8.0 normal, fuera del `.sln` como el resto de
`shellext/`) enlaza (`<Compile Include>`, **no** `ProjectReference`) los ficheros que son lógica
pura, para testear exactamente el mismo código que corre en la DLL sin duplicarlo:

- **`ArchiveDetection.cs`** — se separó de la comprobación con COM: ahora solo contiene
  `IsArchive(string path)` (la lista de 8 extensiones), sin ningún tipo `IShellItem*`. La parte que
  sí recorre un `IShellItemArray` pasó a un fichero nuevo, `ArchiveSelection.cs` (queda solo en el
  proyecto principal, no se enlaza en los tests).
- **`SelectionLogic.cs`** (nuevo) — `StemFromPath` (nombre base para los títulos dinámicos,
  extraído de `ExplorerCommandBase.GetSelectionStem`) y `BuildArguments` (arma la lista de
  argumentos para `UltraArchive.exe`, extraído de `LaunchUltraArchive`/`LaunchUltraArchivePerItem`).
  Mismo comportamiento de antes, ahora aislado y testeado.

**31/31 tests en verde** (`dotnet test shellext/UltraArchive.ShellExtension.Tests`). Total del
proyecto: **419** (`UltraArchive.sln`) **+ 31** (`shellext/`).

### Comprimir desde el menú también oculta la ventana principal

Igualado con el comportamiento de "Extraer" (ver más arriba): los verbos `--compress[-here|-zip|-7z
|-split]` ya no muestran la ventana principal detrás de la ventana "Comprimir". `App.xaml.cs` trata
`isShellCompress` igual que `isShellExtraction` (sin `mainWindow.Show()`) y se suscribe a
`MainViewModel.ShellOperationFinished` para cerrar la aplicación cuando `OpenCompressWindow`
(que sigue abriendo `CompressWindow` normal, con su propia barra de progreso) devuelve el control —
a menos que una instancia reenviada haya hecho visible la ventana principal mientras tanto.
`MainViewModel.OpenCompressWindow` usa `DialogOwner.Pick()` en vez de `Application.Current.MainWindow`
a secas (que lanzaría `InvalidOperationException` si la principal no está visible); sin ninguna
ventana visible, `CompressWindow` se centra en la pantalla en vez de "sobre el propietario".
Verificado: `--compress-zip`/`--compress` desde un solo proceso, sin segunda ventana "UltraArchive",
cierre limpio del proceso al terminar o al cerrar "Comprimir" a mano. 419/419 tests en verde.

---

## 5. Pruebas

### Existentes (**402/402** en verde — build Debug y Release, 0 warnings, 0 errores, 0 omitidos)

| Área | Archivo | Cubre |
| --- | --- | --- |
| Detección de formato | `Core/ArchiveFormatDetectorTests.cs` | Firmas de cabecera, TAR/ISO por offset, extensión, prevalencia contenido>extensión, restauración de posición del stream. |
| Factory de motores | `Core/ArchiveEngineFactoryTests.cs` | Registro/resolución, `UnsupportedFormatException`, mensaje legal de RAR. |
| Matriz de cifrado | `Core/EncryptionSupportTests.cs` | Capacidades por formato; contraseña en 7Z/TAR/GZIP/RAR → error; ZIP+AES OK; estado incoherente; ZipCrypto al crear; cifrar nombres. |
| Seguridad de rutas | `Security/PathSecurityTests.cs` | Rutas de escape, absolutas, UNC, con unidad; caracteres inválidos de Windows; dispositivos reservados; segmentos con punto/espacio final; nombres válidos que **no** deben bloquearse. |
| Bomba de descompresión | `Security/DecompressionGuardTests.cs` | Archivo normal / pequeño con ratio alto (OK); ratio absurdo → `DecompressionBombException`; entrada que supera su tamaño declarado. |
| Extracción segura | `Archives/ExtractionSafetyTests.cs` | Fallo o cancelación a mitad de copia → no queda fichero parcial; colisión Skip / RenameAutomatically. |
| ZIP | `Archives/ZipArchiveEngineTests.cs` | Crear→extraer byte a byte, integridad, añadir/quitar, **ZIP hostil** con entrada `../`, archivo no válido. |
| ZIP cifrado | `Archives/ZipEncryptionTests.cs` | Crear ZIP AES-256 → extraer con contraseña correcta (byte a byte) / incorrecta / sin contraseña → `InvalidPasswordException`; ZIP sin contraseña sigue OK; `EncryptFileNames` → error y sin archivo; añadir a ZIP cifrado → bloqueado. |
| Rechazo de cifrado | `Archives/WriterEncryptionRejectionTests.cs` | Pedir contraseña a 7Z / TAR / GZIP → `EncryptionNotSupportedException` y **no** queda archivo. |
| 7Z | `Archives/SevenZipArchiveEngineTests.cs` | Crear→extraer, integridad. |
| TAR | `Archives/TarArchiveEngineTests.cs` | Crear→extraer, integridad. |
| GZIP | `Archives/GZipArchiveEngineTests.cs` | `.gz` y `.tar.gz`, integridad. |
| RAR | `Archives/RarArchiveEngineTests.cs` | Listar, extraer byte a byte, extracción selectiva, integridad, RAR corrupto, cabecera cifrada sin/con/mal contraseña, detección sobre fixture real. |
| ISO — probe de volumen | `Iso/IsoVolumeProbeTests.cs` | PVD sin Joliet/UDF; detección de Joliet; no mueve la posición del stream; bytes que no son ISO; stream demasiado corto; PVD corrupto. |
| ISO — motor | `Iso/IsoArchiveEngineTests.cs` | Abrir + info de volumen; corrupta / truncada / no-ISO → `ArchiveCorruptedException`; imagen UDF pura → `UnsupportedFormatException`; detección sobre fixture real; listar carpetas+ficheros con tamaño; ISO vacía; Joliet (listar y **extraer byte a byte** con acentos/espacios); extraer todo byte a byte; extracción selectiva; progreso; **cancelación a mitad + sin fichero parcial**; integridad; colisión Skip; **path traversal** (`..\..\XX`) bloqueado; **dispositivo reservado** (`AUX\...`) bloqueado. |
| ViewModel "Comprimir" | `ViewModels/CompressViewModelTests.cs` | Confirmación de contraseña que no coincide / vacía → no se lanza; sin cifrado se ignora la contraseña; formato sin soporte oculta los campos; `ClearSensitiveData` borra la contraseña; **RAR presente en la lista pero `CanCreate == false`** y `ValidateBeforeStart()` → `FormatNotCreatable`. |
| Árbol de carpetas (UI) | `ViewModels/ArchiveFolderTreeTests.cs` | Archivo plano → solo raíz; deduce carpetas de rutas de fichero sin entradas de carpeta; entradas de carpeta explícitas/vacías; `EntriesIn(raíz)` y `EntriesIn(subcarpeta)` → hijos directos (subcarpetas primero, luego ficheros); `Find` de ruta inexistente → null; normaliza `\`↔`/`. |
| Colisiones de extracción | `Core/CollisionResolverTests.cs` + `Archives/ExtractionSafetyTests.cs` | Sin conflicto → misma ruta; políticas Overwrite/Skip/Rename; `Ask` sin manejador → sobrescribe; `Ask` llama al manejador 1×/conflicto; "aplicar a todos" no vuelve a preguntar; `Cancel` → `OperationCanceledException` y no se toca el fichero existente; `UniquePath` incremental; test de motor real (ZIP) con `Ask` decidiendo por entrada + reporte de `SkippedEntries`. |

**Fixtures ISO**: se generan en cada test con `CDBuilder` (misma librería), sin binarios embebidos ni
herramientas externas. Los casos hostiles (traversal, dispositivos reservados, UDF, raw 2352) se
obtienen parcheando/reempaquetando bytes de una imagen generada.

### Cobertura que se deja fuera a propósito

- **ISO multi-extent** (fichero único > 4 GiB): requeriría un fichero real de 4 GiB en el test →
  no se prueba; DiscUtils tampoco lo soporta (documentado en §3).
- **Rock Ridge real**: `CDBuilder` no genera árboles Rock Ridge y el proyecto no embebe binarios de
  test → la lectura de nombres RR está implementada pero sin fixture de regresión.
- Smoke test del instalador (Fase 8).

---

## 6. Cómo compilar y probar

```powershell
dotnet build UltraArchive.sln
dotnet test  UltraArchive.Tests
dotnet run   --project UltraArchive.App

# Distribución portable + zip verificado (Fase 8)
pwsh build/publish.ps1            # -> publish/UltraArchive/
pwsh build/package.ps1            # -> publish/UltraArchive-1.0.0.zip (+ SHA-256)

# MSI (solo si el WiX Toolset está disponible)
dotnet build installer/UltraArchive.Installer.wixproj -c Release
```

Requisitos: Windows 10/11, .NET 8 SDK. El MSI necesita además el paquete `WixToolset.Sdk`
(se restaura solo al construir el `.wixproj`).
