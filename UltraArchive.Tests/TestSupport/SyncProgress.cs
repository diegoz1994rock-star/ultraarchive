namespace UltraArchive.Tests.TestSupport;

/// <summary>
/// <see cref="IProgress{T}"/> que invoca el handler <b>de forma síncrona</b> en el hilo que llama a
/// <see cref="Report"/>, a diferencia de <see cref="Progress{T}"/> que lo pospone al contexto de
/// sincronización. Útil para tests deterministas de cancelación (cancelar dentro del propio callback).
/// </summary>
internal sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
