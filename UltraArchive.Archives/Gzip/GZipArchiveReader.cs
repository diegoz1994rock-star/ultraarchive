using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using SharpCompress.Archives;
using SharpCompress.Archives.GZip;
using SharpCompress.Readers;
using UltraArchive.Archives.Common;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using UltraArchive.Security;

namespace UltraArchive.Archives.Gzip;

/// <summary>
/// Lector de archivos GZIP. Un .gz "puro" solo puede contener un único fichero (así es el formato);
/// cuando ese fichero es en realidad un TAR (el caso típico ".tar.gz"), este lector lo detecta
/// decodificando los primeros bytes y delega en la API de lectura en streaming de SharpCompress
/// (<see cref="ReaderFactory"/>), que soporta TAR comprimido de forma nativa.
///
/// Nota de rendimiento: para ".tar.gz" no hay forma de conocer el tamaño total sin comprimir por
/// adelantado sin descomprimir dos veces, así que el progreso de extracción se calcula sobre los
/// bytes COMPRIMIDOS ya leídos del archivo (posición en el fichero de entrada) en vez de sobre bytes
/// descomprimidos: es una aproximación razonable y de una sola pasada, no una medida exacta.
/// </summary>
public sealed class GZipArchiveReader : IArchiveReader
{
    private string? _archivePath;
    private bool _wrapsTar;
    private IArchive? _plainGzipArchive;
    private bool _disposed;

    public ArchiveFormat Format => ArchiveFormat.GZip;

    public bool IsPasswordProtected => false;

    public async Task OpenAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _archivePath = archivePath;

        try
        {
            _wrapsTar = await DecompressedContentIsTarAsync(archivePath, cancellationToken).ConfigureAwait(false);
            if (!_wrapsTar)
            {
                _plainGzipArchive = GZipArchive.OpenArchive(archivePath);

                // Ver nota equivalente en ZipArchiveReader: fuerza la validación de la estructura del
                // archivo ahora, para que un GZIP corrupto se traduzca a ArchiveCorruptedException al abrir.
                _ = _plainGzipArchive.Entries.Count();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ArchiveCorruptedException($"No se pudo abrir '{Path.GetFileName(archivePath)}' como GZIP. Puede estar dañado o no ser un GZIP válido.", ex);
        }
    }

    public Task<IReadOnlyList<ArchiveEntry>> GetEntriesAsync(CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        cancellationToken.ThrowIfCancellationRequested();

        if (!_wrapsTar)
        {
            IReadOnlyList<ArchiveEntry> single = _plainGzipArchive!.Entries.Select(ArchiveEntryMapper.ToArchiveEntry).ToList();
            return Task.FromResult(single);
        }

        var entries = new List<ArchiveEntry>();
        using var fileStream = File.OpenRead(_archivePath!);
        using var reader = ReaderFactory.OpenReader(fileStream);
        while (reader.MoveToNextEntry())
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(ArchiveEntryMapper.ToArchiveEntry(reader.Entry));
        }

