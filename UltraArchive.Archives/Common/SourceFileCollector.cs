namespace UltraArchive.Archives.Common;

/// <summary>
/// Recorre las rutas de origen elegidas por el usuario (archivos sueltos y/o carpetas) y calcula la
/// clave (ruta dentro del archivo) de cada fichero a añadir, respetando o aplanando la estructura de
/// carpetas según <c>preserveFolderStructure</c>. Compartido por los escritores de ZIP, TAR, 7Z y GZIP.
/// </summary>
internal static class SourceFileCollector
{
    public sealed record CollectedFile(string EntryKey, string FullPath, long Length, DateTime LastWriteTimeUtc);

    public static IReadOnlyList<CollectedFile> Collect(IReadOnlyList<string> sourcePaths, bool preserveFolderStructure)
    {
        var files = new List<CollectedFile>();

        foreach (var sourcePath in sourcePaths)
        {
            if (Directory.Exists(sourcePath))
            {
                var baseDir = sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var rootFolderName = Path.GetFileName(baseDir);

                foreach (var filePath in Directory.EnumerateFiles(baseDir, "*", SearchOption.AllDirectories))
                {
                    var info = new FileInfo(filePath);
                    var key = preserveFolderStructure
                        ? $"{rootFolderName}/{Path.GetRelativePath(baseDir, filePath).Replace('\\', '/')}"
                        : info.Name;

                    files.Add(new CollectedFile(key, filePath, info.Length, info.LastWriteTimeUtc));
                }
            }
            else if (File.Exists(sourcePath))
            {
                var info = new FileInfo(sourcePath);
                files.Add(new CollectedFile(info.Name, sourcePath, info.Length, info.LastWriteTimeUtc));
            }
            else
            {
                throw new FileNotFoundException($"No se encontró el origen '{sourcePath}'.", sourcePath);
            }
        }

        return DisambiguateDuplicateKeys(files);
    }

    /// <summary>Evita colisiones de nombre cuando, por ejemplo, se aplana la estructura y dos archivos comparten nombre.</summary>
    private static List<CollectedFile> DisambiguateDuplicateKeys(List<CollectedFile> files)
    {
        var seenCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CollectedFile>(files.Count);

        foreach (var file in files)
        {
            if (!seenCounts.TryGetValue(file.EntryKey, out var count))
            {
                seenCounts[file.EntryKey] = 0;
                result.Add(file);
                continue;
            }

            count++;
            seenCounts[file.EntryKey] = count;

            var extension = Path.GetExtension(file.EntryKey);
            var baseName = file.EntryKey[..^extension.Length];
            result.Add(file with { EntryKey = $"{baseName} ({count}){extension}" });
        }

        return result;
    }
}
