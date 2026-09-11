using UltraArchive.Core.Exceptions;

namespace UltraArchive.Security;

/// <summary>
/// Defensa contra "bombas de descompresión": archivos pequeños que declaran (o producen) un contenido
/// descomprimido enorme, capaz de agotar el disco o la memoria del equipo.
///
/// Se comprueba <b>antes</b> de empezar a extraer, con los tamaños declarados en las cabeceras
/// (rápido, sin descomprimir nada), y opcionalmente <b>durante</b> la extracción con
/// <see cref="ThrowIfEntryExceedsLimit"/> por si una entrada miente sobre su tamaño.
///
/// Los límites son deliberadamente holgados: buscan frenar un 42.zip, no un backup legítimo grande.
/// </summary>
public static class DecompressionGuard
{
    /// <summary>Ratio máximo (bytes descomprimidos / bytes comprimidos) que se considera legítimo.</summary>
    public const long MaxCompressionRatio = 1000;

    /// <summary>Por debajo de este tamaño total declarado no se aplica el control de ratio (archivos pequeños).</summary>
    public const long RatioCheckThresholdBytes = 256L * 1024 * 1024; // 256 MiB

    /// <summary>Tope absoluto de contenido descomprimido total. 0 = sin tope.</summary>
    public const long MaxTotalUncompressedBytes = 0;

    /// <summary>
    /// Comprueba, con los tamaños de cabecera, si el archivo parece una bomba de descompresión.
    /// </summary>
    /// <param name="totalCompressedBytes">Suma del tamaño comprimido de las entradas de archivo.</param>
    /// <param name="totalUncompressedBytes">Suma del tamaño descomprimido declarado de las entradas de archivo.</param>
    public static void ThrowIfArchiveLooksLikeBomb(long totalCompressedBytes, long totalUncompressedBytes)
    {
        if (MaxTotalUncompressedBytes > 0 && totalUncompressedBytes > MaxTotalUncompressedBytes)
        {
            throw new DecompressionBombException(
                $"El archivo declara {Format(totalUncompressedBytes)} de contenido, por encima del límite de seguridad " +
                $"({Format(MaxTotalUncompressedBytes)}). Extracción cancelada.");
        }

        if (totalCompressedBytes > 0 &&
            totalUncompressedBytes >= RatioCheckThresholdBytes &&
            totalUncompressedBytes / totalCompressedBytes > MaxCompressionRatio)
        {
            throw new DecompressionBombException(
                $"El archivo se expande {totalUncompressedBytes / Math.Max(1, totalCompressedBytes)}× " +
                $"(de {Format(totalCompressedBytes)} a {Format(totalUncompressedBytes)}), un ratio propio de una bomba " +
                "de descompresión. Extracción cancelada.");
        }
    }

    /// <summary>
    /// Comprueba durante la extracción que una entrada no supere su tamaño declarado en más de un
    /// margen razonable (por si la cabecera mentía). <paramref name="declaredSize"/> 0 o negativo
    /// desactiva la comprobación de esa entrada.
    /// </summary>
    public static void ThrowIfEntryExceedsLimit(string entryName, long declaredSize, long actualBytesSoFar)
    {
        if (declaredSize <= 0)
        {
            return;
        }

        // 64 KiB de margen para diferencias de padding/relleno entre formatos.
        var ceiling = declaredSize + 64L * 1024;
        if (actualBytesSoFar > ceiling)
        {
            throw new DecompressionBombException(
                $"La entrada '{entryName}' produce más datos ({Format(actualBytesSoFar)}) de los que declara " +
                $"({Format(declaredSize)}). Extracción cancelada por seguridad.");
        }
    }

    /// <summary>
    /// Control de ratio <b>incremental</b> para formatos que no exponen el tamaño descomprimido total
    /// por adelantado (p. ej. <c>.tar.gz</c>): se van sumando los bytes realmente descomprimidos y se
    /// aborta si el ratio respecto al tamaño comprimido supera <see cref="MaxCompressionRatio"/> una
    /// vez pasado <see cref="RatioCheckThresholdBytes"/>.
    /// </summary>
    public sealed class RatioGuard
    {
        private readonly long _compressedBytes;
        private readonly long _ratioThresholdBytes;
        private readonly long _maxRatio;

        public RatioGuard(long compressedBytes, long? ratioThresholdBytes = null, long? maxRatio = null)
        {
            _compressedBytes = compressedBytes;
            _ratioThresholdBytes = ratioThresholdBytes ?? RatioCheckThresholdBytes;
            _maxRatio = maxRatio ?? MaxCompressionRatio;
        }

        /// <summary>Comprueba el total descomprimido acumulado hasta ahora. Lanza si parece una bomba.</summary>
        public void Check(long totalUncompressedSoFar, string context)
        {
            if (_compressedBytes > 0 &&
                totalUncompressedSoFar >= _ratioThresholdBytes &&
                totalUncompressedSoFar / _compressedBytes > _maxRatio)
            {
                throw new DecompressionBombException(
                    $"El contenido de '{context}' se expande {totalUncompressedSoFar / Math.Max(1, _compressedBytes)}× " +
                    $"(ya {Format(totalUncompressedSoFar)} desde {Format(_compressedBytes)}), un ratio propio de una bomba " +
                    "de descompresión. Extracción cancelada.");
            }
        }
    }

    private static string Format(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.#} GiB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.#} MiB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):0.#} KiB",
        _ => $"{bytes} B",
    };
}
