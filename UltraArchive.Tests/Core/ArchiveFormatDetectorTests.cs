using UltraArchive.Core.Models;
using UltraArchive.Core.Services;

namespace UltraArchive.Tests.Core;

public class ArchiveFormatDetectorTests
{
    private readonly ArchiveFormatDetector _detector = new();

    [Theory]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, ArchiveFormat.Zip)]
    [InlineData(new byte[] { 0x50, 0x4B, 0x05, 0x06 }, ArchiveFormat.Zip)]
    [InlineData(new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }, ArchiveFormat.SevenZip)]
    [InlineData(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 }, ArchiveFormat.Rar)]
    [InlineData(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 }, ArchiveFormat.Rar)]
    [InlineData(new byte[] { 0x1F, 0x8B }, ArchiveFormat.GZip)]
    [InlineData(new byte[] { 0x42, 0x5A, 0x68, (byte)'9' }, ArchiveFormat.BZip2)]
    public async Task DetectFromContentAsync_DetectaFirmasDeCabecera(byte[] header, ArchiveFormat expected)
    {
        await using var stream = new MemoryStream(header);

        var result = await _detector.DetectFromContentAsync(stream);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task DetectFromContentAsync_DetectaTarPosixPorMagicUstar()
    {
        var buffer = new byte[512];
        System.Text.Encoding.ASCII.GetBytes("ustar\000").CopyTo(buffer, 257);
        await using var stream = new MemoryStream(buffer);

        var result = await _detector.DetectFromContentAsync(stream);

        Assert.Equal(ArchiveFormat.Tar, result);
    }

    [Fact]
    public async Task DetectFromContentAsync_DetectaIso9660PorIdentificadorCD001()
    {
        var buffer = new byte[32769 + 5];
        System.Text.Encoding.ASCII.GetBytes("CD001").CopyTo(buffer, 32769);
        await using var stream = new MemoryStream(buffer);

        var result = await _detector.DetectFromContentAsync(stream);

        Assert.Equal(ArchiveFormat.Iso9660, result);
    }

    [Fact]
    public async Task DetectFromContentAsync_ContenidoDesconocido_DevuelveUnknown()
    {
        await using var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02, 0x03 });

        var result = await _detector.DetectFromContentAsync(stream);

        Assert.Equal(ArchiveFormat.Unknown, result);
    }

    [Fact]
    public async Task DetectFromContentAsync_StreamVacio_DevuelveUnknown()
    {
        await using var stream = new MemoryStream();

        var result = await _detector.DetectFromContentAsync(stream);

        Assert.Equal(ArchiveFormat.Unknown, result);
    }

    [Fact]
    public async Task DetectFromContentAsync_RestauraLaPosicionOriginalDelStream()
    {
        var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xAA, 0xBB };
        await using var stream = new MemoryStream(bytes);
        stream.Position = 2;

        await _detector.DetectFromContentAsync(stream);

        Assert.Equal(2, stream.Position);
    }

    [Theory]
    [InlineData("archivo.zip", ArchiveFormat.Zip)]
    [InlineData("archivo.7z", ArchiveFormat.SevenZip)]
    [InlineData("archivo.rar", ArchiveFormat.Rar)]
    [InlineData("archivo.tar", ArchiveFormat.Tar)]
    [InlineData("archivo.gz", ArchiveFormat.GZip)]
    [InlineData("respaldo.tar.gz", ArchiveFormat.GZip)]
    [InlineData("archivo.bz2", ArchiveFormat.BZip2)]
    [InlineData("imagen.iso", ArchiveFormat.Iso9660)]
    [InlineData("documento.pdf", ArchiveFormat.Unknown)]
    public void DetectFromExtension_MapeaExtensionesConocidas(string fileName, ArchiveFormat expected)
    {
        var result = _detector.DetectFromExtension(fileName);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task DetectAsync_ArchivoConExtensionEnganosa_PrevaleceElContenidoRealSobreLaExtension()
    {
        // Un ZIP renombrado a .txt debe detectarse igualmente como ZIP por su firma de bytes.
        var tempFile = Path.Combine(Path.GetTempPath(), $"ultraarchive-test-{Guid.NewGuid():N}.txt");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 });

        try
        {
            var result = await _detector.DetectAsync(tempFile);
            Assert.Equal(ArchiveFormat.Zip, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task DetectAsync_ArchivoInexistente_CaePorExtension()
    {
        var result = await _detector.DetectAsync(@"C:\ruta\inexistente\archivo.rar");

        Assert.Equal(ArchiveFormat.Rar, result);
    }
}
