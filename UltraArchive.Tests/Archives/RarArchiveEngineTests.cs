using UltraArchive.Archives.Rar;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Archives;

/// <summary>
/// Pruebas del lector de RAR (Fase 3, solo lectura). Los archivos de muestra están incrustados en
/// <see cref="RarFixtures"/> porque UltraArchive no puede crear RAR.
/// </summary>
public class RarArchiveEngineTests
{
    [Fact]
    public async Task OpenAsync_LuegoGetEntriesAsync_ListaElContenidoSinExtraer()
    {
        using var workspace = new TempWorkspace();
        var archivePath = RarFixtures.WritePlain(workspace.ArchivePath("muestra.rar"));

        using var reader = new RarArchiveReader();
        await reader.OpenAsync(archivePath);

        var entries = await reader.GetEntriesAsync();

        Assert.Contains(entries, e => e.FullPath == "src/readme.txt" && !e.IsDirectory);
        Assert.Contains(entries, e => e.FullPath == "src/sub/data.bin" && !e.IsDirectory);
        Assert.False(reader.IsPasswordProtected);
    }

    [Fact]
    public async Task ExtractAsync_ReproduceElContenidoOriginalByteAByte()
    {
        using var workspace = new TempWorkspace();
        var archivePath = RarFixtures.WritePlain(workspace.ArchivePath("muestra.rar"));

        using var reader = new RarArchiveReader();
        await reader.OpenAsync(archivePath);

        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = workspace.OutputDir });

        Assert.Equal(2, result.ExtractedCount);
        Assert.Empty(result.BlockedEntries);
        Assert.Equal(RarFixtures.ReadmeContent, await File.ReadAllTextAsync(Path.Combine(workspace.OutputDir, "src", "readme.txt")));
        Assert.Equal(RarFixtures.DataBinContent, await File.ReadAllTextAsync(Path.Combine(workspace.OutputDir, "src", "sub", "data.bin")));
    }

    [Fact]
    public async Task ExtractAsync_SoloUnaEntradaSolicitada_ExtraeSoloEsa()
    {
        using var workspace = new TempWorkspace();
        var archivePath = RarFixtures.WritePlain(workspace.ArchivePath("muestra.rar"));

        using var reader = new RarArchiveReader();
        await reader.OpenAsync(archivePath);

        var result = await reader.ExtractAsync(new ExtractOptions
        {
            DestinationPath = workspace.OutputDir,
            EntriesToExtract = new[] { "src/readme.txt" },
        });

        Assert.Equal(1, result.ExtractedCount);
        Assert.True(File.Exists(Path.Combine(workspace.OutputDir, "src", "readme.txt")));
        Assert.False(File.Exists(Path.Combine(workspace.OutputDir, "src", "sub", "data.bin")));
    }

    [Fact]
    public async Task TestIntegrityAsync_ArchivoIntacto_DevuelveTrue()
    {
        using var workspace = new TempWorkspace();
        var archivePath = RarFixtures.WritePlain(workspace.ArchivePath("muestra.rar"));

        using var reader = new RarArchiveReader();
        await reader.OpenAsync(archivePath);

        Assert.True(await reader.TestIntegrityAsync());
    }

    [Fact]
    public async Task OpenAsync_ArchivoNoValido_LanzaArchiveCorruptedException()
    {
        using var workspace = new TempWorkspace();
        var badPath = workspace.ArchivePath("no-es-un-rar.rar");
        await File.WriteAllTextAsync(badPath, "esto no es un rar real");

        using var reader = new RarArchiveReader();
        await Assert.ThrowsAsync<ArchiveCorruptedException>(() => reader.OpenAsync(badPath));
    }

    [Fact]
    public async Task OpenAsync_RarConCabeceraCifradaSinContrasena_LanzaInvalidPasswordException()
    {
        using var workspace = new TempWorkspace();
        var archivePath = RarFixtures.WriteEncrypted(workspace.ArchivePath("cifrado.rar"));

        using var reader = new RarArchiveReader();
        await Assert.ThrowsAsync<InvalidPasswordException>(() => reader.OpenAsync(archivePath));
    }

    [Fact]
    public async Task OpenAsync_RarCifradoConContrasenaCorrecta_ExtraeElContenido()
    {
        using var workspace = new TempWorkspace();
        var archivePath = RarFixtures.WriteEncrypted(workspace.ArchivePath("cifrado.rar"));

        using var reader = new RarArchiveReader();
        await reader.OpenAsync(archivePath, RarFixtures.Password);

        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = workspace.OutputDir });

        Assert.Equal(2, result.ExtractedCount);
        Assert.Equal(RarFixtures.ReadmeContent, await File.ReadAllTextAsync(Path.Combine(workspace.OutputDir, "src", "readme.txt")));
    }

    [Fact]
    public async Task OpenAsync_RarCifradoConContrasenaIncorrecta_LanzaInvalidPasswordException()
    {
        using var workspace = new TempWorkspace();
        var archivePath = RarFixtures.WriteEncrypted(workspace.ArchivePath("cifrado.rar"));

        using var reader = new RarArchiveReader();
        await Assert.ThrowsAsync<InvalidPasswordException>(() => reader.OpenAsync(archivePath, "contraseña-incorrecta"));
    }

    [Fact]
    public async Task TestIntegrityAsync_RarCifrado_DevuelveTrue()
    {
        // RAR5 cifrado guarda un MAC con clave en lugar del CRC32 del contenido en claro; el motor no
        // debe compararlo con un CRC32 (falso negativo). Que SharpCompress descifre el flujo entero
        // sin lanzar es la comprobación válida.
        using var workspace = new TempWorkspace();
        var archivePath = RarFixtures.WriteEncrypted(workspace.ArchivePath("cifrado.rar"));

        using var reader = new RarArchiveReader();
        await reader.OpenAsync(archivePath, RarFixtures.Password);

        Assert.True(await reader.TestIntegrityAsync());
    }

    [Fact]
    public async Task DetectAsync_SobreUnFixtureReal_DevuelveRar()
    {
        using var workspace = new TempWorkspace();
        var archivePath = RarFixtures.WritePlain(workspace.ArchivePath("muestra.rar"));

        var format = await new ArchiveFormatDetector().DetectAsync(archivePath);

        Assert.Equal(ArchiveFormat.Rar, format);
    }
}
