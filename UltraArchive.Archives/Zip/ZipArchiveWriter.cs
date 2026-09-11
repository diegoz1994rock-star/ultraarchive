using ICSharpCode.SharpZipLib.Zip;
using SharpCompress.Common;
using SharpCompress.Writers.Zip;
using UltraArchive.Archives.Common;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using ZipArchiveSc = SharpCompress.Archives.Zip.ZipArchive;

namespace UltraArchive.Archives.Zip;

/// <summary>
/// Escritor de archivos ZIP.
///
/// - <b>Creación</b> (<see cref="CreateAsync"/>): usa <c>SharpZipLib</c>, que sí sabe cifrar con
///   <b>WinZip AES-256</b> al crear (SharpCompress solo descifra al leer, no cifra al escribir).
///   Sin contraseña produce un ZIP Deflate normal. El cifrado real lo hace SharpZipLib sobre las
///   primitivas de <c>System.Security.Cryptography</c>; UltraArchive no implementa criptografía.
/// - <b>Edición in situ</b> (<see cref="IMutableArchiveWriter"/>): usa SharpCompress para añadir/quitar
///   entradas de un ZIP existente sin recrearlo entrada por entrada. Solo se permite sobre ZIP
///   <b>no cifrado</b>: SharpCompress no sabe volver a cifrar, así que reescribir un ZIP cifrado lo
///   dejaría sin protección; en ese caso la operación se bloquea.
/// </summary>
public sealed class ZipArchiveWriter : IMutableArchiveWriter
{
    public ArchiveFormat Format => ArchiveFormat.Zip;

    public async Task CreateAsync(CreateArchiveOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Falla ANTES de tocar el disco si se pide un cifrado que el formato/librería no soporta.
        EncryptionSupport.Validate(options);
        SplitSupport.Validate(options);

        var files = SourceFileCollector.Collect(options.SourcePaths, options.PreserveFolderStructure);
        var tracker = new OperationProgressTracker(files.Sum(f => f.Length));
        var encrypt = !string.IsNullOrEmpty(options.Password) && options.Encryption == EncryptionMethod.Aes256;

        try
        {
            await Task.Run(async () =>
            {
                await using var outputStream = File.Create(options.OutputPath);
                // IsStreamOwner=false: el FileStream lo gestiona este método (await using), no el ZipOutputStream.
                using var zip = new ZipOutputStream(outputStream) { IsStreamOwner = false };
                zip.SetLevel(CompressionLevelMapper.ToDeflateLevel(options.CompressionLevel));

                if (encrypt)
                {
                    zip.Password = options.Password;
                }

                var buffer = new byte[StreamCopy.BufferSize];

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var entry = new ZipEntry(file.EntryKey)
                    {
                        DateTime = file.LastWriteTimeUtc,
                        Size = file.Length,
                        IsUnicodeText = true,
                    };

                    if (options.CompressionLevel == CompressionLevel.Store)
                    {
                        entry.CompressionMethod = CompressionMethod.Stored;
                    }

                    if (encrypt)
                    {
                        entry.AESKeySize = 256;
                    }

                    zip.PutNextEntry(entry);

                    await using (var source = File.OpenRead(file.FullPath))
                    {
                        int read;
                        long copiedInEntry = 0;
                        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            zip.Write(buffer, 0, read);
                            copiedInEntry += read;
                            progress?.Report(tracker.ForEntryProgress(file.EntryKey, copiedInEntry));
                        }
                    }

                    zip.CloseEntry();
                    tracker.CompleteEntry(file.Length);
                }

                zip.Finish();
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // No dejar un ZIP a medio escribir (y menos uno que pareciera cifrado sin estarlo del todo).
            TryDelete(options.OutputPath);
            throw;
        }

        progress?.Report(new OperationProgress { BytesProcessed = tracker.TotalBytes, TotalBytes = tracker.TotalBytes, Elapsed = TimeSpan.Zero });

        if (options.DeleteSourceAfterCompress)
        {
            DeleteSources(options.SourcePaths);
        }
    }

    public async Task AddEntriesAsync(string archivePath, IReadOnlyList<string> sourcePaths, string? password,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureNotEncrypted(archivePath, "añadir entradas a");

        var files = SourceFileCollector.Collect(sourcePaths, preserveFolderStructure: true);

        // Sin "using": RewriteArchiveAsync necesita liberar el archivo original (y su handle de lectura
        // sobre archivePath) ANTES de poder borrarlo y sustituirlo por el temporal reescrito.
        var archive = ZipArchiveSc.OpenArchive(archivePath);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            archive.AddEntry(file.EntryKey, File.OpenRead(file.FullPath), closeStream: true, file.Length, file.LastWriteTimeUtc);
        }

        await RewriteArchiveAsync(archive, archivePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteEntriesAsync(string archivePath, IReadOnlyList<string> entryPaths,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureNotEncrypted(archivePath, "eliminar entradas de");

        var normalizedTargets = entryPaths.Select(p => p.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Sin "using": ver nota en AddEntriesAsync.
        var archive = ZipArchiveSc.OpenArchive(archivePath);

        foreach (var entry in archive.Entries.Where(e => normalizedTargets.Contains((e.Key ?? string.Empty).Replace('\\', '/'))).ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            archive.RemoveEntry(entry);
        }

        await RewriteArchiveAsync(archive, archivePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Añadir/quitar entradas reescribe todo el ZIP con SharpCompress, que no sabe re-cifrar. Si el
    /// archivo tiene alguna entrada cifrada, la operación se bloquea para no dejarlo sin protección.
    /// </summary>
    private static void EnsureNotEncrypted(string archivePath, string actionDescription)
    {
        using var archive = ZipArchiveSc.OpenArchive(archivePath);
        if (archive.Entries.Any(e => e.IsEncrypted))
        {
            throw new EncryptionNotSupportedException(
                $"'{Path.GetFileName(archivePath)}' está protegido con contraseña. UltraArchive no puede {actionDescription} " +
                "un ZIP cifrado sin dejarlo sin protección; crea un archivo nuevo con la contraseña.");
        }
    }

    /// <summary>
    /// ZIP no admite "editar in situ": para añadir/quitar entradas hay que reescribir el archivo completo
    /// a un fichero temporal y sustituirlo. <paramref name="archive"/> se recibe SIN disponer (ver
    /// llamadores): sigue teniendo abierto el archivo original de <paramref name="archivePath"/> (lo
    /// necesita para leer las entradas ya existentes durante SaveTo), así que hay que liberarlo aquí
    /// mismo justo después de escribir el temporal y ANTES de borrar/sustituir el original.
    /// </summary>
    private static async Task RewriteArchiveAsync(SharpCompress.Archives.IWritableArchive<ZipWriterOptions> archive, string archivePath, CancellationToken cancellationToken)
    {
        var tempPath = archivePath + ".tmp";

        try
        {
            await using (var outputStream = File.Create(tempPath))
            {
                await Task.Run(() => archive.SaveTo(outputStream, new ZipWriterOptions(CompressionType.Deflate)), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            archive.Dispose();
        }

        File.Delete(archivePath);
        File.Move(tempPath, archivePath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort
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
