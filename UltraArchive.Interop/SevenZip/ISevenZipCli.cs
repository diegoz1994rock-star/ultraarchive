namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Ejecuta una operación de 7zr ya validada, de forma controlada y aislada:
///   1. crea el response file con las rutas de origen;
///   2. construye los argumentos (con un <c>.7z</c> temporal en el directorio de destino);
///   3. arranca 7zr;
///   4. drena stdout/stderr, interpreta el progreso, vigila la inactividad (watchdog);
///   5. espera la finalización o cancela;
///   6. clasifica el resultado (Success / Warning / Failed / Cancelled);
///   7. borra el response file <b>siempre</b>, y el <c>.7z</c> temporal si no terminó con éxito.
///
/// No publica el archivo final: eso corresponde a un paso posterior (el writer moverá
/// <see cref="SevenZipExecutionResult.OutputArchivePath"/> al destino cuando el resultado sea válido).
/// </summary>
public interface ISevenZipCli
{
    Task<SevenZipExecutionResult> ExecuteAsync(SevenZipExecutionRequest request, CancellationToken cancellationToken = default);
}
