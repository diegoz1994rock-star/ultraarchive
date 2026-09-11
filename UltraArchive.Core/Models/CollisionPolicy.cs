namespace UltraArchive.Core.Models;

/// <summary>Qué hacer cuando, al extraer, ya existe un archivo con el mismo nombre en el destino.</summary>
public enum CollisionPolicy
{
    /// <summary>Preguntar al usuario para cada colisión.</summary>
    Ask,
    Overwrite,
    Skip,
    RenameAutomatically
}
