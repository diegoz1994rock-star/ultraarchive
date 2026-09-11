# Firma de código real para UltraArchive — guía de compra y puesta en marcha

Hoy UltraArchive usa un **certificado autofirmado** (`CN=UltraArchive`) solo para que el menú
contextual moderno de Windows 11 funcione **en este equipo** (ver `shellext/README.md`). Este
documento explica qué hace falta para pasar a un **certificado de firma de código real**, que serviría
para:

- Firmar `UltraArchive.exe` y el MSI → Windows SmartScreen deja de mostrar "Editor desconocido" al
  descargarlo o ejecutarlo en cualquier equipo.
- Firmar `UltraArchive.ShellExtension.msix` con una cadena de confianza pública → el menú contextual
  moderno funcionaría en **cualquier equipo**, sin que cada uno tenga que confiar a mano en tu
  certificado.

## 1. Qué tipo de certificado hace falta

Hay dos tipos de **certificado de firma de código** (*Code Signing Certificate*):

| Tipo | Qué es | Reputación SmartScreen | Coste orientativo/año |
| --- | --- | --- | --- |
| **OV** (Organization Validated) | La CA verifica que ASTRIM existe como organización (registro mercantil, teléfono, etc.) | Empieza en cero; sube con el tiempo según cuántos usuarios ejecuten binarios firmados con él | ~70-250 € |
| **EV** (Extended Validation) | Verificación más estricta (persona autorizada, dirección física, a veces videollamada) + la clave privada vive en un **token USB (HSM)**, nunca en un fichero | **Reputación SmartScreen inmediata** desde el primer binario firmado | ~250-450 € |

Para un proyecto que se empieza a distribuir, un **certificado EV** es la opción que evita el aviso
de SmartScreen desde el día uno; un **OV** es más barato pero los primeros usuarios verán el aviso
"Editor desconocido" hasta acumular reputación (puede tardar semanas/meses).

## 2. Proveedores habituales (CAs)

Cualquier Autoridad de Certificación reconocida por Microsoft sirve. Los más usados para firma de
código en 2026: **DigiCert**, **Sectigo**, **SSL.com**, **GlobalSign**. Todos ofrecen OV y EV; los
precios varían por proveedor y por si se compra directo o vía revendedor (a veces más barato).

## 3. Qué va a pedir el proveedor (verificación de identidad)

- Nombre legal de la organización (ASTRIM, si está registrada como empresa/autónomo) — para EV,
  normalmente hace falta que la organización esté dada de alta en un registro público (Cámara de
  Comercio, Hacienda, etc.) con al menos algunos meses de antigüedad, según la CA.
  - Si ASTRIM **no** es todavía una entidad registrada, hay certificados **individuales** (a nombre
    de una persona física) en algunas CAs — más limitado pero válido para empezar.
- Teléfono de contacto verificable (te llaman).
- Para EV: verificación reforzada (documento de identidad, a veces una notarización o videollamada) y
  el token USB con la clave privada te lo envía físicamente la CA o un integrador.

## 4. Qué cambia en el proyecto cuando lo tengas

1. **Firmar el `.exe` y el `.msi`**: añadir un paso `signtool sign /fd SHA256 /tr <timestamp-url> /td SHA256 /a` (o `/sha1 <thumbprint>` si el cert está en el almacén, o `/csp .. /kc ..` si es un token EV) a `build/publish.ps1` / al pipeline del MSI. El *timestamping* (`/tr`) es importante: sin él la firma caduca cuando caduque el certificado.
2. **Firmar el `.msix`** de la extensión de shell con el mismo certificado en vez del autofirmado —
   cambia el `Publisher` del `AppxManifest.xml` a la cadena exacta del sujeto del certificado
   (ej. `CN=ASTRIM, O=ASTRIM, ...` — tiene que coincidir carácter a carácter).
3. **Dejar de instalar el certificado en `TrustedPeople`/`Root`** (`register.ps1`/`create-certificate.ps1`
   ya no harían falta) — un certificado de una CA pública ya es de confianza en cualquier Windows sin
   pasos manuales.
4. Sin más cambios de código: el resto (MSI, menú contextual, ventana de progreso, etc.) funciona igual.

## 5. Mientras tanto

No hace falta comprar nada para seguir usando UltraArchive en este equipo: el certificado autofirmado
actual sigue funcionando indefinidamente **aquí**, y solo caduca (2027-09-10) para efectos de firmar
**nuevos** paquetes — se puede regenerar sin coste con `build/shellext/create-certificate.ps1` cuando
llegue el momento.
