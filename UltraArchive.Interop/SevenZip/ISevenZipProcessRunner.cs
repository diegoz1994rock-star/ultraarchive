namespace UltraArchive.Interop.SevenZip;

/// <summary>Datos para lanzar 7zr. La lista de argumentos viene tal cual de <see cref="SevenZipArguments.ArgumentList"/>.</summary>
public sealed class SevenZipProcessStartInfo
{
    public required string ExecutablePath { get; init; }
    public required IReadOnlyList<string> ArgumentList { get; init; }
    public required string WorkingDirectory { get; init; }
}

/// <summary>Un fragmento de salida del proceso: texto de una línea de stdout o stderr.</summary>
public readonly record struct SevenZipOutputChunk(string Text, bool IsError);

/// <summary>
/// Abstracción sobre el proceso 7zr en ejecución. Permite sustituirlo por un doble en las pruebas
/// (no se ejecuta ningún 7zr real en los tests).
/// </summary>
public interface ISevenZipProcess : IDisposable
{
    /// <summary>Espera a que el proceso termine y devuelve su código de salida. Lanza si <paramref name="cancellationToken"/> se cancela.</summary>
    Task<int> WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>Mata el proceso y todo su árbol de hijos. Best-effort, no lanza.</summary>
    void KillTree();

    bool HasExited { get; }

    /// <summary>Código de salida si <see cref="HasExited"/>; si no, null.</summary>
    int? ExitCode { get; }
}

/// <summary>
/// Crea y arranca el proceso 7zr con las reglas obligatorias de <c>ProcessStartInfo</c>
/// (<c>UseShellExecute=false</c>, <c>CreateNoWindow=true</c>, stdout/stderr redirigidos y drenados de
/// forma <b>concurrente</b>). Nunca usa cmd/powershell/ShellExecute ni busca el ejecutable en el PATH.
/// </summary>
public interface ISevenZipProcessRunner
{
    /// <summary>
    /// Arranca 7zr. <paramref name="onOutput"/> se invoca (posiblemente en hilos de fondo) por cada
    /// línea de stdout/stderr según llega. Lanza <see cref="SevenZipProcessStartException"/> si el
    /// ejecutable no es válido o no se puede iniciar.
    /// </summary>
    ISevenZipProcess Start(SevenZipProcessStartInfo startInfo, Action<SevenZipOutputChunk> onOutput);
}

/// <summary>El proceso 7zr no se pudo iniciar (ejecutable inexistente/no válido, acceso denegado…).</summary>
public sealed class SevenZipProcessStartException : Exception
{
    public SevenZipProcessStartException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
