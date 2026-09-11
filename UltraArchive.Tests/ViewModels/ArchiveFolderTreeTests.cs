using UltraArchive.App.Services;
using UltraArchive.App.ViewModels;
using UltraArchive.Core.Models;

namespace UltraArchive.Tests.ViewModels;

/// <summary>
/// Árbol de carpetas del panel lateral (<see cref="ArchiveFolderTree"/>): construcción a partir de la
/// lista plana de entradas y resolución de las filas visibles por carpeta. Lógica pura.
/// </summary>
public class ArchiveFolderTreeTests
{
    private static ArchiveEntry File(string path, long size = 10) =>
        new() { Name = path.Split('/')[^1], FullPath = path, IsDirectory = false, UncompressedSize = size };

    private static ArchiveEntry Dir(string path) =>
        new() { Name = path.TrimEnd('/').Split('/')[^1], FullPath = path, IsDirectory = true };

    [Fact]
    public void Build_ArchivoPlano_SoloRaiz()
    {
        var root = ArchiveFolderTree.Build(new[] { File("gta-6.jpg"), File("leeme.txt") }, "gta-6.zip");

        Assert.True(root.IsRoot);
        Assert.Equal("gta-6.zip", root.Name);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void Build_DeduceCarpetasDeLasRutasDeFichero_AunkSinEntradasDeCarpeta()
    {
        var root = ArchiveFolderTree.Build(new[]
        {
            File("docs/nota.txt"),
            File("docs/sub/datos.bin"),
            File("imagenes/a.png"),
        }, "x.7z");

        Assert.Equal(new[] { "docs", "imagenes" }, root.Children.Select(c => c.Name).ToArray());
        var docs = root.Children.First(c => c.Name == "docs");
        Assert.Equal(new[] { "sub" }, docs.Children.Select(c => c.Name).ToArray());
        Assert.Equal("docs/sub", docs.Children[0].FullPath);
    }

    [Fact]
    public void Build_UsaEntradasDeCarpetaExplicitasYVacias()
    {
        var root = ArchiveFolderTree.Build(new[] { Dir("vacia/"), Dir("con/"), File("con/x.txt") }, "a.tar");

        Assert.Contains(root.Children, c => c.Name == "vacia");
        Assert.Contains(root.Children, c => c.Name == "con");
    }

    [Fact]
    public void EntriesIn_Raiz_DevuelveSubcarpetasYFicherosDeNivelSuperior()
    {
        var entries = new[]
        {
            File("raiz.txt"),
            File("docs/nota.txt"),
            File("docs/sub/hondo.txt"),
        };
        var root = ArchiveFolderTree.Build(entries, "x.zip");

        var rows = ArchiveFolderTree.EntriesIn(entries, root);

        // Primero la subcarpeta "docs", luego el fichero "raiz.txt".
        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].IsDirectory);
        Assert.Equal("docs", rows[0].Name);
        Assert.Equal("raiz.txt", rows[1].Name);
        Assert.DoesNotContain(rows, r => r.Name == "nota.txt"); // no es hijo directo de la raíz
    }

    [Fact]
    public void EntriesIn_Subcarpeta_SoloSusHijosDirectos()
    {
        var entries = new[]
        {
            File("docs/nota.txt"),
            File("docs/otra.txt"),
            File("docs/sub/hondo.txt"),
        };
        var root = ArchiveFolderTree.Build(entries, "x.zip");
        var docs = ArchiveFolderTree.Find(root, "docs")!;

        var rows = ArchiveFolderTree.EntriesIn(entries, docs);

        Assert.Equal(new[] { "sub", "nota.txt", "otra.txt" }, rows.Select(r => r.Name).ToArray());
        Assert.True(rows[0].IsDirectory);
        Assert.False(rows[1].IsDirectory);
    }

    [Fact]
    public void Find_RutaInexistente_DevuelveNull_YRaizVaciaDevuelveRaiz()
    {
        var root = ArchiveFolderTree.Build(new[] { File("docs/a.txt") }, "x.zip");

        Assert.Same(root, ArchiveFolderTree.Find(root, ""));
        Assert.Equal("docs", ArchiveFolderTree.Find(root, "docs")!.Name);
        Assert.Null(ArchiveFolderTree.Find(root, "no/existe"));
    }

    [Fact]
    public void Build_NormalizaSeparadoresYBarrasSobrantes()
    {
        var root = ArchiveFolderTree.Build(new[]
        {
            new ArchiveEntry { Name = "x", FullPath = @"carpeta\sub\x.txt", IsDirectory = false },
        }, "x.rar");

        var sub = ArchiveFolderTree.Find(root, "carpeta/sub");
        Assert.NotNull(sub);
        Assert.Equal("carpeta/sub", sub!.FullPath);
    }
}
