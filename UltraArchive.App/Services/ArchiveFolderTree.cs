using UltraArchive.App.ViewModels;
using UltraArchive.Core.Models;

namespace UltraArchive.App.Services;

/// <summary>
/// Construye el árbol de carpetas de un archivo abierto a partir de su lista plana de entradas, y
/// resuelve qué filas mostrar para una carpeta concreta. Lógica pura (sin WPF, sin IO) para poder
/// probarla de forma aislada. Funciona con cualquier formato: solo usa
/// <see cref="ArchiveEntry.FullPath"/> / <see cref="ArchiveEntry.IsDirectory"/>.
/// </summary>
public static class ArchiveFolderTree
{
    /// <summary>
    /// Devuelve el nodo raíz (<c>FullPath == ""</c>) con toda la jerarquía de carpetas. Las carpetas
    /// se deducen tanto de las entradas de tipo carpeta como de la ruta padre de cada fichero (hay
    /// formatos que no listan las carpetas explícitamente).
    /// </summary>
    public static ArchiveTreeNode Build(IEnumerable<ArchiveEntry> entries, string rootName)
    {
        var root = new ArchiveTreeNode { Name = rootName, FullPath = string.Empty, IsExpanded = true };
        var byPath = new Dictionary<string, ArchiveTreeNode>(StringComparer.OrdinalIgnoreCase)
        {
            [string.Empty] = root,
        };

        foreach (var entry in entries)
        {
            var normalized = Normalize(entry.FullPath);
            var folderPath = entry.IsDirectory ? normalized : ParentOf(normalized);
            EnsureFolder(folderPath, byPath, root);
        }

        SortRecursive(root);
        return root;
    }

    /// <summary>
    /// Filas a mostrar cuando está seleccionada la carpeta <paramref name="folderPath"/>: sus
    /// subcarpetas directas (como filas de tipo carpeta) seguidas de sus ficheros directos.
    /// </summary>
    public static IReadOnlyList<ArchiveEntry> EntriesIn(
        IReadOnlyList<ArchiveEntry> allEntries, ArchiveTreeNode folder)
    {
        var prefix = folder.FullPath.Length == 0 ? string.Empty : folder.FullPath + "/";

        var subFolders = folder.Children
            .Select(child => new ArchiveEntry
            {
                Name = child.Name,
                FullPath = child.FullPath,
                IsDirectory = true,
            });

        var files = allEntries
            .Where(e => !e.IsDirectory)
            .Where(e =>
            {
                var p = Normalize(e.FullPath);
                if (prefix.Length == 0)
                {
                    return !p.Contains('/');
                }

                return p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                       && !p[prefix.Length..].Contains('/');
            })
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);

        return subFolders.Concat(files).ToList();
    }

    /// <summary>Busca el nodo cuya ruta es <paramref name="fullPath"/>, o null.</summary>
    public static ArchiveTreeNode? Find(ArchiveTreeNode root, string fullPath)
    {
        var target = Normalize(fullPath);
        if (target.Length == 0)
        {
            return root;
        }

        var stack = new Stack<ArchiveTreeNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (string.Equals(node.FullPath, target, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                stack.Push(child);
            }
        }

        return null;
    }

    private static void EnsureFolder(string path, Dictionary<string, ArchiveTreeNode> byPath, ArchiveTreeNode root)
    {
        path = path.Trim('/');
        if (path.Length == 0 || byPath.ContainsKey(path))
        {
            return;
        }

        var lastSlash = path.LastIndexOf('/');
        var parentPath = lastSlash < 0 ? string.Empty : path[..lastSlash];
        var name = lastSlash < 0 ? path : path[(lastSlash + 1)..];

        EnsureFolder(parentPath, byPath, root);
        var parent = byPath[parentPath];

        var node = new ArchiveTreeNode { Name = name, FullPath = path };
        parent.Children.Add(node);
        byPath[path] = node;
    }

    private static void SortRecursive(ArchiveTreeNode node)
    {
        var ordered = node.Children.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        node.Children.Clear();
        foreach (var child in ordered)
        {
            node.Children.Add(child);
            SortRecursive(child);
        }
    }

    private static string Normalize(string? fullPath) =>
        (fullPath ?? string.Empty).Replace('\\', '/').Trim('/');

    private static string ParentOf(string normalizedPath)
    {
        var lastSlash = normalizedPath.LastIndexOf('/');
        return lastSlash < 0 ? string.Empty : normalizedPath[..lastSlash];
    }
}
