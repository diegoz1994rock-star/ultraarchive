using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Interop.SevenZip;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Interop;

/// <summary>
/// Fase 6A, Paso 4: <see cref="RoutingSevenZipWriter"/> — enrutado managed/CLI + publicación del
/// archivo. No se ejecuta 7zr real: se usa <see cref="FakeSevenZipCli"/>.
/// </summary>
public class RoutingSevenZipWriterTests
{
    private const string Password = "Clave-7Z-2026";

    // ---- dobles ----

    private sealed class FakeManagedWriter : IArchiveWriter
    {
        public int Calls { get; private set; }
        public CreateArchiveOptions? LastOptions { get; private set; }
        public ArchiveFormat Format => ArchiveFormat.SevenZip;

        public Task CreateAsync(CreateArchiveOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastOptions = options;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSevenZipCli : ISevenZipCli
    {
        public int Calls { get; private set; }
        public SevenZipExecutionRequest? LastRequest { get; private set; }
        public Func<SevenZipExecutionRequest, SevenZipExecutionResult> Responder { get; set; } = _ =>
            SevenZipExecutionResult.ForFailure(2, 0, "sin responder configurado");

        public Task<SevenZipExecutionResult> ExecuteAsync(SevenZipExecutionRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(Responder(request));
        }
    }

    private sealed class FakeCapability(bool available, string? exe) : ISevenZipCapability
    {
        public bool CanCreateEncryptedSevenZip => available;
        public string? VerifiedExecutablePath => exe;
        public string StatusExplanation => available ? "verificado" : "no disponible";
    }

    private static FakeCapability AvailableCapability(TempWorkspace ws)
    {
        var exe = ws.ArchivePath("7zr.exe");
        File.WriteAllBytes(exe, new byte[] { 1 });
        return new FakeCapability(available: true, exe);
    }

    private static CreateArchiveOptions Options(
        TempWorkspace ws,
        string? password,
        string outName = "salida.7z",
        ArchiveFormat format = ArchiveFormat.SevenZip,
        bool deleteSource = false)
    {
        ws.CreateSampleFile("a.txt", "hola");
        return new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = Path.Combine(ws.OutputDir, outName),
            Format = format,
            Password = password,
            Encryption = string.IsNullOrEmpty(password) ? EncryptionMethod.None : EncryptionMethod.Aes256,
            DeleteSourceAfterCompress = deleteSource,
        };
    }

    /// <summary>Responder que "crea" el .uatmp temporal en la carpeta del destino y devuelve el estado dado.</summary>
    private static Func<SevenZipExecutionRequest, SevenZipExecutionResult> RespondCreating(SevenZipExecutionStatus status, string content = "7z cifrado simulado")
    {
        return request =>
        {
            var dir = Path.GetDirectoryName(request.Compression.OutputPath)!;
            var temp = Path.Combine(dir, $"{SevenZipTempArchivePath.Prefix}{Guid.NewGuid():N}{SevenZipTempArchivePath.Extension}");

            if (status is SevenZipExecutionStatus.Success or SevenZipExecutionStatus.Warning)
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(temp, content);
                return status == SevenZipExecutionStatus.Success
                    ? SevenZipExecutionResult.ForSuccess(100, temp, null)
                    : SevenZipExecutionResult.ForWarning(100, temp, "un fichero estaba bloqueado");
            }

            return status switch
            {
                SevenZipExecutionStatus.Cancelled => SevenZipExecutionResult.ForCancellation(255, 40),
                _ => SevenZipExecutionResult.ForFailure(2, 40, "error de 7zr"),
            };
        };
    }

    // ==================== A. Routing ====================

    [Fact]
    public async Task Sin7ZConPassword_UsaElWriterGestionado_NoElCli_YSinAlterarLasOpciones()
    {
        using var ws = new TempWorkspace();
        var managed = new FakeManagedWriter();
        var cli = new FakeSevenZipCli();
        var writer = new RoutingSevenZipWriter(managed, cli, new FakeCapability(false, null));
        var options = Options(ws, password: null);

        await writer.CreateAsync(options);

        Assert.Equal(1, managed.Calls);
        Assert.Equal(0, cli.Calls);
        Assert.Same(options, managed.LastOptions); // se pasa tal cual: 7Z normal sin cambios
    }

    [Fact]
    public async Task PasswordCadenaVacia_TratadoComoSinPassword_UsaElWriterGestionado()
    {
        using var ws = new TempWorkspace();
        var managed = new FakeManagedWriter();
        var cli = new FakeSevenZipCli();
        var writer = new RoutingSevenZipWriter(managed, cli, new FakeCapability(false, null));

        await writer.CreateAsync(Options(ws, password: string.Empty));

        Assert.Equal(1, managed.Calls);
        Assert.Equal(0, cli.Calls);
    }

