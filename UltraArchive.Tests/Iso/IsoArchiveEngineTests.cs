using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using UltraArchive.Iso;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Iso;

/// <summary>Fase 5: lectura, exploración y extracción de imágenes ISO9660 (incl. Joliet).</summary>
public class IsoArchiveEngineTests
{
    private static string WriteIso(TempWorkspace ws, byte[] bytes, string name = "imagen.iso")
    {
        var path = ws.ArchivePath(name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    // ---- Apertura / detección ----

    [Fact]
    public async Task OpenAsync_IsoValida_LeeLaInfoDeVolumen()
    {
        using var ws = new TempWorkspace();
        var path = WriteIso(ws, IsoFixtures.SingleFile());

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);

        Assert.False(reader.IsPasswordProtected);
        Assert.NotNull(reader.VolumeInfo);
        Assert.True(reader.VolumeInfo!.IsIso9660);
        Assert.Equal(IsoFixtures.VolumeLabel, reader.VolumeInfo.VolumeLabel);
    }

    [Fact]
    public async Task OpenAsync_IsoCorrupta_LanzaArchiveCorruptedException()
    {
        using var ws = new TempWorkspace();
        var bytes = IsoFixtures.SingleFile();
        for (var i = 16 * 2048; i < 20 * 2048 && i < bytes.Length; i++)
        {
            bytes[i] = 0xEE;
        }

        var path = WriteIso(ws, bytes);
        using var reader = new IsoArchiveReader();
        await Assert.ThrowsAsync<ArchiveCorruptedException>(() => reader.OpenAsync(path));
    }

    [Fact]
    public async Task OpenAsync_IsoTruncada_LanzaArchiveCorruptedException()
    {
        using var ws = new TempWorkspace();
        var full = IsoFixtures.WithFolders();
        var path = WriteIso(ws, full[..(full.Length * 2 / 3)]);

        using var reader = new IsoArchiveReader();
        await Assert.ThrowsAsync<ArchiveCorruptedException>(() => reader.OpenAsync(path));
    }

    [Fact]
    public async Task OpenAsync_ArchivoQueNoEsIso_LanzaArchiveCorruptedException()
    {
        using var ws = new TempWorkspace();
        var path = ws.ArchivePath("no-es.iso");
        await File.WriteAllBytesAsync(path, new byte[90_000]);

        using var reader = new IsoArchiveReader();
        await Assert.ThrowsAsync<ArchiveCorruptedException>(() => reader.OpenAsync(path));
    }

    [Fact]
    public async Task OpenAsync_ImagenRaw2352_LanzaArchiveCorruptedException()
    {
        // Un .bin de CD (2352 bytes/sector) renombrado a .iso: la firma "CD001" ya no cae en
        // el offset esperado (sector 16 * 2048) -> se rechaza, no se intenta interpretar.
        using var ws = new TempWorkspace();
        var cooked = IsoFixtures.SingleFile();
        var raw = new byte[(cooked.Length / 2048 + 1) * 2352];
        for (var sector = 0; sector * 2048 < cooked.Length; sector++)
        {
            var take = Math.Min(2048, cooked.Length - sector * 2048);
            Array.Copy(cooked, sector * 2048, raw, sector * 2352 + 16, take); // +16: hueco de sync/header
        }

        var path = WriteIso(ws, raw, "disco.iso");
        using var reader = new IsoArchiveReader();
        await Assert.ThrowsAsync<ArchiveCorruptedException>(() => reader.OpenAsync(path));
    }

    [Fact]
    public async Task DetectAsync_SobreUnaIsoReal_DevuelveIso9660()
    {
        using var ws = new TempWorkspace();
        var path = WriteIso(ws, IsoFixtures.SingleFile());

        var format = await new ArchiveFormatDetector().DetectAsync(path);

        Assert.Equal(ArchiveFormat.Iso9660, format);
    }

    // ---- Exploración ----

    [Fact]
    public async Task GetEntriesAsync_IsoConCarpetas_ListaCarpetasYFicherosConTamano()
    {
        using var ws = new TempWorkspace();
        var path = WriteIso(ws, IsoFixtures.WithFolders());

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);
        var entries = await reader.GetEntriesAsync();

        Assert.Contains(entries, e => e is { FullPath: "RAIZ.TXT", IsDirectory: false, UncompressedSize: 10 });
        Assert.Contains(entries, e => e is { FullPath: "DOCS", IsDirectory: true });
        Assert.Contains(entries, e => e is { FullPath: "DOCS/SUB", IsDirectory: true });
        Assert.Contains(entries, e => e is { FullPath: "DOCS/SUB/DATOS.BIN", IsDirectory: false, UncompressedSize: 4096 });
    }

