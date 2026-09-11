namespace UltraArchive.Core.Services;

/// <summary>
/// Genera una ruta de fichero libre a partir de una que ya existe: "nombre (1).ext", "nombre (2).ext"…
/// Lógica pura compartida por todos los motores de extracción (Archives e Iso) para no duplicarla.
/// </summary>
public static class UniquePath
{
    /// <summary>
    /// Devuelve <paramref name="path"/> si no existe; en otro caso, la primera variante
    /// "nombre (N).ext" que no exista en el mismo directorio.
    /// </summary>
    public static string NextAvailable(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var counter = 1; ; counter++)
        {
            var candidate = Path.Combine(directory, $"{name} ({counter}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
