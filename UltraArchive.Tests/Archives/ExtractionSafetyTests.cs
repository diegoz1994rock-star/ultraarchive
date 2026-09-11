using System.IO;
using UltraArchive.Archives.Common;
using UltraArchive.Archives.Zip;
using UltraArchive.Core.Models;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Archives;

/// <summary>
/// Fase 4: la extracción no debe dejar ficheros a medias en disco si falla o se cancela, y debe
/// respetar la política de colisión con archivos ya existentes.
/// </summary>
public class ExtractionSafetyTests
{
    [Fact]
    public async Task EntryFileWriter_SiLaCopiaFalla_BorraElFicheroParcial()
    {
        using var ws = new TempWorkspace();
        var dest = Path.Combine(ws.OutputDir, "parcial.bin");

        var failing = new ThrowingStream(bytesBeforeThrow: 4096);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            EntryFileWriter.WriteAsync(failing, dest, _ => { }, CancellationToken.None));

        Assert.False(File.Exists(dest), "el fichero parcial debe borrarse cuando la copia falla");
    }

    [Fact]
    public async Task EntryFileWriter_SiSeCancela_BorraElFicheroParcial()
    {
        using var ws = new TempWorkspace();
        var dest = Path.Combine(ws.OutputDir, "parcial.bin");
        using var cts = new CancellationTokenSource();

        var slow = new ThrowingStream(bytesBeforeThrow: long.MaxValue) { OnRead = () => cts.Cancel() };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            EntryFileWriter.WriteAsync(slow, dest, _ => { }, cts.Token));

        Assert.False(File.Exists(dest));
    }

    [Fact]
    public async Task ExtraerConColisionSkip_NoSobrescribeElArchivoExistente()
    {
        using var ws = new TempWorkspace();
        ws.CreateSampleFile("a.txt", "nuevo contenido");
        var archivePath = ws.ArchivePath("x.zip");
        await new ZipArchiveWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = archivePath,
            Format = ArchiveFormat.Zip,
        });

        var root = Path.GetFileName(ws.SourceDir);
        var existing = Path.Combine(ws.OutputDir, root, "a.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        await File.WriteAllTextAsync(existing, "contenido previo");

        using var reader = new ZipArchiveReader();
        await reader.OpenAsync(archivePath);
        await reader.ExtractAsync(new ExtractOptions
        {
            DestinationPath = ws.OutputDir,
            CollisionPolicy = CollisionPolicy.Skip,
        });

        Assert.Equal("contenido previo", await File.ReadAllTextAsync(existing));
    }

    [Fact]
    public async Task ExtraerConColisionRenombrar_CreaUnaCopiaSinTocarElOriginal()
    {
        using var ws = new TempWorkspace();
        ws.CreateSampleFile("a.txt", "del archivo");
        var archivePath = ws.ArchivePath("x.zip");
        await new ZipArchiveWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = archivePath,
            Format = ArchiveFormat.Zip,
        });

        var root = Path.GetFileName(ws.SourceDir);
        var existing = Path.Combine(ws.OutputDir, root, "a.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        await File.WriteAllTextAsync(existing, "original intacto");

        using var reader = new ZipArchiveReader();
        await reader.OpenAsync(archivePath);
        var result = await reader.ExtractAsync(new ExtractOptions
        {
            DestinationPath = ws.OutputDir,
            CollisionPolicy = CollisionPolicy.RenameAutomatically,
        });

        Assert.Equal(1, result.ExtractedCount);
        Assert.Equal("original intacto", await File.ReadAllTextAsync(existing));
        var copies = Directory.GetFiles(Path.GetDirectoryName(existing)!, "a*");
        Assert.True(copies.Length >= 2, "debe haberse creado una copia con otro nombre");
    }

    [Fact]
    public async Task ExtraerConColisionAsk_ElManejadorDecidePorEntrada_YSeReportanLasOmitidas()
    {
        using var ws = new TempWorkspace();
        ws.CreateSampleFile("a.txt", "del archivo A");
        ws.CreateSampleFile("b.txt", "del archivo B");
        var archivePath = ws.ArchivePath("x.zip");
        await new ZipArchiveWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = archivePath,
            Format = ArchiveFormat.Zip,
        });

        var root = Path.GetFileName(ws.SourceDir);
        var dir = Path.Combine(ws.OutputDir, root);
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "a.txt"), "A previo");
        await File.WriteAllTextAsync(Path.Combine(dir, "b.txt"), "B previo");

        var prompted = new List<string>();

        using var reader = new ZipArchiveReader();
        await reader.OpenAsync(archivePath);
        var result = await reader.ExtractAsync(new ExtractOptions
        {
            DestinationPath = ws.OutputDir,
            CollisionPolicy = CollisionPolicy.Ask,
            OnCollision = path =>
            {
                prompted.Add(Path.GetFileName(path));
                // "a.txt" se sobrescribe; "b.txt" se omite.
                return Path.GetFileName(path) == "a.txt"
                    ? CollisionResolution.Overwrite
                    : CollisionResolution.Skip;
            },
        });

        Assert.Equal(2, prompted.Count);
        Assert.Equal(1, result.ExtractedCount);
        Assert.Single(result.SkippedEntries);
        Assert.Equal("del archivo A", await File.ReadAllTextAsync(Path.Combine(dir, "a.txt")));
        Assert.Equal("B previo", await File.ReadAllTextAsync(Path.Combine(dir, "b.txt")));
    }

    [Fact]
    public async Task ExtraerConColisionAsk_Cancelar_AbortaLaExtraccion()
    {
        using var ws = new TempWorkspace();
        ws.CreateSampleFile("a.txt", "nuevo");
        var archivePath = ws.ArchivePath("x.zip");
        await new ZipArchiveWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = archivePath,
            Format = ArchiveFormat.Zip,
        });

        var root = Path.GetFileName(ws.SourceDir);
        var existing = Path.Combine(ws.OutputDir, root, "a.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        await File.WriteAllTextAsync(existing, "no me toques");

        using var reader = new ZipArchiveReader();
        await reader.OpenAsync(archivePath);

        await Assert.ThrowsAsync<OperationCanceledException>(() => reader.ExtractAsync(new ExtractOptions
        {
            DestinationPath = ws.OutputDir,
            CollisionPolicy = CollisionPolicy.Ask,
            OnCollision = _ => CollisionResolution.Cancel,
        }));

        Assert.Equal("no me toques", await File.ReadAllTextAsync(existing));
    }

    /// <summary>Stream de solo lectura que produce ceros y lanza tras N bytes (o llama a un callback en cada Read).</summary>
    private sealed class ThrowingStream : Stream
    {
        private readonly long _bytesBeforeThrow;
        private long _produced;

        public ThrowingStream(long bytesBeforeThrow) => _bytesBeforeThrow = bytesBeforeThrow;

        public Action? OnRead { get; init; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            OnRead?.Invoke();
            if (_produced >= _bytesBeforeThrow)
            {
                throw new IOException("fallo simulado a mitad de la copia");
            }

            var n = (int)Math.Min(count, _bytesBeforeThrow - _produced);
            Array.Clear(buffer, offset, n);
            _produced += n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _produced; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
