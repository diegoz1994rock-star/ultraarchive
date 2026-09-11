namespace UltraArchive.Core.Exceptions;

/// <summary>
/// Se lanza cuando se pide compresión dividida en volúmenes para un formato que no la soporta con las
/// librerías disponibles. Hoy solo <b>7Z</b> la soporta (vía <c>7zr.exe</c>): ZIP, TAR y GZIP no,
/// porque ni SharpZipLib ni SharpCompress saben escribir archivos divididos/spanned.
/// UltraArchive nunca "inventa" partes troceando el fichero final a mano.
/// </summary>
public sealed class SplitNotSupportedException : ArchiveException
{
    public SplitNotSupportedException(string message)
        : base(message, ArchiveErrorCategory.UnsupportedFormat)
    {
    }
}
