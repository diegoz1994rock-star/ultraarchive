using UltraArchive.Core.Services;

namespace UltraArchive.Tests.Core;

public class VolumeSetInspectorTests
{
    [Theory]
    [InlineData("Mi_Carpeta.7z.001", true)]
    [InlineData("Mi_Carpeta.7z.042", true)]
    [InlineData("archivo.zip.01", true)]
    [InlineData("archivo.7z", false)]
    [InlineData("archivo.txt", false)]
    [InlineData("archivo.7z.1", false)]  // un solo dígito: no
    public void IsVolumePartName(string name, bool expected)
    {
        Assert.Equal(expected, VolumeSetInspector.IsVolumePartName(name));
    }

    [Fact]
    public void TryGetBaseName()
    {
        Assert.Equal("Mi_Carpeta.7z", VolumeSetInspector.TryGetBaseName(@"C:\x\Mi_Carpeta.7z.003"));
        Assert.Null(VolumeSetInspector.TryGetBaseName("Mi_Carpeta.7z"));
    }

    [Fact]
    public void Inspect_ConjuntoCompleto()
    {
        var siblings = new[] { "x.7z.001", "x.7z.002", "x.7z.003", "otra-cosa.txt", "x.7z" };
        var info = VolumeSetInspector.Inspect("x.7z.002", siblings)!;

        Assert.Equal("x.7z", info.BaseName);
        Assert.Equal(".7z", info.InnerExtension);
        Assert.Equal(new[] { 1, 2, 3 }, info.FoundParts);
        Assert.Empty(info.MissingParts);
        Assert.Empty(info.DuplicateParts);
        Assert.True(info.IsComplete);
        Assert.Equal(new[] { "x.7z.001", "x.7z.002", "x.7z.003" }, info.OrderedPartFileNames);
    }

    [Fact]
    public void Inspect_FaltaParteIntermedia()
    {
        var siblings = new[] { "x.7z.001", "x.7z.002", "x.7z.004" };
        var info = VolumeSetInspector.Inspect("x.7z.001", siblings)!;

        Assert.Equal(new[] { 1, 2, 4 }, info.FoundParts);
        Assert.Equal(new[] { 3 }, info.MissingParts);
        Assert.False(info.IsComplete);
    }

    [Fact]
    public void Inspect_ParteDuplicada_DistintoAncho()
    {
        var siblings = new[] { "x.7z.001", "x.7z.01", "x.7z.002" };
        var info = VolumeSetInspector.Inspect("x.7z.001", siblings)!;

        Assert.Contains(1, info.DuplicateParts);
        Assert.False(info.IsComplete);
    }

    [Fact]
    public void Inspect_NoEsParte_DevuelveNull()
    {
        Assert.Null(VolumeSetInspector.Inspect("archivo.7z", new[] { "archivo.7z" }));
    }

    [Fact]
    public void Inspect_IgnoraOtrosConjuntos()
    {
        var siblings = new[] { "a.7z.001", "a.7z.002", "b.7z.001" };
        var info = VolumeSetInspector.Inspect("a.7z.001", siblings)!;

        Assert.Equal(new[] { 1, 2 }, info.FoundParts);
    }
}
