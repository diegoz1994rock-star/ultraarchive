using UltraArchive.Archives.Gzip;
using UltraArchive.Core.Models;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Archives;

public class GZipArchiveEngineTests
{
    [Fact]
    public async Task CreateAsync_UnSoloArchivoSuelto_CreaGzipPlanoYSeExtraeIgual()
    {
        using var workspace = new TempWorkspace();
        var loosFile = workspace.CreateSampleFile("solo.txt", "contenido de un único archivo");
        var archivePath = workspace.ArchivePath("solo.txt.gz");

        await new GZipArchiveWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { loosFile },
            OutputPath = archivePath,
            Format = ArchiveFormat.GZip,
        });

        Assert.True(File.Exists(archivePath));

        using var reader = new GZipArchiveReader();
        await reader.OpenAsync(archivePath);

        var entries = await reader.GetEntriesAsync();
        Assert.Single(entries);

        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = workspace.OutputDir });
        Assert.Equal(1, result.ExtractedCount);

        Assert.Equal("contenido de un único archivo", await File.ReadAllTextAsync(Path.Combine(workspace.OutputDir, "solo.txt")));
        Assert.True(await reader.TestIntegrityAsync());
    }

    [Fact]
    public async Task CreateAsync_VariosOrigenes_CreaTarGzYSeExtraeConEstructura()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateSampleFile("readme.txt", "hola tar.gz");
        workspace.CreateSampleFile("sub/data.bin", "contenido binario tar.gz");
        var archivePath = workspace.ArchivePath("salida.tar.gz");

        await new GZipArchiveWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { workspace.SourceDir },
            OutputPath = archivePath,
            Format = ArchiveFormat.GZip,
        });

        Assert.True(File.Exists(archivePath));

        using var reader = new GZipArchiveReader();
        await reader.OpenAsync(archivePath);

        var entries = await reader.GetEntriesAsync();
        Assert.Equal(2, entries.Count(e => !e.IsDirectory));

        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = workspace.OutputDir });
        Assert.Equal(2, result.ExtractedCount);

        var sourceRootName = Path.GetFileName(workspace.SourceDir);
        Assert.Equal("hola tar.gz", await File.ReadAllTextAsync(Path.Combine(workspace.OutputDir, sourceRootName, "readme.txt")));
        Assert.Equal("contenido binario tar.gz", await File.ReadAllTextAsync(Path.Combine(workspace.OutputDir, sourceRootName, "sub", "data.bin")));
    }
}
