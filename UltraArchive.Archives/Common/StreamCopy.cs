namespace UltraArchive.Archives.Common;

/// <summary>Copia de streams por buffer, con progreso incremental y soporte de cancelación real (no cooperativa a medias).</summary>
internal static class StreamCopy
{
    public const int BufferSize = 81920;

    /// <summary>Copia todo <paramref name="source"/> a <paramref name="destination"/>, invocando <paramref name="onBytesCopied"/> tras cada bloque con el total acumulado copiado en esta llamada.</summary>
    public static async Task<long> CopyWithProgressAsync(
        Stream source,
        Stream destination,
        Action<long> onBytesCopied,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        long total = 0;
        int read;

        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
            onBytesCopied(total);
        }

        return total;
    }
}
