using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Implementación real de <see cref="ISevenZipProcessRunner"/> sobre <see cref="Process"/>.
///
/// Reglas fijas (no configurables):
///   - <c>UseShellExecute = false</c>, <c>CreateNoWindow = true</c>.
///   - stdout y stderr redirigidos y drenados <b>simultáneamente</b> vía eventos
///     (<c>OutputDataReceived</c>/<c>ErrorDataReceived</c> + <c>BeginOutputReadLine</c>/
///     <c>BeginErrorReadLine</c>) — nunca <c>ReadToEnd</c> secuencial.
///   - stdin redirigido y cerrado (7zr no debe quedarse esperando un prompt).
///   - El ejecutable debe ser una ruta absoluta existente; no se busca en PATH ni en ningún otro sitio.
/// </summary>
public sealed class SevenZipProcessRunner : ISevenZipProcessRunner
{
    public ISevenZipProcess Start(SevenZipProcessStartInfo startInfo, Action<SevenZipOutputChunk> onOutput)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentNullException.ThrowIfNull(onOutput);

        if (!Path.IsPathFullyQualified(startInfo.ExecutablePath))
        {
            throw new SevenZipProcessStartException(
                $"La ruta del ejecutable no es absoluta: '{startInfo.ExecutablePath}'.");
        }

        if (!File.Exists(startInfo.ExecutablePath))
        {
            throw new SevenZipProcessStartException(
                $"El ejecutable no existe: '{startInfo.ExecutablePath}'.");
        }

        var process = new Process
        {
            StartInfo = BuildStartInfo(startInfo),
            EnableRaisingEvents = true,
        };

        var wrapper = new RunningProcess(process, onOutput);
        wrapper.Begin();
        return wrapper;
    }

    /// <summary>Construcción del <see cref="ProcessStartInfo"/> — aislada para poder verificarla en tests.</summary>
    internal static ProcessStartInfo BuildStartInfo(SevenZipProcessStartInfo startInfo)
    {
        var psi = new ProcessStartInfo
        {
            FileName = startInfo.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = startInfo.WorkingDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in startInfo.ArgumentList)
        {
            psi.ArgumentList.Add(argument);
        }

        return psi;
    }

    private sealed class RunningProcess : ISevenZipProcess
    {
        private readonly Process _process;
        private readonly Action<SevenZipOutputChunk> _onOutput;
        private readonly TaskCompletionSource _stdoutClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _stderrClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _disposed;

        public RunningProcess(Process process, Action<SevenZipOutputChunk> onOutput)
        {
            _process = process;
            _onOutput = onOutput;
        }

        public void Begin()
        {
            _process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    _stdoutClosed.TrySetResult();
                }
                else
                {
                    SafeEmit(e.Data, isError: false);
                }
            };

            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    _stderrClosed.TrySetResult();
                }
                else
                {
                    SafeEmit(e.Data, isError: true);
                }
            };

            try
            {
                _process.Start();
            }
            catch (Win32Exception ex)
            {
                throw new SevenZipProcessStartException($"No se pudo iniciar 7zr: {ex.Message}", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new SevenZipProcessStartException($"No se pudo iniciar 7zr: {ex.Message}", ex);
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            try
            {
                _process.StandardInput.Close();
            }
            catch
            {
                // 7zr no debe pedir nada por consola; si no se puede cerrar stdin, seguimos.
            }
        }

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            // Dar un margen acotado a que los eventos de stdout/stderr entreguen todo lo pendiente.
            var flush = Task.WhenAll(_stdoutClosed.Task, _stderrClosed.Task);
            await Task.WhenAny(flush, Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None)).ConfigureAwait(false);

            return _process.ExitCode;
        }

        public void KillTree()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // best-effort: si ya terminó o no se puede matar, no propagar.
            }
        }

        public bool HasExited
        {
            get
            {
                try
                {
                    return _process.HasExited;
                }
                catch
                {
                    return false;
                }
            }
        }

        public int? ExitCode
        {
            get
            {
                try
                {
                    return _process.HasExited ? _process.ExitCode : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            KillTree();
            try
            {
                _process.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        private void SafeEmit(string data, bool isError)
        {
            try
            {
                _onOutput(new SevenZipOutputChunk(data, isError));
            }
            catch
            {
                // El consumidor nunca debe poder derribar el lector de salida.
            }
        }
    }
}
