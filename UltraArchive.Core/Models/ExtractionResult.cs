namespace UltraArchive.Core.Models;

/// <summary>
/// Resultado de una operación de extracción. Se introduce en la Fase 2, al implementar la extracción
/// real, porque una vez hay protección contra Zip Slip (ver <c>UltraArchive.Security.PathSecurity</c>)
/// el llamador necesita saber si alguna entrada fue bloqueada por seguridad en lugar de que eso
/// desaparezca en silencio.
/// </summary>
public sealed class ExtractionResult
{
    /// <summary>Número de entradas extraídas correctamente.</summary>
    public int ExtractedCount { get; init; }

    /// <summary>
    /// Rutas (dentro del archivo) de entradas que NO se extrajeron porque su ruta intentaba escribir
    /// fuera de la carpeta de destino (Zip Slip / path traversal). La extracción del resto continúa con normalidad.
    /// </summary>
    public IReadOnlyList<string> BlockedEntries { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Rutas (dentro del archivo) de entradas que NO se extrajeron porque ya existía un fichero en el
    /// destino y la política de colisión (o el usuario) decidió saltárselas. No es un error.
    /// </summary>
    public IReadOnlyList<string> SkippedEntries { get; init; } = Array.Empty<string>();
}
