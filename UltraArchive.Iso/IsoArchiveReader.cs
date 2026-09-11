using System.Diagnostics;
using DiscUtils.Iso9660;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using UltraArchive.Security;

namespace UltraArchive.Iso;

/// <summary>
/// Motor de lectura de imágenes ISO9660 (Fase 5) sobre <c>LTRData.DiscUtils.Iso9660</c> (MIT).
/// Solo lectura: abrir, explorar, extraer y comprobar (lectura estructural). UltraArchive **no crea**
/// imágenes ISO en esta versión.
///
/// Compatibilidad:
///   - ISO9660 Level 1 y 2.
///   - Joliet (nombres Unicode) — se prefiere automáticamente cuando la imagen lo trae.
///   - Rock Ridge (nombres POSIX largos) — se leen; los permisos POSIX NUNCA se aplican al extraer
///     en Windows.
///   - UDF: NO soportado. Una imagen UDF pura se rechaza con un mensaje claro.
///   - Un único fichero &gt; 4 GiB dentro de ISO9660 (multi-extent): no soportado por la librería.
///
/// Seguridad: cada nombre que devuelve la librería pasa por <see cref="PathSecurity"/> antes de
/// escribir en disco (path traversal, rutas absolutas, nombres inválidos de Windows, dispositivos
/// reservados). Una ISO nunca puede escribir fuera de la carpeta de destino elegida.
/// </summary>
public sealed class IsoArchiveReader : IArchiveReader
{
    private FileStream? _stream;
    private CDReader? _reader;
    private bool _disposed;

    public ArchiveFormat Format => ArchiveFormat.Iso9660;

    /// <summary>Las imágenes ISO9660 no tienen protección con contraseña.</summary>
    public bool IsPasswordProtected => false;

    /// <summary>
    /// Información del volumen leída al abrir (etiqueta, fecha, si trae Joliet/UDF). Null si aún no se
    /// ha abierto ninguna imagen. Pensado para mostrarlo en la UI en una fase posterior.
    /// </summary>
    public IsoVolumeInfo? VolumeInfo { get; private set; }

    public Task OpenAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var info = IsoVolumeProbe.Probe(stream);
            VolumeInfo = info;

            if (!info.IsIso9660)
            {
                if (info.HasUdf)
                {
                    throw new UnsupportedFormatException(
                        $"'{Path.GetFileName(archivePath)}' es una imagen UDF. UltraArchive todavía no lee el sistema de archivos UDF; " +
                        "solo ISO9660 (con Joliet o Rock Ridge).");
                }

                throw new ArchiveCorruptedException(
                    $"No se pudo abrir '{Path.GetFileName(archivePath)}' como imagen ISO9660. Puede estar dañada, truncada o no ser una imagen de disco.");
            }

            stream.Position = 0;

