using System.IO;
using System.IO.Pipes;
using System.Text;

namespace UltraArchive.App.Services;

/// <summary>
/// Coordinación de instancia única: la primera instancia se queda escuchando en un named pipe local;
/// una segunda instancia (típicamente lanzada desde el Explorador con "Abrir con UltraArchive")
/// reenvía sus argumentos a la primera y se cierra sin abrir otra ventana.
///
/// Diseño defensivo: cualquier fallo de mutex o de pipe hace que la instancia arranque con normalidad
/// (mejor dos ventanas que ninguna). No usa nada persistente: el mutex y el pipe viven solo mientras
/// el proceso está en marcha y son locales a la sesión del usuario (<c>Local\</c>).
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\UltraArchive.SingleInstance.Mutex";
    private const string PipeName = "UltraArchive.SingleInstance.Pipe";
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly Mutex? _mutex;
    private readonly CancellationTokenSource _listenCts = new();
    private bool _disposed;

    /// <summary>true si este proceso es la instancia primaria (debe abrir la ventana y escuchar).</summary>
    public bool IsPrimaryInstance { get; }

    /// <summary>Se dispara (en un hilo del pool) cuando otra instancia reenvía sus argumentos. Puede ser una lista vacía (solo "actívate").</summary>
    public event Action<string[]>? ArgumentsReceived;

    public SingleInstance()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            IsPrimaryInstance = createdNew;
        }
        catch
        {
            // Sin mutex disponible: comportarse como instancia primaria normal.
            _mutex = null;
            IsPrimaryInstance = true;
        }
    }

    /// <summary>Arranca el bucle de escucha del pipe (solo la instancia primaria).</summary>
    public void StartListening()
    {
        if (!IsPrimaryInstance)
        {
            return;
        }

        _ = Task.Run(() => ListenLoopAsync(_listenCts.Token));
    }

    /// <summary>Reenvía <paramref name="args"/> a la instancia primaria. Devuelve false si no hay ninguna a la escucha.</summary>
    public static bool TrySendToPrimary(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client, Utf8NoBom) { AutoFlush = true };
            writer.Write(string.Join('\n', args));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(server, Utf8NoBom);
                var payload = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var args = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                ArgumentsReceived?.Invoke(args);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Un cliente mal formado no debe tumbar el bucle: se sigue escuchando.
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
        try { _listenCts.Cancel(); } catch { /* ignore */ }
        _listenCts.Dispose();

        try
        {
            if (_mutex is not null && IsPrimaryInstance)
            {
                _mutex.ReleaseMutex();
            }
        }
        catch
        {
            // ignore
        }

        _mutex?.Dispose();
    }
}
