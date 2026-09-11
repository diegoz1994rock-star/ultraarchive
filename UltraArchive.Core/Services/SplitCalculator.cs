namespace UltraArchive.Core.Services;

/// <summary>
/// Cálculos puros para la compresión dividida en volúmenes. Sin IO, sin estado: fácil de probar.
/// El resultado (bytes por volumen) es lo único que llega a los escritores.
/// </summary>
public static class SplitCalculator
{
    /// <summary>
    /// Tamaño mínimo razonable de un volumen. Por debajo de esto la división produce cientos/miles de
    /// ficheros minúsculos y el propio 7-Zip se comporta mal. 1 MiB es un límite conservador.
    /// </summary>
    public const long MinVolumeSizeBytes = 1L * 1024 * 1024;

    public const long BytesPerMB = 1024L * 1024;
    public const long BytesPerGB = 1024L * 1024 * 1024;

    /// <summary>Convierte un valor + unidad a bytes. Lanza <see cref="ArgumentOutOfRangeException"/> si es &lt;= 0.</summary>
    public static long ToBytes(double value, Models.SizeUnit unit)
    {
        if (double.IsNaN(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "El tamaño debe ser mayor que cero.");
        }

        var bytes = unit switch
        {
            Models.SizeUnit.MB => value * BytesPerMB,
            Models.SizeUnit.GB => value * BytesPerGB,
            _ => throw new ArgumentOutOfRangeException(nameof(unit)),
        };

        return checked((long)Math.Round(bytes));
    }

    /// <summary>
    /// Tamaño (bytes) por volumen para dividir un total de <paramref name="totalSourceBytes"/> en
    /// exactamente <paramref name="partCount"/> partes. Redondea hacia arriba para que
    /// <c>ceil(total / size) == partCount</c>. Nunca devuelve menos de <see cref="MinVolumeSizeBytes"/>.
    /// </summary>
    public static long BytesPerVolumeForPartCount(long totalSourceBytes, int partCount)
    {
        if (partCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(partCount), "El número de partes debe ser al menos 2.");
        }

        if (totalSourceBytes <= 0)
        {
            // No se conoce el tamaño de origen (o es 0): no se puede repartir por número de partes.
            throw new ArgumentOutOfRangeException(nameof(totalSourceBytes),
                "No se puede dividir por número de partes si no se conoce el tamaño de los orígenes.");
        }

        var size = (totalSourceBytes + partCount - 1) / partCount; // ceil
        return Math.Max(size, MinVolumeSizeBytes);
    }

    /// <summary>
    /// Estima cuántas partes saldrán al dividir <paramref name="totalBytes"/> en volúmenes de
    /// <paramref name="volumeSizeBytes"/>. Solo para mostrar información en la UI (el tamaño real
    /// comprimido puede ser menor). Devuelve al menos 1.
    /// </summary>
    public static int EstimatePartCount(long totalBytes, long volumeSizeBytes)
    {
        if (volumeSizeBytes <= 0 || totalBytes <= 0)
        {
            return 1;
        }

        return (int)Math.Max(1, (totalBytes + volumeSizeBytes - 1) / volumeSizeBytes);
    }

    /// <summary>
    /// Valida un tamaño de volumen antes de comprimir. Lanza <see cref="ArgumentOutOfRangeException"/>
    /// con un mensaje claro si es demasiado pequeño.
    /// </summary>
    public static void EnsureValidVolumeSize(long volumeSizeBytes)
    {
        if (volumeSizeBytes < MinVolumeSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(volumeSizeBytes),
                $"El tamaño por parte es demasiado pequeño (mínimo {MinVolumeSizeBytes / BytesPerMB} MB).");
        }
    }
}
