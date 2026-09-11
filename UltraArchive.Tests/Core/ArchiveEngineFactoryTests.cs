using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;

namespace UltraArchive.Tests.Core;

public class ArchiveEngineFactoryTests
{
    [Fact]
    public void CreateReader_SinMotorRegistrado_LanzaUnsupportedFormatException()
    {
        var factory = new ArchiveEngineFactory();

        var ex = Assert.Throws<UnsupportedFormatException>(() => factory.CreateReader(ArchiveFormat.Zip));
        Assert.Equal(ArchiveErrorCategory.UnsupportedFormat, ex.Category);
    }

    [Fact]
    public void CreateWriter_ParaRar_MencionaLaRestriccionLegalEnElMensaje()
    {
        var factory = new ArchiveEngineFactory();

        var ex = Assert.Throws<UnsupportedFormatException>(() => factory.CreateWriter(ArchiveFormat.Rar));
        Assert.Contains("legales", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegisterReader_PermiteResolverElMotorRegistradoDespues()
    {
        var factory = new ArchiveEngineFactory();
        var fakeReader = new FakeArchiveReader();

        factory.RegisterReader(ArchiveFormat.Zip, () => fakeReader);

        Assert.True(factory.CanRead(ArchiveFormat.Zip));
        Assert.Same(fakeReader, factory.CreateReader(ArchiveFormat.Zip));
        Assert.Contains(ArchiveFormat.Zip, factory.SupportedReadFormats);
    }

    [Fact]
    public void CanWrite_FormatoNoRegistrado_DevuelveFalse()
    {
        var factory = new ArchiveEngineFactory();

        Assert.False(factory.CanWrite(ArchiveFormat.SevenZip));
    }

    private sealed class FakeArchiveReader : IArchiveReader
    {
        public ArchiveFormat Format => ArchiveFormat.Zip;
        public bool IsPasswordProtected => false;

        public Task OpenAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ArchiveEntry>> GetEntriesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ArchiveEntry>>(Array.Empty<ArchiveEntry>());

        public Task<ExtractionResult> ExtractAsync(ExtractOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ExtractionResult());

        public Task<bool> TestIntegrityAsync(IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public void Dispose()
        {
        }
    }
}
