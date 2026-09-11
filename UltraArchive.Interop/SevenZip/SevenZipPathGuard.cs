namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Comprobaciones para que ninguna ruta controlada por el usuario (orígenes o salida) pueda
/// interpretarse como una opción de 7-Zip ni inyectar entradas extra.
///
/// Es el sustituto de <c>--</c>: 7-Zip deshabilita el parseo de <c>@listfile</c> cuando se usa
/// <c>--</c> (documentado, y bug #2221), así que en su lugar validamos cada ruta:
///   - debe ser <b>totalmente cualificada</b> (unidad o UNC) — nunca relativa;
///   - no puede empezar por <c>-</c> ni por <c>@</c> (caracteres reservados de 7-Zip);
///   - no puede contener CR, LF ni NUL (evita inyectar líneas extra en el response file).
/// </summary>
internal static class SevenZipPathGuard
{
    private static readonly char[] ForbiddenControlChars = { '\r', '\n', '\0' };

    public static void EnsureSafe(string? path, string role)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"Hay una {role} vacía.");
        }

        if (path.IndexOfAny(ForbiddenControlChars) >= 0)
        {
            throw new ArgumentException($"Una {role} contiene caracteres de control no permitidos (salto de línea o nulo).");
        }

        if (path[0] is '-' or '@')
        {
            throw new ArgumentException(
                $"Una {role} empieza por el carácter reservado '{path[0]}' y 7-Zip podría interpretarla como una opción.");
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"Una {role} no es una ruta absoluta totalmente cualificada.");
        }
    }
}
