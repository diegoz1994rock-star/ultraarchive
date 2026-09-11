namespace UltraArchive.Archives.Common;

/// <summary>
/// SharpCompress no señala "contraseña incorrecta/ausente" de una forma única: según el formato y el
/// método de cifrado lanza <c>SharpCompress.Common.CryptographicException</c> (7Z, RAR, ZipCrypto,
/// ZIP-AES sin contraseña) o <c>SharpCompress.Common.InvalidFormatException</c> con el mensaje
/// "bad password" (ZIP-AES con contraseña incorrecta). Este helper reconoce ambos casos para
/// traducirlos a <see cref="Core.Exceptions.InvalidPasswordException"/> y que la UI pida la
/// contraseña y reintente, sin confundirlos con un archivo realmente corrupto.
/// </summary>
internal static class PasswordErrors
{
    public static bool IsPasswordFailure(Exception ex) => ex switch
    {
        SharpCompress.Common.CryptographicException => true,
        System.Security.Cryptography.CryptographicException => true,
        SharpCompress.Common.InvalidFormatException => ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };
}
