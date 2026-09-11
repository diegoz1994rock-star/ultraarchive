using UltraArchive.Core.Models;

namespace UltraArchive.Core.Interfaces;

/// <summary>Motor de creación de archivos para un formato concreto que soporte escritura.</summary>
public interface IArchiveWriter
{
    /// <summary>Formato que este escritor sabe crear.</summary>
    ArchiveFormat Format { get; }

    /// <summary>Crea un nuevo archivo comprimido a partir de las opciones indicadas.</summary>
    Task CreateAsync(CreateArchiveOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Capacidad adicional opcional para formatos que permiten modificar un archivo ya existente
/// sin recrearlo por completo (p. ej. ZIP). Los formatos que no lo permitan (7Z de solo streaming,
/// TAR.GZ, RAR de solo lectura) simplemente no implementan esta interfaz: la UI comprueba con
/// "is IMutableArchiveWriter" si puede ofrecer "Añadir"/"Eliminar" sobre un archivo existente.
/// </summary>
public interface IMutableArchiveWriter : IArchiveWriter
{
    /// <summary>Añade archivos/carpetas a un archivo ya existente.</summary>
    Task AddEntriesAsync(string archivePath, IReadOnlyList<string> sourcePaths, string? password,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Elimina entradas (por su FullPath) de un archivo ya existente.</summary>
    Task DeleteEntriesAsync(string archivePath, IReadOnlyList<string> entryPaths,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
}
