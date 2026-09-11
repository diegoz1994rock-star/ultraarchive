namespace UltraArchive.Core.Models;

/// <summary>Acción concreta a aplicar sobre un fichero que ya existe en el destino de la extracción.</summary>
public enum CollisionAction
{
    /// <summary>Sobrescribir el fichero existente.</summary>
    Overwrite,

    /// <summary>No extraer esta entrada; dejar el fichero existente intacto.</summary>
    Skip,

    /// <summary>Extraer con un nombre libre ("nombre (1).ext", "nombre (2).ext"…).</summary>
    Rename,

    /// <summary>Abortar toda la extracción.</summary>
    Cancel,
}

/// <summary>
/// Respuesta del usuario (o de una política) ante un conflicto de ficheros al extraer.
/// <see cref="ApplyToAll"/> hace que la misma acción se aplique automáticamente al resto de
/// conflictos de la operación en curso, sin volver a preguntar.
/// </summary>
public sealed record CollisionResolution(CollisionAction Action, bool ApplyToAll = false)
{
    public static readonly CollisionResolution Overwrite = new(CollisionAction.Overwrite);
    public static readonly CollisionResolution Skip = new(CollisionAction.Skip);
    public static readonly CollisionResolution Rename = new(CollisionAction.Rename);
    public static readonly CollisionResolution Cancel = new(CollisionAction.Cancel);
}
