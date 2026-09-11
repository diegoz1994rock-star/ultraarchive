namespace UltraArchive.Archives.Common;

/// <summary>
/// Stream de <b>solo lectura</b> y <b>seekable</b> que presenta varios ficheros de volumen
/// (<c>Archivo.7z.001</c>, <c>.002</c>…) como un único stream contiguo.
///
/// Los volúmenes que genera 7-Zip con <c>-v</c> son una <b>partición por bytes</b> del <c>.7z</c>
/// original: concatenarlos byte a byte reproduce el archivo íntegro. Este stream hace esa
/// concatenación <b>sin copiar nada a disco ni a memoria</b> (lee de cada parte bajo demanda), para
/// poder pasarlo directamente al lector de 7Z existente y descomprimir archivos de decenas o cientos
/// de GB.
/// </summary>
internal sealed class MultiVolumeReadStream : Stream
{
    private readonly FileStream[] _parts;
    private readonly long[] _partStart; // offset absoluto donde empieza cada parte
    private readonly long _length;
    private long _position;
    private bool _disposed;

    /// <param name="volumePaths">Rutas de los volúmenes <b>en orden</b> (001, 002, …). No vacío.</param>
    public MultiVolumeReadStream(IReadOnlyList<string> volumePaths)
    {
        ArgumentNullException.ThrowIfNull(volumePaths);
        if (volumePaths.Count == 0)
        {
            throw new ArgumentException("Se necesita al menos un volumen.", nameof(volumePaths));
        }

        _parts = new FileStream[volumePaths.Count];
        _partStart = new long[volumePaths.Count];

        long cumulative = 0;
        try
        {
            for (var i = 0; i < volumePaths.Count; i++)
            {
                _parts[i] = new FileStream(
                    volumePaths[i], FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 1 << 20, FileOptions.RandomAccess);
                _partStart[i] = cumulative;
                cumulative += _parts[i].Length;
            }
        }
        catch
        {
            foreach (var part in _parts)
            {
                part?.Dispose();
            }

            throw;
        }

        _length = cumulative;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateBufferArgs(buffer, offset, count);

        if (_position >= _length || count == 0)
        {
            return 0;
        }

        var totalRead = 0;
        while (count > 0 && _position < _length)
        {
            var partIndex = FindPart(_position);
            var part = _parts[partIndex];
            var offsetInPart = _position - _partStart[partIndex];

            if (part.Position != offsetInPart)
            {
                part.Position = offsetInPart;
            }

            var remainingInPart = part.Length - offsetInPart;
            var toRead = (int)Math.Min(count, remainingInPart);

            var read = part.Read(buffer, offset, toRead);
            if (read == 0)
            {
                break; // parte más corta de lo esperado: no seguir
            }

            totalRead += read;
            offset += read;
            count -= read;
            _position += read;
        }

        return totalRead;
    }

    public override int Read(Span<byte> buffer)
    {
        // Ruta simple y correcta: apoyarse en la sobrecarga de array.
        var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            var read = Read(rented, 0, buffer.Length);
            rented.AsSpan(0, read).CopyTo(buffer);
            return read;
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        if (target < 0)
        {
            throw new IOException("Intento de posicionar antes del inicio del stream.");
        }

        _position = target; // se permite posicionar más allá del final (Read devolverá 0)
        return _position;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int FindPart(long absolutePosition)
    {
        // Búsqueda binaria del último partStart <= absolutePosition.
        var lo = 0;
        var hi = _parts.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (_partStart[mid] <= absolutePosition)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return lo;
    }

    private static void ValidateBufferArgs(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            foreach (var part in _parts)
            {
                part.Dispose();
            }
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}
