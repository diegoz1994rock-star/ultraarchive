namespace UltraArchive.Core.Exceptions;

/// <summary>
/// Se lanza cuando la contraseña proporcionada por el usuario no permite abrir/extraer el archivo.
/// Nunca debe registrarse la contraseña en el mensaje ni en los logs.
/// </summary>
public sealed class InvalidPasswordException : ArchiveException
{
    public InvalidPasswordException(string archiveDisplayName)
        : base($"La contraseña introducida no es válida para '{archiveDisplayName}'.", ArchiveErrorCategory.WrongPassword)
    {
    }
}
