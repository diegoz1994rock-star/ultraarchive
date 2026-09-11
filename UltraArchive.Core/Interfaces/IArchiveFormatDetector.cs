using UltraArchive.Core.Models;

namespace UltraArchive.Core.Interfaces;

/// <summary>
/// Detecta el formato real de un archivo. La detección por firma de bytes (magic numbers) es la
/// autoritativa; la detección por extensión es solo un mecanismo de apoyo/fallback, porque un
/// archivo puede tener la extensión cambiada o ser ambigua (p. ej. ".tar.gz").
/// </summary>
public interface IArchiveFormatDetector
{
    /// <summary>Detecta el formato leyendo la ruta indicada del disco (firma de bytes + fallback a extensión).</summary>
    Task<ArchiveFormat> DetectAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detecta el formato a partir del contenido de un stream ya abierto, inspeccionando sus primeros bytes
    /// (y, si el stream permite Seek, también offsets más profundos para TAR e ISO9660).
    /// No cierra ni consume el stream: al terminar deja la posición donde estaba al empezar si es seekable.
    /// </summary>
    Task<ArchiveFormat> DetectFromContentAsync(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>Detección basada únicamente en la extensión del nombre de archivo. Usar solo como fallback.</summary>
    ArchiveFormat DetectFromExtension(string filePath);
}
