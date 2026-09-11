using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.SevenZip;
using UltraArchive.Archives.Common;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;

namespace UltraArchive.Archives.SevenZip;

/// <summary>
/// Escritor de archivos 7Z. A diferencia de ZIP/TAR, SharpCompress no expone una API de "archivo"
/// editable para 7Z (7Z no es un formato pensado para streaming, ver docs de SharpCompress); la única
/// forma soportada de crear un 7Z es la API de escritura secuencial <see cref="WriterFactory"/>, por
/// lo que este escritor solo implementa <see cref="IArchiveWriter"/> (creación), no
/// <see cref="IMutableArchiveWriter"/>: añadir/quitar entradas de un 7Z ya existente no es viable sin
/// recomprimir el archivo entero, así que la UI no debe ofrecer esas acciones para 7Z.
/// </summary>
public sealed class SevenZipArchiveWriter : IArchiveWriter
{
    public ArchiveFormat Format => ArchiveFormat.SevenZip;

    public async Task CreateAsync(CreateArchiveOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 7Z cifrado al crear no está disponible con la librería actual (llega en la Fase 6 vía 7za.exe).
        // Falla de forma explícita en vez de crear un 7Z sin cifrar cuando se pidió contraseña.
        EncryptionSupport.Validate(options);
        SplitSupport.Validate(options);

        // Este motor gestionado (SharpCompress) NO sabe dividir en volúmenes. La división de 7Z la
        // hace 7zr.exe a través de RoutingSevenZipWriter; si la petición llega aquí con división es
        // porque 7zr no está disponible → fallar claro en vez de crear un único .7z sin dividir.
        if (SplitSupport.WantsSplit(options))
        {
            throw new SplitNotSupportedException(
                "La división de 7Z en partes necesita el binario 7zr.exe verificado, que no está disponible " +
                "en esta instalación. Sin él se puede crear un 7Z único pero no dividirlo.");
        }

        var files = SourceFileCollector.Collect(options.SourcePaths, options.PreserveFolderStructure);
        var tracker = new OperationProgressTracker(files.Sum(f => f.Length));

        // SharpCompress.SevenZipWriterOptions solo admite LZMA/LZMA2 y no expone niveles de compresión
        // variables en esta versión (ver CompressionLevelMapper): se usa siempre LZMA2 con cabecera comprimida.
        var writerOptions = new SevenZipWriterOptions(CompressionType.LZMA2) { CompressHeader = true };

        await Task.Run(() =>
        {
            using var outputStream = File.Create(options.OutputPath);
            using var writer = WriterFactory.OpenWriter(outputStream, ArchiveType.SevenZip, writerOptions);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var sourceStream = File.OpenRead(file.FullPath);
                writer.Write(file.EntryKey, sourceStream, file.LastWriteTimeUtc);

                tracker.CompleteEntry(file.Length);
                progress?.Report(tracker.ForEntryProgress(file.EntryKey, file.Length));
            }
        }, cancellationToken).ConfigureAwait(false);

        if (options.DeleteSourceAfterCompress)
        {
            DeleteSources(options.SourcePaths);
        }
    }

    private static void DeleteSources(IReadOnlyList<string> sourcePaths)
    {
        foreach (var path in sourcePaths)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
