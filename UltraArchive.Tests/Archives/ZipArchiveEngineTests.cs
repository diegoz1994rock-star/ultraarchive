using System.IO.Compression;
using UltraArchive.Archives.Zip;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Models;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Archives;

public class ZipArchiveEngineTests
{
    [Fact]
    public async Task CreateAsync_LuegoExtractAsync_ReproduceElContenidoOriginal()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateSampleFile("readme.txt", "hola mundo");
        workspace.CreateSampleFile("sub/data.bin", "contenido binario simulado");
        var archivePath = workspace.ArchivePath("salida.zip");

        await new ZipArchiveWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { workspace.SourceDir },
            OutputPath = archivePath,
            Format = ArchiveFormat.Zip,
        });

        Assert.True(File.Exists(archivePath));

        using var reader = new ZipArchiveReader();
        await reader.OpenAsync(archivePath);

        var entries = await reader.GetEntriesAsync();
        Assert.Equal(2, entries.Count(e => !e.IsDirectory));

        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = workspace.OutputDir });
        Assert.Equal(2, result.ExtractedCount);
        Assert.Empty(result.BlockedEntries);

        var sourceRootName = Path.GetFileName(workspace.SourceDir);
        Assert.Equal("hola mundo", await File.ReadAllTextAsync(Path.Combine(workspace.OutputDir, sourceRootName, "readme.txt")));
        Assert.Equal("contenido binario simulado", await File.ReadAllTextAsync(Path.Combine(workspace.OutputDir, sourceRootName, "sub", "data.bin")));
    }

    [Fact]
    public async Task TestIntegrityAsync_ArchivoRecienCreado_DevuelveTrue()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateSampleFile("a.txt", "abc");
        var archivePath = workspace.ArchivePath("salida.zip");

        await new ZipArchiveWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { workspace.SourceDir },
            OutputPath = archivePath,
            Format = ArchiveFormat.Zip,
        });

        using var reader = new ZipArchiveReader();
        await reader.OpenAsync(archivePath);

        Assert.True(await reader.TestIntegrityAsync());
    }

    [Fact]
    public async Task AddEntriesAsync_LuegoDeleteEntriesAsync_ModificaElArchivoExistente()
    {
        using var workspace = new TempWorkspace();
        var firstFile = workspace.CreateSampleFile("a.txt", "primero");
        var archivePath = workspace.ArchivePath("mutable.zip");

        var writer = new ZipArchiveWriter();
        await writer.CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { firstFile },
            OutputPath = archivePath,
            Format = ArchiveFormat.Zip,
        });

        var extraFile = workspace.CreateSampleFile("b.txt", "segundo");
        await writer.AddEntriesAsync(archivePath, new[] { extraFile }, password: null);

        using (var reader = new ZipArchiveReader())
        {
            await reader.OpenAsync(archivePath);
            var entries = await reader.GetEntriesAsync();
            Assert.Contains(entries, e => e.Name == "a.txt");
            Assert.Contains(entries, e => e.Name == "b.txt");
        }

        await writer.DeleteEntriesAsync(archivePath, new[] { "a.txt" });

        using (var reader = new ZipArchiveReader())
        {
            await reader.OpenAsync(archivePath);
            var entries = await reader.GetEntriesAsync();
            Assert.DoesNotContain(entries, e => e.Name == "a.txt");
            Assert.Contains(entries, e => e.Name == "b.txt");
        }
    }

    [Fact]
    public async Task ExtractAsync_EntradaConRutaTraversal_SeBloqueaYNoEscribeFueraDelDestino()
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.ArchivePath("malicioso.zip");

        // Construido con System.IO.Compression (no con nuestro escritor) para simular un ZIP hostil
        // creado por otra herramienta, con una entrada que intenta escapar del destino de extracción.
        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            using (var entryStream = zip.CreateEntry("../evil.txt").Open())
            using (var streamWriter = new StreamWriter(entryStream))
            {
                streamWriter.Write("payload");
            }

            using (var okStream = zip.CreateEntry("ok.txt").Open())
            using (var okWriter = new StreamWriter(okStream))
            {
                okWriter.Write("contenido legítimo");
            }
        }

        using var reader = new ZipArchiveReader();
        await reader.OpenAsync(archivePath);

        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = workspace.OutputDir });

        Assert.Equal(1, result.ExtractedCount);
        Assert.Single(result.BlockedEntries);
        Assert.Contains("evil.txt", result.BlockedEntries[0]);
        Assert.False(File.Exists(Path.Combine(workspace.RootPath, "evil.txt")));
        Assert.True(File.Exists(Path.Combine(workspace.OutputDir, "ok.txt")));
    }

    [Fact]
    public async Task OpenAsync_ArchivoNoValido_LanzaArchiveCorruptedException()
    {
        using var workspace = new TempWorkspace();
        var badPath = workspace.ArchivePath("no-es-un-zip.zip");
        await File.WriteAllTextAsync(badPath, "esto no es un zip real");

        using var reader = new ZipArchiveReader();
        await Assert.ThrowsAsync<ArchiveCorruptedException>(() => reader.OpenAsync(badPath));
    }
}
