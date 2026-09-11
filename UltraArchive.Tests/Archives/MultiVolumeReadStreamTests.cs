using System.Security.Cryptography;
using UltraArchive.Archives.Common;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Archives;

/// <summary><c>MultiVolumeReadStream</c> (internal, visible por <c>InternalsVisibleTo</c>).</summary>
public class MultiVolumeReadStreamTests
{
    private static Stream Create(IReadOnlyList<string> paths) => new MultiVolumeReadStream(paths);

    [Fact]
    public void ConcatenaLasPartesComoUnStreamContiguo()
    {
        using var ws = new TempWorkspace();
        var rnd = new Random(7);
        var whole = new byte[1_000_003];
        rnd.NextBytes(whole);

        // Trocear en 4 partes desiguales.
        var sizes = new[] { 300_000, 400_000, 1, 300_002 };
        var paths = new List<string>();
        var offset = 0;
        for (var i = 0; i < sizes.Length; i++)
        {
            var p = ws.ArchivePath($"w.bin.{i + 1:D3}");
            File.WriteAllBytes(p, whole.AsSpan(offset, sizes[i]).ToArray());
            offset += sizes[i];
            paths.Add(p);
        }

        using var stream = Create(paths);

        Assert.True(stream.CanRead && stream.CanSeek && !stream.CanWrite);
        Assert.Equal(whole.Length, stream.Length);

        // Lectura secuencial completa.
        using var ms = new MemoryStream();
        stream.CopyTo(ms, 65536);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(whole)),
                     Convert.ToHexString(SHA256.HashData(ms.ToArray())));

        // Seek a un punto que cruza el límite de dos partes y leer.
        stream.Position = 699_998;
        var buf = new byte[10];
        var read = stream.Read(buf, 0, buf.Length);
        Assert.Equal(10, read);
        Assert.Equal(whole.AsSpan(699_998, 10).ToArray(), buf);

        // Seek desde el final.
        stream.Seek(-5, SeekOrigin.End);
        var tail = new byte[5];
        ReadExactly(stream, tail);
        Assert.Equal(whole.AsSpan(whole.Length - 5, 5).ToArray(), tail);

        // Leer más allá del final → 0.
        stream.Position = stream.Length + 100;
        Assert.Equal(0, stream.Read(buf, 0, buf.Length));
    }

    private static void ReadExactly(Stream s, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var n = s.Read(buffer, total, buffer.Length - total);
            if (n == 0) break;
            total += n;
        }
    }
}
