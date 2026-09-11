using System.IO;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Core;

/// <summary>
/// <see cref="CollisionResolver"/>: centraliza la política de colisiones de ficheros al extraer
/// (antes duplicada en cada motor). Incluye el diálogo interactivo (<see cref="CollisionPolicy.Ask"/>)
/// y el "aplicar a todos".
/// </summary>
public class CollisionResolverTests
{
    private static string ExistingFile(TempWorkspace ws, string name)
    {
        var path = Path.Combine(ws.OutputDir, name);
        Directory.CreateDirectory(ws.OutputDir);
        File.WriteAllText(path, "previo");
        return path;
    }

    [Fact]
    public void SinConflicto_DevuelveLaMismaRuta()
    {
        using var ws = new TempWorkspace();
        var target = Path.Combine(ws.OutputDir, "libre.txt");
        Directory.CreateDirectory(ws.OutputDir);

        var resolver = new CollisionResolver(CollisionPolicy.Ask, _ => CollisionResolution.Skip);

        Assert.Equal(target, resolver.Resolve(target));
    }

    [Fact]
    public void PoliticaOverwrite_DevuelveLaRutaExistente()
    {
        using var ws = new TempWorkspace();
        var existing = ExistingFile(ws, "a.txt");
        var resolver = new CollisionResolver(CollisionPolicy.Overwrite, null);

        Assert.Equal(existing, resolver.Resolve(existing));
    }

    [Fact]
    public void PoliticaSkip_DevuelveNull()
    {
        using var ws = new TempWorkspace();
        var existing = ExistingFile(ws, "a.txt");
        var resolver = new CollisionResolver(CollisionPolicy.Skip, null);

        Assert.Null(resolver.Resolve(existing));
    }

    [Fact]
    public void PoliticaRename_DevuelveUnaRutaLibre()
    {
        using var ws = new TempWorkspace();
        var existing = ExistingFile(ws, "a.txt");
        var resolver = new CollisionResolver(CollisionPolicy.RenameAutomatically, null);

        var resolved = resolver.Resolve(existing);

        Assert.NotNull(resolved);
        Assert.NotEqual(existing, resolved);
        Assert.False(File.Exists(resolved!));
        Assert.EndsWith(".txt", resolved);
    }

    [Fact]
    public void Ask_SinManejador_Sobrescribe()
    {
        using var ws = new TempWorkspace();
        var existing = ExistingFile(ws, "a.txt");
        var resolver = new CollisionResolver(CollisionPolicy.Ask, onCollision: null);

        Assert.Equal(existing, resolver.Resolve(existing));
    }

    [Fact]
    public void Ask_LlamaAlManejadorUnaVezPorConflicto()
    {
        using var ws = new TempWorkspace();
        var a = ExistingFile(ws, "a.txt");
        var b = ExistingFile(ws, "b.txt");
        var calls = 0;

        var resolver = new CollisionResolver(CollisionPolicy.Ask, _ =>
        {
            calls++;
            return CollisionResolution.Skip;
        });

        Assert.Null(resolver.Resolve(a));
        Assert.Null(resolver.Resolve(b));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Ask_ApplyToAll_NoVuelveAPreguntar()
    {
        using var ws = new TempWorkspace();
        var a = ExistingFile(ws, "a.txt");
        var b = ExistingFile(ws, "b.txt");
        var calls = 0;

        var resolver = new CollisionResolver(CollisionPolicy.Ask, _ =>
        {
            calls++;
            return new CollisionResolution(CollisionAction.Skip, ApplyToAll: true);
        });

        Assert.Null(resolver.Resolve(a));
        Assert.Null(resolver.Resolve(b));
        Assert.Equal(1, calls); // solo el primer conflicto pregunta
    }

    [Fact]
    public void Ask_Cancel_LanzaOperationCanceled()
    {
        using var ws = new TempWorkspace();
        var existing = ExistingFile(ws, "a.txt");
        var resolver = new CollisionResolver(CollisionPolicy.Ask, _ => CollisionResolution.Cancel);

        Assert.Throws<OperationCanceledException>(() => resolver.Resolve(existing));
    }

    [Fact]
    public void UniquePath_GeneraNombresIncrementales()
    {
        using var ws = new TempWorkspace();
        Directory.CreateDirectory(ws.OutputDir);
        var basePath = Path.Combine(ws.OutputDir, "doc.txt");
        File.WriteAllText(basePath, "0");
        File.WriteAllText(Path.Combine(ws.OutputDir, "doc (1).txt"), "1");

        var next = UniquePath.NextAvailable(basePath);

        Assert.Equal(Path.Combine(ws.OutputDir, "doc (2).txt"), next);
    }
}
