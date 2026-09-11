using DiscUtils.Iso9660;

namespace UltraArchive.Tests.TestSupport;

/// <summary>
/// Genera imágenes ISO de prueba en memoria con <see cref="CDBuilder"/> (la misma librería que usa el
/// motor). No se embeben binarios: se construyen en cada test, deterministas y sin herramientas
/// externas. Para los casos hostiles (path traversal, nombres peligrosos) se parchean bytes sobre una
/// imagen generada — ver <see cref="IsoBytePatch"/>.
/// </summary>
internal static class IsoFixtures
{
    public const string VolumeLabel = "ULTRAARCHIVE";

    /// <summary>ISO9660 simple con un fichero en la raíz.</summary>
    public static byte[] SingleFile(string name = "LEEME.TXT", string content = "hola iso")
    {
        var builder = NewBuilder();
        builder.AddFile(name, System.Text.Encoding.UTF8.GetBytes(content));
        return Build(builder);
    }

    /// <summary>ISO9660 con carpetas anidadas y varios ficheros.</summary>
    public static byte[] WithFolders()
    {
        var builder = NewBuilder();
        builder.AddFile("RAIZ.TXT", System.Text.Encoding.UTF8.GetBytes("en la raiz"));
        builder.AddDirectory("DOCS");
        builder.AddFile(@"DOCS\NOTA.TXT", System.Text.Encoding.UTF8.GetBytes("una nota"));
        builder.AddDirectory(@"DOCS\SUB");
        builder.AddFile(@"DOCS\SUB\DATOS.BIN", RepeatingBytes(4096));
        return Build(builder);
    }

    /// <summary>ISO con árbol Joliet: nombres largos, con espacios y acentos.</summary>
    public static byte[] Joliet()
    {
        var builder = NewBuilder();
        builder.UseJoliet = true;
        builder.AddFile("Léeme primero.txt", System.Text.Encoding.UTF8.GetBytes("con Joliet"));
        builder.AddDirectory("Carpeta con espacios");
        builder.AddFile(@"Carpeta con espacios\Canción ñoña.txt", System.Text.Encoding.UTF8.GetBytes("acentos y ñ"));
        return Build(builder);
    }

    /// <summary>ISO vacía (solo el volumen, sin ficheros).</summary>
    public static byte[] Empty() => Build(NewBuilder());

    /// <summary>ISO con un fichero de <paramref name="sizeBytes"/> bytes (para streaming/progreso/cancelación).</summary>
    public static byte[] LargeFile(string name, int sizeBytes)
    {
        var builder = NewBuilder();
        builder.AddFile(name, RepeatingBytes(sizeBytes));
        return Build(builder);
    }

    public static byte[] RepeatingBytes(int count)
    {
        var data = new byte[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = (byte)(i % 251);
        }

        return data;
    }

    // CDBuilder tiene UseJoliet = true por defecto; los fixtures "ISO9660 puro" lo desactivan
    // explícitamente para que sean genuinamente sin árbol Joliet.
    /// <summary>
    /// ISO9660 (sin Joliet) con un único fichero cuyo nombre es exactamente <paramref name="markerName"/>
    /// (ASCII, sin extensión) para poder localizarlo y reescribirlo byte a byte en los tests hostiles.
    /// </summary>
    public static byte[] SingleFileNamed(string markerName, string content = "payload")
    {
        var builder = NewBuilder();
        builder.AddFile(markerName, System.Text.Encoding.UTF8.GetBytes(content));
        return Build(builder);
    }

    /// <summary>
    /// Reemplaza <b>todas</b> las apariciones de la secuencia ASCII <paramref name="original"/> por
    /// <paramref name="replacement"/> (misma longitud) en la imagen. Se usa para inyectar nombres
    /// hostiles (path traversal, dispositivos reservados) que <see cref="CDBuilder"/> no deja crear.
    /// </summary>
    public static byte[] PatchAsciiName(byte[] iso, string original, string replacement)
    {
        if (original.Length != replacement.Length)
        {
            throw new ArgumentException("El nombre de reemplazo debe tener exactamente la misma longitud.");
        }

        var from = System.Text.Encoding.ASCII.GetBytes(original);
        var to = System.Text.Encoding.ASCII.GetBytes(replacement);
        var patched = (byte[])iso.Clone();
        var replacements = 0;

        for (var i = 0; i + from.Length <= patched.Length; i++)
        {
            if (patched.AsSpan(i, from.Length).SequenceEqual(from))
            {
                to.CopyTo(patched.AsSpan(i, to.Length));
                replacements++;
                i += from.Length - 1;
            }
        }

        if (replacements == 0)
        {
            throw new InvalidOperationException($"No se encontró '{original}' en la imagen para parchear.");
        }

        return patched;
    }

    private static CDBuilder NewBuilder() => new() { VolumeIdentifier = VolumeLabel, UseJoliet = false };

    private static byte[] Build(CDBuilder builder)
    {
        using var ms = new MemoryStream();
        builder.Build(ms);
        return ms.ToArray();
    }
}
