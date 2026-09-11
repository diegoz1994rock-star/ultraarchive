namespace UltraArchive.Core.Models;

/// <summary>
/// Instantánea del progreso de una operación larga (extraer, comprimir, verificar, crear ISO).
/// Se reporta a la UI vía <see cref="IProgress{T}"/> para no bloquear el hilo de interfaz.
/// </summary>
public sealed class OperationProgress
{
    /// <summary>Nombre de la entrada que se está procesando en este instante, si aplica.</summary>
    public string? CurrentEntryName { get; init; }

    public long BytesProcessed { get; init; }

    public long TotalBytes { get; init; }

    /// <summary>Tiempo transcurrido desde el inicio de la operación.</summary>
    public TimeSpan Elapsed { get; init; }

    public double PercentComplete =>
        TotalBytes <= 0 ? 0 : Math.Clamp(BytesProcessed * 100.0 / TotalBytes, 0, 100);

    public double BytesPerSecond =>
        Elapsed.TotalSeconds > 0 ? BytesProcessed / Elapsed.TotalSeconds : 0;

    /// <summary>Tiempo restante estimado en base a la velocidad media hasta ahora, o null si aún no se puede calcular.</summary>
    public TimeSpan? EstimatedTimeRemaining
    {
        get
        {
            var speed = BytesPerSecond;
            if (speed <= 0 || TotalBytes <= 0)
            {
                return null;
            }

            var remainingBytes = Math.Max(0, TotalBytes - BytesProcessed);
            return TimeSpan.FromSeconds(remainingBytes / speed);
        }
    }
}
