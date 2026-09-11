using System.Text.RegularExpressions;

namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Interpreta el progreso que 7zr escribe con <c>-bsp1</c>: líneas/fragmentos que contienen un
/// porcentaje (<c>0%</c> … <c>100%</c>, con espacios opcionales alrededor del número).
///
/// Reglas:
///   - Solo se acepta un entero entre 0 y 100. <c>123%</c>, <c>-5</c>, texto sin porcentaje → se ignoran.
///   - El progreso es <b>monotónico</b>: <see cref="Current"/> nunca retrocede.
///   - Mientras no se haya visto ningún porcentaje válido, <see cref="IsIndeterminate"/> es true.
///   - <see cref="Feed"/> nunca lanza.
/// </summary>
public sealed partial class SevenZipCliProgress
{
    // (?<!\d) evita tomar "234%" de "1234%". \s* tolera "  25 %".
    [GeneratedRegex(@"(?<!\d)(\d{1,3})\s*%", RegexOptions.CultureInvariant)]
    private static partial Regex PercentRegex();

    private static readonly char[] LineSeparators = { '\r', '\n' };

    /// <summary>Último porcentaje válido más alto observado (0-100). 0 hasta el primer porcentaje.</summary>
    public int Current { get; private set; }

    /// <summary>True si aún no se ha observado ningún porcentaje válido.</summary>
    public bool IsIndeterminate => !SawAnyPercent;

    /// <summary>True en cuanto se ha visto al menos un porcentaje válido.</summary>
    public bool SawAnyPercent { get; private set; }

    /// <summary>
    /// Procesa un trozo de salida de 7zr (puede contener varias líneas o fragmentos separados por
    /// CR/LF). Devuelve true si en este trozo apareció <b>al menos un porcentaje válido</b> — eso es
    /// lo que renueva el watchdog de inactividad.
    /// </summary>
    public bool Feed(string? chunk)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return false;
        }

        var sawValid = false;

        foreach (var fragment in chunk.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (Match match in PercentRegex().Matches(fragment))
            {
                if (int.TryParse(match.Groups[1].ValueSpan, out var percent) && percent is >= 0 and <= 100)
                {
                    sawValid = true;
                    SawAnyPercent = true;
                    if (percent > Current)
                    {
                        Current = percent;
                    }
                }
            }
        }

        return sawValid;
    }

    /// <summary>Utilidad pura: primer porcentaje válido (0-100) de <paramref name="text"/>, o false.</summary>
    public static bool TryParseFirstPercent(string? text, out int percent)
    {
        percent = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (Match match in PercentRegex().Matches(text))
        {
            if (int.TryParse(match.Groups[1].ValueSpan, out var value) && value is >= 0 and <= 100)
            {
                percent = value;
                return true;
            }
        }

        return false;
    }
}
