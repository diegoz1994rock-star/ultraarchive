namespace UltraArchive.Core.Models;

/// <summary>
/// Método de cifrado aplicado al crear un archivo protegido con contraseña.
/// UltraArchive nunca implementa criptografía propia: todo se apoya en
/// System.Security.Cryptography o en el cifrado nativo del propio formato (7z, ZIP AES).
/// </summary>
public enum EncryptionMethod
{
    /// <summary>Sin cifrado.</summary>
    None,

    /// <summary>
    /// ZipCrypto clásico (PKWARE). Débil criptográficamente: solo se ofrece para compatibilidad
    /// al LEER archivos antiguos, nunca como opción por defecto al crear.
    /// </summary>
    ZipCrypto,

    /// <summary>AES-256, el único método recomendado y ofrecido por defecto para crear archivos protegidos.</summary>
    Aes256
}
