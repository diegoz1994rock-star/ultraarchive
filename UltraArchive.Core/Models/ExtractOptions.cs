namespace UltraArchive.Core.Models;

/// <summary>Opciones elegidas por el usuario en el diálogo "Extraer".</summary>
public sealed class ExtractOptions
{
    /// <summary>Carpeta de destino absoluta. Ningún archivo extraído puede terminar fuera de esta ruta (ver protección Zip Slip).</summary>
    public required string DestinationPath { get; init; }

    /// <summary>Si se debe reproducir la estructura de carpetas del archivo o volcar todo en un único nivel.</summary>
    public bool PreserveStructure { get; init; } = true;

    public CollisionPolicy CollisionPolicy { get; init; } = CollisionPolicy.Ask;

    /// <summary>Contraseña proporcionada por el usuario, si el archivo está protegido.</summary>
    public string? Password { get; init; }

    /// <summary>
    /// Subconjunto de rutas (FullPath de <see cref="ArchiveEntry"/>) a extraer. Null o vacío significa "extraer todo".
    /// </summary>
    public IReadOnlyList<string>? EntriesToExtract { get; init; }

    /// <summary>
    /// Manejador de conflictos para <see cref="CollisionPolicy.Ask"/>: recibe la ruta absoluta del
    /// fichero que ya existe y devuelve qué hacer. Si es <c>null</c> con política <c>Ask</c>, se
    /// sobrescribe (comportamiento histórico). Lo invoca <see cref="Services.CollisionResolver"/>
    /// desde el hilo en el que corra el motor: la implementación de UI debe marshalizar al hilo de UI.
    /// </summary>
    public Func<string, CollisionResolution>? OnCollision { get; init; }
}
