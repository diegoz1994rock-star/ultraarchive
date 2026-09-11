using System.Diagnostics;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;

namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Escritor de 7Z que <b>enruta</b> según la operación, sin cambiar el comportamiento existente:
///
///   - <b>Sin contraseña y sin división</b> → <c>SevenZipArchiveWriter</c> gestionado (SharpCompress), tal cual.
///   - <b>Con contraseña y/o dividido en volúmenes</b> → <see cref="ISevenZipCli"/> (7zr.exe), que crea
///     un <c>.7z</c> temporal (o <c>.7z.001/.002…</c>); este escritor lo valida y lo <b>mueve</b>
///     (rename dentro del mismo volumen) al destino final. Con contraseña se cifra siempre también la
///     cabecera (<c>-mhe=on</c>). La división usa el switch nativo <c>-v</c> de 7zr.
///
/// No duplica lógica: la construcción de argumentos, la validación de rutas, el proceso, el progreso
/// y el manejo de la contraseña siguen en sus componentes (<c>SevenZipArgumentBuilder</c>,
/// <c>SevenZipRequest</c>, <c>SevenZipCli</c>…).
/// </summary>
public sealed class RoutingSevenZipWriter : IArchiveWriter
{
    private readonly IArchiveWriter _managedWriter;
    private readonly ISevenZipCli _cli;
    private readonly ISevenZipCapability _capability;

    public RoutingSevenZipWriter(IArchiveWriter managedWriter, ISevenZipCli cli, ISevenZipCapability capability)
    {
        _managedWriter = managedWriter ?? throw new ArgumentNullException(nameof(managedWriter));
        _cli = cli ?? throw new ArgumentNullException(nameof(cli));
        _capability = capability ?? throw new ArgumentNullException(nameof(capability));
    }

    public ArchiveFormat Format => ArchiveFormat.SevenZip;

    public async Task CreateAsync(CreateArchiveOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Format != ArchiveFormat.SevenZip)
        {
            throw new ArgumentException("RoutingSevenZipWriter solo crea archivos 7Z.", nameof(options));
        }

        var wantsPassword = !string.IsNullOrEmpty(options.Password);
        var wantsSplit = SplitSupport.WantsSplit(options);

        if (!wantsPassword && !wantsSplit)
        {
            // Ruta sin cambios: 7Z normal con el motor gestionado.
            await _managedWriter.CreateAsync(options, progress, cancellationToken).ConfigureAwait(false);
            return;
        }

