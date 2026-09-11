using System.Diagnostics;
using UltraArchive.Core.Models;

namespace UltraArchive.Archives.Common;

/// <summary>
/// Construye instantáneas de <see cref="OperationProgress"/> consistentes a lo largo de una operación
/// (extraer/comprimir/probar) que procesa varias entradas: acumula los bytes de las entradas ya
/// terminadas y añade el avance dentro de la entrada actual.
/// </summary>
internal sealed class OperationProgressTracker
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _bytesCompletedBeforeCurrentEntry;

    public OperationProgressTracker(long totalBytes) => TotalBytes = totalBytes;

    public long TotalBytes { get; }

    public OperationProgress ForEntryProgress(string entryName, long bytesCopiedInEntrySoFar) => new()
    {
        CurrentEntryName = entryName,
        BytesProcessed = _bytesCompletedBeforeCurrentEntry + bytesCopiedInEntrySoFar,
        TotalBytes = TotalBytes,
        Elapsed = _stopwatch.Elapsed,
    };

    public void CompleteEntry(long entrySize) => _bytesCompletedBeforeCurrentEntry += Math.Max(0, entrySize);
}
