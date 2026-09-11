using UltraArchive.Shell;

namespace UltraArchive.Tests.Shell;

/// <summary>Fase 6B: interpretación de los argumentos con los que se lanza UltraArchive.</summary>
public class ShellCommandLineParserTests
{
    [Fact]
    public void SinArgumentos_ArranqueNormal()
    {
        Assert.Equal(ShellAction.None, ShellCommandLineParser.Parse(Array.Empty<string>()).Action);
        Assert.Equal(ShellAction.None, ShellCommandLineParser.Parse(null).Action);
    }

    [Fact]
    public void UnArchivo_Abrirlo()
    {
        var cmd = ShellCommandLineParser.Parse(new[] { @"C:\descargas\cosas.zip" });
        Assert.Equal(ShellAction.OpenFile, cmd.Action);
        Assert.Equal(@"C:\descargas\cosas.zip", cmd.Path);
    }

    [Theory]
    [InlineData("--extract-here", ShellAction.ExtractHere)]
    [InlineData("--extract-here-flat", ShellAction.ExtractHereFlat)]
    [InlineData("--extract-to", ShellAction.ExtractTo)]
    public void ExtractConRuta_SeInterpreta(string flag, ShellAction expected)
    {
        var cmd = ShellCommandLineParser.Parse(new[] { flag, @"C:\a b\archivo con espacios.7z" });
        Assert.Equal(expected, cmd.Action);
        Assert.Equal(@"C:\a b\archivo con espacios.7z", cmd.Path);
    }

    [Theory]
    [InlineData("--compress", ShellAction.Compress)]
    [InlineData("--compress-here", ShellAction.CompressHere)]
    [InlineData("--compress-zip", ShellAction.CompressToZip)]
    [InlineData("--compress-7z", ShellAction.CompressTo7z)]
    [InlineData("--compress-split", ShellAction.CompressSplit)]
    public void CompressConUnaRuta_SeInterpreta(string flag, ShellAction expected)
    {
        var cmd = ShellCommandLineParser.Parse(new[] { flag, @"C:\fotos\viaje" });
        Assert.Equal(expected, cmd.Action);
        Assert.Equal(@"C:\fotos\viaje", cmd.Path);
        Assert.Equal(new[] { @"C:\fotos\viaje" }, cmd.AllPaths);
    }

    [Fact]
    public void CompressConVariasRutas_SeleccionMultiple()
    {
        var cmd = ShellCommandLineParser.Parse(new[]
        {
            "--compress", @"C:\a\1.txt", @"C:\a\2.txt", @"C:\a\sub carpeta",
        });

        Assert.Equal(ShellAction.Compress, cmd.Action);
        Assert.Equal(@"C:\a\1.txt", cmd.Path);
        Assert.Equal(new[] { @"C:\a\1.txt", @"C:\a\2.txt", @"C:\a\sub carpeta" }, cmd.AllPaths);
    }

    [Fact]
    public void CompressConUnaRutaMalFormada_InvalidaTodaLaOperacion()
    {
        var cmd = ShellCommandLineParser.Parse(new[] { "--compress", @"C:\a\ok.txt", "\0malo" });
        Assert.Equal(ShellAction.None, cmd.Action);
    }

    [Fact]
    public void CompressSinRutas_ArranqueNormal()
    {
        Assert.Equal(ShellAction.None, ShellCommandLineParser.Parse(new[] { "--compress" }).Action);
    }

    [Fact]
    public void RutasUnicode_SeConservan()
    {
        var cmd = ShellCommandLineParser.Parse(new[] { "--extract-here", @"C:\café\ñandú — copia.rar" });
        Assert.Equal(@"C:\café\ñandú — copia.rar", cmd.Path);
    }

    [Theory]
    [InlineData("--install-shell", ShellAction.InstallShell)]
    [InlineData("--uninstall-shell", ShellAction.UninstallShell)]
    [InlineData("--install-shell-allusers", ShellAction.InstallShellAllUsers)]
    [InlineData("--uninstall-shell-allusers", ShellAction.UninstallShellAllUsers)]
    public void InstallUninstall_SinRuta(string flag, ShellAction expected)
    {
        Assert.Equal(expected, ShellCommandLineParser.Parse(new[] { flag }).Action);
        // No admiten argumentos.
        Assert.Equal(ShellAction.None, ShellCommandLineParser.Parse(new[] { flag, "algo" }).Action);
    }

    [Fact]
    public void EntradasMalFormadas_ArranqueNormal_SinEjecutarNada()
    {
        string[][] malas =
        {
            new[] { "--extract-here" },                   // falta la ruta
            new[] { "--extract-here", "a.zip", "extra" }, // argumentos de más
            new[] { "--extract-to", "-otra-opcion" },     // "ruta" que parece un flag
            new[] { "--install-shell", "algo" },          // install no admite argumentos
            new[] { "--desconocido", "x" },               // flag desconocido
            new[] { "a.zip", "b.zip" },                   // dos archivos
        };

        foreach (var args in malas)
        {
            Assert.Equal(ShellAction.None, ShellCommandLineParser.Parse(args).Action);
        }
    }

    [Theory]
    [InlineData("C:\\a\r\nb.zip")]   // salto de línea
    [InlineData("C:\\a\0b.zip")]     // NUL
    [InlineData("   ")]
    public void RutasPeligrosas_NoSeAceptan(string path)
    {
        Assert.Equal(ShellAction.None, ShellCommandLineParser.Parse(new[] { "--extract-here", path }).Action);
        Assert.Equal(ShellAction.None, ShellCommandLineParser.Parse(new[] { path }).Action);
    }
}
