namespace UltraArchive.Core.Models;

/// <summary>
/// Cómo el usuario decide el tamaño de los volúmenes al comprimir en partes. Solo afecta a la UI:
/// ambos métodos se resuelven a un <see cref="CreateArchiveOptions.SplitVolumeSizeBytes"/> concreto
/// antes de llegar a los escritores (ver <see cref="Services.SplitCalculator"/>).
/// </summary>
public enum SplitMethod
{
    /// <summary>Sin división: la compresión funciona exactamente como siempre.</summary>
    None,

    /// <summary>El usuario indica el tamaño máximo de cada parte (100 MB, 5 GB, personalizado…).</summary>
    MaxSizePerVolume,

    /// <summary>El usuario indica en cuántas partes dividir; el tamaño por parte se calcula.</summary>
    NumberOfParts,
}

/// <summary>Unidad para el tamaño de volumen personalizado.</summary>
public enum SizeUnit
{
    MB,
    GB,
}
