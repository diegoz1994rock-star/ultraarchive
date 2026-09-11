namespace UltraArchive.Interop.SevenZip;

/// <summary>Estado con el que terminó una ejecución de 7zr.</summary>
public enum SevenZipExecutionStatus
{
    /// <summary>7zr terminó con código 0.</summary>
    Success,

    /// <summary>7zr terminó con código 1: el archivo se creó pero con advertencias (p. ej. un fichero bloqueado).</summary>
    Warning,

    /// <summary>7zr falló (código 2/7/8, código desconocido, no se pudo iniciar, error inesperado, o watchdog).</summary>
    Failed,

    /// <summary>El usuario canceló la operación (<see cref="System.Threading.CancellationToken"/>), o 7zr devolvió 255.</summary>
    Cancelled,
}

/// <summary>
/// Resultado de <see cref="ISevenZipCli.ExecuteAsync"/>. Toda la información textual es <b>segura</b>:
/// nunca contiene la contraseña, ni la lista de argumentos sin redactar, ni datos sensibles.
/// </summary>
public sealed class SevenZipExecutionResult
{
    private SevenZipExecutionResult(
        SevenZipExecutionStatus status,
        int? exitCode,
        bool timedOut,
        int progressReached,
        string? errorMessage,
        string? warningMessage,
        string? outputArchivePath)
    {
        Status = status;
        ExitCode = exitCode;
        TimedOut = timedOut;
        ProgressReached = progressReached;
        ErrorMessage = errorMessage;
        WarningMessage = warningMessage;
        OutputArchivePath = outputArchivePath;
    }

    public SevenZipExecutionStatus Status { get; }

    /// <summary>Código de salida de 7zr, o null si no llegó a terminar (no se pudo iniciar, watchdog…).</summary>
    public int? ExitCode { get; }

    /// <summary>True si el proceso terminó de forma limpia con éxito.</summary>
    public bool Succeeded => Status == SevenZipExecutionStatus.Success;

    /// <summary>True si el archivo se creó (con o sin advertencias) y está listo para publicarse.</summary>
    public bool CompletedWithArchive => Status is SevenZipExecutionStatus.Success or SevenZipExecutionStatus.Warning;

    /// <summary>True si la operación se canceló (petición del usuario o 7zr devolvió 255).</summary>
    public bool WasCancelled => Status == SevenZipExecutionStatus.Cancelled;

    /// <summary>True si se detuvo por el watchdog de inactividad (10 min sin progreso).</summary>
    public bool TimedOut { get; }

    /// <summary>Máximo porcentaje de progreso observado (0-100).</summary>
    public int ProgressReached { get; }

    /// <summary>Mensaje de error seguro (stderr de 7zr recortado y limpiado), o null.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Mensaje de advertencia seguro, o null.</summary>
    public string? WarningMessage { get; }

    /// <summary>
    /// Ruta del <c>.7z</c> temporal generado, presente solo cuando <see cref="CompletedWithArchive"/>.
    /// El paso posterior (writer) lo moverá al destino final. En cualquier otro caso el temporal ya
    /// se ha borrado y esto es null.
    /// </summary>
    public string? OutputArchivePath { get; }

    // --- fábricas internas ---

    internal static SevenZipExecutionResult ForSuccess(int progress, string outputArchivePath, string? warning) =>
        new(SevenZipExecutionStatus.Success, 0, timedOut: false, progress, errorMessage: null, warning, outputArchivePath);

    internal static SevenZipExecutionResult ForWarning(int progress, string outputArchivePath, string? warning) =>
        new(SevenZipExecutionStatus.Warning, 1, timedOut: false, progress, errorMessage: null, warning ?? "7zr terminó con advertencias.", outputArchivePath);

    internal static SevenZipExecutionResult ForFailure(int? exitCode, int progress, string? error) =>
        new(SevenZipExecutionStatus.Failed, exitCode, timedOut: false, progress, error ?? "7zr terminó con un error.", warningMessage: null, outputArchivePath: null);

    internal static SevenZipExecutionResult ForTimeout(int progress, string? error) =>
        new(SevenZipExecutionStatus.Failed, exitCode: null, timedOut: true, progress,
            error ?? $"7zr no reportó ningún progreso durante {SevenZipExecutionDefaults.InactivityLimit.TotalMinutes:0} minutos y se ha detenido.",
            warningMessage: null, outputArchivePath: null);

    internal static SevenZipExecutionResult ForCancellation(int? exitCode, int progress) =>
        new(SevenZipExecutionStatus.Cancelled, exitCode, timedOut: false, progress,
            errorMessage: null, warningMessage: null, outputArchivePath: null);
}
