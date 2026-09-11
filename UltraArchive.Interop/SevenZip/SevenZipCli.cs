namespace UltraArchive.Interop.SevenZip;

/// <summary>Implementación de <see cref="ISevenZipCli"/>. Ver la interfaz para el flujo completo.</summary>
public sealed class SevenZipCli : ISevenZipCli
{
    private readonly ISevenZipProcessRunner _runner;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _inactivityLimit;
    private readonly string? _responseFileDirectory;

    public SevenZipCli(ISevenZipProcessRunner runner)
        : this(runner, clock: null, delay: null, pollInterval: null, inactivityLimit: null, responseFileDirectory: null)
    {
    }

    /// <summary>Sobrecarga para pruebas: reloj, retardo entre sondeos, límites y carpeta del response file inyectables (sin Thread.Sleep).</summary>
    internal SevenZipCli(
        ISevenZipProcessRunner runner,
        Func<DateTimeOffset>? clock,
        Func<TimeSpan, CancellationToken, Task>? delay,
        TimeSpan? pollInterval,
        TimeSpan? inactivityLimit,
        string? responseFileDirectory = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _delay = delay ?? ((ts, ct) => Task.Delay(ts, ct));
        _pollInterval = pollInterval ?? SevenZipExecutionDefaults.PollInterval;
        _inactivityLimit = inactivityLimit ?? SevenZipExecutionDefaults.InactivityLimit;
        _responseFileDirectory = responseFileDirectory;
    }

    public async Task<SevenZipExecutionResult> ExecuteAsync(SevenZipExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var executableError = ValidateExecutable(request);
        if (executableError is not null)
        {
            return executableError;
        }

        var progress = new SevenZipCliProgress();
        var password = request.Compression.Password;
        var gate = new object();

        string? tempArchive = null;
        SevenZipResponseFile? responseFile = null;
        ISevenZipProcess? process = null;
        SevenZipExecutionResult? result = null;

        try
        {
            tempArchive = SevenZipTempArchivePath.Create(request.Compression.OutputPath);

            var tempRequest = SevenZipRequest.Create(
                request.Compression.SourcePaths,
                tempArchive,
                password,
                request.Compression.Level,
                request.Compression.EncryptHeaders,
                request.Compression.VolumeSizeBytes);

            responseFile = SevenZipResponseFile.Create(tempRequest.SourcePaths, _responseFileDirectory);
            var arguments = new SevenZipArgumentBuilder().Build(tempRequest, responseFile);

            var watchdog = new SevenZipInactivityWatchdog(_inactivityLimit, _clock);
            var stdout = new SevenZipSecretScrubber.BoundedBuffer(password);
            var stderr = new SevenZipSecretScrubber.BoundedBuffer(password);

            var startInfo = new SevenZipProcessStartInfo
            {
                ExecutablePath = request.ExecutablePath,
                ArgumentList = arguments.ArgumentList,
                WorkingDirectory = Path.GetDirectoryName(tempArchive)!,
            };

            process = _runner.Start(startInfo, chunk =>
                HandleChunk(chunk, gate, progress, watchdog, request.Progress, stdout, stderr));

            try
            {
                var exitCode = await WaitLoopAsync(process, watchdog, gate, cancellationToken).ConfigureAwait(false);
                result = Classify(exitCode, progress.Current, stdout, stderr, tempArchive, tempRequest.IsSplit);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (!process.HasExited)
                {
                    process.KillTree();
                }

                await SafeAwaitExitAsync(process).ConfigureAwait(false);
                result = SevenZipExecutionResult.ForCancellation(process.ExitCode, progress.Current);
            }
            catch (SevenZipWatchdogAbort)
            {
                result = SevenZipExecutionResult.ForTimeout(progress.Current, error: null);
            }

            return result;
        }
        catch (SevenZipProcessStartException ex)
        {
            result = SevenZipExecutionResult.ForFailure(null, progress.Current,
                "No se pudo iniciar 7zr: " + SevenZipSecretScrubber.Scrub(ex.Message, password));
            return result;
        }
        catch (OperationCanceledException)
        {
            result = SevenZipExecutionResult.ForCancellation(process?.ExitCode, progress.Current);
            return result;
        }
        catch (Exception ex)
        {
            result = SevenZipExecutionResult.ForFailure(null, progress.Current,
                "Error inesperado al ejecutar 7zr: " + SevenZipSecretScrubber.Scrub(ex.Message, password));
            return result;
        }
        finally
        {
            responseFile?.Dispose(); // el response file se borra SIEMPRE
            process?.Dispose();

            if ((result is null || !result.CompletedWithArchive) && tempArchive is not null)
            {
                // El .7z temporal (y sus volúmenes .NNN) solo sobreviven si la operación fue válida.
                TryDelete(tempArchive);
                TryDeleteVolumes(tempArchive);
            }
        }
    }

    private async Task<int> WaitLoopAsync(ISevenZipProcess process, SevenZipInactivityWatchdog watchdog, object gate, CancellationToken ct)
    {
        while (true)
        {
            if (ct.IsCancellationRequested)
            {
                process.KillTree();
                await SafeAwaitExitAsync(process).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
            }

            bool shouldAbort;
            lock (gate)
            {
                shouldAbort = watchdog.ShouldAbort();
            }

            if (shouldAbort && !process.HasExited)
            {
                process.KillTree();
                await SafeAwaitExitAsync(process).ConfigureAwait(false);
                throw new SevenZipWatchdogAbort();
            }

            if (await WaitOnceAsync(process, ct).ConfigureAwait(false))
            {
                return process.ExitCode ?? -1;
            }
        }
    }

