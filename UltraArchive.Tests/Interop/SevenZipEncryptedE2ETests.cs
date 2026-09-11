using UltraArchive.Archives.SevenZip;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Interop.SevenZip;
using UltraArchive.Tests.TestSupport;

namespace UltraArchive.Tests.Interop;

/// <summary>
/// Fase 6A, Paso 4 (F): prueba de integración REAL de creación + lectura de 7Z cifrado con 7zr.exe.
///
/// Se ejecuta SOLO si <see cref="SevenZipLocator"/> devuelve <see cref="SevenZipToolStatus.Available"/>
/// (binario oficial incorporado y <c>SevenZipToolReference.ExpectedSha256</c> configurado, previsto
/// para la Fase 8). Mientras tanto queda documentada y bloqueada: no se falsifica la integración.
///
/// Nunca se descarga un ejecutable ni se usa un 7-Zip del PATH o del usuario sin verificación.
/// </summary>
public class SevenZipEncryptedE2ETests
{
    private sealed class RealCapability : ISevenZipCapability
    {
        private readonly SevenZipLocateResult _result = new SevenZipLocator().Locate();
        public bool CanCreateEncryptedSevenZip => _result.Status == SevenZipToolStatus.Available;
        public string? VerifiedExecutablePath => _result.Tool?.ExecutablePath;
        public string StatusExplanation => _result.Message;
    }

    [Fact]
    public async Task Crear_Y_Extraer_SevenZipCifrado_Real()
    {
        var located = new SevenZipLocator().Locate();

        if (located.Status != SevenZipToolStatus.Available)
        {
            // BLOQUEADA hasta la Fase 8: no hay 7zr.exe oficial verificado.
            Assert.Contains(located.Status, new[]
            {
                SevenZipToolStatus.NotConfigured, // ExpectedSha256 pendiente (caso actual)
                SevenZipToolStatus.NotFound,      // aún no se ha bundleado el binario
            });
            Assert.True(string.IsNullOrEmpty(SevenZipToolReference.ExpectedSha256),
                "El hash oficial de 7zr sigue pendiente de configurar (Fase 8).");
            return;
        }

        // -------- E2E real (solo cuando el binario esté disponible y verificado) --------
        using var ws = new TempWorkspace();
        ws.CreateSampleFile("documento.txt", "contenido confidencial e2e");
        ws.CreateSampleFile("sub/datos.bin", "mas datos");
        var output = Path.Combine(ws.OutputDir, "cifrado.7z");
        var password = "E2E-" + Guid.NewGuid().ToString("N"); // nunca fija; solo dentro del test

        var writer = new RoutingSevenZipWriter(
            new SevenZipArchiveWriter(),
            new SevenZipCli(new SevenZipProcessRunner()),
            new RealCapability());

        var options = new CreateArchiveOptions
        {
            SourcePaths = new[] { ws.SourceDir },
            OutputPath = output,
            Format = ArchiveFormat.SevenZip,
            Password = password,
            Encryption = EncryptionMethod.Aes256,
            EncryptFileNames = true,
        };

        await writer.CreateAsync(options);

        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 0);

        // Lectura con el motor gestionado existente: contraseña correcta.
        using (var reader = new SevenZipArchiveReader())
        {
            await reader.OpenAsync(output, password);
            var result = await reader.ExtractAsync(new ExtractOptions { DestinationPath = ws.RootPath });
            Assert.True(result.ExtractedCount >= 2);
        }

        // Contraseña incorrecta: fallo controlado (el mensaje no incluye la contraseña real).
        using (var reader = new SevenZipArchiveReader())
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await reader.OpenAsync(output, "contraseña-incorrecta");
                await reader.ExtractAsync(new ExtractOptions { DestinationPath = Path.Combine(ws.RootPath, "mal") });
            });
            Assert.DoesNotContain(password, ex.ToString());
        }

        // Tras una ejecución REAL con éxito no debe quedar el temporal .uatmp-* en el destino
        // (la limpieza del response file está cubierta aparte en SevenZipCliTests).
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(output)!, ".uatmp-*"));
    }
}
