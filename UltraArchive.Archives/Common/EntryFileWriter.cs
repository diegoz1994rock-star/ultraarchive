namespace UltraArchive.Archives.Common;

/// <summary>
/// Escribe el contenido de una entrada de archivo a su fichero de destino de forma segura: si la copia
/// falla o se cancela a mitad, borra el fichero parcial en vez de dejar en disco datos incompletos
/// (que el usuario podría confundir con una extracción correcta). Compartido por todos los lectores.
/// </summary>
internal static class EntryFileWriter
{
    public static async Task WriteAsync(Stream source, string destinationPath, Action<long> onBytesCopied, CancellationToken cancellationToken)
    {
        try
        {
            await using var fileStream = File.Create(destinationPath);
            await StreamCopy.CopyWithProgressAsync(source, fileStream, onBytesCopied, cancellationToken).ConfigureAwait(false);
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
            // best-effort: si el fichero parcial queda bloqueado, no empeorar el error original.
        }
    }
}
