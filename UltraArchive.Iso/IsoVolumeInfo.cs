namespace UltraArchive.Iso;

/// <summary>
/// Información del volumen de una imagen ISO, obtenida de los Volume Descriptors (a partir del
/// sector 16) sin abrir el sistema de archivos completo.
///
/// De momento solo la consume <see cref="IsoArchiveReader"/> internamente para decidir cómo abrir la
/// imagen; se expone como estructura pública para poder mostrarla en la UI en una fase posterior
/// (etiqueta de volumen, fecha, si trae Joliet / Rock Ridge / UDF) sin cambiar la arquitectura.
/// </summary>
public sealed class IsoVolumeInfo
{
    /// <summary>True si se encontró un Primary Volume Descriptor ISO9660 válido (firma "CD001").</summary>
    public required bool IsIso9660 { get; init; }

    /// <summary>True si hay un Supplementary Volume Descriptor con secuencia de escape Joliet (nombres Unicode).</summary>
    public required bool HasJoliet { get; init; }

    /// <summary>
    /// True si el conjunto de descriptores incluye la secuencia de reconocimiento UDF (NSR02/NSR03).
    /// UltraArchive **no** lee UDF en esta versión: si la imagen es UDF pura se rechaza con un mensaje claro.
    /// </summary>
    public required bool HasUdf { get; init; }

    /// <summary>Etiqueta de volumen (campo Volume Identifier del PVD), ya recortada. Puede ser null o vacía.</summary>
    public string? VolumeLabel { get; init; }

    /// <summary>Fecha de creación del volumen declarada en el PVD, si es válida.</summary>
    public DateTimeOffset? CreatedUtc { get; init; }
}
