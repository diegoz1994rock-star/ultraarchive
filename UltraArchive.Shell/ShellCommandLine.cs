namespace UltraArchive.Shell;

/// <summary>Acción con la que se ha lanzado UltraArchive desde la línea de comandos / el menú contextual.</summary>
public enum ShellAction
{
    /// <summary>Arranque normal, sin argumentos.</summary>
    None,

    /// <summary>Abrir el archivo indicado en la ventana principal ("Abrir con Ultra Archive").</summary>
    OpenFile,

    /// <summary>Extraer el archivo en una subcarpeta con su nombre ("Extraer en «Nombre»\").</summary>
    ExtractHere,

    /// <summary>Extraer el archivo directamente en su carpeta actual ("Extraer aquí").</summary>
    ExtractHereFlat,

    /// <summary>Extraer el archivo pidiendo carpeta de destino ("Extraer ficheros…").</summary>
    ExtractTo,

    /// <summary>Abrir la ventana "Comprimir" con los orígenes seleccionados ya cargados.</summary>
    Compress,

    /// <summary>Comprimir los orígenes en un único 7Z junto a ellos, sin diálogo ("Comprimir aquí").</summary>
    CompressHere,

    /// <summary>Comprimir los orígenes en un .ZIP junto a ellos, sin diálogo.</summary>
    CompressToZip,

    /// <summary>Comprimir los orígenes en un .7Z junto a ellos, sin diálogo.</summary>
    CompressTo7z,

    /// <summary>Abrir "Comprimir" con los orígenes cargados y la división en partes preseleccionada.</summary>
    CompressSplit,

    /// <summary>Instalar la integración con el Explorador para el usuario actual (HKCU) y salir.</summary>
    InstallShell,

    /// <summary>Desinstalar la integración con el Explorador del usuario actual (HKCU) y salir.</summary>
    UninstallShell,

    /// <summary>Instalar la integración para todos los usuarios (HKLM) — lo usa el instalador. Requiere administrador.</summary>
    InstallShellAllUsers,

    /// <summary>Desinstalar la integración de todos los usuarios (HKLM) — lo usa el desinstalador.</summary>
    UninstallShellAllUsers,
}

/// <summary>
/// Resultado de interpretar los argumentos de proceso. <see cref="Path"/> es la primera ruta (para
/// las acciones de un solo elemento, p. ej. extraer). <see cref="Paths"/> son todas las rutas
/// seleccionadas (para las acciones de compresión, que aceptan selección múltiple).
/// </summary>
public sealed record ShellCommand(ShellAction Action, string? Path, IReadOnlyList<string>? Paths = null)
{
    public static readonly ShellCommand Normal = new(ShellAction.None, null);

    /// <summary>Todas las rutas de la operación (nunca null; al menos la de <see cref="Path"/> si existe).</summary>
    public IReadOnlyList<string> AllPaths =>
        Paths is { Count: > 0 } ? Paths : Path is { Length: > 0 } ? new[] { Path } : Array.Empty<string>();
}

/// <summary>
/// Interpreta <c>Environment.GetCommandLineArgs()</c> / <c>e.Args</c> de forma estricta:
///
///   Un solo elemento (acciones de extracción / apertura):
///     <c>UltraArchive.exe "&lt;archivo&gt;"</c>                       → abrir
///     <c>UltraArchive.exe --extract-here "&lt;archivo&gt;"</c>        → extraer en subcarpeta
///     <c>UltraArchive.exe --extract-here-flat "&lt;archivo&gt;"</c>   → extraer aquí
///     <c>UltraArchive.exe --extract-to "&lt;archivo&gt;"</c>          → extraer eligiendo carpeta
///
///   Uno o varios elementos (acciones de compresión, selección múltiple del Explorador):
///     <c>UltraArchive.exe --compress "&lt;a&gt;" "&lt;b&gt;" …</c>
///     <c>UltraArchive.exe --compress-here | --compress-zip | --compress-7z | --compress-split  "&lt;a&gt;" …</c>
///
///   Sin ruta:
///     <c>UltraArchive.exe --install-shell | --uninstall-shell | --install-shell-allusers | --uninstall-shell-allusers</c>
///
/// No se pasa nada a un shell: los argumentos llegan ya separados por el SO. Aun así se valida el
/// número y la forma; cualquier cosa no reconocida → arranque normal (no se ejecuta nada raro).
/// </summary>
public static class ShellCommandLineParser
{
    private static readonly IReadOnlyDictionary<string, ShellAction> SinglePathVerbs = new Dictionary<string, ShellAction>
    {
        ["--extract-here"] = ShellAction.ExtractHere,
        ["--extract-here-flat"] = ShellAction.ExtractHereFlat,
        ["--extract-to"] = ShellAction.ExtractTo,
    };

    private static readonly IReadOnlyDictionary<string, ShellAction> MultiPathVerbs = new Dictionary<string, ShellAction>
    {
        ["--compress"] = ShellAction.Compress,
        ["--compress-here"] = ShellAction.CompressHere,
        ["--compress-zip"] = ShellAction.CompressToZip,
        ["--compress-7z"] = ShellAction.CompressTo7z,
        ["--compress-split"] = ShellAction.CompressSplit,
    };

    private static readonly IReadOnlyDictionary<string, ShellAction> NoPathVerbs = new Dictionary<string, ShellAction>
    {
        ["--install-shell"] = ShellAction.InstallShell,
        ["--uninstall-shell"] = ShellAction.UninstallShell,
        ["--install-shell-allusers"] = ShellAction.InstallShellAllUsers,
        ["--uninstall-shell-allusers"] = ShellAction.UninstallShellAllUsers,
    };

    public static ShellCommand Parse(IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0)
        {
            return ShellCommand.Normal;
        }

        var first = args[0];

        if (NoPathVerbs.TryGetValue(first, out var noPathAction))
        {
            return args.Count == 1 ? new ShellCommand(noPathAction, null) : ShellCommand.Normal;
        }

        if (SinglePathVerbs.TryGetValue(first, out var singleAction))
        {
            return args.Count == 2 && IsPlausiblePath(args[1])
                ? new ShellCommand(singleAction, args[1])
                : ShellCommand.Normal;
        }

        if (MultiPathVerbs.TryGetValue(first, out var multiAction))
        {
            var paths = new List<string>(args.Count - 1);
            for (var i = 1; i < args.Count; i++)
            {
                if (!IsPlausiblePath(args[i]))
                {
                    return ShellCommand.Normal; // una ruta mal formada invalida toda la operación
                }

                paths.Add(args[i]);
            }

            return paths.Count > 0 ? new ShellCommand(multiAction, paths[0], paths) : ShellCommand.Normal;
        }

        // Sin flag: el único argumento admitido es una ruta de archivo a abrir.
        if (args.Count == 1 && IsPlausiblePath(first))
        {
            return new ShellCommand(ShellAction.OpenFile, first);
        }

        return ShellCommand.Normal;
    }

    /// <summary>
    /// Comprobación de forma (no de existencia): no vacía, sin saltos de línea ni NUL, y no empieza
    /// por '-' (evita que un flag mal escrito se tome como ruta).
    /// </summary>
    private static bool IsPlausiblePath(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.IndexOfAny(new[] { '\r', '\n', '\0' }) < 0
        && !value.StartsWith('-');
}
