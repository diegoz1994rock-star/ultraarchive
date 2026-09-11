using UltraArchive.Core.Models;

namespace UltraArchive.Core.Interfaces;

/// <summary>
/// Pregunta al usuario qué hacer cuando, al extraer, ya existe un fichero en el destino. La
/// implementación de UI (WPF) vive en UltraArchive.App; los motores nunca la conocen: reciben el
/// resultado como un simple <c>Func&lt;string, CollisionResolution&gt;</c> en <see cref="ExtractOptions.OnCollision"/>.
/// </summary>
public interface ICollisionPrompt
{
    /// <summary>
    /// Muestra el diálogo de conflicto para <paramref name="existingFilePath"/> y devuelve la
    /// decisión. Puede invocarse desde un hilo distinto al de UI: la implementación debe marshalizar.
    /// </summary>
    CollisionResolution Resolve(string existingFilePath);
}
