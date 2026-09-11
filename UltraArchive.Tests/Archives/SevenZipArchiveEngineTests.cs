using UltraArchive.Archives.SevenZip;
using UltraArchive.Core.Models;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Archives;

public class SevenZipArchiveEngineTests
{
    [Fact]
    public async Task CreateAsync_LuegoExtractAsync_ReproduceElContenidoOriginal()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateSampleFile("readme.txt", "hola 7z");
        workspace.CreateSampleFile("sub/data.bin", "contenido binario 7z");
        var archivePath = workspace.ArchivePath("salida.7z");

        await new SevenZipArchiveWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { workspace.SourceDir },
            OutputPath = archivePath,
            Format = ArchiveFormat.SevenZip,
        });

        Assert.True(File.Exists(archivePath));

        using var reader = new SevenZipArchiveReader();
        await reader.OpenAsync(archivePath);

        var entries = await reader.GetEntriesAsync();
        Assert.Equal(2, entries.Count(e => !e.IsDirectory));

        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = workspace.OutputDir });
        Assert.Equal(2, result.ExtractedCount);
        Assert.Empty(result.BlockedEntries);

        var sourceRootName = Path.GetFileName(workspace.SourceDir);
        Assert.Equal("hola 7z", await File.ReadAllTextAsync(Path.Combine(workspace.OutputDir, sourceRootName, "readme.txt")));
        Assert.Equal("contenido binario 7z", await File.ReadAllTextAsync(Path.Combine(workspace.OutputDir, sourceRootName, "sub", "data.bin")));
    }

    [Fact]
    public async Task TestIntegrityAsync_ArchivoRecienCreado_DevuelveTrue()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateSampleFile("a.txt", "abc");
        var archivePath = workspace.ArchivePath("salida.7z");

        await new SevenZipArchiveWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { workspace.SourceDir },
            OutputPath = archivePath,
            Format = ArchiveFormat.SevenZip,
        });

        using var reader = new SevenZipArchiveReader();
        await reader.OpenAsync(archivePath);

        Assert.True(await reader.TestIntegrityAsync());
    }
}
