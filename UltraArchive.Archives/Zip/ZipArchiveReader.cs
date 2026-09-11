using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;
using UltraArchive.Archives.Common;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using UltraArchive.Security;

namespace UltraArchive.Archives.Zip;

/// <summary>
/// Lector de archivos ZIP sobre SharpCompress (MIT). Soporta listar el contenido sin extraer,
/// extracción con protección Zip Slip, y detección de contraseña incorrecta.
/// </summary>
public sealed class ZipArchiveReader : IArchiveReader
{
    private IArchive? _archive;
    private string? _archivePath;
    private bool _disposed;

    public ArchiveFormat Format => ArchiveFormat.Zip;

    // OJO: en ZIP con WinZip AES, el flag de archivo (_archive.IsEncrypted) es poco fiable y suele
    // devolver false aunque las entradas estén cifradas; hay que mirar entrada por entrada.
    public bool IsPasswordProtected => _archive?.Entries.Any(e => e.IsEncrypted) ?? false;

    public Task OpenAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _archivePath = archivePath;

        try
        {
            var readerOptions = string.IsNullOrEmpty(password) ? null : new ReaderOptions { Password = password };
            _archive = ZipArchive.OpenArchive(archivePath, readerOptions);

            // SharpCompress analiza la tabla central de forma perezosa (solo al enumerar Entries), así
            // que OpenArchive() por sí solo NO detecta un ZIP corrupto/inválido. Forzamos aquí esa
            // validación para que la corrupción se traduzca a ArchiveCorruptedException al abrir, tal y
            // como promete IArchiveReader.OpenAsync, en vez de reventar sin traducir en la primera
            // llamada a GetEntriesAsync/ExtractAsync.
            _ = _archive.Entries.Count();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ArchiveCorruptedException($"No se pudo abrir '{Path.GetFileName(archivePath)}' como ZIP. Puede estar dañado o no ser un ZIP válido.", ex);
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

        var allFiles = _archive!.Entries.Where(e => !e.IsDirectory).ToList();
        DecompressionGuard.ThrowIfArchiveLooksLikeBomb(allFiles.Sum(e => e.CompressedSize), allFiles.Sum(e => e.Size));

        var entriesToExtract = allFiles
            .Where(e => ShouldExtract(e.Key, options.EntriesToExtract))
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

            try
            {
                using var entryStream = entry.OpenEntryStream();
                await EntryFileWriter.WriteAsync(
                    entryStream,
                    destinationPath,
                    copied => progress?.Report(tracker.ForEntryProgress(key, copied)),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (PasswordErrors.IsPasswordFailure(ex))
            {
                throw new InvalidPasswordException(Path.GetFileName(_archivePath ?? string.Empty));
            }

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
                var hasher = new System.IO.Hashing.Crc32();
                var buffer = new byte[StreamCopy.BufferSize];
                long copied = 0;
                int read;

                while ((read = await entryStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    hasher.Append(buffer.AsSpan(0, read));
                    copied += read;
                    progress?.Report(tracker.ForEntryProgress(key, copied));
                }

                if (entry.Crc != 0 && hasher.GetCurrentHashAsUInt32() != unchecked((uint)entry.Crc))
                {
                    allOk = false;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (PasswordErrors.IsPasswordFailure(ex))
            {
                throw new InvalidPasswordException(Path.GetFileName(_archivePath ?? string.Empty));
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
            throw new InvalidOperationException("El archivo ZIP no está abierto. Llama a OpenAsync primero.");
        }
    }

    private static string NormalizeKey(string? key) => (key ?? string.Empty).Replace('\\', '/');

    private static bool ShouldExtract(string? key, IReadOnlyList<string>? requestedEntries) =>
        requestedEntries is null || requestedEntries.Count == 0 || requestedEntries.Contains(NormalizeKey(key));
}
