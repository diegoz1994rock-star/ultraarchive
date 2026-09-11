namespace UltraArchive.Iso;

/// <summary>
/// Copia el contenido de un fichero de una imagen ISO a su destino en disco de forma segura: progreso
/// incremental, cancelación real, y borrado del fichero parcial si la copia falla o se cancela a mitad
/// (para no dejar datos incompletos que parezcan una extracción correcta).
///
/// Equivalente al <c>EntryFileWriter</c> de <c>UltraArchive.Archives</c>; se mantiene una copia propia
/// aquí a propósito para no crear una dependencia entre <c>UltraArchive.Iso</c> y
/// <c>UltraArchive.Archives</c> ni tocar código ya estable de fases anteriores.
/// </summary>
internal static class IsoEntryExtractor
{
    private const int BufferSize = 81920;

    public static async Task WriteAsync(Stream source, string destinationPath, Action<long> onBytesCopied, CancellationToken cancellationToken)
    {
        try
        {
            await using var fileStream = File.Create(destinationPath);

            var buffer = new byte[BufferSize];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                total += read;
                onBytesCopied(total);
            }
        }
        catch
        {
            TryDelete(destinationPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort: no empeorar el error original si el fichero parcial queda bloqueado.
        }
    }
}
