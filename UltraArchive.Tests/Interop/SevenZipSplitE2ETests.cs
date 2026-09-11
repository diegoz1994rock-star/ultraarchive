using System.Security.Cryptography;
using UltraArchive.Archives.SevenZip;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using UltraArchive.Interop.SevenZip;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Interop;

/// <summary>
/// E2E REAL de compresión dividida en volúmenes con 7zr.exe: crear <c>X.7z.001/.002/…</c>, abrir a
/// partir de la primera parte, extraer y comparar byte a byte con el original; detección de parte
/// faltante. Se ejecuta solo si hay un 7zr.exe verificado (igual que <see cref="SevenZipEncryptedE2ETests"/>).
/// </summary>
public class SevenZipSplitE2ETests
{
    private sealed class RealCapability : ISevenZipCapability
    {
        private readonly SevenZipLocateResult _result = new SevenZipLocator().Locate();
        public bool CanCreateEncryptedSevenZip => _result.Status == SevenZipToolStatus.Available;
        public string? VerifiedExecutablePath => _result.Tool?.ExecutablePath;
        public string StatusExplanation => _result.Message;
    }

    private static bool SevenZrAvailable => new SevenZipLocator().Locate().Status == SevenZipToolStatus.Available;

    private static RoutingSevenZipWriter NewWriter() =>
        new(new SevenZipArchiveWriter(), new SevenZipCli(new SevenZipProcessRunner()), new RealCapability());

    /// <summary>Estructura variada: carpetas anidadas, archivos grandes y pequeños, nombres con espacios/acentos.</summary>
    private static Dictionary<string, byte[]> BuildTree(TempWorkspace ws)
    {
        var expected = new Dictionary<string, byte[]>();

        void Add(string rel, byte[] data)
        {
            var full = Path.Combine(ws.SourceDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, data);
            expected[rel.Replace('\\', '/')] = data;
        }

        var rnd = new Random(1234);
        byte[] Blob(int n) { var b = new byte[n]; rnd.NextBytes(b); return b; }

        Add("raiz.txt", "contenido raíz"u8.ToArray());
        Add(@"Carpeta con espacios\documento ñoño.txt", "acentos: áéíóú ñ ü"u8.ToArray());
        Add(@"Carpeta con espacios\sub\datos.bin", Blob(2 * 1024 * 1024));      // 2 MiB
        Add(@"imágenes\icono-16.dat", Blob(4096));
        Add(@"imágenes\grande.iso", Blob(5 * 1024 * 1024));                     // 5 MiB
        Add(@"código\núcleo\Programa.cs", "// código fuente\nclass P {}"u8.ToArray());
        Add("vacío.dat", Array.Empty<byte>());
        return expected;
    }

    [Fact]
    public async Task Crear_Y_Extraer_7Z_Dividido_SinContrasena()
    {
        if (!SevenZrAvailable) { Assert.True(true, "7zr no disponible: E2E de división omitido."); return; }

        using var ws = new TempWorkspace();
        var expected = BuildTree(ws);
        var output = Path.Combine(ws.OutputDir, "Mi_Carpeta.7z");

        // Volumen de 1 MiB → varias partes seguro.
        var options = new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = output,
            Format = ArchiveFormat.SevenZip,
            CompressionLevel = CompressionLevel.Fast,
            SplitVolumeSizeBytes = 1L * 1024 * 1024,
        };

        await NewWriter().CreateAsync(options);

        // El .7z único NO se crea; sí las partes .001, .002, …
        Assert.False(File.Exists(output), "no debe existir el .7z único");
        var parts = Directory.GetFiles(ws.OutputDir, "Mi_Carpeta.7z.*").OrderBy(p => p).ToList();
        Assert.True(parts.Count >= 2, $"deberían crearse varias partes, hay {parts.Count}");
        Assert.EndsWith(".001", parts[0]);
        Assert.All(parts, p => Assert.True(new FileInfo(p).Length > 0));

