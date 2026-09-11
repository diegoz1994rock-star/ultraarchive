namespace UltraArchive.Core.Models;

/// <summary>
/// Formatos de archivo comprimido/imagen soportados por UltraArchive.
/// El soporte real de lectura/escritura de cada formato se registra en
/// <see cref="Interfaces.IArchiveEngineFactory"/> a medida que se implementan los motores
/// (ver hoja de ruta por fases).
/// </summary>
public enum ArchiveFormat
{
    /// <summary>No se ha podido determinar el formato a partir de la firma de bytes ni de la extensión.</summary>
    Unknown = 0,

    /// <summary>ZIP (PKWARE), incluye variantes cifradas con ZipCrypto o AES.</summary>
    Zip,

    /// <summary>7-Zip (7z).</summary>
    SevenZip,

    /// <summary>RAR 4.x o RAR5. Solo lectura/extracción por restricciones legales del formato propietario.</summary>
    Rar,

    /// <summary>TAR (POSIX ustar o GNU tar).</summary>
    Tar,

    /// <summary>GZIP, normalmente envolviendo un flujo TAR (.tar.gz/.tgz).</summary>
    GZip,

    /// <summary>BZip2.</summary>
    BZip2,

    /// <summary>Imagen de disco óptico ISO9660 (con o sin extensiones Joliet).</summary>
    Iso9660
}
