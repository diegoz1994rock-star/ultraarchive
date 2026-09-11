using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Writers.Tar;
using UltraArchive.Archives.Common;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using TarArchiveSc = SharpCompress.Archives.Tar.TarArchive;

namespace UltraArchive.Archives.Tar;

/// <summary>Escritor de archivos TAR sin comprimir. TAR no tiene "nivel de compresión" propio (ver CompressionLevelMapper).</summary>
public sealed class TarArchiveWriter : IArchiveWriter
{
    public ArchiveFormat Format => ArchiveFormat.Tar;

    public async Task CreateAsync(CreateArchiveOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // El formato TAR no tiene cifrado: falla si se pidió contraseña en vez de crear un TAR en claro.
        EncryptionSupport.Validate(options);
        SplitSupport.Validate(options);

        var files = SourceFileCollector.Collect(options.SourcePaths, options.PreserveFolderStructure);
        var tracker = new OperationProgressTracker(files.Sum(f => f.Length));

        using var archive = TarArchiveSc.CreateArchive();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Ver nota en ZipArchiveWriter: IWritableArchive.AddEntry solo admite Stream, no ruta.
            archive.AddEntry(file.EntryKey, File.OpenRead(file.FullPath), closeStream: true, file.Length, file.LastWriteTimeUtc);
        }

        await using (var outputStream = File.Create(options.OutputPath))
        {
            // HeaderFormat: USTAR explícito. El valor por defecto de SharpCompress (GNU_TAR_LONG_LINK) no
            // escribe la firma "ustar" en el offset 257 de cada cabecera, lo que rompe tanto la detección
            // de formato por firma de bytes de la Fase 1 (ArchiveFormatDetector.IsPosixTarAsync) como la
            // detección de "gzip que envuelve un tar" de GZipArchiveReader para los .tar.gz que creamos.
            var writerOptions = new TarWriterOptions(CompressionType.None, finalizeArchiveOnClose: true, TarHeaderWriteFormat.USTAR);
            await Task.Run(() => archive.SaveTo(outputStream, writerOptions), cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(new OperationProgress { BytesProcessed = tracker.TotalBytes, TotalBytes = tracker.TotalBytes, Elapsed = TimeSpan.Zero });

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
