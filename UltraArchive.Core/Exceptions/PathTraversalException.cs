namespace UltraArchive.Core.Exceptions;

/// <summary>
/// Se lanza cuando una entrada del archivo no se puede extraer de forma segura al destino elegido:
/// bien porque intenta escribir fuera de la carpeta de destino (Zip Slip / path traversal, p. ej.
/// "../../Windows/System32/x.dll"), bien porque su nombre no es válido/seguro en Windows (caracteres
/// prohibidos, nombres de dispositivo reservados como "CON", segmentos con punto o espacio final).
/// La extracción de esa entrada se cancela y se reporta; nunca se escribe fuera del destino ni con
/// un nombre peligroso.
/// </summary>
public class PathTraversalException : ArchiveException
{
    public string EntryPath { get; }

    public PathTraversalException(string entryPath)
        : this(entryPath, "intenta escribir fuera de la carpeta de destino")
    {
    }

    public PathTraversalException(string entryPath, string reason)
        : base($"La entrada '{entryPath}' {reason} y ha sido bloqueada por seguridad.",
               ArchiveErrorCategory.PathTraversalBlocked)
    {
        EntryPath = entryPath;
    }
}

/// <summary>
/// Caso concreto de <see cref="PathTraversalException"/>: el nombre de la entrada no es válido en
/// Windows (carácter prohibido, nombre de dispositivo reservado, o segmento con punto/espacio final).
/// </summary>
public sealed class UnsafeEntryNameException : PathTraversalException
{
    public UnsafeEntryNameException(string entryPath, string reason)
        : base(entryPath, reason)
    {
    }
}
