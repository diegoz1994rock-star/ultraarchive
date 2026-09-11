namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Todo lo que <see cref="ISevenZipCli.ExecuteAsync"/> necesita para lanzar una operación ya validada.
/// El <c>ISevenZipCli</c> se encarga internamente de crear el response file, construir los argumentos
/// (con un <c>.7z</c> temporal), ejecutar 7zr y limpiar. No se pasan argumentos "en crudo".
/// </summary>
public sealed class SevenZipExecutionRequest
{
    /// <summary>Ruta absoluta al 7zr.exe ya localizado y verificado por <see cref="SevenZipLocator"/>.</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>
    /// Ruta esperada del ejecutable (la que devolvió <see cref="SevenZipLocateResult.Tool"/>). Si se
    /// indica y no coincide con <see cref="ExecutablePath"/>, la ejecución se rechaza.
    /// </summary>
    public string? ExpectedExecutablePath { get; init; }

    /// <summary>Petición de compresión validada (Paso 2). Su <c>OutputPath</c> es el destino final.</summary>
    public required SevenZipRequest Compression { get; init; }

    /// <summary>Callback de progreso (0-100). Puede ser null. Sus excepciones se tragan.</summary>
    public IProgress<int>? Progress { get; init; }
}
