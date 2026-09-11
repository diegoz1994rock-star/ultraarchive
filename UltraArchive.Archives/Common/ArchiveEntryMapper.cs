using SharpCompress.Common;
using UltraArchive.Core.Models;

namespace UltraArchive.Archives.Common;

/// <summary>Convierte una <c>IEntry</c> de SharpCompress al modelo de dominio <see cref="ArchiveEntry"/>.</summary>
internal static class ArchiveEntryMapper
{
    public static ArchiveEntry ToArchiveEntry(IEntry entry)
    {
        var key = (entry.Key ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        var name = key.Length == 0 ? key : key[(key.LastIndexOf('/') + 1)..];

        return new ArchiveEntry
        {
            Name = name,
            FullPath = key,
            IsDirectory = entry.IsDirectory,
            UncompressedSize = entry.Size,
            CompressedSize = entry.CompressedSize,
            LastModifiedUtc = entry.LastModifiedTime.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(entry.LastModifiedTime.Value, DateTimeKind.Utc))
                : null,
            CompressionMethod = DisplayNameFor(entry.CompressionType),
            IsEncrypted = entry.IsEncrypted,
            Crc32 = ReadCrcOrNull(entry),
        };
    }

    /// <summary>
    /// Lee el CRC32 declarado por el archivo, o null si el formato no lo expone para esa entrada.
    /// En RAR, SharpCompress lanza <see cref="ArgumentNullException"/> al pedir el CRC de una entrada
    /// de carpeta (no llevan checksum): se trata como "sin CRC" en vez de propagar el fallo.
    /// </summary>
    private static uint? ReadCrcOrNull(IEntry entry)
    {
        try
        {
            return entry.Crc == 0 ? null : unchecked((uint)entry.Crc);
        }
        catch (ArgumentNullException)
        {
            return null;
        }
    }

    private static string DisplayNameFor(CompressionType compressionType) => compressionType switch
    {
        CompressionType.None => "Sin comprimir",
        CompressionType.Deflate => "Deflate",
        CompressionType.Deflate64 => "Deflate64",
        CompressionType.GZip => "GZip",
        CompressionType.BZip2 => "BZip2",
        CompressionType.LZMA => "LZMA",
        CompressionType.LZMA2 => "LZMA2",
        CompressionType.PPMd => "PPMd",
        CompressionType.ZStandard => "ZStandard",
        _ => compressionType.ToString(),
    };
}
