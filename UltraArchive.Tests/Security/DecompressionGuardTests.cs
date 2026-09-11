using UltraArchive.Core.Exceptions;
using UltraArchive.Security;

namespace UltraArchive.Tests.Security;

public class DecompressionGuardTests
{
    [Fact]
    public void ArchivoNormal_NoLanza()
    {
        // 50 MiB que se expanden a 200 MiB: ratio 4x, tamaño moderado.
        DecompressionGuard.ThrowIfArchiveLooksLikeBomb(50L * 1024 * 1024, 200L * 1024 * 1024);
    }

    [Fact]
    public void ArchivoPequenoConRatioAlto_NoLanza()
    {
        // Por debajo del umbral de tamaño no se aplica el control de ratio (un .zip de un .txt repetido).
        DecompressionGuard.ThrowIfArchiveLooksLikeBomb(1024, 10L * 1024 * 1024);
    }

    [Fact]
    public void BombaDeDescompresion_RatioAbsurdo_Lanza()
    {
        // 1 MiB que declara 5 GiB: ratio ~5000x y por encima del umbral de tamaño.
        Assert.Throws<DecompressionBombException>(() =>
            DecompressionGuard.ThrowIfArchiveLooksLikeBomb(1L * 1024 * 1024, 5L * 1024 * 1024 * 1024));
    }

    [Fact]
    public void EntradaQueSuperaSuTamanoDeclarado_Lanza()
    {
        Assert.Throws<DecompressionBombException>(() =>
            DecompressionGuard.ThrowIfEntryExceedsLimit("x.bin", declaredSize: 1000, actualBytesSoFar: 5_000_000));
    }

    [Fact]
    public void EntradaDentroDeSuTamanoDeclarado_NoLanza()
    {
        DecompressionGuard.ThrowIfEntryExceedsLimit("x.bin", declaredSize: 1_000_000, actualBytesSoFar: 999_999);
        DecompressionGuard.ThrowIfEntryExceedsLimit("x.bin", declaredSize: 0, actualBytesSoFar: 9_999_999); // 0 = sin comprobación
    }

    // ---- RatioGuard (control incremental para .tar.gz / .gz) ----

    [Fact]
    public void RatioGuard_ExpansionRazonable_NoLanza()
    {
        var guard = new DecompressionGuard.RatioGuard(compressedBytes: 10_000_000);
        guard.Check(totalUncompressedSoFar: 40_000_000, "backup.tar.gz"); // 4×
    }

    [Fact]
    public void RatioGuard_PorDebajoDelUmbralDeTamano_NoLanza_AunConRatioAlto()
    {
        var guard = new DecompressionGuard.RatioGuard(compressedBytes: 1_000);
        guard.Check(totalUncompressedSoFar: 10_000_000, "pequeño.gz"); // ratio enorme pero < 256 MiB
    }

    [Fact]
    public void RatioGuard_RatioAbsurdoYPasadoElUmbral_Lanza()
    {
        var guard = new DecompressionGuard.RatioGuard(compressedBytes: 1L * 1024 * 1024); // 1 MiB comprimido
        Assert.Throws<DecompressionBombException>(() =>
            guard.Check(totalUncompressedSoFar: 5L * 1024 * 1024 * 1024, "bomba.tar.gz")); // 5 GiB → ratio ~5000×
    }

    [Fact]
    public void RatioGuard_UmbralesInyectables_ParaPoderProbarloRapido()
    {
        var guard = new DecompressionGuard.RatioGuard(compressedBytes: 100, ratioThresholdBytes: 1_000, maxRatio: 10);
        guard.Check(900, "ok");   // aún por debajo del umbral de tamaño
        Assert.Throws<DecompressionBombException>(() => guard.Check(2_000, "malo")); // 2000/100 = 20 > 10
    }
}
