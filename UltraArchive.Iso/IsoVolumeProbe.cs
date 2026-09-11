using System.Text;

namespace UltraArchive.Iso;

/// <summary>
/// Lee el "Volume Descriptor Set" de una imagen ISO (sectores de 2048 bytes a partir del 16) para
/// decidir, antes de abrir el sistema de archivos con la librería, si:
///   - es realmente una ISO9660 (firma "CD001" en un Primary Volume Descriptor),
///   - trae un árbol Joliet (nombres Unicode) — para pedirlo explícitamente al abrir,
///   - trae la secuencia de reconocimiento UDF (que UltraArchive no soporta todavía).
///
/// No depende de DiscUtils: es un parser mínimo y defensivo (tolera imágenes truncadas o basura).
/// Formato: ECMA-119 §8 (Volume Descriptors) y ECMA-167 / UDF (Volume Recognition Sequence).
/// </summary>
internal static class IsoVolumeProbe
{
    private const int SectorSize = 2048;
    private const int SystemAreaSectors = 16;
    private const int MaxDescriptorsToScan = 64; // sanidad: un VDS real tiene un puñado de descriptores

    private const byte VdTypeBootRecord = 0;
    private const byte VdTypePrimary = 1;
    private const byte VdTypeSupplementary = 2;
    private const byte VdTypeTerminator = 255;

    // Offsets dentro de un Volume Descriptor (relativos al inicio del sector).
    private const int StdIdentifierOffset = 1;   // 5 bytes: "CD001", "NSR02", ...
    private const int VersionOffset = 6;
    private const int VolumeIdentifierOffset = 40;  // 32 bytes
    private const int EscapeSequencesOffset = 88;   // 32 bytes (solo tipo 2)
    private const int CreationDateOffset = 813;     // 17 bytes: "YYYYMMDDHHMMSScc" + offset GMT

    public static IsoVolumeInfo Probe(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek || stream.Length < (SystemAreaSectors + 1) * SectorSize)
        {
            return new IsoVolumeInfo { IsIso9660 = false, HasJoliet = false, HasUdf = false };
        }

        var originalPosition = stream.Position;
        try
        {
            var isIso9660 = false;
            var hasJoliet = false;
            var hasUdf = false;
            string? volumeLabel = null;
            DateTimeOffset? createdUtc = null;

            var sector = new byte[SectorSize];

            for (var i = 0; i < MaxDescriptorsToScan; i++)
            {
                var sectorIndex = SystemAreaSectors + i;
                var offset = (long)sectorIndex * SectorSize;
                if (offset + SectorSize > stream.Length)
                {
                    break;
                }

                stream.Position = offset;
                if (!TryReadFull(stream, sector))
                {
                    break;
                }

                var stdId = Encoding.ASCII.GetString(sector, StdIdentifierOffset, 5);
                var type = sector[0];

                if (stdId == "CD001")
                {
                    switch (type)
                    {
                        case VdTypePrimary:
                            isIso9660 = true;
                            volumeLabel ??= ReadStrD(sector, VolumeIdentifierOffset, 32);
                            createdUtc ??= ReadVolumeDate(sector, CreationDateOffset);
                            break;

                        case VdTypeSupplementary:
                            if (IsJolietEscape(sector, EscapeSequencesOffset))
                            {
                                hasJoliet = true;
                            }

                            break;

                        case VdTypeTerminator:
                            return Build();

                        case VdTypeBootRecord:
                            break;
                    }

                    continue;
                }

                // Secuencia de reconocimiento UDF (ECMA-167): BEA01 / NSR02 / NSR03 / TEA01.
                if (stdId is "NSR02" or "NSR03")
                {
                    hasUdf = true;
                    continue;
                }

                if (stdId is "BEA01" or "TEA01")
                {
                    continue;
                }

                // Descriptor desconocido: fin del conjunto.
                break;
            }

            return Build();

            IsoVolumeInfo Build() => new()
            {
                IsIso9660 = isIso9660,
                HasJoliet = hasJoliet,
                HasUdf = hasUdf,
                VolumeLabel = string.IsNullOrWhiteSpace(volumeLabel) ? null : volumeLabel,
                CreatedUtc = createdUtc,
            };
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static bool IsJolietEscape(byte[] sector, int offset)
    {
        // Joliet: "%/@" (UCS-2 nivel 1), "%/C" (nivel 2) o "%/E" (nivel 3) al inicio del campo.
        if (sector[offset] != 0x25 || sector[offset + 1] != 0x2F)
        {
            return false;
        }

        var third = sector[offset + 2];
        return third is 0x40 or 0x43 or 0x45;
    }

    /// <summary>Campo "d-characters" ISO9660: ASCII, rellenado con espacios a la derecha.</summary>
    private static string ReadStrD(byte[] buffer, int offset, int length) =>
        Encoding.ASCII.GetString(buffer, offset, length).TrimEnd(' ', '\0').Trim();

    /// <summary>
    /// Fecha de volumen ISO9660: 16 dígitos ASCII "YYYYMMDDHHMMSScc" + 1 byte con el offset GMT en
    /// intervalos de 15 minutos (con signo). Devuelve null si el campo está a cero o es inválido.
    /// </summary>
    private static DateTimeOffset? ReadVolumeDate(byte[] buffer, int offset)
    {
        var text = Encoding.ASCII.GetString(buffer, offset, 16);
        if (text is "0000000000000000" || text.Contains('\0'))
        {
            return null;
        }

        if (!int.TryParse(text.AsSpan(0, 4), out var year) || year < 1 ||
            !int.TryParse(text.AsSpan(4, 2), out var month) || month is < 1 or > 12 ||
            !int.TryParse(text.AsSpan(6, 2), out var day) || day is < 1 or > 31 ||
            !int.TryParse(text.AsSpan(8, 2), out var hour) || hour > 23 ||
            !int.TryParse(text.AsSpan(10, 2), out var minute) || minute > 59 ||
            !int.TryParse(text.AsSpan(12, 2), out var second) || second > 59)
        {
            return null;
        }

        var gmtOffsetQuarters = unchecked((sbyte)buffer[offset + 16]);
        var tz = TimeSpan.FromMinutes(gmtOffsetQuarters * 15);

        try
        {
            return new DateTimeOffset(year, month, day, hour, minute, second, tz).ToUniversalTime();
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool TryReadFull(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                return false;
            }

            total += read;
        }

        return true;
    }
}
