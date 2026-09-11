using System.Text;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;

namespace UltraArchive.Core.Services;

/// <summary>
/// Detector de formatos basado en firmas de bytes (magic numbers), con fallback a extensión.
///
/// Firmas utilizadas (documentación pública de cada formato):
///   ZIP      : 50 4B 03 04 (local file header) | 50 4B 05 06 (archivo vacío) | 50 4B 07 08 (spanned)
///   7Z       : 37 7A BC AF 27 1C
///   RAR4     : 52 61 72 21 1A 07 00        ("Rar!\x1A\x07\x00")
///   RAR5     : 52 61 72 21 1A 07 01 00     ("Rar!\x1A\x07\x01\x00")
///   GZIP     : 1F 8B
///   BZip2    : 42 5A 68 ('1'-'9')          ("BZh" + nivel de bloque)
///   TAR      : "ustar" en el offset 257 de la cabecera (formato POSIX/GNU). El TAR "v7" clásico
///              anterior a POSIX NO tiene firma y no se puede detectar de forma fiable por contenido;
///              en ese caso solo queda el fallback por extensión (limitación documentada, no oculta).
///   ISO9660  : "CD001" en el offset 32769 (sector 16 + 1 byte, tamaño de sector estándar 2048).
///
/// La detección por contenido requiere que el stream permita Seek para poder comprobar TAR e ISO9660
/// (offsets alejados del principio). Si no es seekable, solo se comprueban las firmas de cabecera.
/// </summary>
public sealed class ArchiveFormatDetector : IArchiveFormatDetector
{
    private const int TarMagicOffset = 257;
    private const int TarMagicLength = 5; // "ustar"
    private const long IsoIdentifierOffset = 32769; // 16 sectores de 2048 bytes + 1
    private const int IsoIdentifierLength = 5; // "CD001"

    private static readonly Dictionary<string, ArchiveFormat> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".zip"] = ArchiveFormat.Zip,
        [".7z"] = ArchiveFormat.SevenZip,
        [".rar"] = ArchiveFormat.Rar,
        [".tar"] = ArchiveFormat.Tar,
        [".gz"] = ArchiveFormat.GZip,
        [".tgz"] = ArchiveFormat.GZip,
        [".bz2"] = ArchiveFormat.BZip2,
        [".iso"] = ArchiveFormat.Iso9660,
    };

    public async Task<ArchiveFormat> DetectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (File.Exists(filePath))
        {
            await using var stream = File.OpenRead(filePath);
            var byContent = await DetectFromContentAsync(stream, cancellationToken).ConfigureAwait(false);
            if (byContent != ArchiveFormat.Unknown)
            {
                return byContent;
            }
        }

        // Fallback: el archivo no existe (aún), está vacío, o su contenido no coincide con ninguna
        // firma conocida (p. ej. TAR clásico pre-POSIX). Nos apoyamos en la extensión.
        return DetectFromExtension(filePath);
    }

    public async Task<ArchiveFormat> DetectFromContentAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var startPosition = stream.CanSeek ? stream.Position : -1;

        try
        {
            var header = new byte[8];
            var headerRead = await ReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false);

            if (headerRead >= 4 && header[0] == 0x50 && header[1] == 0x4B &&
                ((header[2] == 0x03 && header[3] == 0x04) ||
                 (header[2] == 0x05 && header[3] == 0x06) ||
                 (header[2] == 0x07 && header[3] == 0x08)))
            {
                return ArchiveFormat.Zip;
            }

            if (headerRead >= 6 && header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC &&
                header[3] == 0xAF && header[4] == 0x27 && header[5] == 0x1C)
            {
                return ArchiveFormat.SevenZip;
            }

            if (headerRead >= 7 && header[0] == 0x52 && header[1] == 0x61 && header[2] == 0x72 &&
                header[3] == 0x21 && header[4] == 0x1A && header[5] == 0x07 &&
                (header[6] == 0x00 || (header[6] == 0x01 && headerRead >= 8 && header[7] == 0x00)))
            {
                return ArchiveFormat.Rar;
            }

            if (headerRead >= 2 && header[0] == 0x1F && header[1] == 0x8B)
            {
                return ArchiveFormat.GZip;
            }

            if (headerRead >= 4 && header[0] == 0x42 && header[1] == 0x5A && header[2] == 0x68 &&
                header[3] >= (byte)'1' && header[3] <= (byte)'9')
            {
                return ArchiveFormat.BZip2;
            }

            if (!stream.CanSeek)
            {
                return ArchiveFormat.Unknown;
            }

            if (await IsIso9660Async(stream, cancellationToken).ConfigureAwait(false))
            {
                return ArchiveFormat.Iso9660;
            }

            if (await IsPosixTarAsync(stream, cancellationToken).ConfigureAwait(false))
            {
                return ArchiveFormat.Tar;
            }

            return ArchiveFormat.Unknown;
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = startPosition;
            }
        }
    }

    public ArchiveFormat DetectFromExtension(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Parte de un archivo dividido en volúmenes ("Archivo.7z.001", "...002"…): el formato lo da la
        // extensión "interna" (antes del sufijo numérico). La primera parte además lleva la firma real,
        // así que la detección por contenido ya la resuelve; esto cubre además las partes 002, 003…
        var volumeBase = VolumeSetInspector.TryGetBaseName(filePath);
        if (volumeBase is not null)
        {
            var inner = Path.GetExtension(volumeBase);
            if (ExtensionMap.TryGetValue(inner, out var innerFormat))
            {
                return innerFormat;
            }
        }

        // Caso especial: .tar.gz / .tar.bz2 son, a nivel de contenedor físico, un GZIP/BZip2
        // que envuelve un TAR. Se detectan como el formato de la capa exterior.
        if (filePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            return ArchiveFormat.GZip;
        }

        if (filePath.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase))
        {
            return ArchiveFormat.BZip2;
        }

        var extension = Path.GetExtension(filePath);
        return ExtensionMap.GetValueOrDefault(extension, ArchiveFormat.Unknown);
    }

    private static async Task<bool> IsIso9660Async(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.Length < IsoIdentifierOffset + IsoIdentifierLength)
        {
            return false;
        }

        stream.Position = IsoIdentifierOffset;
        var buffer = new byte[IsoIdentifierLength];
        var read = await ReadExactAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
        return read == IsoIdentifierLength && Encoding.ASCII.GetString(buffer) == "CD001";
    }

    private static async Task<bool> IsPosixTarAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.Length < TarMagicOffset + TarMagicLength)
        {
            return false;
        }

        stream.Position = TarMagicOffset;
        var buffer = new byte[TarMagicLength];
        var read = await ReadExactAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
        return read == TarMagicLength && Encoding.ASCII.GetString(buffer) == "ustar";
    }

    /// <summary>Lee hasta llenar <paramref name="buffer"/> o hasta fin de stream, devolviendo los bytes realmente leídos.</summary>
    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }
}