    private async Task<bool> WaitOnceAsync(ISevenZipProcess process, CancellationToken ct)
    {
        var exitTask = process.WaitForExitAsync(ct);
        var delayTask = _delay(_pollInterval, ct);

        var finished = await Task.WhenAny(exitTask, delayTask).ConfigureAwait(false);
        if (finished == exitTask)
        {
            await exitTask.ConfigureAwait(false); // observar (propaga OperationCanceledException si aplica)
            return true;
        }

        // Ganó el retardo: si la espera de salida ya falló (p. ej. por cancelación), observar la
        // excepción para que no quede como "unobserved". El bucle exterior gestiona la cancelación.
        if (exitTask.IsFaulted)
        {
            _ = exitTask.Exception;
        }

        return process.HasExited;
    }

    private static void HandleChunk(
        SevenZipOutputChunk chunk,
        object gate,
        SevenZipCliProgress progress,
        SevenZipInactivityWatchdog watchdog,
        IProgress<int>? reporter,
        SevenZipSecretScrubber.BoundedBuffer stdout,
        SevenZipSecretScrubber.BoundedBuffer stderr)
    {
        int? toReport = null;

        lock (gate)
        {
            if (progress.Feed(chunk.Text))
            {
                watchdog.NotifyProgress();
                toReport = progress.Current;
            }

            (chunk.IsError ? stderr : stdout).Append(chunk.Text);
        }

        if (toReport is int percent)
        {
            try
            {
                reporter?.Report(percent);
            }
            catch
            {
                // Un callback de progreso que lance NUNCA debe derribar la lectura de salida ni el proceso.
            }
        }
    }

    private static SevenZipExecutionResult Classify(
        int exitCode,
        int progress,
        SevenZipSecretScrubber.BoundedBuffer stdout,
        SevenZipSecretScrubber.BoundedBuffer stderr,
        string tempArchive,
        bool isSplit)
    {
        var diagnostic = stderr.HasContent ? stderr.ToString()
            : stdout.HasContent ? stdout.ToString()
            : null;

        // Dividido: 7zr NO crea "tempArchive" sino "tempArchive.001", ".002"… → el archivo que
        // confirma el éxito es la primera parte. OutputArchivePath sigue siendo la base: el writer
        // publica todo el conjunto ".NNN".
        var producedFile = isSplit ? tempArchive + ".001" : tempArchive;

        return exitCode switch
        {
            0 => File.Exists(producedFile)
                ? SevenZipExecutionResult.ForSuccess(progress, tempArchive, warning: null)
                : SevenZipExecutionResult.ForFailure(0, progress, "7zr devolvió éxito pero no se generó el archivo."),

            1 => File.Exists(producedFile)
                ? SevenZipExecutionResult.ForWarning(progress, tempArchive, diagnostic)
                : SevenZipExecutionResult.ForFailure(1, progress, diagnostic ?? "7zr terminó con advertencias y sin archivo."),

            255 => SevenZipExecutionResult.ForCancellation(255, progress),

            _ => SevenZipExecutionResult.ForFailure(exitCode, progress,
                diagnostic ?? $"7zr terminó con el código de salida {exitCode}."),
        };
    }

    private static SevenZipExecutionResult? ValidateExecutable(SevenZipExecutionRequest request)
    {
        var path = request.ExecutablePath;

        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return SevenZipExecutionResult.ForFailure(null, 0, "La ruta del 7zr.exe no es una ruta absoluta.");
        }

        if (!File.Exists(path))
        {
            return SevenZipExecutionResult.ForFailure(null, 0, "El 7zr.exe indicado no existe.");
        }

        if (request.ExpectedExecutablePath is { Length: > 0 } expected &&
            !string.Equals(Path.GetFullPath(path), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase))
        {
            return SevenZipExecutionResult.ForFailure(null, 0, "La ruta del 7zr.exe no coincide con la verificada.");
        }

        return null;
    }

    private static async Task SafeAwaitExitAsync(ISevenZipProcess process)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // El proceso ya está matado; si no confirma la salida a tiempo, no bloqueamos.
        }
    }

    private static void TryDelete(string path)
    {
        // Reintenta unas veces: tras matar 7zr el SO puede tardar unos ms en liberar el handle del
        // fichero, y no queremos dejar temporales (.uatmp-*) tras una cancelación.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                File.Delete(path);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(60);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(60);
            }
            catch
            {
                return; // otro error: no insistir
            }
        }
    }

    /// <summary>
    /// Borra cualquier resto de un intento con 7zr: los volúmenes <c>&lt;tempArchive&gt;.NNN</c>, los
    /// temporales de volumen a medias que 7-Zip crea con sufijo <c>.tmp</c> (<c>...7z.001.tmp</c>),
    /// y el propio <c>&lt;tempArchive&gt;</c>. El prefijo <c>.uatmp-&lt;guid&gt;</c> es único, así que
    /// borrar todo lo que empiece por ese nombre es seguro.
    /// </summary>
    private static void TryDeleteVolumes(string tempArchive)
    {
        try
        {
            var directory = Path.GetDirectoryName(tempArchive);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return;
            }

            var tempName = Path.GetFileName(tempArchive); // ".uatmp-<guid>.7z"
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (Path.GetFileName(file).StartsWith(tempName, StringComparison.Ordinal))
                {
                    TryDelete(file);
                }
            }
        }
        catch
        {
            // best-effort
        }
    }

    /// <summary>Señal interna: el watchdog de inactividad ha detenido el proceso.</summary>
    private sealed class SevenZipWatchdogAbort : Exception
    {
    }
}
