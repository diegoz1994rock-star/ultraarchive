using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Readers;
using UltraArchive.Archives.Common;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using UltraArchive.Security;

namespace UltraArchive.Archives.Rar;

/// <summary>
/// Lector de archivos RAR (RAR4 y RAR5) sobre SharpCompress. Solo lectura por decisión de proyecto:
/// el algoritmo de compresión RAR es propietario y su especificación prohíbe expresamente escribir
/// un compresor compatible, así que UltraArchive nunca crea archivos RAR (ver el mensaje de
/// <c>ArchiveEngineFactory.CreateWriter</c>). La descompresión sí es libre: SharpCompress incluye un
/// port administrado del descompresor unrar bajo licencia permisiva.
///
/// A diferencia de 7Z, SharpCompress sí permite abrir cada entrada de un RAR de forma independiente
/// con <c>OpenEntryStream</c> aunque el archivo sea "solid", así que la extracción y la comprobación
/// de integridad recorren <see cref="RarArchive.Entries"/> igual que el lector de TAR/ZIP.
/// </summary>
public sealed class RarArchiveReader : IArchiveReader
{
    private IArchive? _archive;
    private string? _archivePath;
    private bool _disposed;

    public ArchiveFormat Format => ArchiveFormat.Rar;

    public bool IsPasswordProtected => _archive?.IsEncrypted ?? false;

    public Task OpenAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _archivePath = archivePath;

        try
        {
            var readerOptions = string.IsNullOrEmpty(password) ? null : new ReaderOptions { Password = password };
            _archive = RarArchive.OpenArchive(archivePath, readerOptions);

            // Ver nota equivalente en ZipArchiveReader: SharpCompress lee la cabecera de forma
            // perezosa, así que forzamos aquí la enumeración para que la corrupción (o la falta de
            // contraseña en un RAR cifrado) se detecte al abrir y no más tarde.
            _ = _archive.Entries.Count();
        }
        catch (Exception ex) when (PasswordErrors.IsPasswordFailure(ex))
        {
            // RAR cifrado (-p o -hp) abierto sin contraseña o con una incorrecta: se traduce a
            // InvalidPasswordException para que la UI pida la contraseña vía IPasswordProvider y reintente.
            throw new InvalidPasswordException(Path.GetFileName(archivePath));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ArchiveCorruptedException($"No se pudo abrir '{Path.GetFileName(archivePath)}' como RAR. Puede estar dañado o no ser un RAR válido.", ex);
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

                // RAR5 cifrado: el campo de checksum de la cabecera NO es un CRC32 del contenido en
                // claro, sino un MAC con clave (flag "use MAC instead of checksum"). Compararlo con un
                // CRC32 daría siempre un falso negativo. Para entradas cifradas, que SharpCompress haya
                // descifrado y descomprimido el flujo entero sin lanzar (valida su propio checksum/MAC
                // internamente) ES la comprobación de integridad.
                if (!entry.IsEncrypted)
                {
                    var declaredCrc = ReadCrc(entry);
                    if (declaredCrc is { } crc && hasher.GetCurrentHashAsUInt32() != crc)
                    {
                        allOk = false;
                    }
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
            throw new InvalidOperationException("El archivo RAR no está abierto. Llama a OpenAsync primero.");
        }
    }

    private static string NormalizeKey(string? key) => (key ?? string.Empty).Replace('\\', '/');

    /// <summary>
    /// CRC32 declarado en el archivo para una entrada, o null si RAR no lo expone. SharpCompress lanza
    /// <see cref="ArgumentNullException"/> al pedir el CRC de entradas sin checksum (p. ej. carpetas).
    /// </summary>
    private static uint? ReadCrc(SharpCompress.Common.IEntry entry)
    {
        try
        {
            return entry.Crc == 0 ? null : unchecked((uint)entry.Crc);
        }
        catch (ArgumentNullException)
        {
            return null;
        }
    }
}
