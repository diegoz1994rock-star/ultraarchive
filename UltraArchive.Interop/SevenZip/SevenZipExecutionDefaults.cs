namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Valores centralizados de la ejecución de 7zr, para poder ajustarlos sin tocar la arquitectura.
/// </summary>
public static class SevenZipExecutionDefaults
{
    /// <summary>
    /// Watchdog de <b>inactividad</b>: si 7zr sigue vivo pero durante este tiempo no reporta ningún
    /// progreso válido (un porcentaje 0-100), se detiene el proceso. NO es un timeout global de
    /// duración total.
    /// </summary>
    public static readonly TimeSpan InactivityLimit = TimeSpan.FromMinutes(10);

    /// <summary>Cada cuánto el orquestador comprueba si el proceso terminó / si el watchdog debe disparar.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Máximo de texto (stdout+stderr) que se conserva para diagnóstico. Nunca contiene la contraseña.</summary>
    public const int MaxDiagnosticChars = 8192;
}
