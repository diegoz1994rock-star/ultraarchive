using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using UltraArchive.Archives.Common;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using UltraArchive.Security;

namespace UltraArchive.Archives.Tar;

/// <summary>
/// Lector de archivos TAR (sin comprimir) sobre SharpCompress. TAR no tiene concepto de contraseña,
/// así que <see cref="IsPasswordProtected"/> siempre es false.
/// </summary>
public sealed class TarArchiveReader : IArchiveReader
{
    private IArchive? _archive;
    private bool _disposed;

    public ArchiveFormat Format => ArchiveFormat.Tar;

    public bool IsPasswordProtected => false;

    public Task OpenAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _archive = TarArchive.OpenArchive(archivePath);

            // Ver nota equivalente en ZipArchiveReader: fuerza la validación de la estructura del
            // archivo ahora, para que un TAR corrupto se traduzca a ArchiveCorruptedException al abrir.
            _ = _archive.Entries.Count();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ArchiveCorruptedException($"No se pudo abrir '{Path.GetFileName(archivePath)}' como TAR. Puede estar dañado o no ser un TAR válido.", ex);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ArchiveEntry>> GetEntriesAsync(CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ArchiveEntry> entries = _archive!.Entries.Select(ArchiveEntryMapper.ToArchiveEntry).ToList();
        return Task.FromResult(entries);
    }

    public async Task<ExtractionResult> ExtractAsync(ExtractOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        Directory.CreateDirectory(options.DestinationPath);

        var wantedKeys = options.EntriesToExtract is { Count: > 0 } ? options.EntriesToExtract.ToHashSet() : null;
        var allFiles = _archive!.Entries.Where(e => !e.IsDirectory).ToList();
        DecompressionGuard.ThrowIfArchiveLooksLikeBomb(allFiles.Sum(e => e.CompressedSize), allFiles.Sum(e => e.Size));

        var entriesToExtract = allFiles
            .Where(e => wantedKeys is null || wantedKeys.Contains(NormalizeKey(e.Key)))
            .ToList();

        var tracker = new OperationProgressTracker(entriesToExtract.Sum(e => e.Size));
        var blocked = new List<string>();
        var skipped = new List<string>();
        var collisions = new CollisionResolver(options);
        var extractedCount = 0;

        foreach (var entry in entriesToExtract)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = NormalizeKey(entry.Key);

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
                tracker.CompleteEntry(entry.Size);
                continue;
            }
            destinationPath = resolvedPath;

            using var entryStream = entry.OpenEntryStream();
            await EntryFileWriter.WriteAsync(
                entryStream,
                destinationPath,
                copied => progress?.Report(tracker.ForEntryProgress(key, copied)),
                cancellationToken).ConfigureAwait(false);

            tracker.CompleteEntry(entry.Size);
            extractedCount++;
        }

        return new ExtractionResult { ExtractedCount = extractedCount, BlockedEntries = blocked, SkippedEntries = skipped };
    }

    public async Task<bool> TestIntegrityAsync(IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureOpen();

        var entries = _archive!.Entries.Where(e => !e.IsDirectory).ToList();
        var tracker = new OperationProgressTracker(entries.Sum(e => e.Size));
        var allOk = true;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = NormalizeKey(entry.Key);

            try
            {
                using var entryStream = entry.OpenEntryStream();
                var buffer = new byte[StreamCopy.BufferSize];
                long copied = 0;
                int read;
                while ((read = await entryStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    copied += read;
                    progress?.Report(tracker.ForEntryProgress(key, copied));
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

            tracker.CompleteEntry(entry.Size);
        }

        return allOk;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _archive?.Dispose();
        _disposed = true;
    }

    private void EnsureOpen()
    {
        if (_archive is null)
        {
            throw new InvalidOperationException("El archivo TAR no está abierto. Llama a OpenAsync primero.");
        }
    }

    private static string NormalizeKey(string? key) => (key ?? string.Empty).Replace('\\', '/');
}
