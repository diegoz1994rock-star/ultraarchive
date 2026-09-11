using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using SharpCompress.Writers.Tar;
using UltraArchive.Archives.Common;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using GZipArchiveSc = SharpCompress.Archives.GZip.GZipArchive;

namespace UltraArchive.Archives.Gzip;

/// <summary>
/// Escritor GZIP. El formato GZIP solo puede contener UN fichero (no es un contenedor multi-archivo
/// como ZIP/TAR): si el usuario selecciona un único archivo suelto se crea un ".gz" simple; si
/// selecciona varios archivos y/o carpetas, se combinan primero en un TAR y el resultado se comprime
/// como ".tar.gz" (el mismo comportamiento que "tar czf" en otras plataformas).
/// </summary>
public sealed class GZipArchiveWriter : IArchiveWriter
{
    public ArchiveFormat Format => ArchiveFormat.GZip;

    public async Task CreateAsync(CreateArchiveOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // El formato GZIP no tiene cifrado: falla si se pidió contraseña en vez de crear un .gz en claro.
        EncryptionSupport.Validate(options);
        SplitSupport.Validate(options);

        var isSingleLooseFile = options.SourcePaths.Count == 1 && File.Exists(options.SourcePaths[0]);

        if (isSingleLooseFile)
        {
            await CreatePlainGzipAsync(options, progress, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await CreateTarGzAsync(options, progress, cancellationToken).ConfigureAwait(false);
        }

        if (options.DeleteSourceAfterCompress)
        {
            DeleteSources(options.SourcePaths);
        }
    }

    private static async Task CreatePlainGzipAsync(CreateArchiveOptions options, IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        var sourcePath = options.SourcePaths[0];
        var sourceInfo = new FileInfo(sourcePath);
        var tracker = new OperationProgressTracker(sourceInfo.Length);

        using var archive = GZipArchiveSc.CreateArchive();
        // Ver nota en ZipArchiveWriter: IWritableArchive.AddEntry solo admite Stream, no ruta.
        archive.AddEntry(sourceInfo.Name, File.OpenRead(sourcePath), closeStream: true, sourceInfo.Length, sourceInfo.LastWriteTimeUtc);

        await Task.Run(() =>
        {
            using var outputStream = File.Create(options.OutputPath);
            archive.SaveTo(outputStream, new GZipWriterOptions(CompressionLevelMapper.ToDeflateLevel(options.CompressionLevel)));
        }, cancellationToken).ConfigureAwait(false);

        progress?.Report(tracker.ForEntryProgress(sourceInfo.Name, sourceInfo.Length));
    }

    private static async Task CreateTarGzAsync(CreateArchiveOptions options, IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        var files = SourceFileCollector.Collect(options.SourcePaths, options.PreserveFolderStructure);
        var tracker = new OperationProgressTracker(files.Sum(f => f.Length));

        // TarWriterOptions en vez de WriterOptions.ForTar(...): necesitamos fijar HeaderFormat a USTAR
        // explícitamente (ver la misma nota en TarArchiveWriter), y esa propiedad solo existe en el tipo
        // concreto, no en el WriterOptions genérico que devuelve ForTar.
        var writerOptions = new TarWriterOptions(CompressionType.GZip, finalizeArchiveOnClose: true, TarHeaderWriteFormat.USTAR)
        {
            CompressionLevel = CompressionLevelMapper.ToDeflateLevel(options.CompressionLevel),
        };

        await Task.Run(() =>
        {
            using var outputStream = File.Create(options.OutputPath);
            using var writer = WriterFactory.OpenWriter(outputStream, ArchiveType.Tar, writerOptions);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var sourceStream = File.OpenRead(file.FullPath);
                writer.Write(file.EntryKey, sourceStream, file.LastWriteTimeUtc);

                tracker.CompleteEntry(file.Length);
                progress?.Report(tracker.ForEntryProgress(file.EntryKey, file.Length));
            }
        }, cancellationToken).ConfigureAwait(false);
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
