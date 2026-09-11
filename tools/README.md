# `tools/` — binarios externos que UltraArchive redistribuye

## `7zr.exe` (creación de 7Z cifrado)

**Estado: INCORPORADO.** `7zr.exe` (LZMA SDK 26.02, build x86, dominio público) está en esta
carpeta y su SHA‑256 fijado en `UltraArchive.Interop/SevenZip/SevenZipToolReference.cs`:

```
56B8CC9F4971CEF253644FAFE54063ED7FDCA551D4DEE0F8C6BAA81B855ACD72
```

`SevenZipLocator` lo resuelve como `Available` y la creación de **7Z cifrado con contraseña**
(AES‑256 + `-mhe=on`) está activa. UltraArchive verifica ese hash antes de ejecutar el binario y
rechaza cualquier otro `7zr.exe`.

El resto de la sección explica cómo se **actualizaría** a una versión futura del SDK.

### Cómo incorporarlo (Fase 8 / empaquetado)

1. Descargar el **LZMA SDK** oficial desde <https://www.7-zip.org/sdk.html>.
   - El LZMA SDK está en **dominio público** (declarado por Igor Pavlov). No hay obligaciones
     de redistribución. Ver `../THIRD-PARTY-NOTICES.txt`.
   - Usar `7zr.exe` (versión reducida de 7z.exe, **solo** formato 7z). **No** usar
     `7z.exe` / `7z.dll` / `7za.exe` (LGPL + restricción unRAR).
2. Copiar `bin/7zr.exe` del SDK a **`tools/7zr.exe`** (esta carpeta).
3. Calcular su SHA‑256:
   ```powershell
   Get-FileHash tools\7zr.exe -Algorithm SHA256
   ```
4. Pegar ese hash (en HEX, mayúsculas) en:
   `UltraArchive.Interop/SevenZip/SevenZipToolReference.cs` → `ExpectedSha256`.
5. Actualizar `../THIRD-PARTY-NOTICES.txt` con: versión del SDK, fecha, URL y el SHA‑256 real.
6. `dotnet build` — el binario se copiará automáticamente a `<salida>/tools/7zr.exe`.
7. Verificar: al arrancar, `SevenZipLocator.Locate()` debe devolver `Available` y la casilla
   "Proteger con contraseña" debe aparecer para el formato 7Z.

> El instalador debe distribuir **exactamente** el binario cuyo hash se fijó. UltraArchive
> se niega a ejecutar cualquier `7zr.exe` cuyo SHA‑256 no coincida.
