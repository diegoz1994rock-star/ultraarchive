using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;

namespace UltraArchive.Core.Services;

/// <summary>
/// Única fuente de verdad sobre qué puede cifrar UltraArchive al **crear** cada formato. La capacidad
/// depende de la <b>implementación realmente disponible</b>, no del nombre del formato:
///
///   - ZIP  → cifrado AES-256 gestionado (SharpZipLib), siempre disponible.
///   - 7Z   → cifrado (contenido + cabecera) SOLO si hay un 7zr.exe verificado
///            (<see cref="ISevenZipCapability"/>). Sin él, no está disponible.
///   - TAR / GZIP → el formato no cifra.
///   - RAR / ISO  → solo lectura.
///
/// La usan los escritores (para validar antes de tocar disco) y la UI (para mostrar/ocultar los
/// campos de contraseña). Pasar <paramref name="sevenZip"/> = null equivale a "7zr no disponible".
/// </summary>
public static class EncryptionSupport
{
    /// <summary>True si el formato admite protección con contraseña al crearlo.</summary>
    public static bool SupportsPasswordOnCreate(ArchiveFormat format, ISevenZipCapability? sevenZip = null) => format switch
    {
        ArchiveFormat.Zip => true,
        ArchiveFormat.SevenZip => sevenZip?.CanCreateEncryptedSevenZip ?? false,
        _ => false,
    };

    /// <summary>True si el formato admite cifrado AES-256 al crearlo.</summary>
    public static bool SupportsAes256OnCreate(ArchiveFormat format, ISevenZipCapability? sevenZip = null) => format switch
    {
        ArchiveFormat.Zip => true,
        ArchiveFormat.SevenZip => sevenZip?.CanCreateEncryptedSevenZip ?? false,
        _ => false,
    };

    /// <summary>
    /// True si el formato admite cifrar también los nombres de las entradas al crearlo.
    /// ZIP (WinZip AES) nunca puede: deja el índice central en claro. 7Z sí, cuando hay 7zr verificado
    /// (se crea siempre con <c>-mhe=on</c>).
    /// </summary>
    public static bool SupportsEncryptedFileNamesOnCreate(ArchiveFormat format, ISevenZipCapability? sevenZip = null) =>
        format == ArchiveFormat.SevenZip && (sevenZip?.CanCreateEncryptedSevenZip ?? false);

    /// <summary>
    /// Valida las opciones de cifrado de <paramref name="options"/> contra lo que el formato admite.
    /// Lanza <see cref="EncryptionNotSupportedException"/> (sin efectos secundarios) si se pide algo
    /// no soportado, para que el escritor falle antes de crear ningún archivo.
    /// </summary>
    public static void Validate(CreateArchiveOptions options, ISevenZipCapability? sevenZip = null)
    {
        var wantsPassword = !string.IsNullOrEmpty(options.Password);
        var wantsEncryption = options.Encryption != EncryptionMethod.None;

        if (!wantsPassword && !wantsEncryption && !options.EncryptFileNames)
        {
            return; // Sin cifrado: nada que validar.
        }

        if (wantsPassword != wantsEncryption)
        {
            throw new EncryptionNotSupportedException(
                "Estado de cifrado incoherente: hay que indicar contraseña y método de cifrado a la vez, o ninguno de los dos.");
        }

        if (options.Encryption == EncryptionMethod.ZipCrypto)
        {
            throw new EncryptionNotSupportedException(
                "ZipCrypto es un cifrado débil y solo se admite para LEER archivos antiguos, nunca para crear. Usa AES-256.");
        }

        if (!SupportsPasswordOnCreate(options.Format, sevenZip))
        {
            throw new EncryptionNotSupportedException(FormatLimitationMessage(options.Format, sevenZip));
        }

        if (options.Encryption == EncryptionMethod.Aes256 && !SupportsAes256OnCreate(options.Format, sevenZip))
        {
            throw new EncryptionNotSupportedException(FormatLimitationMessage(options.Format, sevenZip));
        }

        if (options.EncryptFileNames && !SupportsEncryptedFileNamesOnCreate(options.Format, sevenZip))
        {
            throw new EncryptionNotSupportedException(
                "El cifrado de los nombres de archivo solo está disponible en 7Z con un 7zr.exe verificado. " +
                "El ZIP con AES-256 (WinZip) siempre deja visible el índice de nombres.");
        }
    }

    private static string FormatLimitationMessage(ArchiveFormat format, ISevenZipCapability? sevenZip) => format switch
    {
        ArchiveFormat.SevenZip =>
            "La creación de 7Z cifrado requiere el binario 7zr.exe verificado, que no está disponible en esta " +
            $"instalación. {sevenZip?.StatusExplanation} Para un archivo protegido, usa ZIP con AES-256.",
        ArchiveFormat.Tar or ArchiveFormat.GZip =>
            $"El formato {format.ToString().ToUpperInvariant()} no admite cifrado. Para un archivo protegido, usa ZIP con AES-256.",
        ArchiveFormat.Rar =>
            "UltraArchive no crea archivos RAR (restricción legal del formato).",
        _ =>
            $"El formato {format} no admite protección con contraseña al crearlo. Usa ZIP con AES-256.",
    };
}
