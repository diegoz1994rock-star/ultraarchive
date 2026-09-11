namespace UltraArchive.Interop.SevenZip;

/// <summary>Por qué está (o no está) disponible el <c>7zr.exe</c> bundleado.</summary>
public enum SevenZipToolStatus
{
    /// <summary>Localizado, hash verificado y versión compatible: se puede usar.</summary>
    Available,

    /// <summary>No existe <c>&lt;app&gt;/tools/7zr.exe</c>. (Estado normal hasta que se bundlee el binario.)</summary>
    NotFound,

    /// <summary>El SHA-256 esperado aún no está fijado (<see cref="SevenZipToolReference.ExpectedSha256"/> vacío).</summary>
    NotConfigured,

    /// <summary>El fichero existe pero su SHA-256 no coincide con el esperado. <b>No se ejecuta.</b></summary>
    HashMismatch,

    /// <summary>El fichero pasó el hash pero no responde con un banner de 7-Zip reconocible.</summary>
    InvalidExecutable,

    /// <summary>El banner es de 7-Zip pero la versión es anterior a la mínima soportada.</summary>
    IncompatibleVersion,
}

/// <summary>
/// Resultado de <see cref="SevenZipLocator.Locate"/>. Nunca lanza por los casos esperados de arriba:
/// el llamador (y la UI) consultan <see cref="Status"/> / <see cref="IsAvailable"/>.
/// </summary>
public sealed class SevenZipLocateResult
{
    private SevenZipLocateResult(SevenZipToolStatus status, string expectedPath, SevenZipToolInfo? tool, string message)
    {
        Status = status;
        ExpectedPath = expectedPath;
        Tool = tool;
        Message = message;
    }

    public SevenZipToolStatus Status { get; }

    /// <summary>Ruta fija donde se buscó el binario (útil para diagnóstico y para mensajes de UI).</summary>
    public string ExpectedPath { get; }

    /// <summary>El binario verificado, solo cuando <see cref="Status"/> es <see cref="SevenZipToolStatus.Available"/>.</summary>
    public SevenZipToolInfo? Tool { get; }

    /// <summary>Mensaje legible (para logs y UI). Nunca contiene datos sensibles.</summary>
    public string Message { get; }

    /// <summary>True solo si el binario está listo para usarse.</summary>
    public bool IsAvailable => Status == SevenZipToolStatus.Available;

    internal static SevenZipLocateResult Available(string expectedPath, SevenZipToolInfo tool) =>
        new(SevenZipToolStatus.Available, expectedPath, tool,
            $"7zr.exe {tool.Version} verificado en '{expectedPath}'.");

    internal static SevenZipLocateResult NotFound(string expectedPath) =>
        new(SevenZipToolStatus.NotFound, expectedPath, null,
            $"No se encontró el binario 7zr.exe en '{expectedPath}'. La creación de 7Z cifrado no está disponible.");

    internal static SevenZipLocateResult NotConfigured(string expectedPath) =>
        new(SevenZipToolStatus.NotConfigured, expectedPath, null,
            "El hash SHA-256 del 7zr.exe bundleado aún no está fijado; la creación de 7Z cifrado se habilitará al empaquetar (Fase 8).");

    internal static SevenZipLocateResult HashMismatch(string expectedPath) =>
        new(SevenZipToolStatus.HashMismatch, expectedPath, null,
            $"El binario '{expectedPath}' no coincide con el 7zr.exe esperado (SHA-256 distinto). No se ejecutará por seguridad.");

    internal static SevenZipLocateResult InvalidExecutable(string expectedPath, string detail) =>
        new(SevenZipToolStatus.InvalidExecutable, expectedPath, null,
            $"'{expectedPath}' no respondió como un 7-Zip válido: {detail}");

    internal static SevenZipLocateResult IncompatibleVersion(string expectedPath, Version found, Version minimum) =>
        new(SevenZipToolStatus.IncompatibleVersion, expectedPath, null,
            $"7zr.exe {found} es anterior a la versión mínima soportada ({minimum}).");
}