            // joliet:true solo si la imagen trae un árbol Joliet — si no, joliet:false permite que la
            // librería use los nombres Rock Ridge del árbol primario cuando existan.
            _reader = new CDReader(stream, joliet: info.HasJoliet, hideVersions: true);
            _stream = stream;
        }
        catch (Exception ex) when (ex is DiscUtils.InvalidFileSystemException or IOException or EndOfStreamException or NotSupportedException or InvalidDataException)
        {
            stream.Dispose();
            throw new ArchiveCorruptedException(
                $"No se pudo abrir '{Path.GetFileName(archivePath)}' como imagen ISO9660. Puede estar dañada o truncada.", ex);
        }
        catch
        {
            stream.Dispose();
            throw;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ArchiveEntry>> GetEntriesAsync(CancellationToken cancellationToken = default)
    {
        var reader = EnsureOpen();
        cancellationToken.ThrowIfCancellationRequested();

        var entries = new List<ArchiveEntry>();

        foreach (var dirPath in reader.GetDirectories(string.Empty, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = NormalizeKey(dirPath);
            if (key.Length == 0)
            {
                continue;
            }

            entries.Add(new ArchiveEntry
            {
                Name = LeafName(key),
                FullPath = key,
                IsDirectory = true,
                UncompressedSize = 0,
                CompressedSize = 0,
            });
        }

        foreach (var filePath in reader.GetFiles(string.Empty, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = NormalizeKey(filePath);

            long size;
            DateTimeOffset? modified;
            try
            {
                var info = reader.GetFileInfo(filePath);
                size = info.Length;
                modified = new DateTimeOffset(DateTime.SpecifyKind(info.LastWriteTimeUtc, DateTimeKind.Utc));
            }
            catch (Exception ex) when (ex is IOException or DiscUtils.InvalidFileSystemException)
            {
                size = 0;
                modified = null;
            }

            entries.Add(new ArchiveEntry
            {
                Name = LeafName(key),
                FullPath = key,
                IsDirectory = false,
                UncompressedSize = size,
                CompressedSize = size, // ISO9660 no comprime: tamaño en imagen == tamaño real.
                LastModifiedUtc = modified,
                CompressionMethod = "Sin comprimir",
            });
        }

        IReadOnlyList<ArchiveEntry> result = entries
            .OrderBy(e => e.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult(result);
    }

    public async Task<ExtractionResult> ExtractAsync(ExtractOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var reader = EnsureOpen();
        Directory.CreateDirectory(options.DestinationPath);

        var wantedKeys = options.EntriesToExtract is { Count: > 0 }
            ? options.EntriesToExtract.Select(NormalizeKey).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

        var files = reader.GetFiles(string.Empty, "*", SearchOption.AllDirectories)
            .Select(raw => (Raw: raw, Key: NormalizeKey(raw)))
            .Where(f => wantedKeys is null || wantedKeys.Contains(f.Key))
            .ToList();

        long totalBytes = 0;
        foreach (var f in files)
        {
            totalBytes += SafeLength(reader, f.Raw);
        }

        var stopwatch = Stopwatch.StartNew();
        var blocked = new List<string>();
        var skipped = new List<string>();
        var collisions = new CollisionResolver(options);
        var extractedCount = 0;
        long bytesDoneBeforeCurrent = 0;

        foreach (var (raw, key) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            var entrySize = SafeLength(reader, raw);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            var resolvedPath = collisions.Resolve(destinationPath);
            if (resolvedPath is null)
            {
                skipped.Add(key);
                bytesDoneBeforeCurrent += entrySize;
                continue;
            }
            destinationPath = resolvedPath;

            var doneBefore = bytesDoneBeforeCurrent;
            using (var entryStream = reader.OpenFile(raw, FileMode.Open, FileAccess.Read))
            {
                await IsoEntryExtractor.WriteAsync(
                    entryStream,
                    destinationPath,
                    copied => progress?.Report(new OperationProgress
                    {
                        CurrentEntryName = key,
                        BytesProcessed = doneBefore + copied,
                        TotalBytes = totalBytes,
                        Elapsed = stopwatch.Elapsed,
                    }),
                    cancellationToken).ConfigureAwait(false);
            }

            bytesDoneBeforeCurrent += entrySize;
            extractedCount++;
        }

        return new ExtractionResult { ExtractedCount = extractedCount, BlockedEntries = blocked, SkippedEntries = skipped };
    }

    public async Task<bool> TestIntegrityAsync(IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var reader = EnsureOpen();

        // ISO9660 no lleva checksum por fichero. La comprobación posible es: recorrer cada fichero de
        // principio a fin sin error de E/S ni de estructura. Es una validación estructural, no un CRC.
        var files = reader.GetFiles(string.Empty, "*", SearchOption.AllDirectories).ToList();
        long totalBytes = files.Sum(f => SafeLength(reader, f));
        var stopwatch = Stopwatch.StartNew();
        var allOk = true;
        long bytesDone = 0;
        var buffer = new byte[81920];

        foreach (var raw in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = NormalizeKey(raw);
            try
            {
                using var entryStream = reader.OpenFile(raw, FileMode.Open, FileAccess.Read);
                int read;
                while ((read = await entryStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    bytesDone += read;
                    progress?.Report(new OperationProgress
                    {
                        CurrentEntryName = key,
                        BytesProcessed = bytesDone,
                        TotalBytes = totalBytes,
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

        _reader?.Dispose();
        _stream?.Dispose(); // CDReader NO cierra el stream: lo cerramos nosotros.
        _disposed = true;
    }

    private CDReader EnsureOpen()
    {
        if (_reader is null)
        {
            throw new InvalidOperationException("La imagen ISO no está abierta. Llama a OpenAsync primero.");
        }

        return _reader;
    }

    private static long SafeLength(CDReader reader, string rawPath)
    {
        try
        {
            return reader.GetFileLength(rawPath);
        }
        catch (Exception ex) when (ex is IOException or DiscUtils.InvalidFileSystemException)
        {
            return 0;
        }
    }

    /// <summary>Convierte una ruta de DiscUtils ("\DOCS\NOTA.TXT") a la clave de dominio ("DOCS/NOTA.TXT").</summary>
    private static string NormalizeKey(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim('/');

    private static string LeafName(string normalizedKey)
    {
        var slash = normalizedKey.LastIndexOf('/');
        return slash < 0 ? normalizedKey : normalizedKey[(slash + 1)..];
    }

}
