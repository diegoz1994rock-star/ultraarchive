using UltraArchive.Interop.SevenZip;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Interop;

/// <summary>Fase 6A, Paso 3 (13): nombre del .7z temporal.</summary>
public class SevenZipTempArchivePathTests
{
    [Fact]
    public void Create_EnLaMismaCarpetaQueElDestino_ConPrefijoYExtension()
    {
        using var ws = new TempWorkspace();
        var final = Path.Combine(ws.OutputDir, "resultado.7z");

        var temp = SevenZipTempArchivePath.Create(final);

        Assert.Equal(ws.OutputDir, Path.GetDirectoryName(temp));
        Assert.StartsWith(".uatmp-", Path.GetFileName(temp));
        Assert.EndsWith(".7z", temp);
        Assert.False(File.Exists(temp));
    }

    [Fact]
    public void Create_NombreNoPredecible()
    {
        using var ws = new TempWorkspace();
        var final = Path.Combine(ws.OutputDir, "x.7z");

        var a = SevenZipTempArchivePath.Create(final);
        var b = SevenZipTempArchivePath.Create(final);

        Assert.NotEqual(a, b);
        Assert.Equal("uatmp-".Length + 1 + 32 + ".7z".Length, Path.GetFileName(a).Length); // ".uatmp-" + guid(32) + ".7z"
    }

    [Fact]
    public void Create_RutaVacia_Lanza()
    {
        Assert.Throws<ArgumentException>(() => SevenZipTempArchivePath.Create("   "));
    }
}
