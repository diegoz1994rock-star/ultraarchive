namespace UltraArchive.Core.Exceptions;

/// <summary>
/// Se lanza cuando un archivo parece una "bomba de descompresión": su contenido declarado (o real)
/// excede los límites de seguridad respecto a su tamaño comprimido, lo que podría agotar el disco o
/// la memoria del equipo. La extracción se aborta antes de escribir nada peligroso.
/// </summary>
public sealed class DecompressionBombException : ArchiveException
{
    public DecompressionBombException(string message)
        : base(message, ArchiveErrorCategory.CorruptedArchive)
    {
    }
}
