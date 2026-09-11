namespace UltraArchive.Core.Exceptions;

/// <summary>
/// Se lanza cuando se solicita leer/crear un formato para el que todavía no hay
/// (o nunca habrá, como crear RAR) un motor registrado en <see cref="Interfaces.IArchiveEngineFactory"/>.
/// </summary>
public sealed class UnsupportedFormatException : ArchiveException
{
    public UnsupportedFormatException(string message)
        : base(message, ArchiveErrorCategory.UnsupportedFormat)
    {
    }
}
