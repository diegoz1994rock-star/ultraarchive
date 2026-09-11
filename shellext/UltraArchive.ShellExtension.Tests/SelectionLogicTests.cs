namespace UltraArchive.ShellExtension;

public class SelectionLogicTests
{
    [Theory]
    [InlineData(@"C:\Users\Diego\Documents\informe.zip", "informe")]
    [InlineData(@"C:\Users\Diego\Documents\copia.tar.gz", "copia")] // doble extensión .tar.*
    [InlineData(@"C:\Users\Diego\Documents\copia.tar.bz2", "copia")] // idem: cualquier .tar.X cuenta
    [InlineData(@"C:\Users\Diego\Documents\sin_extension", "sin_extension")]
    public void StemFromPath_QuitaLaExtension(string path, string expected)
    {
        Assert.Equal(expected, SelectionLogic.StemFromPath(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void StemFromPath_SinRuta_CaeAArchivo(string? path)
    {
        Assert.Equal("archivo", SelectionLogic.StemFromPath(path));
    }

    [Fact]
    public void BuildArguments_SinFlag_SoloRutas()
    {
        var args = SelectionLogic.BuildArguments(null, new[] { "a.zip", "b.zip" });

        Assert.Equal(new[] { "a.zip", "b.zip" }, args);
    }

    [Fact]
    public void BuildArguments_ConFlag_FlagPrimero()
    {
        var args = SelectionLogic.BuildArguments("--compress-zip", new[] { "a.txt", "b.txt" });

        Assert.Equal(new[] { "--compress-zip", "a.txt", "b.txt" }, args);
    }

    [Fact]
    public void BuildArguments_FlagVacio_SeTrataComoSinFlag()
    {
        var args = SelectionLogic.BuildArguments(string.Empty, new[] { "a.zip" });

        Assert.Equal(new[] { "a.zip" }, args);
    }

    [Fact]
    public void BuildArguments_UnSoloElemento_VerboDeUnaRuta()
    {
        // Refleja cómo LaunchUltraArchivePerItem invoca esto: un elemento por llamada.
        var args = SelectionLogic.BuildArguments("--extract-here", new[] { "archivo.zip" });

        Assert.Equal(new[] { "--extract-here", "archivo.zip" }, args);
    }
}
