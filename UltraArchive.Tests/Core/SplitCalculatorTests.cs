using UltraArchive.Core.Models;
using UltraArchive.Core.Services;

namespace UltraArchive.Tests.Core;

public class SplitCalculatorTests
{
    [Theory]
    [InlineData(100, SizeUnit.MB, 100L * 1024 * 1024)]
    [InlineData(5, SizeUnit.GB, 5L * 1024 * 1024 * 1024)]
    [InlineData(1.5, SizeUnit.GB, (long)(1.5 * 1024 * 1024 * 1024))]
    public void ToBytes_ConvierteCorrectamente(double value, SizeUnit unit, long expected)
    {
        Assert.Equal(expected, SplitCalculator.ToBytes(value, unit));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ToBytes_ValorNoPositivo_Lanza(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SplitCalculator.ToBytes(value, SizeUnit.MB));
    }

    [Fact]
    public void BytesPerVolumeForPartCount_RepartoExacto()
    {
        // 20 GiB en 4 partes → 5 GiB por parte, y ceil(20/5) == 4.
        var total = 20L * 1024 * 1024 * 1024;
        var perVolume = SplitCalculator.BytesPerVolumeForPartCount(total, 4);

        Assert.Equal(5L * 1024 * 1024 * 1024, perVolume);
        Assert.Equal(4, (int)((total + perVolume - 1) / perVolume));
    }

    [Fact]
    public void BytesPerVolumeForPartCount_NoDivisibleExacto_RedondeaArriba()
    {
        var total = 10_000_000L;
        var perVolume = SplitCalculator.BytesPerVolumeForPartCount(total, 3);

        Assert.Equal(3, (int)((total + perVolume - 1) / perVolume)); // exactamente 3 partes
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void BytesPerVolumeForPartCount_MenosDe2Partes_Lanza(int parts)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SplitCalculator.BytesPerVolumeForPartCount(1000, parts));
    }

    [Fact]
    public void BytesPerVolumeForPartCount_SinTamanoOrigen_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SplitCalculator.BytesPerVolumeForPartCount(0, 4));
    }

    [Fact]
    public void EstimatePartCount_Basico()
    {
        Assert.Equal(4, SplitCalculator.EstimatePartCount(20L * 1024 * 1024 * 1024, 5L * 1024 * 1024 * 1024));
        Assert.Equal(1, SplitCalculator.EstimatePartCount(0, 100));
        Assert.Equal(1, SplitCalculator.EstimatePartCount(100, 0));
    }

    [Fact]
    public void EnsureValidVolumeSize_DemasiadoPequeno_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SplitCalculator.EnsureValidVolumeSize(1024));
        SplitCalculator.EnsureValidVolumeSize(SplitCalculator.MinVolumeSizeBytes); // no lanza
    }
}
