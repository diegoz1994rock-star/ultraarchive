namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Genera el nombre del <c>.7z</c> <b>temporal</b> que 7zr escribirá, en el <b>mismo directorio</b>
/// que el archivo final. El archivo final no se toca: en un paso posterior, el writer moverá el
/// temporal al destino solo si 7zr terminó con éxito.
///
///   - misma carpeta que la ruta final (mismo volumen → el movimiento posterior es atómico);
///   - nombre no predecible (GUID), prefijo <c>.uatmp-</c>, extensión <c>.7z</c>;
///   - no sobrescribe un archivo existente.
/// </summary>
public static class SevenZipTempArchivePath
{
    public const string Prefix = ".uatmp-";
    public const string Extension = ".7z";

    public static string Create(string finalOutputPath)
    {
        if (string.IsNullOrWhiteSpace(finalOutputPath))
        {
            throw new ArgumentException("La ruta de salida final no puede estar vacía.", nameof(finalOutputPath));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(finalOutputPath));
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException("La ruta de salida final no tiene directorio.", nameof(finalOutputPath));
        }

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var candidate = Path.Combine(directory, $"{Prefix}{Guid.NewGuid():N}{Extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("No se pudo generar un nombre de archivo temporal único para el 7Z.");
    }
}
