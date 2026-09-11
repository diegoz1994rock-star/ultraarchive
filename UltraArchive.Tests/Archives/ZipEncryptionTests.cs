using UltraArchive.Archives.Zip;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Models;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Archives;

/// <summary>Fase 4: creación de ZIP protegido con WinZip AES-256 (SharpZipLib) y su lectura (SharpCompress).</summary>
public class ZipEncryptionTests
{
    private const string Password = "Clave-Fuerte-2026!";

    private static CreateArchiveOptions EncryptedZipOptions(TempWorkspace ws, string archivePath, string? password) => new()
    {
        SourcePaths = new[] { ws.SourceDir },
        OutputPath = archivePath,
        Format = ArchiveFormat.Zip,
        Password = password,
        Encryption = password is null ? EncryptionMethod.None : EncryptionMethod.Aes256,
    };

    [Fact]
    public async Task CrearZipCifrado_LuegoExtraerConContrasenaCorrecta_ReproduceElContenido()
    {
        using var ws = new TempWorkspace();
        ws.CreateSampleFile("doc.txt", "contenido confidencial");
        ws.CreateSampleFile("sub/n.bin", "mas datos");
        var archivePath = ws.ArchivePath("secreto.zip");

        await new ZipArchiveWriter().CreateAsync(EncryptedZipOptions(ws, archivePath, Password));

        Assert.True(File.Exists(archivePath));

        using var reader = new ZipArchiveReader();
        await reader.OpenAsync(archivePath, Password);
        Assert.True(reader.IsPasswordProtected);

        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = ws.OutputDir });

        Assert.Equal(2, result.ExtractedCount);
        var root = Path.GetFileName(ws.SourceDir);
        Assert.Equal("contenido confidencial", await File.ReadAllTextAsync(Path.Combine(ws.OutputDir, root, "doc.txt")));
        Assert.Equal("mas datos", await File.ReadAllTextAsync(Path.Combine(ws.OutputDir, root, "sub", "n.bin")));
    }

    [Fact]
    public async Task ExtraerZipCifrado_ConContrasenaIncorrecta_LanzaInvalidPasswordException()
    {
        using var ws = new TempWorkspace();
        ws.CreateSampleFile("doc.txt", "x");
        var archivePath = ws.ArchivePath("secreto.zip");
        await new ZipArchiveWriter().CreateAsync(EncryptedZipOptions(ws, archivePath, Password));

        using var reader = new ZipArchiveReader();
        await reader.OpenAsync(archivePath, "contrasena-mala");

        await Assert.ThrowsAsync<InvalidPasswordException>(() =>
            reader.ExtractAsync(new ExtractOptions { DestinationPath = ws.OutputDir }));
    }

    [Fact]
    public async Task ExtraerZipCifrado_SinContrasena_LanzaInvalidPasswordException()
    {
        using var ws = new TempWorkspace();
        ws.CreateSampleFile("doc.txt", "x");
        var archivePath = ws.ArchivePath("secreto.zip");
        await new ZipArchiveWriter().CreateAsync(EncryptedZipOptions(ws, archivePath, Password));

        using var reader = new ZipArchiveReader();
        await reader.OpenAsync(archivePath);

        await Assert.ThrowsAsync<InvalidPasswordException>(() =>
            reader.ExtractAsync(new ExtractOptions { DestinationPath = ws.OutputDir }));
    }

    [Fact]
    public async Task CrearZipSinContrasena_SigueFuncionandoComoAntes()
    {
        using var ws = new TempWorkspace();
        ws.CreateSampleFile("a.txt", "hola");
        var archivePath = ws.ArchivePath("normal.zip");

        await new ZipArchiveWriter().CreateAsync(EncryptedZipOptions(ws, archivePath, password: null));

        using var reader = new ZipArchiveReader();
        await reader.OpenAsync(archivePath);
        Assert.False(reader.IsPasswordProtected);
        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = ws.OutputDir });
        Assert.Equal(1, result.ExtractedCount);
        Assert.True(await reader.TestIntegrityAsync());
    }

    [Fact]
    public async Task CrearZip_PidiendoCifrarNombres_LanzaYNoCreaArchivo()
    {
        using var ws = new TempWorkspace();
        ws.CreateSampleFile("a.txt", "hola");
        var archivePath = ws.ArchivePath("x.zip");

        var options = new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = archivePath,
            Format = ArchiveFormat.Zip,
            Password = Password,
            Encryption = EncryptionMethod.Aes256,
            EncryptFileNames = true,
        };

        await Assert.ThrowsAsync<EncryptionNotSupportedException>(() => new ZipArchiveWriter().CreateAsync(options));
        Assert.False(File.Exists(archivePath));
    }

    [Fact]
    public async Task AnadirEntradas_AUnZipCifrado_SeBloquea()
    {
        using var ws = new TempWorkspace();
        ws.CreateSampleFile("a.txt", "hola");
        var archivePath = ws.ArchivePath("secreto.zip");
        var writer = new ZipArchiveWriter();
        await writer.CreateAsync(EncryptedZipOptions(ws, archivePath, Password));

        var extra = ws.CreateSampleFile("b.txt", "nuevo");

        await Assert.ThrowsAsync<EncryptionNotSupportedException>(() =>
            writer.AddEntriesAsync(archivePath, new[] { extra }, Password));
    }
}
