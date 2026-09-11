using UltraArchive.Interop.SevenZip;

namespace UltraArchive.Tests.TestSupport;

/// <summary>Doble de <see cref="ISevenZipProcess"/> para probar <c>SevenZipCli</c> sin lanzar ningún 7zr real.</summary>
internal sealed class FakeSevenZipProcess : ISevenZipProcess
{
    private readonly TaskCompletionSource<int> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _pollCount;

    internal Action<SevenZipOutputChunk>? OnOutput { get; set; }

    public int KillTreeCalls { get; private set; }

    public int PollCount => _pollCount;

    /// <summary>Se invoca antes de cada espera del bucle de sondeo (n = 1, 2, 3, ...), solo mientras el proceso siga vivo.</summary>
    public Action<int, FakeSevenZipProcess>? BeforeEachWait { get; set; }

    /// <summary>Tope de seguridad: tras este nº de sondeos el proceso "termina" con <see cref="AutoExitCode"/> (evita colgar un test).</summary>
    public int MaxPollsBeforeAutoExit { get; set; } = 500;

    public int AutoExitCode { get; set; }

    public void EmitStdout(string text) => OnOutput?.Invoke(new SevenZipOutputChunk(text, IsError: false));

    public void EmitStderr(string text) => OnOutput?.Invoke(new SevenZipOutputChunk(text, IsError: true));

    public void CompleteWith(int exitCode) => _exit.TrySetResult(exitCode);

    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        if (!_exit.Task.IsCompleted)
        {
            var n = Interlocked.Increment(ref _pollCount);
            BeforeEachWait?.Invoke(n, this);
            if (n >= MaxPollsBeforeAutoExit)
            {
                _exit.TrySetResult(AutoExitCode);
            }
        }

        return await _exit.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void KillTree()
    {
        KillTreeCalls++;
        _exit.TrySetResult(-1);
    }

    public bool HasExited => _exit.Task.IsCompleted;

    public int? ExitCode => _exit.Task.IsCompletedSuccessfully ? _exit.Task.Result : null;

    public void Dispose()
    {
    }
}

/// <summary>Doble de <see cref="ISevenZipProcessRunner"/>.</summary>
internal sealed class FakeSevenZipProcessRunner : ISevenZipProcessRunner
{
    private readonly Func<SevenZipProcessStartInfo, FakeSevenZipProcess> _factory;

    public FakeSevenZipProcessRunner(Func<SevenZipProcessStartInfo, FakeSevenZipProcess>? factory = null)
        => _factory = factory ?? (_ => new FakeSevenZipProcess());

    public SevenZipProcessStartInfo? LastStartInfo { get; private set; }

    public FakeSevenZipProcess? LastProcess { get; private set; }

    /// <summary>Si se indica, <see cref="Start"/> lanza esta excepción en vez de arrancar.</summary>
    public Exception? StartException { get; set; }

    public int StartCalls { get; private set; }

    public ISevenZipProcess Start(SevenZipProcessStartInfo startInfo, Action<SevenZipOutputChunk> onOutput)
    {
        StartCalls++;
        LastStartInfo = startInfo;

        if (StartException is not null)
        {
            throw StartException;
        }

        var process = _factory(startInfo);
        process.OnOutput = onOutput;
        LastProcess = process;
        return process;
    }
}
