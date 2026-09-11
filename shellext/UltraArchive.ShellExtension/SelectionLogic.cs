#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace UltraArchive.ShellExtension;

/// <summary>
/// Lógica pura (sin COM, sin proceso, sin registro) de la extensión de shell: se separa aquí
/// precisamente para poder testearla con xUnit desde <c>UltraArchive.ShellExtension.Tests</c>
/// (un proyecto normal net8.0, fuera del .sln como el resto de <c>shellext/</c>, pero SIN Native
/// AOT — así sí puede referenciar xUnit). El resto de la clase (<see cref="ExplorerCommandBase"/>)
/// llama a estos métodos; nunca duplica la lógica.
/// </summary>
internal static class SelectionLogic
{
    /// <summary>
    /// Nombre base para los títulos dinámicos ("Comprimir en «stem.zip»", "Extraer en «stem\»"):
    /// quita la extensión, y la doble en el caso de <c>.tar.gz</c>/<c>.tar.bz2</c>. Nunca vacío
    /// (cae a "archivo") para que el título del menú nunca quede en blanco.
    /// </summary>
    public static string StemFromPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return "archivo";

        string name = Path.GetFileNameWithoutExtension(path);
        if (Path.GetExtension(name).Equals(".tar", StringComparison.OrdinalIgnoreCase))
            name = Path.GetFileNameWithoutExtension(name);

        return name.Length == 0 ? "archivo" : name;
    }

    /// <summary>
    /// Construye la lista de argumentos para <c>UltraArchive.exe</c>: <paramref name="flag"/>
    /// primero (si no es null/vacío) y luego todas las rutas, en orden. Debe coincidir exactamente
    /// con lo que <c>ShellCommandLineParser</c> (en <c>UltraArchive.Shell</c>) espera: el flag como
    /// primer argumento, las rutas después, ninguna concatenación en una sola cadena.
    /// </summary>
    public static IReadOnlyList<string> BuildArguments(string? flag, IReadOnlyList<string> paths)
    {
        var args = new List<string>(paths.Count + 1);
        if (!string.IsNullOrEmpty(flag))
            args.Add(flag);
        args.AddRange(paths);
        return args;
    }
}
