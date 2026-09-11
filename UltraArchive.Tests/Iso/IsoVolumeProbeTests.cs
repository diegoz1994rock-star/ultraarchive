using UltraArchive.Iso;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Iso;

public class IsoVolumeProbeTests
{
    [Fact]
    public void Probe_Iso9660Simple_DetectaPvpSinJolietNiUdf()
    {
        using var ms = new MemoryStream(IsoFixtures.SingleFile(), writable: false);

        var info = IsoVolumeProbe.Probe(ms);

        Assert.True(info.IsIso9660);
        Assert.False(info.HasJoliet);
        Assert.False(info.HasUdf);
        Assert.Equal(IsoFixtures.VolumeLabel, info.VolumeLabel);
    }

    [Fact]
    public void Probe_IsoConJoliet_LoDetecta()
    {
        using var ms = new MemoryStream(IsoFixtures.Joliet(), writable: false);

        var info = IsoVolumeProbe.Probe(ms);

        Assert.True(info.IsIso9660);
        Assert.True(info.HasJoliet);
        Assert.False(info.HasUdf);
    }

    [Fact]
    public void Probe_NoDejaMovidaLaPosicionDelStream()
    {
        using var ms = new MemoryStream(IsoFixtures.SingleFile(), writable: false) { Position = 123 };

        IsoVolumeProbe.Probe(ms);

        Assert.Equal(123, ms.Position);
    }

    [Fact]
    public void Probe_BytesQueNoSonIso_DevuelveIsIso9660False()
    {
        using var ms = new MemoryStream(new byte[80_000], writable: false);

        var info = IsoVolumeProbe.Probe(ms);

        Assert.False(info.IsIso9660);
    }

    [Fact]
    public void Probe_StreamDemasiadoCorto_DevuelveIsIso9660False()
    {
        using var ms = new MemoryStream(new byte[1024], writable: false);

        var info = IsoVolumeProbe.Probe(ms);

        Assert.False(info.IsIso9660);
    }

    [Fact]
    public void Probe_PvdCorrupto_DevuelveIsIso9660False()
    {
        var bytes = IsoFixtures.SingleFile();
        // Machaca la firma "CD001" del PVD (sector 16).
        for (var i = 16 * 2048; i < 16 * 2048 + 10; i++)
        {
            bytes[i] = 0;
        }

        using var ms = new MemoryStream(bytes, writable: false);
        Assert.False(IsoVolumeProbe.Probe(ms).IsIso9660);
    }
}
