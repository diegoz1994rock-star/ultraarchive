using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;

namespace UltraArchive.Core.Services;

/// <summary>
/// Implementación por defecto de <see cref="IArchiveEngineFactory"/>: un registro en memoria de
/// fábricas por formato. En Fase 1 no hay ningún motor registrado todavía (se registran a partir
/// de la Fase 2 en el composition root de UltraArchive.App, uno por cada proyecto de motor), por lo
/// que <see cref="CreateReader"/>/<see cref="CreateWriter"/> lanzarán legítimamente
/// <see cref="UnsupportedFormatException"/> hasta entonces: es el comportamiento correcto, no un stub.
/// </summary>
public sealed class ArchiveEngineFactory : IArchiveEngineFactory
{
    private readonly Dictionary<ArchiveFormat, Func<IArchiveReader>> _readerFactories = new();
    private readonly Dictionary<ArchiveFormat, Func<IArchiveWriter>> _writerFactories = new();

    public void RegisterReader(ArchiveFormat format, Func<IArchiveReader> readerFactory)
    {
        ArgumentNullException.ThrowIfNull(readerFactory);
        _readerFactories[format] = readerFactory;
    }

    public void RegisterWriter(ArchiveFormat format, Func<IArchiveWriter> writerFactory)
    {
        ArgumentNullException.ThrowIfNull(writerFactory);
        _writerFactories[format] = writerFactory;
    }

    public bool CanRead(ArchiveFormat format) => _readerFactories.ContainsKey(format);

    public bool CanWrite(ArchiveFormat format) => _writerFactories.ContainsKey(format);

    public IReadOnlyCollection<ArchiveFormat> SupportedReadFormats => _readerFactories.Keys.ToList();

    public IReadOnlyCollection<ArchiveFormat> SupportedWriteFormats => _writerFactories.Keys.ToList();

    public IArchiveReader CreateReader(ArchiveFormat format)
    {
        if (_readerFactories.TryGetValue(format, out var factory))
        {
            return factory();
        }

        throw new UnsupportedFormatException(
            $"Todavía no hay soporte de lectura para el formato '{format}'. Este motor se incorporará en una fase posterior del desarrollo.");
    }

    public IArchiveWriter CreateWriter(ArchiveFormat format)
    {
        if (_writerFactories.TryGetValue(format, out var factory))
        {
            return factory();
        }

        var extra = format == ArchiveFormat.Rar
            ? " La creación de archivos RAR no está disponible por restricciones legales del formato propietario; usa 7Z o ZIP como alternativa."
            : " Este motor se incorporará en una fase posterior del desarrollo.";

        throw new UnsupportedFormatException($"Todavía no hay soporte de creación para el formato '{format}'.{extra}");
    }
}