        await CreateViaCliAsync(options, wantsPassword, wantsSplit, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task CreateViaCliAsync(
        CreateArchiveOptions options, bool wantsPassword, bool wantsSplit,
        IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        if (!_capability.CanCreateEncryptedSevenZip || _capability.VerifiedExecutablePath is not { Length: > 0 } executablePath)
        {
            // Con contraseña → EncryptionNotSupportedException (semántica de cifrado, comportamiento
            // preexistente). Solo división → SplitNotSupportedException.
            if (wantsPassword)
            {
                throw new EncryptionNotSupportedException(
                    "La creación de 7Z cifrado necesita el binario 7zr.exe verificado, que no está disponible. " +
                    $"{_capability.StatusExplanation} Usa ZIP con AES-256.");
            }

            throw new SplitNotSupportedException(
                "La creación de 7Z dividido en partes necesita el binario 7zr.exe verificado, que no está disponible. " +
                $"{_capability.StatusExplanation}");
        }

        var compression = SevenZipRequest.Create(
            options.SourcePaths,
            options.OutputPath,
            options.Password ?? string.Empty,
            options.CompressionLevel,
            encryptHeaders: wantsPassword, // solo se cifra la cabecera si hay contraseña
            volumeSizeBytes: wantsSplit ? options.SplitVolumeSizeBytes : null);

        var execution = new SevenZipExecutionRequest
        {
            ExecutablePath = executablePath,
            ExpectedExecutablePath = executablePath,
            Compression = compression,
            Progress = progress is null ? null : new PercentToOperationProgress(progress),
        };

        var result = await _cli.ExecuteAsync(execution, cancellationToken).ConfigureAwait(false);

        switch (result.Status)
        {
            case SevenZipExecutionStatus.Success:
            case SevenZipExecutionStatus.Warning:
                if (wantsSplit)
                {
                    PublishVolumeSet(result.OutputArchivePath, options.OutputPath);
                }
                else
                {
                    Publish(result.OutputArchivePath, options.OutputPath);
                }

                if (options.DeleteSourceAfterCompress)
                {
                    DeleteSources(options.SourcePaths);
                }

                break;

            case SevenZipExecutionStatus.Cancelled:
                throw new OperationCanceledException();

            default: // Failed (incluye TimedOut): NO se toca el destino final.
                throw new ArchiveException(
                    result.ErrorMessage ?? "7zr no pudo crear el archivo 7Z cifrado.",
                    ArchiveErrorCategory.Unknown);
        }
    }

    /// <summary>
    /// Valida el <c>.7z</c> temporal generado y lo mueve al destino final. El destino no se altera
    /// hasta que el temporal está completo y validado. <paramref name="tempArchivePath"/> debe ser
    /// exactamente el <c>.uatmp-*.7z</c> generado por UltraArchive (nunca un path arbitrario de 7zr).
    /// </summary>
    private static void Publish(string? tempArchivePath, string finalOutputPath)
    {
        if (string.IsNullOrEmpty(tempArchivePath))
        {
            throw new ArchiveException("7zr terminó con éxito pero no devolvió la ruta del archivo generado.");
        }

        var temp = Path.GetFullPath(tempArchivePath);
        var final = Path.GetFullPath(finalOutputPath);

        if (!Path.GetFileName(temp).StartsWith(SevenZipTempArchivePath.Prefix, StringComparison.Ordinal) ||
            !temp.EndsWith(SevenZipTempArchivePath.Extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArchiveException("El archivo temporal no es el generado por UltraArchive.");
        }

        if (!string.Equals(Path.GetDirectoryName(temp), Path.GetDirectoryName(final), StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(temp);
            throw new ArchiveException("El archivo temporal no está en la carpeta de destino esperada.");
        }

        if (!File.Exists(temp))
        {
            throw new ArchiveException("7zr terminó con éxito pero no se encontró el archivo generado.");
        }

        var info = new FileInfo(temp);
        if ((info.Attributes & FileAttributes.Directory) != 0 || info.Length == 0)
        {
            TryDelete(temp);
            throw new ArchiveException("El archivo 7Z generado no es válido (vacío o no es un fichero regular).");
        }

        try
        {
            // Move/rename dentro del mismo volumen. overwrite:true reemplaza el destino SOLO en este
            // instante, con el temporal ya completo y validado — nunca antes.
            File.Move(temp, final, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            throw new ArchiveException($"No se pudo publicar el archivo 7Z en el destino: {ex.Message}", ex,
                ArchiveErrorCategory.FileInUse);
        }
    }

    /// <summary>
    /// Publica un conjunto de volúmenes: mueve <c>&lt;temp&gt;.7z.001</c>, <c>.002</c>… a
    /// <c>&lt;destino&gt;.001</c>, <c>.002</c>… El destino no se toca hasta que TODAS las partes
    /// temporales están validadas. Si algo falla a mitad, se deja el destino como estaba y se limpian
    /// tanto los temporales como las partes ya publicadas de este intento.
    /// </summary>
    private static void PublishVolumeSet(string? tempArchiveBase, string finalOutputPath)
    {
        if (string.IsNullOrEmpty(tempArchiveBase))
        {
            throw new ArchiveException("7zr terminó con éxito pero no devolvió la ruta del archivo generado.");
        }

        var tempBase = Path.GetFullPath(tempArchiveBase);
        var finalBase = Path.GetFullPath(finalOutputPath);
        var directory = Path.GetDirectoryName(tempBase)!;

        if (!Path.GetFileName(tempBase).StartsWith(SevenZipTempArchivePath.Prefix, StringComparison.Ordinal) ||
            !tempBase.EndsWith(SevenZipTempArchivePath.Extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArchiveException("El archivo temporal no es el generado por UltraArchive.");
        }

        if (!string.Equals(directory, Path.GetDirectoryName(finalBase), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArchiveException("El archivo temporal no está en la carpeta de destino esperada.");
        }

        // Reunir y ordenar las partes temporales (.001, .002…).
        var tempPrefix = Path.GetFileName(tempBase) + ".";
        var tempParts = Directory.EnumerateFiles(directory, Path.GetFileName(tempBase) + ".*")
            .Select(p => (Path: p, Suffix: Path.GetFileName(p)[tempPrefix.Length..]))
            .Where(x => x.Suffix.Length >= 1 && x.Suffix.All(char.IsAsciiDigit))
            .OrderBy(x => int.Parse(x.Suffix))
            .ToList();

        if (tempParts.Count == 0)
        {
            throw new ArchiveException("7zr terminó con éxito pero no se encontró ninguna parte generada.");
        }

        // La secuencia temporal debe ser 1..N sin huecos y cada parte debe ser un fichero no vacío.
        for (var i = 0; i < tempParts.Count; i++)
        {
            if (int.Parse(tempParts[i].Suffix) != i + 1)
            {
                CleanUp(tempParts.Select(t => t.Path));
                throw new ArchiveException("Las partes generadas por 7zr no forman una secuencia completa.");
            }

            var info = new FileInfo(tempParts[i].Path);
            if ((info.Attributes & FileAttributes.Directory) != 0 || info.Length == 0)
            {
                CleanUp(tempParts.Select(t => t.Path));
                throw new ArchiveException("Una de las partes 7Z generadas no es válida (vacía o no es un fichero).");
            }
        }

        // Mover cada parte al destino "<final>.NNN" (3 dígitos, nomenclatura estándar).
        var published = new List<string>();
        try
        {
            foreach (var (path, suffix) in tempParts)
            {
                var target = finalBase + "." + int.Parse(suffix).ToString("D3");
                File.Move(path, target, overwrite: true);
                published.Add(target);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CleanUp(published);
            CleanUp(tempParts.Select(t => t.Path));
            throw new ArchiveException($"No se pudieron publicar todas las partes en el destino: {ex.Message}", ex,
                ArchiveErrorCategory.FileInUse);
        }
    }

    private static void CleanUp(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            TryDelete(path);
        }
    }

    private static void DeleteSources(IReadOnlyList<string> sourcePaths)
    {
        foreach (var path in sourcePaths)
        {
            try
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
            catch
            {
                // best-effort, igual que el resto de escritores.
            }
        }
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

    /// <summary>Adapta el progreso 0-100 de <see cref="ISevenZipCli"/> al <see cref="OperationProgress"/> del dominio.</summary>
    private sealed class PercentToOperationProgress : IProgress<int>
    {
        private readonly IProgress<OperationProgress> _inner;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public PercentToOperationProgress(IProgress<OperationProgress> inner) => _inner = inner;

        public void Report(int percent) => _inner.Report(new OperationProgress
        {
            BytesProcessed = Math.Clamp(percent, 0, 100),
            TotalBytes = 100,
            Elapsed = _stopwatch.Elapsed,
        });
    }
}
