namespace UltraArchive.Core.Models;

/// <summary>
/// Representa una entrada (archivo o carpeta) dentro de un archivo comprimido o una imagen ISO,
/// tal y como se muestra en el explorador de UltraArchive sin necesidad de extraerla.
/// </summary>
public sealed class ArchiveEntry
{
    /// <summary>Nombre simple de la entrada (sin ruta), por ejemplo "README.txt".</summary>
    public required string Name { get; init; }

    /// <summary>Ruta completa dentro del archivo, usando '/' como separador, por ejemplo "Documentos/README.txt".</summary>
    public required string FullPath { get; init; }

    /// <summary>True si la entrada es una carpeta.</summary>
    public bool IsDirectory { get; init; }

    /// <summary>Tamaño original (sin comprimir) en bytes. 0 para carpetas.</summary>
    public long UncompressedSize { get; init; }

    /// <summary>Tamaño comprimido en bytes almacenado en el archivo. 0 para carpetas o formatos sin compresión por entrada.</summary>
    public long CompressedSize { get; init; }

    /// <summary>Fecha de última modificación registrada en el archivo, si está disponible.</summary>
    public DateTimeOffset? LastModifiedUtc { get; init; }

    /// <summary>Método de compresión legible para el usuario (p. ej. "Deflate", "LZMA2", "Store").</summary>
    public string? CompressionMethod { get; init; }

    /// <summary>True si esta entrada concreta está cifrada.</summary>
    public bool IsEncrypted { get; init; }

    /// <summary>CRC32 declarado por el archivo, si el formato lo expone (usado para verificación de integridad).</summary>
    public uint? Crc32 { get; init; }

    /// <summary>Ratio de compresión (0-100) calculado a partir de los tamaños, o null si no aplica.</summary>
    public double? CompressionRatioPercent =>
        IsDirectory || UncompressedSize <= 0
            ? null
            : Math.Round(100.0 - (CompressedSize * 100.0 / UncompressedSize), 1);
}
