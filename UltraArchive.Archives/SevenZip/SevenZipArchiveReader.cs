using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;
using UltraArchive.Archives.Common;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using UltraArchive.Security;

namespace UltraArchive.Archives.SevenZip;

/// <summary>
/// Lector de archivos 7Z sobre SharpCompress (implementación LZMA SDK de dominio público, portada a
/// C# administrado). Solo lectura: el propio SharpCompress no ofrece una API de archivo editable para
/// 7Z (ver <see cref="SevenZipArchiveWriter"/> para la creación, que usa la API de escritura en streaming).
/// </summary>
public sealed class SevenZipArchiveReader : IArchiveReader
{
    private IArchive? _archive;
    private Stream? _volumeStream;
    private string? _archivePath;
    private string _displayName = string.Empty;
    private bool _disposed;

    public ArchiveFormat Format => ArchiveFormat.SevenZip;

    public bool IsPasswordProtected => _archive?.IsEncrypted ?? false;

    public Task OpenAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _archivePath = archivePath;
        _displayName = Path.GetFileName(archivePath);

        try
        {
            var readerOptions = string.IsNullOrEmpty(password) ? null : new ReaderOptions { Password = password };

            if (VolumeSetInspector.IsVolumePartName(archivePath))
            {
                OpenVolumeSet(archivePath, readerOptions);
            }
            else
            {
                _archive = SevenZipArchive.OpenArchive(archivePath, readerOptions);
            }

            // Ver nota equivalente en ZipArchiveReader: fuerza la validación de la estructura del
            // archivo ahora, para que un 7Z corrupto se traduzca a ArchiveCorruptedException al abrir.
            _ = _archive!.Entries.Count();
        }
        catch (IncompleteVolumeSetException)
        {
            throw; // mensaje ya claro (partes encontradas / faltantes); no envolver
        }
        catch (Exception ex) when (PasswordErrors.IsPasswordFailure(ex))
        {
            // 7Z con cabecera cifrada abierto sin contraseña o con una incorrecta.
            throw new InvalidPasswordException(_displayName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ArchiveCorruptedException($"No se pudo abrir '{_displayName}' como 7Z. Puede estar dañado o no ser un 7Z válido.", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Abre un 7Z dividido en volúmenes a partir de una de sus partes. Reúne todas las partes de la
    /// carpeta, exige que la secuencia esté completa (1..N sin huecos ni duplicados) y las presenta
    /// como un stream contiguo (sin copiar nada a disco). Si falta alguna → <see cref="IncompleteVolumeSetException"/>.
    /// </summary>
    private void OpenVolumeSet(string anyPartPath, ReaderOptions? readerOptions)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(anyPartPath)) ?? ".";
        var siblings = Directory.EnumerateFiles(directory).Select(Path.GetFileName).ToList()!;

        var info = VolumeSetInspector.Inspect(Path.GetFileName(anyPartPath), siblings!);
        if (info is null)
        {
            // No parece un conjunto de volúmenes: abrir como fichero suelto.
            _archive = SevenZipArchive.OpenArchive(anyPartPath, readerOptions);
            return;
        }

        _displayName = info.BaseName;

        if (!info.IsComplete)
        {
            throw new IncompleteVolumeSetException(info.BaseName, info.FoundParts, info.MissingParts, info.DuplicateParts);
        }

        var orderedPaths = info.OrderedPartFileNames.Select(name => Path.Combine(directory, name)).ToList();
        _volumeStream = new MultiVolumeReadStream(orderedPaths);
        _archive = SevenZipArchive.OpenArchive(_volumeStream, readerOptions ?? new ReaderOptions());
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

        // Los archivos 7Z son SOLID: hay que extraer las entradas en orden secuencial para que el
        // rendimiento sea aceptable (ver SharpCompress docs). Por eso se usa ExtractAllEntries()
        // en lugar de abrir cada entrada suelta con OpenEntryStream() como en ZIP.
        var wantedKeys = options.EntriesToExtract is { Count: > 0 } ? options.EntriesToExtract.ToHashSet() : null;
        var allFiles = _archive!.Entries.Where(e => !e.IsDirectory).ToList();
        DecompressionGuard.ThrowIfArchiveLooksLikeBomb(allFiles.Sum(e => e.CompressedSize), allFiles.Sum(e => e.Size));

        var totalBytes = allFiles.Sum(e => e.Size);
        var tracker = new OperationProgressTracker(totalBytes);
        var blocked = new List<string>();
        var skipped = new List<string>();
        var collisions = new CollisionResolver(options);
        var extractedCount = 0;

        using var reader = _archive.ExtractAllEntries();

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
                tracker.CompleteEntry(reader.Entry.Size);
                continue;
            }
            destinationPath = resolvedPath;

            try
            {
                using var entryStream = reader.OpenEntryStream();
                await EntryFileWriter.WriteAsync(
                    entryStream,
                    destinationPath,
                    copied => progress?.Report(tracker.ForEntryProgress(key, copied)),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (PasswordErrors.IsPasswordFailure(ex))
            {
                throw new InvalidPasswordException(_displayName);
            }

            tracker.CompleteEntry(reader.Entry.Size);
            extractedCount++;
        }

        return new ExtractionResult { ExtractedCount = extractedCount, BlockedEntries = blocked, SkippedEntries = skipped };
    }

    public async Task<bool> TestIntegrityAsync(IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureOpen();

        var totalBytes = _archive!.Entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
        var tracker = new OperationProgressTracker(totalBytes);
        var allOk = true;

        using var reader = _archive.ExtractAllEntries();

        while (reader.MoveToNextEntry())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            var key = NormalizeKey(reader.Entry.Key);
            try
            {
                using var entryStream = reader.OpenEntryStream();
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

                if (reader.Entry.Crc != 0 && hasher.GetCurrentHashAsUInt32() != unchecked((uint)reader.Entry.Crc))
                {
                    allOk = false;
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

            tracker.CompleteEntry(reader.Entry.Size);
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
        _volumeStream?.Dispose();
        _disposed = true;
    }

    private void EnsureOpen()
    {
        if (_archive is null)
        {
            throw new InvalidOperationException("El archivo 7Z no está abierto. Llama a OpenAsync primero.");
        }
    }

    private static string NormalizeKey(string? key) => (key ?? string.Empty).Replace('\\', '/');
}