    [Fact]
    public async Task ConPassword_UsaElCli_NoElWriterGestionado()
    {
        using var ws = new TempWorkspace();
        var managed = new FakeManagedWriter();
        var cli = new FakeSevenZipCli { Responder = RespondCreating(SevenZipExecutionStatus.Success) };
        var writer = new RoutingSevenZipWriter(managed, cli, AvailableCapability(ws));

        await writer.CreateAsync(Options(ws, Password));

        Assert.Equal(0, managed.Calls);
        Assert.Equal(1, cli.Calls);
    }

    [Fact]
    public async Task FormatoDistintoDe7Z_Rechazado()
    {
        using var ws = new TempWorkspace();
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), new FakeSevenZipCli(), new FakeCapability(false, null));
        var options = Options(ws, password: null, format: ArchiveFormat.Zip);

        await Assert.ThrowsAsync<ArgumentException>(() => writer.CreateAsync(options));
    }

    [Fact]
    public async Task ConPassword_Pero7zrNoDisponible_LanzaEncryptionNotSupported_SinTocarNada()
    {
        using var ws = new TempWorkspace();
        var cli = new FakeSevenZipCli();
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), cli, new FakeCapability(false, null));

        await Assert.ThrowsAsync<EncryptionNotSupportedException>(() => writer.CreateAsync(Options(ws, Password)));
        Assert.Equal(0, cli.Calls);
    }

    // ==================== C. Publicación ====================

    [Theory]
    [InlineData(SevenZipExecutionStatus.Success)]
    [InlineData(SevenZipExecutionStatus.Warning)]
    public async Task ExitoOWarning_MueveElTemporalAlDestino(SevenZipExecutionStatus status)
    {
        using var ws = new TempWorkspace();
        var options = Options(ws, Password);
        var cli = new FakeSevenZipCli { Responder = RespondCreating(status) };
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), cli, AvailableCapability(ws));

        await writer.CreateAsync(options);

        Assert.True(File.Exists(options.OutputPath));
        Assert.Equal("7z cifrado simulado", await File.ReadAllTextAsync(options.OutputPath));
        Assert.Empty(Directory.GetFiles(ws.OutputDir, $"{SevenZipTempArchivePath.Prefix}*"));
    }

    [Fact]
    public async Task Failed_NoTocaElDestino_YLanza()
    {
        using var ws = new TempWorkspace();
        var options = Options(ws, Password);
        var cli = new FakeSevenZipCli { Responder = RespondCreating(SevenZipExecutionStatus.Failed) };
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), cli, AvailableCapability(ws));

        await Assert.ThrowsAsync<ArchiveException>(() => writer.CreateAsync(options));
        Assert.False(File.Exists(options.OutputPath));
    }

    [Fact]
    public async Task Cancelled_LanzaOperationCanceled_YNoTocaElDestino()
    {
        using var ws = new TempWorkspace();
        var options = Options(ws, Password);
        var cli = new FakeSevenZipCli { Responder = RespondCreating(SevenZipExecutionStatus.Cancelled) };
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), cli, AvailableCapability(ws));

        await Assert.ThrowsAsync<OperationCanceledException>(() => writer.CreateAsync(options));
        Assert.False(File.Exists(options.OutputPath));
        Assert.Empty(Directory.GetFiles(ws.OutputDir, $"{SevenZipTempArchivePath.Prefix}*")); // sin temporal huérfano
    }

    [Fact]
    public async Task Timeout_ErrorControlado_NoPublica()
    {
        using var ws = new TempWorkspace();
        var options = Options(ws, Password);
        var cli = new FakeSevenZipCli { Responder = _ => SevenZipExecutionResult.ForTimeout(30, null) };
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), cli, AvailableCapability(ws));

        await Assert.ThrowsAsync<ArchiveException>(() => writer.CreateAsync(options));
        Assert.False(File.Exists(options.OutputPath));
    }

    [Fact]
    public async Task ArchivoDestinoYaExiste_SeRespetaElComportamientoActual_Sobrescribe()
    {
        using var ws = new TempWorkspace();
        var options = Options(ws, Password);
        Directory.CreateDirectory(ws.OutputDir);
        await File.WriteAllTextAsync(options.OutputPath, "7z ANTERIOR");

        var cli = new FakeSevenZipCli { Responder = RespondCreating(SevenZipExecutionStatus.Success) };
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), cli, AvailableCapability(ws));

        await writer.CreateAsync(options);

        // El comportamiento actual de creación en UltraArchive es sobrescribir (el diálogo "Guardar como"
        // ya avisa antes). El temporal se valida antes de reemplazar el destino.
        Assert.Equal("7z cifrado simulado", await File.ReadAllTextAsync(options.OutputPath));
    }

    [Fact]
    public async Task TemporalDevueltoNoEsUnUatmp_NoPublica()
    {
        using var ws = new TempWorkspace();
        var options = Options(ws, Password);
        var fakeArchive = Path.Combine(ws.OutputDir, "cosa-rara.7z");
        Directory.CreateDirectory(ws.OutputDir);
        await File.WriteAllTextAsync(fakeArchive, "x");

        var cli = new FakeSevenZipCli { Responder = _ => SevenZipExecutionResult.ForSuccess(100, fakeArchive, null) };
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), cli, AvailableCapability(ws));

        await Assert.ThrowsAsync<ArchiveException>(() => writer.CreateAsync(options));
        Assert.False(File.Exists(options.OutputPath));
    }

    [Fact]
    public async Task TemporalVacio_NoPublica_YSeLimpia()
    {
        using var ws = new TempWorkspace();
        var options = Options(ws, Password);
        var cli = new FakeSevenZipCli
        {
            Responder = request =>
            {
                var dir = Path.GetDirectoryName(request.Compression.OutputPath)!;
                Directory.CreateDirectory(dir);
                var temp = Path.Combine(dir, $"{SevenZipTempArchivePath.Prefix}{Guid.NewGuid():N}.7z");
                File.WriteAllBytes(temp, Array.Empty<byte>()); // 0 bytes
                return SevenZipExecutionResult.ForSuccess(100, temp, null);
            },
        };
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), cli, AvailableCapability(ws));

        await Assert.ThrowsAsync<ArchiveException>(() => writer.CreateAsync(options));
        Assert.False(File.Exists(options.OutputPath));
        Assert.Empty(Directory.GetFiles(ws.OutputDir, $"{SevenZipTempArchivePath.Prefix}*"));
    }

    [Fact]
    public async Task DeleteSourceAfterCompress_SoloTrasPublicarConExito()
    {
        using var ws = new TempWorkspace();
        var options = Options(ws, Password, deleteSource: true);
        var cli = new FakeSevenZipCli { Responder = RespondCreating(SevenZipExecutionStatus.Success) };
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), cli, AvailableCapability(ws));

        await writer.CreateAsync(options);

        Assert.True(File.Exists(options.OutputPath));
        Assert.False(Directory.Exists(ws.SourceDir)); // origen borrado
    }

    // ==================== D. Seguridad ====================

    [Fact]
    public async Task LaContrasenaNoApareceEnLasExcepciones()
    {
        using var ws = new TempWorkspace();
        var options = Options(ws, Password);
        var cli = new FakeSevenZipCli { Responder = _ => SevenZipExecutionResult.ForFailure(2, 10, "fallo generico de 7zr") };
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), cli, AvailableCapability(ws));

        var ex = await Assert.ThrowsAsync<ArchiveException>(() => writer.CreateAsync(options));

        Assert.DoesNotContain(Password, ex.Message);
        Assert.DoesNotContain(Password, ex.ToString());
    }

    // ==================== E. Integración ====================

    [Fact]
    public async Task ElCliRecibeLaPeticionDeCompresionCorrecta()
    {
        using var ws = new TempWorkspace();
        var options = Options(ws, Password);
        var cli = new FakeSevenZipCli { Responder = RespondCreating(SevenZipExecutionStatus.Success) };
        var capability = AvailableCapability(ws);
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), cli, capability);

        await writer.CreateAsync(options);

        var req = cli.LastRequest!;
        Assert.Equal(capability.VerifiedExecutablePath, req.ExecutablePath);
        Assert.Equal(capability.VerifiedExecutablePath, req.ExpectedExecutablePath);
        Assert.Equal(Path.GetFullPath(options.OutputPath), req.Compression.OutputPath);
        Assert.True(req.Compression.EncryptHeaders); // 7Z cifrado ⇒ -mhe=on siempre
        Assert.Equal(Password, req.Compression.Password);
    }

    [Fact]
    public async Task ElWriterPublicaSoloDespuesDeExito_ElArchivoTemporalEsUnUatmpEnLaCarpetaDelDestino()
    {
        using var ws = new TempWorkspace();
        var options = Options(ws, Password);
        string? capturedTemp = null;
        var cli = new FakeSevenZipCli
        {
            Responder = request =>
            {
                var dir = Path.GetDirectoryName(request.Compression.OutputPath)!;
                Directory.CreateDirectory(dir);
                capturedTemp = Path.Combine(dir, $"{SevenZipTempArchivePath.Prefix}{Guid.NewGuid():N}.7z");
                File.WriteAllText(capturedTemp, "ok");
                // antes de publicar, el destino final NO debe existir todavía
                Assert.False(File.Exists(request.Compression.OutputPath));
                return SevenZipExecutionResult.ForSuccess(100, capturedTemp, null);
            },
        };
        var writer = new RoutingSevenZipWriter(new FakeManagedWriter(), cli, AvailableCapability(ws));

        await writer.CreateAsync(options);

        Assert.True(File.Exists(options.OutputPath));
        Assert.False(File.Exists(capturedTemp!)); // el temporal ya no está (se movió)
    }
}
