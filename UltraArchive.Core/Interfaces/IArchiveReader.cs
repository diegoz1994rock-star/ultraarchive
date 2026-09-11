using UltraArchive.Core.Models;

namespace UltraArchive.Core.Interfaces;

/// <summary>
/// Motor de lectura/exploración/extracción para un formato concreto (ZIP, 7Z, RAR, TAR, GZIP...).
/// Cada instancia representa un único archivo abierto: se crea vía <see cref="IArchiveEngineFactory"/>,
/// se abre con <see cref="OpenAsync"/> y se libera con <see cref="IDisposable.Dispose"/>.
/// </summary>
public interface IArchiveReader : IDisposable
{
    /// <summary>Formato que este lector sabe manejar.</summary>
    ArchiveFormat Format { get; }

    /// <summary>True si, tras abrir el archivo, se ha detectado que está protegido con contraseña.</summary>
    bool IsPasswordProtected { get; }

    /// <summary>
    /// Abre el archivo para su exploración. Si está protegido y no se indica <paramref name="password"/>,
    /// las implementaciones deben lanzar <see cref="Exceptions.InvalidPasswordException"/> al intentar leer
    /// contenido cifrado, para que el llamador pida la contraseña vía <see cref="IPasswordProvider"/> y reintente.
    /// </summary>
    Task OpenAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>Lista las entradas del archivo sin extraer su contenido.</summary>
    Task<IReadOnlyList<ArchiveEntry>> GetEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Extrae entradas al destino indicado en <paramref name="options"/>, validando cada ruta para impedir
    /// escrituras fuera de <see cref="ExtractOptions.DestinationPath"/> (Zip Slip / path traversal).
    /// Las entradas bloqueadas por esa validación se omiten y se reportan en el resultado en vez de
    /// abortar toda la operación.
    /// </summary>
    Task<ExtractionResult> ExtractAsync(ExtractOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica la integridad del archivo (p. ej. recalculando CRC32/checksums) sin escribir nada a disco.
    /// Devuelve true si todas las entradas verificadas son correctas.
    /// </summary>
    Task<bool> TestIntegrityAsync(IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
}
