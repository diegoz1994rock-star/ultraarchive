using UltraArchive.Core.Models;
using UltraArchive.Interop.SevenZip;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Interop;

/// <summary>
/// Fase 6A, Paso 3: ejecución controlada de 7zr. NO se ejecuta ningún 7zr real: se usan
/// <see cref="FakeSevenZipProcessRunner"/> / <see cref="FakeSevenZipProcess"/> + reloj/retardo inyectados.
/// </summary>
public class SevenZipCliTests
{
    private const string Password = "Clave-Secreta-2026";

    private static SevenZipExecutionRequest MakeRequest(TempWorkspace ws, IProgress<int>? progress = null, bool mhe = true)
    {
        var exe = ws.ArchivePath("7zr.exe");
        File.WriteAllBytes(exe, new byte[] { 0x4D, 0x5A, 1, 2, 3 });

        var compression = SevenZipRequest.Create(
            new[] { @"C:\datos\a.txt", @"C:\datos\b.bin" },
            Path.Combine(ws.OutputDir, "final.7z"),
            Password,
            CompressionLevel.Normal,
            encryptHeaders: mhe);

        return new SevenZipExecutionRequest
        {
            ExecutablePath = exe,
            ExpectedExecutablePath = exe,
            Compression = compression,
            Progress = progress,
        };
    }

    private static SevenZipCli NewCli(FakeSevenZipProcessRunner runner, MutableClock? clock = null, string? rspDir = null) =>
        new(runner,
            clock: clock?.Now,
            delay: (_, ct) => Task.Delay(1, ct),
            pollInterval: TimeSpan.FromMilliseconds(1),
            inactivityLimit: TimeSpan.FromMinutes(10),
            responseFileDirectory: rspDir);

    /// <summary>Runner cuyo proceso simula que 7zr crea el archivo temporal y luego sale con <paramref name="exitCode"/>.</summary>
    private static FakeSevenZipProcessRunner RunnerThatCreatesArchiveAndExits(int exitCode, string? stderr = null, string? stdout = null)
    {
        return new FakeSevenZipProcessRunner(startInfo =>
        {
            var archivePath = startInfo.ArgumentList.First(a => a.EndsWith(".7z", StringComparison.Ordinal));
            var process = new FakeSevenZipProcess();
            process.BeforeEachWait = (n, proc) =>
            {
                if (n == 1)
                {
                    if (exitCode is 0 or 1)
                    {
                        File.WriteAllText(archivePath, "contenido 7z simulado");
                    }

                    if (stdout is not null) proc.EmitStdout(stdout);
                    if (stderr is not null) proc.EmitStderr(stderr);
                    proc.EmitStdout(" 100%");
                }

                if (n == 2)
                {
                    proc.CompleteWith(exitCode);
                }
            };
            return process;
        });
    }

    // ==================== B. Seguridad ====================

    [Fact]
    public async Task ExecuteAsync_StderrContieneLaContrasena_ElResultadoLaEnmascara()
    {
        using var ws = new TempWorkspace();
        var runner = RunnerThatCreatesArchiveAndExits(2, stderr: $"ERROR abriendo C:\\x\\{Password}-notas.txt");
        var result = await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        Assert.Equal(SevenZipExecutionStatus.Failed, result.Status);
        Assert.NotNull(result.ErrorMessage);
        Assert.DoesNotContain(Password, result.ErrorMessage!);
        Assert.Contains("***", result.ErrorMessage!);
    }

    [Fact]
    public async Task ExecuteAsync_LaContrasenaNoApareceEnNingunCampoDelResultado()
    {
        using var ws = new TempWorkspace();
        var runner = RunnerThatCreatesArchiveAndExits(0, stderr: Password, stdout: Password);
        var result = await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        var dump = $"{result.Status}|{result.ExitCode}|{result.ErrorMessage}|{result.WarningMessage}|{result.OutputArchivePath}|{result.ProgressReached}";
        Assert.DoesNotContain(Password, dump);
    }

    [Fact]
    public async Task ExecuteAsync_LaContrasenaNuncaLlegaAlResponseFile()
    {
        using var ws = new TempWorkspace();
        string? capturedRspContent = null;
        var runner = new FakeSevenZipProcessRunner(startInfo =>
        {
            // el response file sigue vivo durante la ejecución → lo podemos leer aquí
            var rsp = startInfo.ArgumentList.First(a => a.StartsWith('@'))[1..];
            capturedRspContent = File.ReadAllText(rsp);
            var p = new FakeSevenZipProcess();
            p.BeforeEachWait = (n, proc) =>
            {
                if (n == 1) File.WriteAllText(startInfo.ArgumentList.First(a => a.EndsWith(".7z")), "x");
                if (n == 2) proc.CompleteWith(0);
            };
            return p;
        });

        await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        Assert.NotNull(capturedRspContent);
        Assert.DoesNotContain(Password, capturedRspContent!);
    }

