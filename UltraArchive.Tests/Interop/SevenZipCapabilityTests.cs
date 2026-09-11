using System.Security.Cryptography;
using UltraArchive.Interop.SevenZip;

namespace UltraArchive.Tests.Interop;

/// <summary>
/// Fase 6A, Paso 4 (B): la capacidad de 7Z cifrado depende de que <see cref="SevenZipLocator"/>
/// devuelva <see cref="SevenZipToolStatus.Available"/>, no de que exista la integración.
/// </summary>
public sealed class SevenZipCapabilityTests : IDisposable
{
    private const string ValidBanner = "7-Zip 26.02 (x64) : Copyright (c) 1999-2026 Igor Pavlov";
    private static readonly Version MinVersion = new(19, 0);
    private readonly List<string> _tempDirs = new();

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* best-effort */ }
        }
    }

    private sealed class FixedBannerReader(string? banner) : ISevenZipBannerReader
    {
        public string? ReadBanner(string executablePath) => banner;
    }

    private (string AppDir, string Sha) MakeTool(byte[] bytes)
    {
        var appDir = Path.Combine(Path.GetTempPath(), "ua-cap-" + Guid.NewGuid().ToString("N"));
        _tempDirs.Add(appDir);
        Directory.CreateDirectory(Path.Combine(appDir, "tools"));
        File.WriteAllBytes(Path.Combine(appDir, "tools", "7zr.exe"), bytes);
        return (appDir, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private SevenZipCapability Capability(string appDir, string expectedSha, string? banner) =>
        new(new SevenZipLocator(appDir, expectedSha, MinVersion, new FixedBannerReader(banner)));

    [Fact]
    public void NotFound_False()
    {
        var appDir = Path.Combine(Path.GetTempPath(), "ua-cap-vacio-" + Guid.NewGuid().ToString("N"));
        _tempDirs.Add(appDir);
        var cap = Capability(appDir, "ABC", ValidBanner);

        Assert.False(cap.CanCreateEncryptedSevenZip);
        Assert.Null(cap.VerifiedExecutablePath);
    }

    [Fact]
    public void NotConfigured_False()
    {
        var (appDir, _) = MakeTool(new byte[] { 1, 2, 3 });
        var cap = Capability(appDir, expectedSha: "", ValidBanner);

        Assert.False(cap.CanCreateEncryptedSevenZip);
        Assert.Null(cap.VerifiedExecutablePath);
    }

    [Fact]
    public void HashMismatch_False()
    {
        var (appDir, _) = MakeTool(new byte[] { 1, 2, 3 });
        var cap = Capability(appDir, expectedSha: "DEADBEEF" + new string('0', 56), ValidBanner);

        Assert.False(cap.CanCreateEncryptedSevenZip);
    }

    [Fact]
    public void InvalidExecutable_False()
    {
        var (appDir, sha) = MakeTool(new byte[] { 9, 9, 9 });
        var cap = Capability(appDir, sha, banner: null); // no responde como 7-Zip

        Assert.False(cap.CanCreateEncryptedSevenZip);
    }

    [Fact]
    public void IncompatibleVersion_False()
    {
        var (appDir, sha) = MakeTool(new byte[] { 7 });
        var cap = Capability(appDir, sha, banner: "7-Zip (A) 9.20  Copyright (c) 1999-2010 Igor Pavlov");

        Assert.False(cap.CanCreateEncryptedSevenZip);
    }

    [Fact]
    public void Available_True_YExponeLaRutaVerificada()
    {
        var (appDir, sha) = MakeTool(new byte[] { 4, 2 });
        var cap = Capability(appDir, sha, ValidBanner);

        Assert.True(cap.CanCreateEncryptedSevenZip);
        Assert.EndsWith(Path.Combine("tools", "7zr.exe"), cap.VerifiedExecutablePath);
    }

    [Fact]
    public void SeEvaluaUnaSolaVez()
    {
        var calls = 0;
        var (appDir, sha) = MakeTool(new byte[] { 1 });
        var locator = new SevenZipLocator(appDir, sha, MinVersion, new CountingBannerReader(ValidBanner, () => calls++));
        var cap = new SevenZipCapability(locator);

        _ = cap.CanCreateEncryptedSevenZip;
        _ = cap.CanCreateEncryptedSevenZip;
        _ = cap.VerifiedExecutablePath;

        Assert.Equal(1, calls);
    }

    private sealed class CountingBannerReader(string banner, Action onRead) : ISevenZipBannerReader
    {
        public string? ReadBanner(string executablePath)
        {
            onRead();
            return banner;
        }
    }
}
