using System.Text;

namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Produce una representación <b>segura para logs y diagnóstico</b> de los argumentos que se pasarán
/// a 7zr.exe: enmascara el valor de los switches sensibles.
///
/// Hoy el único sensible es <c>-p&lt;contraseña&gt;</c>. El diseño está preparado para añadir más
/// prefijos a <see cref="SensitiveSwitchPrefixes"/> sin cambiar nada más.
///
/// Garantías:
///   - <c>-pLoQueSea</c> → <c>-p***</c> (no se filtra ni el valor ni su longitud).
///   - <c>-p</c> a secas (sin valor) se deja tal cual.
///   - La línea de comandos real nunca se registra: este redactor es el único formato de "comando"
///     que se expone.
/// </summary>
public static class SevenZipArgRedactor
{
    /// <summary>Prefijos de switch cuyo valor debe ocultarse. Ampliable en el futuro.</summary>
    public static readonly IReadOnlyList<string> SensitiveSwitchPrefixes = new[] { "-p" };

    private const string Mask = "***";

    /// <summary>Redacta y une los argumentos en un único string legible.</summary>
    public static string Redact(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var builder = new StringBuilder();
        for (var i = 0; i < arguments.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            var safe = RedactOne(arguments[i]);
            // Solo para legibilidad del log; no es escapado real (los args reales van por ArgumentList).
            if (safe.Contains(' ') || safe.Length == 0)
            {
                builder.Append('"').Append(safe).Append('"');
            }
            else
            {
                builder.Append(safe);
            }
        }

        return builder.ToString();
    }

    /// <summary>Redacta un único argumento.</summary>
    public static string RedactOne(string? argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return string.Empty;
        }

        foreach (var prefix in SensitiveSwitchPrefixes)
        {
            if (argument.Length > prefix.Length && argument.StartsWith(prefix, StringComparison.Ordinal))
            {
                return prefix + Mask;
            }
        }

        return argument;
    }
}
