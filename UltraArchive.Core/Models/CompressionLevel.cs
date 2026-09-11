namespace UltraArchive.Core.Models;

/// <summary>Nivel de compresión solicitado al crear un archivo. No todos los formatos soportan todos los niveles.</summary>
public enum CompressionLevel
{
    /// <summary>Sin compresión, solo empaquetado (equivalente a "Store").</summary>
    Store,
    Fastest,
    Fast,
    Normal,
    Maximum,
    Ultra
}
