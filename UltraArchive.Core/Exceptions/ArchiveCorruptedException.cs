namespace UltraArchive.Core.Exceptions;

/// <summary>Se lanza cuando el archivo no se puede leer porque está dañado o su estructura interna es inválida.</summary>
public sealed class ArchiveCorruptedException : ArchiveException
{
    public ArchiveCorruptedException(string message, Exception? innerException = null)
        : base(message, ArchiveErrorCategory.CorruptedArchive)
    {
        if (innerException is not null)
        {
            HResult = innerException.HResult;
        }
    }
}
