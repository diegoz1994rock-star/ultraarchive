using System.Text;

namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Response file (<c>@listfile</c>) temporal con las rutas de origen para 7zr.exe.
///
/// - Contiene <b>solo</b> rutas de origen validadas, una por línea, en UTF-8 con BOM (7zr se invoca
///   con <c>-scsUTF-8</c>). Las líneas de un listfile de 7-Zip son siempre nombres de archivo,
///   nunca switches → una ruta como <c>C:\x\-mhe=off</c> se trata como fichero, no como opción.
/// - <b>Nunca</b> incluye la contraseña ni ningún switch.
/// - Nombre de fichero derivado de un GUID, nunca de datos del usuario.
/// - <see cref="IDisposable"/>: al liberar se borra del disco (limpieza preparada; la ejecución de
///   7zr se implementará en un paso posterior).
/// </summary>
public sealed class SevenZipResponseFile : IDisposable
{
    private static readonly char[] ForbiddenControlChars = { '\r', '\n', '\0' };

    private bool _disposed;

    private SevenZipResponseFile(string path) => Path = path;

    /// <summary>Ruta absoluta del fichero temporal (para pasar como <c>@&lt;Path&gt;</c>).</summary>
    public string Path { get; }

    /// <param name="sourcePaths">Rutas de origen validadas.</param>
    /// <param name="directory">
    /// Carpeta donde crear el fichero temporal. Null → carpeta temporal del sistema. (Se expone para
    /// que las pruebas puedan aislar el temporal; en producción se usa siempre <c>%TEMP%</c>.)
    /// </param>
    public static SevenZipResponseFile Create(IEnumerable<string> sourcePaths, string? directory = null)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var paths = sourcePaths.ToList();
        if (paths.Count == 0)
        {
            throw new ArgumentException("El response file no puede quedar vacío: no hay rutas de origen.", nameof(sourcePaths));
        }

        foreach (var path in paths)
        {
            SevenZipPathGuard.EnsureSafe(path, "ruta de origen");
            if (path.IndexOfAny(ForbiddenControlChars) >= 0)
            {
                // Redundante con EnsureSafe, pero explícito: una ruta con salto de línea inyectaría entradas.
                throw new ArgumentException("Una ruta de origen contiene un salto de línea.", nameof(sourcePaths));
            }
        }

        var targetDirectory = string.IsNullOrWhiteSpace(directory) ? System.IO.Path.GetTempPath() : directory;
        var fileName = $"uarsp-{Guid.NewGuid():N}.txt";
        var fullPath = System.IO.Path.Combine(targetDirectory, fileName);

        var content = string.Join("\r\n", paths) + "\r\n";
        File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        return new SevenZipResponseFile(fullPath);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
        catch
        {
            // Limpieza best-effort: no propagar un error de borrado.
        }
    }
}