        // Abrir por la PRIMERA parte y extraer.
        using (var reader = new SevenZipArchiveReader())
        {
            await reader.OpenAsync(parts[0]);
            var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = Path.Combine(ws.RootPath, "out") });
            Assert.True(result.ExtractedCount >= expected.Count);

            Assert.True(await reader.TestIntegrityAsync(), "la verificación de integridad del conjunto debe pasar");
        }

        // Comparar byte a byte: nada perdido, nada alterado.
        var root = Path.Combine(ws.RootPath, "out", Path.GetFileName(ws.SourceDir));
        foreach (var (rel, data) in expected)
        {
            var onDisk = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(onDisk), $"falta en disco: {rel}");
            Assert.Equal(Convert.ToHexString(SHA256.HashData(data)),
                         Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(onDisk))));
        }
    }

    [Fact]
    public async Task Crear_Y_Extraer_7Z_Dividido_ConContrasena()
    {
        if (!SevenZrAvailable) { Assert.True(true, "7zr no disponible: omitido."); return; }

        using var ws = new TempWorkspace();
        var expected = BuildTree(ws);
        var output = Path.Combine(ws.OutputDir, "protegido.7z");
        var password = "Split-" + Guid.NewGuid().ToString("N");

        await NewWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = output,
            Format = ArchiveFormat.SevenZip,
            CompressionLevel = CompressionLevel.Fast,
            Password = password,
            Encryption = EncryptionMethod.Aes256,
            EncryptFileNames = true,
            SplitVolumeSizeBytes = 1L * 1024 * 1024,
        });

        var parts = Directory.GetFiles(ws.OutputDir, "protegido.7z.*").OrderBy(p => p).ToList();
        Assert.True(parts.Count >= 2);

        // Sin contraseña → error de contraseña (cabecera cifrada).
        using (var reader = new SevenZipArchiveReader())
            await Assert.ThrowsAnyAsync<Exception>(() => reader.OpenAsync(parts[0]));

        // Con contraseña correcta → extrae y verifica.
        using (var reader = new SevenZipArchiveReader())
        {
            await reader.OpenAsync(parts[0], password);
            var result = await reader.ExtractAsync(new ExtractOptions
            {
                DestinationPath = Path.Combine(ws.RootPath, "outp"),
                Password = password,
            });
            Assert.True(result.ExtractedCount >= expected.Count);
        }
    }

    [Fact]
    public async Task FaltaUnaParte_LanzaIncompleteVolumeSet_ConDetalle()
    {
        if (!SevenZrAvailable) { Assert.True(true, "7zr no disponible: omitido."); return; }

        using var ws = new TempWorkspace();
        BuildTree(ws);
        var output = Path.Combine(ws.OutputDir, "conjunto.7z");

        await NewWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = output,
            Format = ArchiveFormat.SevenZip,
            CompressionLevel = CompressionLevel.Fast,
            SplitVolumeSizeBytes = 1L * 1024 * 1024,
        });

        var parts = Directory.GetFiles(ws.OutputDir, "conjunto.7z.*").OrderBy(p => p).ToList();
        Assert.True(parts.Count >= 3, "necesitamos al menos 3 partes para borrar una intermedia");

        // Borrar la segunda parte (.002).
        File.Delete(parts[1]);

        using var reader = new SevenZipArchiveReader();
        var ex = await Assert.ThrowsAsync<IncompleteVolumeSetException>(() => reader.OpenAsync(parts[0]));

        Assert.Contains(2, ex.MissingParts);
        Assert.DoesNotContain(2, ex.FoundParts);
        Assert.Contains("Falta", ex.Message);
        Assert.Contains("002", ex.Message);
    }

    [Fact]
    public async Task DivisionPorNumeroDePartes_ProduceEsaCantidad_YReconstruyeIgual()
    {
        if (!SevenZrAvailable) { Assert.True(true, "7zr no disponible: omitido."); return; }

        using var ws = new TempWorkspace();
        var expected = BuildTree(ws);
        var output = Path.Combine(ws.OutputDir, "por-partes.7z");

        // Almacenar (sin comprimir) para que el tamaño de salida ≈ tamaño de origen y el reparto sea predecible.
        var totalSource = SourcePathSize.TotalBytes(new[] { ws.SourceDir });
        var perVolume = SplitCalculator.BytesPerVolumeForPartCount(totalSource, 3);

        await NewWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = output,
            Format = ArchiveFormat.SevenZip,
            CompressionLevel = CompressionLevel.Store,
            SplitVolumeSizeBytes = perVolume,
        });

        var parts = Directory.GetFiles(ws.OutputDir, "por-partes.7z.*").OrderBy(p => p).ToList();
        Assert.InRange(parts.Count, 3, 4); // ~3 partes (el contenedor 7z añade unos KB de cabecera)

        using var reader = new SevenZipArchiveReader();
        await reader.OpenAsync(parts[0]);
        var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = Path.Combine(ws.RootPath, "np") });
        Assert.True(result.ExtractedCount >= expected.Count);

        var root = Path.Combine(ws.RootPath, "np", Path.GetFileName(ws.SourceDir));
        foreach (var (rel, data) in expected)
        {
            var onDisk = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(Convert.ToHexString(SHA256.HashData(data)),
                         Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(onDisk))));
        }
    }

    [Fact]
    public async Task CancelacionDuranteCompresionDividida_NoDejaPartesEnElDestino()
    {
        if (!SevenZrAvailable) { Assert.True(true, "7zr no disponible: omitido."); return; }

        using var ws = new TempWorkspace();
        // Suficiente para que la compresión tarde algo y dé tiempo a cancelar.
        var rnd = new Random(99);
        for (var i = 0; i < 6; i++)
        {
            var b = new byte[8 * 1024 * 1024];
            rnd.NextBytes(b);
            File.WriteAllBytes(Path.Combine(ws.SourceDir, $"blob{i}.bin"), b);
        }

        var output = Path.Combine(ws.OutputDir, "cancelado.7z");
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NewWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = output,
            Format = ArchiveFormat.SevenZip,
            CompressionLevel = CompressionLevel.Ultra,
            SplitVolumeSizeBytes = 1L * 1024 * 1024,
        }, progress: null, cts.Token));

        var leftovers = Directory.GetFiles(ws.OutputDir);
        var msg = "Ficheros en output: " + string.Join(" | ", leftovers.Select(Path.GetFileName));

        Assert.False(File.Exists(output), msg);
        // Lo crítico: NO debe quedar un resultado publicado (cancelado.7z.NNN) que parezca válido.
        Assert.True(Directory.GetFiles(ws.OutputDir, "cancelado.7z.*").Length == 0, msg);
        // Los temporales .uatmp-* deben limpiarse (best-effort con reintentos).
        Assert.True(Directory.GetFiles(ws.OutputDir, ".uatmp-*").Length == 0, msg);
    }

    [Fact]
    public async Task VerificacionDespuesDeCrear_DetectaUnaParteCorrupta()
    {
        if (!SevenZrAvailable) { Assert.True(true, "7zr no disponible: omitido."); return; }

        using var ws = new TempWorkspace();
        BuildTree(ws);
        var output = Path.Combine(ws.OutputDir, "corrupto.7z");

        await NewWriter().CreateAsync(new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = output,
            Format = ArchiveFormat.SevenZip,
            CompressionLevel = CompressionLevel.Fast,
            SplitVolumeSizeBytes = 1L * 1024 * 1024,
        });

        var parts = Directory.GetFiles(ws.OutputDir, "corrupto.7z.*").OrderBy(p => p).ToList();

        // Corromper bytes en el interior de la segunda parte.
        var bytes = await File.ReadAllBytesAsync(parts[1]);
        for (var i = 100; i < Math.Min(bytes.Length, 4000); i++) bytes[i] ^= 0xFF;
        await File.WriteAllBytesAsync(parts[1], bytes);

        using var reader = new SevenZipArchiveReader();
        // Puede fallar al abrir (cabecera dañada) o al verificar; ambos son "no válido".
        var integrityFailedOrThrew = false;
        try
        {
            await reader.OpenAsync(parts[0]);
            integrityFailedOrThrew = !await reader.TestIntegrityAsync();
        }
        catch (ArchiveException)
        {
            integrityFailedOrThrew = true;
        }

        Assert.True(integrityFailedOrThrew, "una parte corrupta debe detectarse (excepción o verificación fallida)");
    }
}
