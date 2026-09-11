using UltraArchive.Archives.Gzip;
using UltraArchive.Archives.SevenZip;
using UltraArchive.Archives.Tar;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Archives;

/// <summary>
/// Fase 4: pedir cifrado a un formato que no lo soporta al crear (7Z/TAR/GZIP) debe fallar de forma
/// explícita y <b>no</b> dejar un archivo sin cifrar en disco.
/// </summary>
public class WriterEncryptionRejectionTests
{
    public static IEnumerable<object[]> Writers()
    {
        yield return new object[] { new SevenZipArchiveWriter(), ArchiveFormat.SevenZip, "s.7z" };
        yield return new object[] { new TarArchiveWriter(), ArchiveFormat.Tar, "s.tar" };
        yield return new object[] { new GZipArchiveWriter(), ArchiveFormat.GZip, "s.tar.gz" };
    }

    [Theory]
    [MemberData(nameof(Writers))]
    public async Task CrearConContrasena_LanzaYNoDejaArchivo(IArchiveWriter writer, ArchiveFormat format, string fileName)
    {
        using var ws = new TempWorkspace();
        ws.CreateSampleFile("a.txt", "hola");
        ws.CreateSampleFile("b.txt", "mundo");
        var archivePath = ws.ArchivePath(fileName);

        var options = new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = archivePath,
            Format = format,
            Password = "clave-123",
            Encryption = EncryptionMethod.Aes256,
        };

        await Assert.ThrowsAsync<EncryptionNotSupportedException>(() => writer.CreateAsync(options));
        Assert.False(File.Exists(archivePath), "no debe quedar un archivo sin cifrar cuando se pidió cifrado");
    }
}
