namespace UltraArchive.Core.Exceptions;

/// <summary>
/// Excepción base de todos los errores de dominio de UltraArchive relacionados con archivos comprimidos.
/// La UI nunca debe mostrar el mensaje técnico crudo de una excepción al usuario: debe traducirlo
/// a un mensaje claro (ver <see cref="Category"/>) y registrar los detalles técnicos en el log.
/// </summary>
public class ArchiveException : Exception
{
    /// <summary>Categoría del error, usada por la capa de presentación para elegir el mensaje amigable a mostrar.</summary>
    public ArchiveErrorCategory Category { get; }

    public ArchiveException(string message, ArchiveErrorCategory category = ArchiveErrorCategory.Unknown)
        : base(message)
    {
        Category = category;
    }

    public ArchiveException(string message, Exception innerException, ArchiveErrorCategory category = ArchiveErrorCategory.Unknown)
        : base(message, innerException)
    {
        Category = category;
    }
}

/// <summary>Categorías de error de alto nivel, independientes del formato, usadas para generar mensajes de usuario.</summary>
public enum ArchiveErrorCategory
{
    Unknown,
    WrongPassword,
    CorruptedArchive,
    UnsupportedFormat,
    InsufficientPermissions,
    FileInUse,
    PathTraversalBlocked,
    OperationCancelled,
    DiskSpace
}