    [Fact]
    public void ExecuteAsync_RutaDeOrigenPeligrosa_SeRechazaAntesDeEjecutar()
    {
        using var ws = new TempWorkspace();

        // '-mhe=off' como "ruta" de origen → SevenZipRequest.Create lanza (Paso 2, no se relaja).
        Assert.Throws<ArgumentException>(() => SevenZipRequest.Create(
            new[] { "-mhe=off" }, Path.Combine(ws.OutputDir, "f.7z"), Password));
    }

    // ==================== C. Exit codes ====================

    [Theory]
    [InlineData(0, SevenZipExecutionStatus.Success)]
    [InlineData(1, SevenZipExecutionStatus.Warning)]
    [InlineData(2, SevenZipExecutionStatus.Failed)]
    [InlineData(7, SevenZipExecutionStatus.Failed)]
    [InlineData(8, SevenZipExecutionStatus.Failed)]
    [InlineData(255, SevenZipExecutionStatus.Cancelled)]
    [InlineData(99, SevenZipExecutionStatus.Failed)]
    public async Task ExecuteAsync_CodigoDeSalida_SeClasificaCorrectamente(int exitCode, SevenZipExecutionStatus expected)
    {
        using var ws = new TempWorkspace();
        var runner = RunnerThatCreatesArchiveAndExits(exitCode, stderr: exitCode == 0 ? null : "detalle del error");
        var result = await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        Assert.Equal(expected, result.Status);
        if (expected is SevenZipExecutionStatus.Success or SevenZipExecutionStatus.Warning)
        {
            Assert.Equal(exitCode, result.ExitCode);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Exito_DevuelveLaRutaDelArchivoTemporalYNoLoBorra()
    {
        using var ws = new TempWorkspace();
        var runner = RunnerThatCreatesArchiveAndExits(0);
        var result = await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.OutputArchivePath);
        Assert.True(File.Exists(result.OutputArchivePath!));
        Assert.StartsWith(".uatmp-", Path.GetFileName(result.OutputArchivePath!));
        Assert.Equal(ws.OutputDir, Path.GetDirectoryName(result.OutputArchivePath));
    }

    [Fact]
    public async Task ExecuteAsync_Fallo_BorraElArchivoTemporal()
    {
        using var ws = new TempWorkspace();
        var runner = RunnerThatCreatesArchiveAndExits(2, stderr: "error fatal");
        var result = await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        Assert.Equal(SevenZipExecutionStatus.Failed, result.Status);
        Assert.Null(result.OutputArchivePath);
        Assert.Empty(Directory.GetFiles(ws.OutputDir, ".uatmp-*.7z"));
    }

    // ==================== D. Progreso (a través del CLI) ====================

    [Fact]
    public async Task ExecuteAsync_ReportaProgresoDesde7zr_Monotonico()
    {
        using var ws = new TempWorkspace();
        var reports = new List<int>();
        var progress = new SyncProgress<int>(reports.Add);

        var runner = new FakeSevenZipProcessRunner(startInfo =>
        {
            var archivePath = startInfo.ArgumentList.First(a => a.EndsWith(".7z"));
            var p = new FakeSevenZipProcess();
            p.BeforeEachWait = (n, proc) =>
            {
                if (n == 1) proc.EmitStdout(" 10%");
                if (n == 2) proc.EmitStdout(" 45%");
                if (n == 3) proc.EmitStdout(" 30%");   // valor menor: no debe reportarse hacia atrás
                if (n == 4) { File.WriteAllText(archivePath, "x"); proc.EmitStdout("100%"); }
                if (n == 5) proc.CompleteWith(0);
            };
            return p;
        });

        var result = await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws, progress), CancellationToken.None);

