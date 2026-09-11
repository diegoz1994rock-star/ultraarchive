using UltraArchive.Core.Models;

namespace UltraArchive.Core.Interfaces;

/// <summary>
/// Punto único de extensibilidad para registrar y resolver motores de formato.
/// Cada proyecto de motor (UltraArchive.Archives, UltraArchive.Iso) registra sus lectores/escritores
/// aquí en el arranque de la aplicación (composition root), en lugar de que el resto del código
/// dependa de tipos concretos de un formato. Esto es lo que permitirá añadir "plugins de formato"
/// en el futuro sin tocar UltraArchive.Core ni la UI.
/// </summary>
public interface IArchiveEngineFactory
{
    /// <summary>Registra una fábrica de lectores para un formato. Sobrescribe un registro previo del mismo formato.</summary>
    void RegisterReader(ArchiveFormat format, Func<IArchiveReader> readerFactory);

    /// <summary>Registra una fábrica de escritores para un formato. Sobrescribe un registro previo del mismo formato.</summary>
    void RegisterWriter(ArchiveFormat format, Func<IArchiveWriter> writerFactory);

    bool CanRead(ArchiveFormat format);

    bool CanWrite(ArchiveFormat format);

    /// <summary>Formatos para los que hay actualmente un lector registrado.</summary>
    IReadOnlyCollection<ArchiveFormat> SupportedReadFormats { get; }

    /// <summary>Formatos para los que hay actualmente un escritor registrado.</summary>
    IReadOnlyCollection<ArchiveFormat> SupportedWriteFormats { get; }

    /// <summary>
    /// Crea un lector para el formato indicado.
    /// Lanza <see cref="Exceptions.UnsupportedFormatException"/> si no hay ningún motor registrado
    /// (por ejemplo, porque el motor de ese formato aún no se ha implementado en la fase actual).
    /// </summary>
    IArchiveReader CreateReader(ArchiveFormat format);

    /// <summary>
    /// Crea un escritor para el formato indicado.
    /// Lanza <see cref="Exceptions.UnsupportedFormatException"/> si no hay ningún motor registrado,
    /// o si el formato (como RAR) no admite creación por restricciones legales del formato propietario.
    /// </summary>
    IArchiveWriter CreateWriter(ArchiveFormat format);
}
