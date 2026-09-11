using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;

namespace UltraArchive.Tests.Core;

public class SplitSupportTests
{
    private static CreateArchiveOptions Options(ArchiveFormat format, long? split) => new()
    {
        SourcePaths = new[] { @"C:\x" },
        OutputPath = @"C:\out\a",
        Format = format,
        SplitVolumeSizeBytes = split,
    };

    [Fact]
    public void SinDivision_NoLanza_ParaCualquierFormato()
    {
        foreach (var f in new[] { ArchiveFormat.Zip, ArchiveFormat.SevenZip, ArchiveFormat.Tar, ArchiveFormat.GZip })
        {
            SplitSupport.Validate(Options(f, null));
        }
    }

    [Fact]
    public void Division7Z_Valida()
    {
        SplitSupport.Validate(Options(ArchiveFormat.SevenZip, 100L * 1024 * 1024));
        Assert.True(SplitSupport.SupportsSplitOnCreate(ArchiveFormat.SevenZip));
    }

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.Tar)]
    [InlineData(ArchiveFormat.GZip)]
    [InlineData(ArchiveFormat.Rar)]
    public void DivisionEnFormatoNoSoportado_LanzaSplitNotSupported(ArchiveFormat format)
    {
        Assert.False(SplitSupport.SupportsSplitOnCreate(format));
        var ex = Assert.Throws<SplitNotSupportedException>(() => SplitSupport.Validate(Options(format, 100L * 1024 * 1024)));
        Assert.Equal(ArchiveErrorCategory.UnsupportedFormat, ex.Category);
    }

    [Fact]
    public void TamanoDeVolumenAbsurdo_Lanza()
    {
        Assert.Throws<SplitNotSupportedException>(() => SplitSupport.Validate(Options(ArchiveFormat.SevenZip, 1024)));
    }

    [Fact]
    public void WantsSplit()
    {
        Assert.True(SplitSupport.WantsSplit(Options(ArchiveFormat.SevenZip, 5)));
        Assert.False(SplitSupport.WantsSplit(Options(ArchiveFormat.SevenZip, null)));
        Assert.False(SplitSupport.WantsSplit(Options(ArchiveFormat.SevenZip, 0)));
    }
}
