using UltraArchive.Core.Models;

namespace UltraArchive.Archives.Common;

/// <summary>
/// Traduce el <see cref="CompressionLevel"/> elegido por el usuario al nivel numérico Deflate (0-9)
/// que usan los escritores de ZIP y GZIP de SharpCompress.
///
/// Limitación real y documentada: el escritor de 7Z de SharpCompress (<c>SevenZipWriterOptions</c>)
/// no expone niveles de compresión variables para LZMA2 en esta versión (su propiedad
/// <c>CompressionLevel</c> está reservada y no se usa todavía) — por eso <see cref="SevenZip.SevenZipArchiveWriter"/>
/// siempre comprime con los ajustes LZMA2 por defecto, sea cual sea el nivel elegido en la UI.
/// TAR sin compresión tampoco tiene "nivel" propio: solo importa cuando se combina con GZIP.
/// </summary>
internal static class CompressionLevelMapper
{
    public static int ToDeflateLevel(CompressionLevel level) => level switch
    {
        CompressionLevel.Store => 0,
        CompressionLevel.Fastest => 1,
        CompressionLevel.Fast => 3,
        CompressionLevel.Normal => 6,
        CompressionLevel.Maximum => 9,
        CompressionLevel.Ultra => 9,
        _ => 6,
    };
}
