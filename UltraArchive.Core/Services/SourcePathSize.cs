namespace UltraArchive.Core.Services;

/// <summary>
/// Suma el tamaño (bytes) de un conjunto de rutas de origen (archivos y/o carpetas), recursivamente.
/// Solo lee metadatos (no abre los ficheros). Se usa para el modo "dividir en N partes": convertir
/// el número de partes en un tamaño por volumen. Los errores de acceso a un fichero suelto se ignoran
/// (el tamaño resultante es una estimación; 7zr trabajará con los datos reales).
/// </summary>
public static class SourcePathSize
{
    public static long TotalBytes(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        long total = 0;
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                {
                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // fichero inaccesible: se omite del cálculo
                    }
                }
            }
            else if (File.Exists(path))
            {
                try
                {
                    total += new FileInfo(path).Length;
                }
                catch
                {
                    // se omite
                }
            }
        }

        return total;
    }
}
