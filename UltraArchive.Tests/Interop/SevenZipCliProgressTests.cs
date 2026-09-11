using UltraArchive.Interop.SevenZip;

namespace UltraArchive.Tests.Interop;

/// <summary>Fase 6A, Paso 3 (D): interpretación del progreso de 7zr (-bsp1).</summary>
public class SevenZipCliProgressTests
{
    [Theory]
    [InlineData("0%", 0)]
    [InlineData("  1%", 1)]
    [InlineData("50%", 50)]
    [InlineData(" 25 %", 25)]
    [InlineData("99% - archivo.bin", 99)]
    [InlineData("100%", 100)]
    public void Feed_PorcentajeValido_ActualizaCurrent(string line, int expected)
    {
        var progress = new SevenZipCliProgress();

        Assert.True(progress.Feed(line));
        Assert.Equal(expected, progress.Current);
        Assert.False(progress.IsIndeterminate);
    }

    [Theory]
    [InlineData("Scanning the drive:")]
    [InlineData("Add new data to archive: 3 files")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Everything is Ok")]
    public void Feed_SinPorcentaje_NoCuentaComoProgreso(string line)
    {
        var progress = new SevenZipCliProgress();

        Assert.False(progress.Feed(line));
        Assert.True(progress.IsIndeterminate);
        Assert.Equal(0, progress.Current);
    }

    [Theory]
    [InlineData("123%")]
    [InlineData("999%")]
    [InlineData("1000%")]
    public void Feed_PorcentajeFueraDeRango_SeIgnora(string line)
    {
        var progress = new SevenZipCliProgress();

        Assert.False(progress.Feed(line));
        Assert.Equal(0, progress.Current);
    }

    [Fact]
    public void Feed_EsMonotonico_NoRetrocede()
    {
        var progress = new SevenZipCliProgress();

        progress.Feed("40%");
        progress.Feed("70%");
        progress.Feed("55%"); // llega un valor menor
        progress.Feed("70%"); // repetido

        Assert.Equal(70, progress.Current);
    }

    [Fact]
    public void Feed_VariosPorcentajesEnUnMismoTrozo_TomaElMayor()
    {
        var progress = new SevenZipCliProgress();

        Assert.True(progress.Feed("\r 10%\r 20%\r 35%"));
        Assert.Equal(35, progress.Current);
    }

    [Fact]
    public void Feed_ValorRepetido_SigueSiendoProgresoValido()
    {
        var progress = new SevenZipCliProgress();

        Assert.True(progress.Feed("42%"));
        Assert.True(progress.Feed("42%")); // renueva el watchdog aunque no avance
        Assert.Equal(42, progress.Current);
    }

    [Theory]
    [InlineData("nada de porcentajes aqui", false, 0)]
    [InlineData("progreso: 33 %", true, 33)]
    [InlineData("valores 1234% no valen", false, 0)]
    public void TryParseFirstPercent_Casos(string text, bool expectedOk, int expectedPercent)
    {
        var ok = SevenZipCliProgress.TryParseFirstPercent(text, out var percent);

        Assert.Equal(expectedOk, ok);
        Assert.Equal(expectedPercent, percent);
    }
}
