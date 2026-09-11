namespace UltraArchive.Core.Exceptions;

/// <summary>
/// Se lanza cuando el usuario pide cifrado (contraseña, AES-256 o cifrado de nombres) para un formato
/// o una operación que no lo admite con las librerías disponibles. UltraArchive nunca genera un
/// archivo sin cifrar cuando se solicitó cifrado: falla de forma explícita con este error.
/// </summary>
public sealed class EncryptionNotSupportedException : ArchiveException
{
    public EncryptionNotSupportedException(string message)
        : base(message, ArchiveErrorCategory.UnsupportedFormat)
    {
    }
}