    [Fact]
    public async Task GetEntriesAsync_IsoVacia_DevuelveListaVacia()
    {
        using var ws = new TempWorkspace();
        var path = WriteIso(ws, IsoFixtures.Empty());

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);
        var entries = await reader.GetEntriesAsync();

        Assert.Empty(entries.Where(e => !e.IsDirectory));
    }

    [Fact]
    public async Task GetEntriesAsync_IsoJoliet_DevuelveNombresLargosConEspaciosYAcentos()
    {
        using var ws = new TempWorkspace();
        var path = WriteIso(ws, IsoFixtures.Joliet());

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);
        Assert.True(reader.VolumeInfo!.HasJoliet);
        var entries = await reader.GetEntriesAsync();

        Assert.Contains(entries, e => e.FullPath == "Léeme primero.txt");
        Assert.Contains(entries, e => e.FullPath == "Carpeta con espacios/Canción ñoña.txt");
    }

    // ---- Extracción ----

    [Fact]
    public async Task ExtractAsync_TodoElContenido_ReproduceLaEstructuraYLosBytes()
    {
        using var ws = new TempWorkspace();
        var path = WriteIso(ws, IsoFixtures.WithFolders());

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);
        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = ws.OutputDir });

        Assert.Equal(3, result.ExtractedCount);
        Assert.Empty(result.BlockedEntries);
        Assert.Equal("en la raiz", await File.ReadAllTextAsync(Path.Combine(ws.OutputDir, "RAIZ.TXT")));
        Assert.Equal("una nota", await File.ReadAllTextAsync(Path.Combine(ws.OutputDir, "DOCS", "NOTA.TXT")));
        var expectedBin = IsoFixtures.RepeatingBytes(4096);
        Assert.Equal(expectedBin, await File.ReadAllBytesAsync(Path.Combine(ws.OutputDir, "DOCS", "SUB", "DATOS.BIN")));
    }

    [Fact]
    public async Task ExtractAsync_Selectiva_SoloExtraeLoPedido()
    {
        using var ws = new TempWorkspace();
        var path = WriteIso(ws, IsoFixtures.WithFolders());

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);
        var result = await reader.ExtractAsync(new ExtractOptions
        {
            DestinationPath = ws.OutputDir,
            EntriesToExtract = new[] { "DOCS/NOTA.TXT" },
        });

        Assert.Equal(1, result.ExtractedCount);
        Assert.True(File.Exists(Path.Combine(ws.OutputDir, "DOCS", "NOTA.TXT")));
        Assert.False(File.Exists(Path.Combine(ws.OutputDir, "RAIZ.TXT")));
    }

    [Fact]
    public async Task ExtractAsync_ReportaProgreso()
    {
        using var ws = new TempWorkspace();
        var path = WriteIso(ws, IsoFixtures.LargeFile("GRANDE.BIN", 2 * 1024 * 1024));

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);

        var maxPercent = 0.0;
        var reports = 0;
        var progress = new SyncProgress<OperationProgress>(p =>
        {
            reports++;
            maxPercent = Math.Max(maxPercent, p.PercentComplete);
        });
        await reader.ExtractAsync(new ExtractOptions { DestinationPath = ws.OutputDir }, progress);

        Assert.True(reports > 1);
        Assert.True(maxPercent > 0);
    }

    [Fact]
    public async Task ExtractAsync_CancelacionAMitad_LanzaYNoDejaFicheroParcial()
    {
        using var ws = new TempWorkspace();
        var path = WriteIso(ws, IsoFixtures.LargeFile("GRANDE.BIN", 8 * 1024 * 1024));

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);

        using var cts = new CancellationTokenSource();
        // Progreso síncrono: la cancelación ocurre dentro del propio bucle de copia (determinista).
        var progress = new SyncProgress<OperationProgress>(p =>
        {
            if (p.BytesProcessed > 64 * 1024)
            {
                cts.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.ExtractAsync(new ExtractOptions { DestinationPath = ws.OutputDir }, progress, cts.Token));

        var partial = Path.Combine(ws.OutputDir, "GRANDE.BIN");
        Assert.False(File.Exists(partial), "no debe quedar un fichero parcial tras cancelar");
    }

    [Fact]
    public async Task TestIntegrityAsync_IsoIntacta_DevuelveTrue()
    {
        using var ws = new TempWorkspace();
        var path = WriteIso(ws, IsoFixtures.WithFolders());

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);

        Assert.True(await reader.TestIntegrityAsync());
    }

    // ---- Seguridad ----

    [Fact]
    public async Task ExtractAsync_EntradaConPathTraversal_SeBloqueaYNoEscribeFuera()
    {
        using var ws = new TempWorkspace();
        // Fichero "EVILFILE" (8 chars) → se reescribe a "..\..\XX" (8 chars) en la imagen.
        var iso = IsoFixtures.SingleFileNamed("EVILFILE", "payload malicioso");
        var hostile = IsoFixtures.PatchAsciiName(iso, "EVILFILE", @"..\..\XX");
        var path = WriteIso(ws, hostile);

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);
        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = ws.OutputDir });

        Assert.Equal(0, result.ExtractedCount);
        Assert.Single(result.BlockedEntries);
        Assert.False(File.Exists(Path.Combine(ws.RootPath, "XX")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(ws.RootPath)!, "XX")));
    }

    [Fact]
    public async Task ExtractAsync_IsoJoliet_ExtraeContenidoYNombresUnicodeByteAByte()
    {
        using var ws = new TempWorkspace();
        var path = WriteIso(ws, IsoFixtures.Joliet());

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);
        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = ws.OutputDir });

        Assert.Equal(2, result.ExtractedCount);
        Assert.Equal("con Joliet", await File.ReadAllTextAsync(Path.Combine(ws.OutputDir, "Léeme primero.txt")));
        Assert.Equal("acentos y ñ", await File.ReadAllTextAsync(
            Path.Combine(ws.OutputDir, "Carpeta con espacios", "Canción ñoña.txt")));
    }

    [Fact]
    public async Task OpenAsync_ImagenUdfPura_LanzaUnsupportedFormatException()
    {
        using var ws = new TempWorkspace();
        // Convierte el PVD (sector 16) en un descriptor de reconocimiento UDF: "CD001" -> "NSR03".
        var udf = IsoFixtures.PatchAsciiName(IsoFixtures.SingleFile(), "CD001", "NSR03");
        var path = WriteIso(ws, udf);

        using var reader = new IsoArchiveReader();
        var ex = await Assert.ThrowsAsync<UnsupportedFormatException>(() => reader.OpenAsync(path));
        Assert.Contains("UDF", ex.Message);
    }

    [Fact]
    public async Task ExtractAsync_ColisionSkip_NoSobrescribeElArchivoExistente()
    {
        using var ws = new TempWorkspace();
        var path = WriteIso(ws, IsoFixtures.SingleFile("LEEME.TXT", "contenido de la iso"));

        var existing = Path.Combine(ws.OutputDir, "LEEME.TXT");
        await File.WriteAllTextAsync(existing, "contenido previo");

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);
        await reader.ExtractAsync(new ExtractOptions
        {
            DestinationPath = ws.OutputDir,
            CollisionPolicy = CollisionPolicy.Skip,
        });

        Assert.Equal("contenido previo", await File.ReadAllTextAsync(existing));
    }

    [Fact]
    public async Task ExtractAsync_EntradaConNombreDeDispositivoReservado_SeBloquea()
    {
        using var ws = new TempWorkspace();
        // "DEVICEXX" (8) → "CON     " no vale (espacios). Usamos "AUX.TXT " no... 8 chars exactos: "AUX\\A.TX"
        var iso = IsoFixtures.SingleFileNamed("DEVICEXX", "x");
        var hostile = IsoFixtures.PatchAsciiName(iso, "DEVICEXX", @"AUX\A.TX");
        var path = WriteIso(ws, hostile);

        using var reader = new IsoArchiveReader();
        await reader.OpenAsync(path);
        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = ws.OutputDir });

        Assert.Equal(0, result.ExtractedCount);
        Assert.Single(result.BlockedEntries);
    }
}
