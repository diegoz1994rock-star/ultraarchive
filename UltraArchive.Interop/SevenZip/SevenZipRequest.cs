using UltraArchive.Core.Models;

namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Petición validada para crear un archivo <b>7Z cifrado</b> mediante 7zr.exe (Fase 6A).
///
/// - La contraseña vive solo en memoria mientras dura la operación. <b>Nunca</b> se escribe en el
///   response file, ni en logs, ni en mensajes de excepción, ni en <see cref="ToString"/>.
/// - Todas las rutas se validan en <see cref="Create"/> con <see cref="SevenZipPathGuard"/>.
/// </summary>
public sealed class SevenZipRequest
{
    private SevenZipRequest(
        IReadOnlyList<string> sourcePaths,
        string outputPath,
        string password,
        CompressionLevel level,
        bool encryptHeaders,
        long? volumeSizeBytes)
    {
        SourcePaths = sourcePaths;
        OutputPath = outputPath;
        Password = password;
        Level = level;
        EncryptHeaders = encryptHeaders;
        VolumeSizeBytes = volumeSizeBytes;
    }

    /// <summary>Rutas absolutas de archivos y/o carpetas a comprimir. No vacía.</summary>
    public IReadOnlyList<string> SourcePaths { get; }

    /// <summary>Ruta absoluta del <c>.7z</c> resultante (normalizada).</summary>
    public string OutputPath { get; }

    /// <summary>Contraseña en claro. Solo para pasarla a <c>-p</c>; nunca se persiste ni se registra.</summary>
    public string Password { get; }

    public CompressionLevel Level { get; }

    /// <summary>Si además del contenido se debe cifrar la cabecera (lista de nombres) con <c>-mhe=on</c>.</summary>
    public bool EncryptHeaders { get; }

    /// <summary>
    /// Tamaño máximo de cada volumen en bytes (<c>-v&lt;n&gt;b</c>), o null para un único archivo.
    /// Cuando está fijado, 7zr genera <c>&lt;salida&gt;.001</c>, <c>.002</c>… en vez de un fichero único.
    /// </summary>
    public long? VolumeSizeBytes { get; }

    /// <summary>True si esta petición crea un archivo dividido en volúmenes.</summary>
    public bool IsSplit => VolumeSizeBytes is > 0;

    public static SevenZipRequest Create(
        IEnumerable<string> sourcePaths,
        string outputPath,
        string password,
        CompressionLevel level = CompressionLevel.Normal,
        bool encryptHeaders = false,
        long? volumeSizeBytes = null)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var sources = sourcePaths.ToList();
        if (sources.Count == 0)
        {
            throw new ArgumentException("Debe indicarse al menos una ruta de origen.", nameof(sourcePaths));
        }

        foreach (var source in sources)
        {
            SevenZipPathGuard.EnsureSafe(source, "ruta de origen");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("La ruta de salida no puede estar vacía.", nameof(outputPath));
        }

        // Validar la ruta TAL CUAL la da el usuario (antes de normalizar): Path.GetFullPath
        // resolvería "-salida.7z" contra el directorio actual y perdería el prefijo peligroso.
        SevenZipPathGuard.EnsureSafe(outputPath, "ruta de salida");
        var normalizedOutput = Path.GetFullPath(outputPath);
        SevenZipPathGuard.EnsureSafe(normalizedOutput, "ruta de salida");

        // La ruta 7zr.exe se usa para 7Z cifrado y/o dividido en volúmenes. Sin contraseña Y sin
        // división no hay motivo para pasar por aquí (se usa el motor gestionado).
        if (string.IsNullOrEmpty(password) && volumeSizeBytes is not > 0)
        {
            throw new ArgumentException(
                "7zr.exe solo se usa para 7Z cifrado o dividido en partes; sin ninguna de las dos cosas se usa el motor gestionado.",
                nameof(password));
        }

        if (password.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
        {
            // Mensaje deliberadamente genérico: NUNCA incluir el valor de la contraseña.
            throw new ArgumentException("La contraseña contiene caracteres de control no permitidos.", nameof(password));
        }

        if (!Enum.IsDefined(level))
        {
            throw new ArgumentException($"Nivel de compresión no válido: {(int)level}.", nameof(level));
        }

        if (volumeSizeBytes is <= 0)
        {
            throw new ArgumentException("El tamaño de volumen debe ser mayor que cero.", nameof(volumeSizeBytes));
        }

        return new SevenZipRequest(sources, normalizedOutput, password, level, encryptHeaders, volumeSizeBytes);
    }

    /// <summary>Representación segura (sin contraseña) para diagnóstico.</summary>
    public override string ToString() =>
        $"SevenZipRequest(origenes={SourcePaths.Count}, salida='{OutputPath}', nivel={Level}, mhe={EncryptHeaders}, " +
        $"volumen={(VolumeSizeBytes is { } v ? v + "B" : "no")}, contraseña={(Password.Length > 0 ? "***" : "(ninguna)")})";
}
