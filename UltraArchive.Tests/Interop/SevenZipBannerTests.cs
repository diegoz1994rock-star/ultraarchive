using UltraArchive.Interop.SevenZip;

namespace UltraArchive.Tests.Interop;

/// <summary>Parser puro del banner de 7-Zip (sin procesos).</summary>
public class SevenZipBannerTests
{
    [Theory]
    [InlineData("7-Zip 26.02 (x64) : Copyright (c) 1999-2026 Igor Pavlov : 2026-06-25", 26, 2)]
    [InlineData("7-Zip (r) 24.09 (x64) : Copyright (c) 1999-2024 Igor Pavlov", 24, 9)]
    [InlineData("7-Zip (a) 21.07 (x86) : Copyright (c) 1999-2021 Igor Pavlov", 21, 7)]
    [InlineData("7-Zip (A) 9.20  Copyright (c) 1999-2010 Igor Pavlov  2010-11-18", 9, 20)]
    [InlineData("  7-Zip 19.00 (x64)\r\nUsage...", 19, 0)]
    public void TryParseVersion_BannersReales_DevuelveLaVersion(string banner, int major, int minor)
    {
        Assert.True(SevenZipBanner.TryParseVersion(banner, out var version));
        Assert.Equal(new Version(major, minor), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Microsoft Windows [Version 10.0.26100.1]")]
    [InlineData("bzip2, a block-sorting file compressor")]
    [InlineData("7-Zip sin numero de version")]
    [InlineData("Esto menciona 7-Zip 26.02 en mitad de la linea pero no al principio")]
    public void TryParseVersion_TextoNoValido_DevuelveFalse(string? text)
    {
        Assert.False(SevenZipBanner.TryParseVersion(text, out var version));
        Assert.Equal(new Version(0, 0), version);
    }
}
