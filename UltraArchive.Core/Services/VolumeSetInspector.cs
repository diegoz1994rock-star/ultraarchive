using System.Text.RegularExpressions;

namespace UltraArchive.Core.Services;

/// <summary>
/// Resultado de inspeccionar un conjunto de volúmenes (<c>Archivo.7z.001</c>, <c>.002</c>…) a partir
/// de una de sus partes. Lógica pura: recibe la lista de nombres de fichero de una carpeta.
/// </summary>
public sealed class VolumeSetInfo
{
    /// <summary>Nombre base sin el sufijo numérico (p. ej. <c>Archivo.7z</c>).</summary>
    public required string BaseName { get; init; }

    /// <summary>Extensión "interna" del archivo dividido en minúsculas, con punto (p. ej. <c>.7z</c>).</summary>
    public required string InnerExtension { get; init; }

    /// <summary>Números de parte presentes, ordenados (p. ej. [1, 2, 4]).</summary>
    public required IReadOnlyList<int> FoundParts { get; init; }

    /// <summary>Números de parte que faltan para que la secuencia sea 1..max sin huecos.</summary>
    public required IReadOnlyList<int> MissingParts { get; init; }

    /// <summary>Números de parte duplicados (mismo índice con distinto ancho de dígitos, p. ej. <c>.01</c> y <c>.001</c>).</summary>
    public required IReadOnlyList<int> DuplicateParts { get; init; }

    /// <summary>Nombres de fichero de todas las partes encontradas, en orden (para abrir el conjunto).</summary>
    public required IReadOnlyList<string> OrderedPartFileNames { get; init; }

    /// <summary>True si están todas las partes de 1 a max, sin huecos ni duplicados.</summary>
    public bool IsComplete => MissingParts.Count == 0 && DuplicateParts.Count == 0 && FoundParts.Count > 0;
}

/// <summary>
/// Reconoce y valida conjuntos de volúmenes con la nomenclatura estándar
/// <c>&lt;nombre&gt;.&lt;ext&gt;.&lt;NNN&gt;</c> (la que generan 7-Zip y compatibles).
/// No toca el disco: el llamador le pasa el nombre de una parte y los nombres de fichero de la carpeta.
/// </summary>
public static partial class VolumeSetInspector
{
    // "algo.7z.001" -> base "algo.7z", número "001". Al menos 2 dígitos (evita confundir con ".7z").
    [GeneratedRegex(@"^(?<base>.+)\.(?<num>\d{2,})$", RegexOptions.CultureInvariant)]
    private static partial Regex VolumeSuffixRegex();

    /// <summary>
    /// True si <paramref name="fileName"/> tiene forma de parte de un conjunto dividido
    /// (<c>...algo.NNN</c> con NNN de 2+ dígitos). No comprueba el contenido.
    /// </summary>
    public static bool IsVolumePartName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        var name = Path.GetFileName(fileName);
        return VolumeSuffixRegex().IsMatch(name);
    }

    /// <summary>Nombre base (sin el sufijo <c>.NNN</c>) de una parte, o null si no es una parte.</summary>
    public static string? TryGetBaseName(string fileName)
    {
        var match = VolumeSuffixRegex().Match(Path.GetFileName(fileName ?? string.Empty));
        return match.Success ? match.Groups["base"].Value : null;
    }

    /// <summary>
    /// Inspecciona el conjunto al que pertenece <paramref name="anyPartFileName"/> usando la lista de
    /// nombres de fichero <paramref name="siblingFileNames"/> de su misma carpeta. Devuelve null si
    /// <paramref name="anyPartFileName"/> no tiene forma de parte de un conjunto dividido.
    /// </summary>
    public static VolumeSetInfo? Inspect(string anyPartFileName, IEnumerable<string> siblingFileNames)
    {
        ArgumentNullException.ThrowIfNull(siblingFileNames);

        var baseName = TryGetBaseName(anyPartFileName);
        if (baseName is null)
        {
            return null;
        }

        var prefix = baseName + ".";
        var byNumber = new SortedDictionary<int, List<string>>();

        foreach (var sibling in siblingFileNames)
        {
            var name = Path.GetFileName(sibling);
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = name[prefix.Length..];
            if (suffix.Length < 2 || !suffix.All(char.IsAsciiDigit))
            {
                continue;
            }

            if (!int.TryParse(suffix, out var number) || number <= 0)
            {
                continue;
            }

            if (!byNumber.TryGetValue(number, out var list))
            {
                list = new List<string>();
                byNumber[number] = list;
            }

            list.Add(name);
        }

        var found = byNumber.Keys.ToList();
        var duplicates = byNumber.Where(kv => kv.Value.Count > 1).Select(kv => kv.Key).ToList();
        var max = found.Count > 0 ? found[^1] : 0;
        var missing = Enumerable.Range(1, max).Where(n => !byNumber.ContainsKey(n)).ToList();

        var ordered = new List<string>();
        foreach (var n in found)
        {
            // Si hay duplicados para un número, se toma el nombre con más dígitos (el "canónico" .001).
            ordered.Add(byNumber[n].OrderByDescending(s => s.Length).First());
        }

        var innerExt = Path.GetExtension(baseName).ToLowerInvariant();

        return new VolumeSetInfo
        {
            BaseName = baseName,
            InnerExtension = innerExt,
            FoundParts = found,
            MissingParts = missing,
            DuplicateParts = duplicates,
            OrderedPartFileNames = ordered,
        };
    }
}
