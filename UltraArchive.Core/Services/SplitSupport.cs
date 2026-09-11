using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Models;

namespace UltraArchive.Core.Services;

/// <summary>
/// Única fuente de verdad sobre qué formatos admiten <b>compresión dividida en volúmenes</b> al crear,
/// según las librerías que UltraArchive ya usa:
///
///   - 7Z  → SÍ, mediante el <c>7zr.exe</c> verificado y su switch nativo <c>-v</c> (con o sin contraseña).
///   - ZIP → NO. SharpZipLib no escribe ZIP dividido/spanned; SharpCompress tampoco.
///   - TAR / GZIP → NO. No hay soporte de volúmenes en el backend.
///   - RAR → NO (no se crean archivos RAR).
///
/// La usan los escritores (para fallar antes de tocar disco) y la UI (para mostrar/ocultar la opción).
/// </summary>
public static class SplitSupport
{
    /// <summary>True si el formato admite dividir en volúmenes al crearlo con el backend actual.</summary>
    public static bool SupportsSplitOnCreate(ArchiveFormat format) => format == ArchiveFormat.SevenZip;

    /// <summary>True si <paramref name="options"/> pide dividir en volúmenes.</summary>
    public static bool WantsSplit(CreateArchiveOptions options) =>
        options.SplitVolumeSizeBytes is > 0;

    /// <summary>
    /// Valida la opción de división de <paramref name="options"/>. Sin efectos secundarios.
    ///   - Si no se pide división → no hace nada (comportamiento normal intacto).
    ///   - Si se pide para un formato que no la soporta → <see cref="SplitNotSupportedException"/>.
    ///   - Si el tamaño de volumen es absurdo → <see cref="SplitNotSupportedException"/>.
    /// </summary>
    public static void Validate(CreateArchiveOptions options)
    {
        if (!WantsSplit(options))
        {
            return;
        }

        if (!SupportsSplitOnCreate(options.Format))
        {
            throw new SplitNotSupportedException(
                $"La división en partes solo está disponible al crear archivos 7Z. " +
                $"El formato {options.Format.ToString().ToUpperInvariant()} no admite volúmenes con las librerías actuales. " +
                "Elige 7Z, o desactiva la división.");
        }

        try
        {
            SplitCalculator.EnsureValidVolumeSize(options.SplitVolumeSizeBytes!.Value);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new SplitNotSupportedException(ex.Message);
        }
    }
}
