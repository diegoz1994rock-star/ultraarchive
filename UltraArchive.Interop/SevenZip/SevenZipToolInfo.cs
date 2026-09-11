namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Descriptor de un <c>7zr.exe</c> que ha sido <b>localizado y verificado con éxito</b>: la ruta es
/// la fija esperada, el SHA-256 coincide con el bundleado y el binario responde con un banner de
/// 7-Zip de versión compatible.
///
/// Solo se construye cuando <see cref="SevenZipLocateResult.Status"/> es
/// <see cref="SevenZipToolStatus.Available"/>. Nunca contiene la contraseña ni ningún dato sensible.
/// </summary>
public sealed class SevenZipToolInfo
{
    public SevenZipToolInfo(string executablePath, Version version)
    {
        ExecutablePath = executablePath;
        Version = version;
    }

    /// <summary>Ruta absoluta al <c>7zr.exe</c> verificado (<c>&lt;app&gt;/tools/7zr.exe</c>).</summary>
    public string ExecutablePath { get; }

    /// <summary>Versión de 7-Zip detectada en el banner del ejecutable.</summary>
    public Version Version { get; }
}
