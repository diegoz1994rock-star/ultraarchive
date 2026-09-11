namespace UltraArchive.Core.Models;

/// <summary>Opciones elegidas por el usuario en el diálogo "Comprimir" para crear un nuevo archivo.</summary>
public sealed class CreateArchiveOptions
{
    /// <summary>Rutas absolutas de los archivos y/o carpetas de origen a comprimir.</summary>
    public required IReadOnlyList<string> SourcePaths { get; init; }

    /// <summary>Ruta absoluta de salida del archivo resultante.</summary>
    public required string OutputPath { get; init; }

    /// <summary>Formato del archivo a crear.</summary>
    public required ArchiveFormat Format { get; init; }

    public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Normal;

    /// <summary>Contraseña en texto plano proporcionada por el usuario para esta operación únicamente. Nunca se persiste.</summary>
    public string? Password { get; init; }

    public EncryptionMethod Encryption { get; init; } = EncryptionMethod.None;

    /// <summary>Si además de cifrar el contenido se deben cifrar los nombres de archivo (cuando el formato lo soporte).</summary>
    public bool EncryptFileNames { get; init; }

    /// <summary>Tamaño máximo de cada volumen en bytes para dividir en partes, o null para no dividir.</summary>
    public long? SplitVolumeSizeBytes { get; init; }

    /// <summary>Si se deben eliminar los archivos de origen tras comprimir correctamente. Requiere confirmación explícita en la UI.</summary>
    public bool DeleteSourceAfterCompress { get; init; }

    /// <summary>Si se debe conservar la estructura de carpetas de origen dentro del archivo.</summary>
    public bool PreserveFolderStructure { get; init; } = true;
}
