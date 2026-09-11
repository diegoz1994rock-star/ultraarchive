using System.Security.Cryptography;
using UltraArchive.Interop.SevenZip;

namespace UltraArchive.Tests.Interop;

/// <summary>
/// Fase 6A, Paso 1: <see cref="SevenZipLocator"/> localiza y verifica el 7zr.exe bundleado.
/// Ningún test depende de que haya un 7zr.exe real: el arranque del proceso está aislado tras
/// <see cref="ISevenZipBannerReader"/> y aquí se usa un doble.
/// </summary>
public sealed class SevenZipLocatorTests : IDisposable
{
    private const string ValidBanner =
        "7-Zip 26.02 (x64) : Copyright (c) 1999-2026 Igor Pavlov : 2026-06-25\r\n\r\nUsage: 7zr <command> ...";

    private static readonly Version MinVersion = new(19, 0);

    private readonly List<string> _tempDirs = new();

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // limpieza best-effort
            }
        }
    }

    // ---- helpers ----

    private string NewTempDir(string tag)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ua-7zr-{tag}-{Guid.NewGuid():N}");
        _tempDirs.Add(dir);
        return dir;
    }

    /// <summary>Crea &lt;tempDir&gt;/tools/7zr.exe con esos bytes y devuelve (appDir, sha256Hex).</summary>
    private (string AppDir, string Sha256) MakeFakeTool(byte[] bytes)
    {
        var appDir = NewTempDir("tool");
        var toolsDir = Path.Combine(appDir, "tools");
        Directory.CreateDirectory(toolsDir);
        File.WriteAllBytes(Path.Combine(toolsDir, "7zr.exe"), bytes);
        return (appDir, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static byte[] SampleBinary(string tag) =>
        System.Text.Encoding.ASCII.GetBytes("FAKE-7ZR-EXE-CONTENT::" + tag);

    private sealed class FakeBannerReader(string? banner) : ISevenZipBannerReader
    {
        public int Calls { get; private set; }
        public string? LastPath { get; private set; }

        public string? ReadBanner(string executablePath)
        {
            Calls++;
            LastPath = executablePath;
            return banner;
        }
    }

    // ---- 1. archivo inexistente ----

    [Fact]
    public void Locate_SinBinario_DevuelveNotFoundSinLanzar()
    {
        var appDir = NewTempDir("vacio");
        var locator = new SevenZipLocator(appDir, "ABC", MinVersion, new FakeBannerReader(ValidBanner));

        var result = locator.Locate();

        Assert.Equal(SevenZipToolStatus.NotFound, result.Status);
        Assert.False(result.IsAvailable);
        Assert.Null(result.Tool);
    }

    // ---- 2. ruta fija correcta ----

    [Fact]
    public void ExpectedExecutablePath_EsSiempreAppDirBarraToolsBarra7zrExe()
    {
        var appDir = NewTempDir("ruta");
        var locator = new SevenZipLocator(appDir, "ABC", MinVersion, new FakeBannerReader(null));

        var expected = Path.GetFullPath(Path.Combine(appDir, "tools", "7zr.exe"));
        Assert.Equal(expected, locator.ExpectedExecutablePath);
        Assert.EndsWith(Path.Combine("tools", "7zr.exe"), locator.ExpectedExecutablePath);
    }

    // ---- 3. NO usar PATH ----

    [Fact]
    public void Locate_ConUn7zrEnOtraCarpeta_NoLoUsa_DevuelveNotFound()
    {
        // Un "7zr.exe" existe, pero NO en <appDir>/tools/ → el locator no debe encontrarlo.
        var (elsewhereDir, _) = MakeFakeTool(SampleBinary("elsewhere"));
        var appDir = NewTempDir("nopath");
        Directory.CreateDirectory(appDir);

        var locator = new SevenZipLocator(appDir, "ABC", MinVersion, new FakeBannerReader(ValidBanner));

        Assert.Equal(SevenZipToolStatus.NotFound, locator.Locate().Status);
        Assert.DoesNotContain(elsewhereDir, locator.ExpectedExecutablePath);
    }

    // ---- 4. hash correcto (+ 7. banner válido) ----

    [Fact]
    public void Locate_HashCorrectoYBannerValido_DevuelveAvailableConVersion()
    {
        var (appDir, sha) = MakeFakeTool(SampleBinary("good"));
        var banner = new FakeBannerReader(ValidBanner);
        var locator = new SevenZipLocator(appDir, sha, MinVersion, banner);

        var result = locator.Locate();

        Assert.Equal(SevenZipToolStatus.Available, result.Status);
        Assert.True(result.IsAvailable);
        Assert.NotNull(result.Tool);
        Assert.Equal(new Version(26, 2), result.Tool!.Version);
        Assert.Equal(locator.ExpectedExecutablePath, result.Tool.ExecutablePath);
        Assert.Equal(1, banner.Calls);
    }

    // ---- 5. hash incorrecto  +  10. nunca ejecutar si el hash no coincide ----

    [Fact]
    public void Locate_HashIncorrecto_DevuelveHashMismatch_YNoEjecutaElBinario()
    {
        var (appDir, _) = MakeFakeTool(SampleBinary("real"));
        var banner = new FakeBannerReader(ValidBanner);
        var locator = new SevenZipLocator(appDir, "DEADBEEF" + new string('0', 56), MinVersion, banner);

        var result = locator.Locate();

        Assert.Equal(SevenZipToolStatus.HashMismatch, result.Status);
        Assert.False(result.IsAvailable);
        Assert.Equal(0, banner.Calls); // el binario NUNCA se ejecutó
    }

    // ---- 6. binario inválido (hash OK, no responde como 7-Zip) ----

    [Fact]
    public void Locate_HashOkPeroBinarioNoResponde_DevuelveInvalidExecutable()
    {
        var (appDir, sha) = MakeFakeTool(SampleBinary("corrupto"));
        var locator = new SevenZipLocator(appDir, sha, MinVersion, new FakeBannerReader(banner: null));

        var result = locator.Locate();

        Assert.Equal(SevenZipToolStatus.InvalidExecutable, result.Status);
        Assert.False(result.IsAvailable);
    }

    // ---- 8. banner inválido / versión incompatible ----

    [Fact]
    public void Locate_BannerNoEsDe7Zip_DevuelveInvalidExecutable()
    {
        var (appDir, sha) = MakeFakeTool(SampleBinary("otracosa"));
        var locator = new SevenZipLocator(appDir, sha, MinVersion, new FakeBannerReader("Microsoft Windows [Version 10.0]"));

        Assert.Equal(SevenZipToolStatus.InvalidExecutable, locator.Locate().Status);
    }

    [Fact]
    public void Locate_VersionAnteriorALaMinima_DevuelveIncompatibleVersion()
    {
        var (appDir, sha) = MakeFakeTool(SampleBinary("viejo"));
        var oldBanner = "7-Zip (A) 9.20  Copyright (c) 1999-2010 Igor Pavlov  2010-11-18";
        var locator = new SevenZipLocator(appDir, sha, MinVersion, new FakeBannerReader(oldBanner));

        var result = locator.Locate();

        Assert.Equal(SevenZipToolStatus.IncompatibleVersion, result.Status);
        Assert.False(result.IsAvailable);
    }

    // ---- 9. "tool unavailable" nunca lanza ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("basura sin sentido")]
    [InlineData("7-Zip 26.02")]
    public void Locate_TodasLasRamas_DevuelvenResultadoSinLanzar(string? banner)
    {
        var (appDir, sha) = MakeFakeTool(SampleBinary("x-" + (banner ?? "null")));
        var locator = new SevenZipLocator(appDir, sha, MinVersion, new FakeBannerReader(banner));

        var ex = Record.Exception(() => locator.Locate());

        Assert.Null(ex);
    }

    // ---- Configuración de producción por defecto ----
    // El binario oficial 7zr.exe del LZMA SDK 26.02 (dominio público) ya está bundleado en
    // tools/7zr.exe y su SHA-256 fijado en SevenZipToolReference.ExpectedSha256 (Fase 8).
    // El .csproj lo copia a <salida>/tools/7zr.exe, así que el locator de producción debe
    // resolverlo como Available con una versión >= la mínima exigida.

    [Fact]
    public void Locator_PorDefecto_NoLanza_YResuelveElBinarioOficial()
    {
        var locator = new SevenZipLocator();

        var ex = Record.Exception(() => locator.Locate());
        Assert.Null(ex);

        var result = locator.Locate();

        Assert.Equal(SevenZipToolStatus.Available, result.Status);
        Assert.True(result.IsAvailable);
        Assert.NotNull(result.Tool);
        Assert.True(result.Tool!.Version >= SevenZipToolReference.MinimumVersion,
            $"versión {result.Tool.Version} < mínima {SevenZipToolReference.MinimumVersion}");
        Assert.Equal(locator.ExpectedExecutablePath, result.Tool.ExecutablePath);
        Assert.False(string.IsNullOrEmpty(SevenZipToolReference.ExpectedSha256));
    }

    [Fact]
    public void Locate_HashNoFijado_DevuelveNotConfigured_SinEjecutar()
    {
        var (appDir, _) = MakeFakeTool(SampleBinary("cualquiera"));
        var banner = new FakeBannerReader(ValidBanner);
        var locator = new SevenZipLocator(appDir, expectedSha256: "", MinVersion, banner);

        var result = locator.Locate();

        Assert.Equal(SevenZipToolStatus.NotConfigured, result.Status);
        Assert.Equal(0, banner.Calls);
    }
}
