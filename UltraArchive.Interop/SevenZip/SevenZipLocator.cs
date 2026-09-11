using System.Security.Cryptography;

namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Localiza y verifica el <c>7zr.exe</c> que UltraArchive usará para crear archivos 7Z cifrados
/// (Fase 6A, Paso 1).
///
/// Reglas estrictas:
///   - <b>Ruta fija</b>: <c>&lt;directorio de la aplicación&gt;/tools/7zr.exe</c>. Nunca se busca en el
///     PATH ni se usa un 7-Zip instalado por el usuario.
///   - <b>Hash primero</b>: si el SHA-256 del fichero no coincide con el esperado, se devuelve
///     <see cref="SevenZipToolStatus.HashMismatch"/> y <b>no se ejecuta el binario</b> bajo ningún concepto.
///   - Solo tras verificar el hash se lanza el ejecutable (una vez, sin operación de archivo) para
///     leer su banner y comprobar la versión.
///   - Ningún caso esperado lanza excepción: el llamador consulta <see cref="SevenZipLocateResult"/>.
/// </summary>
public sealed class SevenZipLocator
{
    private readonly string _applicationDirectory;
    private readonly string _expectedSha256;
    private readonly Version _minimumVersion;
    private readonly ISevenZipBannerReader _bannerReader;

    /// <summary>Configuración de producción: binario junto a la aplicación, hash y versión del catálogo.</summary>
    public SevenZipLocator()
        : this(AppContext.BaseDirectory,
               SevenZipToolReference.ExpectedSha256,
               SevenZipToolReference.MinimumVersion,
               new ProcessBannerReader())
    {
    }

    /// <summary>Sobrecarga para pruebas / escenarios controlados.</summary>
    public SevenZipLocator(string applicationDirectory, string expectedSha256, Version minimumVersion, ISevenZipBannerReader bannerReader)
    {
        _applicationDirectory = applicationDirectory ?? throw new ArgumentNullException(nameof(applicationDirectory));
        _expectedSha256 = (expectedSha256 ?? string.Empty).Trim();
        _minimumVersion = minimumVersion ?? throw new ArgumentNullException(nameof(minimumVersion));
        _bannerReader = bannerReader ?? throw new ArgumentNullException(nameof(bannerReader));
    }

    /// <summary>Ruta absoluta, fija, donde debe estar el binario. Se calcula sin tocar el disco.</summary>
    public string ExpectedExecutablePath => Path.GetFullPath(Path.Combine(
        _applicationDirectory,
        SevenZipToolReference.ToolsDirectoryName,
        SevenZipToolReference.ExecutableFileName));

    /// <summary>Localiza y verifica el binario. No lanza para los casos esperados.</summary>
    public SevenZipLocateResult Locate()
    {
        var path = ExpectedExecutablePath;

        if (!File.Exists(path))
        {
            return SevenZipLocateResult.NotFound(path);
        }

        if (_expectedSha256.Length == 0)
        {
            // Aún no se ha fijado el hash del binario a bundlear (Fase 8). No se ejecuta nada.
            return SevenZipLocateResult.NotConfigured(path);
        }

        string actualHash;
        try
        {
            actualHash = ComputeSha256Hex(path);
        }
        catch (IOException)
        {
            return SevenZipLocateResult.InvalidExecutable(path, "no se pudo leer el fichero para calcular su hash.");
        }
        catch (UnauthorizedAccessException)
        {
            return SevenZipLocateResult.InvalidExecutable(path, "sin permiso para leer el fichero.");
        }

        if (!string.Equals(actualHash, _expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            // Hash distinto: NUNCA se ejecuta el binario.
            return SevenZipLocateResult.HashMismatch(path);
        }

        var banner = _bannerReader.ReadBanner(path);
        if (!SevenZipBanner.TryParseVersion(banner, out var version))
        {
            return SevenZipLocateResult.InvalidExecutable(path, "no imprimió un banner de 7-Zip reconocible.");
        }

        if (version < _minimumVersion)
        {
            return SevenZipLocateResult.IncompatibleVersion(path, version, _minimumVersion);
        }

        return SevenZipLocateResult.Available(path, new SevenZipToolInfo(path, version));
    }

    internal static string ComputeSha256Hex(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}
