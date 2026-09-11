namespace UltraArchive.Tests.TestSupport;

/// <summary>
/// Carpeta temporal aislada por test (borrada al hacer Dispose) con helpers para crear archivos de
/// muestra. Usada por las pruebas de integración de los motores de UltraArchive.Archives, que
/// necesitan archivos reales en disco para comprimir/extraer (no solo streams en memoria).
/// </summary>
internal sealed class TempWorkspace : IDisposable
{
    public string RootPath { get; }

    public string SourceDir { get; }

    public string OutputDir { get; }

    public TempWorkspace()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "ultraarchive-tests-" + Guid.NewGuid().ToString("N"));
        SourceDir = Path.Combine(RootPath, "source");
        OutputDir = Path.Combine(RootPath, "output");
        Directory.CreateDirectory(SourceDir);
        Directory.CreateDirectory(OutputDir);
    }

    public string ArchivePath(string fileName) => Path.Combine(RootPath, fileName);

    public string CreateSampleFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(SourceDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(RootPath, recursive: true);
        }
        catch
        {
            // Limpieza best-effort: si algún handle sigue abierto no debe hacer fallar el test.
        }
    }
}
