using System.Text.RegularExpressions;

namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Reconoce el "banner" que imprime cualquier ejecutable de 7-Zip al arrancar sin argumentos, p. ej.:
///   <c>7-Zip 26.02 (x64) : Copyright (c) 1999-2026 Igor Pavlov : 2026-06-25</c>
///   <c>7-Zip (r) 24.09 (x64) : Copyright (c) 1999-2024 Igor Pavlov</c>
///   <c>7-Zip (A) 9.20  Copyright (c) 1999-2010 Igor Pavlov  2010-11-18</c>
///
/// Lógica pura (sin procesos) para poder probarla de forma aislada.
/// </summary>
public static partial class SevenZipBanner
{
    [GeneratedRegex(@"^\s*7-Zip(?:\s*\((?:r|a)\))?\s+(\d+)\.(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BannerRegex();

    /// <summary>
    /// Intenta extraer la versión (mayor.menor) de la primera línea reconocible de <paramref name="bannerText"/>.
    /// Devuelve false si el texto no empieza por un banner de 7-Zip válido.
    /// </summary>
    public static bool TryParseVersion(string? bannerText, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(bannerText))
        {
            return false;
        }

        foreach (var rawLine in bannerText.Split('\n'))
        {
            var match = BannerRegex().Match(rawLine.Trim());
            if (!match.Success)
            {
                continue;
            }

            if (int.TryParse(match.Groups[1].ValueSpan, out var major) &&
                int.TryParse(match.Groups[2].ValueSpan, out var minor))
            {
                version = new Version(major, minor);
                return true;
            }
        }

        return false;
    }
}
