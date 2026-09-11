using UltraArchive.Core.Exceptions;

namespace UltraArchive.Security;

/// <summary>
/// Protección contra Zip Slip / path traversal y contra nombres de archivo peligrosos: calcula la
/// ruta de destino real de una entrada de archivo y garantiza que:
///   1. queda dentro de la carpeta de destino elegida por el usuario (nada de "..", rutas absolutas,
///      unidades ni UNC), y
///   2. cada segmento del nombre es válido y seguro en Windows (sin caracteres prohibidos, sin
///      nombres de dispositivo reservados como "CON"/"COM1", sin punto o espacio final).
///
/// Cualquier entrada que no cumpla se rechaza con <see cref="PathTraversalException"/> (o su subtipo
/// <see cref="UnsafeEntryNameException"/>); los llamadores la reportan como "bloqueada" y siguen con
/// el resto de la extracción. Ampliado en la Fase 4 con la validación de nombres de Windows.
/// </summary>
public static class PathSecurity
{
    // Caracteres nunca válidos en un nombre de archivo/carpeta de Windows (además de '/' y '\', que
    // aquí ya se han tratado como separadores). Incluye los de control 0x00-0x1F.
    private static readonly char[] InvalidNameChars = { '<', '>', ':', '"', '|', '?', '*' };

    // Nombres de dispositivo DOS reservados: no pueden ser el nombre de un archivo ni aun con extensión.
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Resuelve la ruta absoluta de escritura para <paramref name="entryRelativePath"/> dentro de
    /// <paramref name="destinationRoot"/>. Lanza <see cref="PathTraversalException"/> si la entrada
    /// intenta escapar de esa carpeta, o <see cref="UnsafeEntryNameException"/> si algún segmento del
    /// nombre no es válido en Windows.
    /// </summary>
    public static string ResolveSafeDestinationPath(string destinationRoot, string entryRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryRelativePath);

        var normalizedRelative = entryRelativePath.Replace('\\', '/');

        // Rutas absolutas (con o sin unidad) o UNC ("\\servidor\recurso") se rechazan directamente:
        // ninguna entrada de un archivo tiene por qué señalar una ruta absoluta del sistema.
        if (normalizedRelative.Contains(':') ||
            normalizedRelative.StartsWith("//", StringComparison.Ordinal) ||
            Path.IsPathRooted(normalizedRelative))
        {
            throw new PathTraversalException(entryRelativePath);
        }

        ValidateWindowsSegments(entryRelativePath, normalizedRelative);

        var rootFull = Path.GetFullPath(destinationRoot);
        var combined = Path.GetFullPath(Path.Combine(rootFull, normalizedRelative));

        var rootWithSeparator = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;

        var staysInsideRoot =
            combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(combined, rootFull, StringComparison.OrdinalIgnoreCase);

        if (!staysInsideRoot)
        {
            throw new PathTraversalException(entryRelativePath);
        }

        return combined;
    }

    private static void ValidateWindowsSegments(string originalEntryPath, string normalizedRelative)
    {
        foreach (var segment in normalizedRelative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
            {
                // El control de escape de más abajo ya lo cubre, pero lo cortamos aquí también.
                continue;
            }

            foreach (var c in segment)
            {
                if (c < ' ' || Array.IndexOf(InvalidNameChars, c) >= 0)
                {
                    throw new UnsafeEntryNameException(originalEntryPath,
                        $"contiene un carácter no válido en Windows ('{(c < ' ' ? $"\\x{(int)c:X2}" : c.ToString())}')");
                }
            }

            if (segment.EndsWith('.') || segment.EndsWith(' '))
            {
                throw new UnsafeEntryNameException(originalEntryPath,
                    "tiene un segmento que termina en punto o espacio (no válido en Windows)");
            }

            var nameWithoutExtension = segment.Contains('.')
                ? segment[..segment.IndexOf('.')]
                : segment;

            if (ReservedDeviceNames.Contains(nameWithoutExtension))
            {
                throw new UnsafeEntryNameException(originalEntryPath,
                    $"usa el nombre de dispositivo reservado de Windows '{nameWithoutExtension}'");
            }
        }
    }
}
