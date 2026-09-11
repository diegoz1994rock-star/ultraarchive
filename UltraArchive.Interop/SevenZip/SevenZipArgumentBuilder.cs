using UltraArchive.Core.Models;

namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Convierte un <see cref="SevenZipRequest"/> (ya validado) + un <see cref="SevenZipResponseFile"/>
/// en la lista de argumentos exacta para 7zr.exe.
///
/// Reglas de construcción:
///   - <b>Nunca</b> se concatena una línea de comandos: cada token es un elemento de la lista, que
///     el llamador copiará a <c>ProcessStartInfo.ArgumentList</c> (sin <c>UseShellExecute</c>).
///   - Los switches y sus valores fijos (<c>-t7z</c>, <c>-mhe=on</c>, <c>-mx=N</c>, <c>-y</c>,
///     <c>-bsp1</c>, <c>-scsUTF-8</c>) los pone UltraArchive; ningún valor del usuario puede
///     convertirse en uno de ellos.
///   - Los datos del usuario aparecen solo en dos posiciones no-switch: la ruta de salida (absoluta,
///     revalidada) y <c>@&lt;response file&gt;</c> (cuyas líneas son nombres, nunca switches).
///
/// Sobre <c>--</c>: 7-Zip deshabilita el parseo de <c>@listfile</c> tras <c>--</c> (documentado; ver
/// bug #2221), así que no se puede usar <c>--</c> junto con el response file. En su lugar,
/// <see cref="SevenZipPathGuard"/> rechaza cualquier ruta que empiece por <c>-</c> o <c>@</c> o que
/// contenga saltos de línea — protección equivalente (y algo más estricta) que <c>--</c>.
/// </summary>
public sealed class SevenZipArgumentBuilder
{
    public SevenZipArguments Build(SevenZipRequest request, SevenZipResponseFile responseFile)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(responseFile);

        // Defensa en profundidad: revalidar justo antes de construir los argumentos.
        SevenZipPathGuard.EnsureSafe(request.OutputPath, "ruta de salida");
        SevenZipPathGuard.EnsureSafe(responseFile.Path, "response file");

        var args = new List<string>
        {
            "a",                     // añadir a archivo
            "-t7z",                  // formato 7z, fijo
            MapCompressionLevel(request.Level),
        };

        if (request.VolumeSizeBytes is { } volumeBytes and > 0)
        {
            // -v<n>b : divide en volúmenes de exactamente n bytes → salida.7z.001, .002…
            // n lo fija UltraArchive (un long positivo), nunca es una cadena del usuario.
            args.Add("-v" + volumeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "b");
        }

        if (request.EncryptHeaders)
        {
            args.Add("-mhe=on");     // valor literal "on", nunca del usuario
        }

        if (request.Password.Length > 0)
        {
            args.Add("-p" + request.Password);   // limitación conocida de 7zr (contraseña en argv)
        }

        args.Add("-y");                      // asumir "sí" a las preguntas
        args.Add("-bsp1");                   // progreso (%) por stdout
        args.Add("-scsUTF-8");               // charset del response file = UTF-8
        args.Add(request.OutputPath);        // ruta absoluta, revalidada (no puede ser un switch)
        args.Add("@" + responseFile.Path);   // listfile: sus líneas son nombres, nunca switches

        return new SevenZipArguments(args);
    }

    private static string MapCompressionLevel(CompressionLevel level) => level switch
    {
        CompressionLevel.Store => "-mx=0",
        CompressionLevel.Fastest => "-mx=1",
        CompressionLevel.Fast => "-mx=3",
        CompressionLevel.Normal => "-mx=5",
        CompressionLevel.Maximum => "-mx=7",
        CompressionLevel.Ultra => "-mx=9",
        _ => throw new ArgumentException($"Nivel de compresión no soportado por 7zr: {level}.", nameof(level)),
    };
}