        Assert.Equal(SevenZipExecutionStatus.Success, result.Status);
        Assert.Equal(100, result.ProgressReached);
        Assert.Equal(reports.OrderBy(x => x), reports); // monotónico
        Assert.DoesNotContain(reports, x => x is < 0 or > 100);
    }

    [Fact]
    public async Task ExecuteAsync_SalidaSinPorcentajes_NoFalla_ProgresoIndeterminado()
    {
        using var ws = new TempWorkspace();
        var runner = new FakeSevenZipProcessRunner(startInfo =>
        {
            var archivePath = startInfo.ArgumentList.First(a => a.EndsWith(".7z"));
            var p = new FakeSevenZipProcess();
            p.BeforeEachWait = (n, proc) =>
            {
                if (n == 1) { proc.EmitStdout("Scanning the drive:"); proc.EmitStdout("Everything is Ok"); }
                if (n == 2) { File.WriteAllText(archivePath, "x"); proc.CompleteWith(0); }
            };
            return p;
        });

        var result = await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        Assert.Equal(SevenZipExecutionStatus.Success, result.Status);
        Assert.Equal(0, result.ProgressReached);
    }

    [Fact]
    public async Task ExecuteAsync_CallbackDeProgresoQueLanza_NoDerribaLaOperacion()
    {
        using var ws = new TempWorkspace();
        var progress = new SyncProgress<int>(_ => throw new InvalidOperationException("boom"));
        var runner = RunnerThatCreatesArchiveAndExits(0);

        var result = await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws, progress), CancellationToken.None);

        Assert.Equal(SevenZipExecutionStatus.Success, result.Status);
    }

    // ==================== E. Cancelación ====================

    [Fact]
    public async Task ExecuteAsync_CancellationToken_MataElArbolYDevuelveCancelled()
    {
        using var ws = new TempWorkspace();
        using var cts = new CancellationTokenSource();
        var runner = new FakeSevenZipProcessRunner(_ =>
        {
            var p = new FakeSevenZipProcess { MaxPollsBeforeAutoExit = 1000 };
            p.BeforeEachWait = (n, _) => { if (n == 2) cts.Cancel(); }; // no CompleteWith: sigue "vivo"
            return p;
        });

        var result = await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws), cts.Token);

        Assert.Equal(SevenZipExecutionStatus.Cancelled, result.Status);
        Assert.True(result.WasCancelled);
        Assert.False(result.TimedOut);
        Assert.Equal(1, runner.LastProcess!.KillTreeCalls);
        Assert.Empty(Directory.GetFiles(ws.OutputDir, ".uatmp-*.7z"));
    }

    [Fact]
    public async Task ExecuteAsync_TokenYaCancelado_DevuelveCancelledSinDejarBasura()
    {
        using var ws = new TempWorkspace();
        var runner = new FakeSevenZipProcessRunner(_ => new FakeSevenZipProcess { MaxPollsBeforeAutoExit = 1000 });

        var result = await NewCli(runner, rspDir: ws.RootPath)
            .ExecuteAsync(MakeRequest(ws), new CancellationToken(canceled: true));

        Assert.Equal(SevenZipExecutionStatus.Cancelled, result.Status);
        Assert.Equal(1, runner.LastProcess!.KillTreeCalls);
    }

    // ==================== Watchdog ====================

    [Fact]
    public async Task ExecuteAsync_SinProgresoDurante10Min_DetieneElProceso_YMarcaTimedOut()
    {
        using var ws = new TempWorkspace();
        var clock = new MutableClock();
        var runner = new FakeSevenZipProcessRunner(_ =>
        {
            var p = new FakeSevenZipProcess { MaxPollsBeforeAutoExit = 1000 };
            p.BeforeEachWait = (n, proc) =>
            {
                if (n == 1) proc.EmitStdout(" 5%");                          // último progreso válido
                if (n == 3) clock.Advance(TimeSpan.FromMinutes(11));         // pasa el tiempo, sin más progreso
            };
            return p;
        });

        var result = await NewCli(runner, clock, ws.RootPath).ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        Assert.Equal(SevenZipExecutionStatus.Failed, result.Status);
        Assert.True(result.TimedOut);
        Assert.False(result.WasCancelled);
        Assert.Equal(1, runner.LastProcess!.KillTreeCalls);
        Assert.False(result.CompletedWithArchive);
    }

    [Fact]
    public async Task ExecuteAsync_ProgresoRegular_NoDisparaElWatchdog()
    {
        using var ws = new TempWorkspace();
        var clock = new MutableClock();
        var runner = new FakeSevenZipProcessRunner(startInfo =>
        {
            var archivePath = startInfo.ArgumentList.First(a => a.EndsWith(".7z"));
            var p = new FakeSevenZipProcess { MaxPollsBeforeAutoExit = 1000 };
            p.BeforeEachWait = (n, proc) =>
            {
                clock.Advance(TimeSpan.FromMinutes(9));   // avanza mucho, pero...
                proc.EmitStdout($" {Math.Min(n * 20, 100)}%"); // ...siempre hay progreso
                if (n >= 5) { File.WriteAllText(archivePath, "x"); proc.CompleteWith(0); }
            };
            return p;
        });

        var result = await NewCli(runner, clock, ws.RootPath).ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        Assert.Equal(SevenZipExecutionStatus.Success, result.Status);
        Assert.False(result.TimedOut);
        Assert.Equal(0, runner.LastProcess!.KillTreeCalls);
    }

    // ==================== F. Limpieza ====================

    [Theory]
    [InlineData(0)]   // éxito
    [InlineData(2)]   // error
    public async Task ExecuteAsync_TrasEjecutar_NoQuedaNingunResponseFile(int exitCode)
    {
        using var ws = new TempWorkspace();
        var runner = RunnerThatCreatesArchiveAndExits(exitCode, stderr: exitCode == 0 ? null : "err");

        await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        Assert.Empty(Directory.GetFiles(ws.RootPath, "uarsp-*.txt"));
    }

    [Fact]
    public async Task ExecuteAsync_TrasCancelar_NoQuedaResponseFile()
    {
        using var ws = new TempWorkspace();
        using var cts = new CancellationTokenSource();
        var runner = new FakeSevenZipProcessRunner(_ =>
        {
            var p = new FakeSevenZipProcess { MaxPollsBeforeAutoExit = 1000 };
            p.BeforeEachWait = (n, _) => { if (n == 1) cts.Cancel(); };
            return p;
        });

        await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws), cts.Token);

        Assert.Empty(Directory.GetFiles(ws.RootPath, "uarsp-*.txt"));
    }

    [Fact]
    public async Task ExecuteAsync_NoSePudoIniciar7zr_LimpiaYDevuelveFailed()
    {
        using var ws = new TempWorkspace();
        var runner = new FakeSevenZipProcessRunner { StartException = new SevenZipProcessStartException("acceso denegado") };

        var result = await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        Assert.Equal(SevenZipExecutionStatus.Failed, result.Status);
        Assert.Null(result.ExitCode);
        Assert.Empty(Directory.GetFiles(ws.RootPath, "uarsp-*.txt"));
        Assert.Empty(Directory.GetFiles(ws.OutputDir, ".uatmp-*.7z"));
    }

    // ==================== G. Ejecución controlada / validación del ejecutable ====================

    [Fact]
    public async Task ExecuteAsync_ExecutablePathRelativo_RechazaSinEjecutar()
    {
        using var ws = new TempWorkspace();
        var request = MakeRequest(ws);
        var bad = new SevenZipExecutionRequest
        {
            ExecutablePath = "7zr.exe", // relativo → PATH → prohibido
            Compression = request.Compression,
        };
        var runner = new FakeSevenZipProcessRunner();

        var result = await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(bad, CancellationToken.None);

        Assert.Equal(SevenZipExecutionStatus.Failed, result.Status);
        Assert.Equal(0, runner.StartCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutablePathNoCoincideConElVerificado_Rechaza()
    {
        using var ws = new TempWorkspace();
        var realExe = ws.ArchivePath("7zr.exe");
        File.WriteAllBytes(realExe, new byte[] { 1 });
        var otherExe = ws.ArchivePath("otro.exe");
        File.WriteAllBytes(otherExe, new byte[] { 1 });

        var request = new SevenZipExecutionRequest
        {
            ExecutablePath = otherExe,
            ExpectedExecutablePath = realExe,
            Compression = SevenZipRequest.Create(new[] { @"C:\d\a.txt" }, Path.Combine(ws.OutputDir, "f.7z"), Password),
        };
        var runner = new FakeSevenZipProcessRunner();

        var result = await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(SevenZipExecutionStatus.Failed, result.Status);
        Assert.Equal(0, runner.StartCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ConsumeLaArgumentListDelBuilder_TalCual()
    {
        using var ws = new TempWorkspace();
        var runner = RunnerThatCreatesArchiveAndExits(0);

        await NewCli(runner, rspDir: ws.RootPath).ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        var args = runner.LastStartInfo!.ArgumentList;
        Assert.Equal("a", args[0]);
        Assert.Contains("-t7z", args);
        Assert.Contains("-mhe=on", args);           // mhe:true en MakeRequest
        Assert.Contains("-p" + Password, args);      // la contraseña va en argv (limitación conocida)
        Assert.Contains("-bsp1", args);
        Assert.Contains(args, a => a.StartsWith('@'));                  // response file
        Assert.Contains(args, a => a.EndsWith(".7z") && a.Contains(".uatmp-")); // archivo temporal, no el final
        Assert.DoesNotContain(args, a => a.Contains("final.7z"));       // el destino final NO se pasa a 7zr
    }

    [Fact]
    public async Task ExecuteAsync_Dispose_DelProceso_EsIdempotente()
    {
        using var ws = new TempWorkspace();
        var runner = RunnerThatCreatesArchiveAndExits(0);
        var cli = NewCli(runner, rspDir: ws.RootPath);

        await cli.ExecuteAsync(MakeRequest(ws), CancellationToken.None);

        // el proceso ya se dispuso en el finally; volver a disponerlo no lanza
        runner.LastProcess!.Dispose();
        runner.LastProcess!.Dispose();
    }
}