        return Task.FromResult<IReadOnlyList<ArchiveEntry>>(entries);
    }

    public Task<ExtractionResult> ExtractAsync(ExtractOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        Directory.CreateDirectory(options.DestinationPath);

        return _wrapsTar
            ? ExtractTarStreamAsync(options, progress, cancellationToken)
            : ExtractSingleFileAsync(options, progress, cancellationToken);
    }

    public async Task<bool> TestIntegrityAsync(IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureOpen();

        if (!_wrapsTar)
        {
            // Reabre el .gz desde disco en vez de reutilizar _plainGzipArchive: SharpCompress cachea la
            // entrada tras la primera enumeración completa de Entries (ya ocurrida en GetEntriesAsync) y
            // GZipArchiveEntry.OpenEntryStream() no admite abrirse una segunda vez sobre esa misma
            // entrada cacheada (lanza ObjectDisposedException sobre el DeflateStream ya consumido).
            using var archive = GZipArchive.OpenArchive(_archivePath!);
            var entry = archive.Entries.Single();
            return await ReadThroughAndVerifyCrcAsync(entry.OpenEntryStream(), entry.Crc, entry.Size, entry.Key ?? Path.GetFileName(_archivePath!), progress, cancellationToken)
                .ConfigureAwait(false);
        }

        var allOk = true;
        var totalCompressedBytes = new FileInfo(_archivePath!).Length;
        var stopwatch = Stopwatch.StartNew();

        using var fileStream = File.OpenRead(_archivePath!);
        using var reader = ReaderFactory.OpenReader(fileStream);

        while (reader.MoveToNextEntry())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            try
            {
                using var entryStream = reader.OpenEntryStream();
                var buffer = new byte[StreamCopy.BufferSize];
                int read;
                while ((read = await entryStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    progress?.Report(new OperationProgress
                    {
                        CurrentEntryName = reader.Entry.Key,
                        BytesProcessed = fileStream.Position,
                        TotalBytes = totalCompressedBytes,
                        Elapsed = stopwatch.Elapsed,
                    });
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                allOk = false;
            }
        }

        return allOk;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _plainGzipArchive?.Dispose();
        _disposed = true;
    }

    private async Task<ExtractionResult> ExtractSingleFileAsync(ExtractOptions options, IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        // Ver nota equivalente en TestIntegrityAsync: se reabre el .gz para garantizar un stream de
        // entrada fresco, independiente de si GetEntriesAsync ya enumeró (y por tanto cacheó) la entrada.
        using var archive = GZipArchive.OpenArchive(_archivePath!);
        var entry = archive.Entries.Single();
        var key = NormalizeKey(entry.Key);
        if (key.Length == 0)
        {
            key = Path.GetFileNameWithoutExtension(_archivePath!);
        }

        string destinationPath;
        try
        {
            destinationPath = PathSecurity.ResolveSafeDestinationPath(options.DestinationPath, key);
        }
        catch (PathTraversalException)
        {
            return new ExtractionResult { ExtractedCount = 0, BlockedEntries = [key] };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        var resolvedPath = new CollisionResolver(options).Resolve(destinationPath);
        if (resolvedPath is null)
        {
            return new ExtractionResult { ExtractedCount = 0, SkippedEntries = [key] };
        }
        destinationPath = resolvedPath;

        var compressedLength = new FileInfo(_archivePath!).Length;
        var tracker = new OperationProgressTracker(entry.Size > 0 ? entry.Size : compressedLength);
        var ratioGuard = new DecompressionGuard.RatioGuard(compressedLength);

        using var entryStream = entry.OpenEntryStream();
        await EntryFileWriter.WriteAsync(
            entryStream,
            destinationPath,
            copied =>
            {
                // El .gz suelto no declara de forma fiable el tamaño → control de ratio incremental
                // (y comprobación del tamaño declarado por si mintiera).
                DecompressionGuard.ThrowIfEntryExceedsLimit(key, entry.Size, copied);
                ratioGuard.Check(copied, key);
                progress?.Report(tracker.ForEntryProgress(key, copied));
            },
            cancellationToken).ConfigureAwait(false);

        return new ExtractionResult { ExtractedCount = 1 };
    }

    private async Task<ExtractionResult> ExtractTarStreamAsync(ExtractOptions options, IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        var wantedKeys = options.EntriesToExtract is { Count: > 0 } ? options.EntriesToExtract.ToHashSet() : null;
        var totalCompressedBytes = new FileInfo(_archivePath!).Length;
        var stopwatch = Stopwatch.StartNew();
        var blocked = new List<string>();
        var skipped = new List<string>();
        var collisions = new CollisionResolver(options);
        var extractedCount = 0;

        // El .tar.gz no expone el tamaño total descomprimido por adelantado → control de ratio
        // incremental + comprobación por entrada de su tamaño declarado en la cabecera TAR.
        var ratioGuard = new DecompressionGuard.RatioGuard(totalCompressedBytes);
        long totalDecompressed = 0;

        using var fileStream = File.OpenRead(_archivePath!);
        using var reader = ReaderFactory.OpenReader(fileStream);

        while (reader.MoveToNextEntry())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            var key = NormalizeKey(reader.Entry.Key);
            if (wantedKeys is not null && !wantedKeys.Contains(key))
            {
                continue;
            }

            string destinationPath;
            try
            {
                destinationPath = PathSecurity.ResolveSafeDestinationPath(options.DestinationPath, key);
            }
            catch (PathTraversalException)
            {
                blocked.Add(key);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            var resolvedPath = collisions.Resolve(destinationPath);
            if (resolvedPath is null)
            {
                skipped.Add(key);
                continue;
            }
            destinationPath = resolvedPath;

            long entryCopiedPrev = 0;
            using var entryStream = reader.OpenEntryStream();
            await EntryFileWriter.WriteAsync(entryStream, destinationPath, copied =>
            {
                totalDecompressed += copied - entryCopiedPrev;
                entryCopiedPrev = copied;

                DecompressionGuard.ThrowIfEntryExceedsLimit(key, reader.Entry.Size, copied);
                ratioGuard.Check(totalDecompressed, key);

                progress?.Report(new OperationProgress
                {
                    CurrentEntryName = key,
                    BytesProcessed = fileStream.Position,
                    TotalBytes = totalCompressedBytes,
                    Elapsed = stopwatch.Elapsed,
                });
            }, cancellationToken).ConfigureAwait(false);

            extractedCount++;
        }

        return new ExtractionResult { ExtractedCount = extractedCount, BlockedEntries = blocked, SkippedEntries = skipped };
    }

    private static async Task<bool> ReadThroughAndVerifyCrcAsync(Stream entryStream, long declaredCrc, long totalBytes, string entryName,
        IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        using var _ = entryStream;
        try
        {
            var tracker = new OperationProgressTracker(totalBytes);
            var hasher = new System.IO.Hashing.Crc32();
            var buffer = new byte[StreamCopy.BufferSize];
            long copied = 0;
            int read;

            while ((read = await entryStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                hasher.Append(buffer.AsSpan(0, read));
                copied += read;
                progress?.Report(tracker.ForEntryProgress(entryName, copied));
            }

            return declaredCrc == 0 || hasher.GetCurrentHashAsUInt32() == unchecked((uint)declaredCrc);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Descomprime los primeros 262 bytes y comprueba la firma "ustar" de POSIX TAR en el offset 257 (ver Fase 1, ArchiveFormatDetector).</summary>
    private static async Task<bool> DecompressedContentIsTarAsync(string gzipFilePath, CancellationToken cancellationToken)
    {
        const int tarMagicOffset = 257;
        const int tarMagicLength = 5; // "ustar"

        await using var fileStream = File.OpenRead(gzipFilePath);
        await using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);

        var buffer = new byte[tarMagicOffset + tarMagicLength];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await gzip.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead == buffer.Length && Encoding.ASCII.GetString(buffer, tarMagicOffset, tarMagicLength) == "ustar";
    }

    private void EnsureOpen()
    {
        if (_archivePath is null)
        {
            throw new InvalidOperationException("El archivo GZIP no está abierto. Llama a OpenAsync primero.");
        }
    }

    private static string NormalizeKey(string? key) => (key ?? string.Empty).Replace('\\', '/');
}
