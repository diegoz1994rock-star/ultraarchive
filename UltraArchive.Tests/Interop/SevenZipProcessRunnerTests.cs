using System.Diagnostics;
using UltraArchive.Interop.SevenZip;

namespace UltraArchive.Tests.Interop;

/// <summary>
/// Fase 6A, Paso 3 (A): reglas obligatorias de <see cref="ProcessStartInfo"/>. No se ejecuta ningún
/// 7zr real — se verifica la construcción del <c>ProcessStartInfo</c> de forma aislada.
/// </summary>
public class SevenZipProcessRunnerTests
{
    private static SevenZipProcessStartInfo SampleStartInfo() => new()
    {
        ExecutablePath = @"C:\app\tools\7zr.exe",
        ArgumentList = new[] { "a", "-t7z", "-mx=5", "-pSECRETO", "-y", "-bsp1", "-scsUTF-8", @"C:\out\a.7z", @"@C:\tmp\uarsp-x.txt" },
        WorkingDirectory = @"C:\out",
    };

    [Fact]
    public void BuildStartInfo_UsaLasReglasObligatorias()
    {
        var psi = SevenZipProcessRunner.BuildStartInfo(SampleStartInfo());

        Assert.False(psi.UseShellExecute);
        Assert.True(psi.CreateNoWindow);
        Assert.True(psi.RedirectStandardOutput);
        Assert.True(psi.RedirectStandardError);
        Assert.True(psi.RedirectStandardInput);
        Assert.Equal(@"C:\app\tools\7zr.exe", psi.FileName);
        Assert.Equal(@"C:\out", psi.WorkingDirectory);
    }

    [Fact]
    public void BuildStartInfo_UsaArgumentList_NoLaCadenaArguments()
    {
        var info = SampleStartInfo();

        var psi = SevenZipProcessRunner.BuildStartInfo(info);

        Assert.Equal(info.ArgumentList, psi.ArgumentList);
        Assert.Equal(string.Empty, psi.Arguments); // nunca se construye una línea manual
    }

    [Fact]
    public void Start_EjecutableNoAbsoluto_LanzaSevenZipProcessStartException()
    {
        var runner = new SevenZipProcessRunner();
        var info = new SevenZipProcessStartInfo
        {
            ExecutablePath = "7zr.exe", // relativo → se buscaría en PATH: prohibido
            ArgumentList = new[] { "a" },
            WorkingDirectory = Path.GetTempPath(),
        };

        Assert.Throws<SevenZipProcessStartException>(() => runner.Start(info, _ => { }));
    }

    [Fact]
    public void Start_EjecutableInexistente_LanzaSevenZipProcessStartException()
    {
        var runner = new SevenZipProcessRunner();
        var info = new SevenZipProcessStartInfo
        {
            ExecutablePath = Path.Combine(Path.GetTempPath(), "no-existe-7zr-" + Guid.NewGuid().ToString("N") + ".exe"),
            ArgumentList = new[] { "a" },
            WorkingDirectory = Path.GetTempPath(),
        };

        Assert.Throws<SevenZipProcessStartException>(() => runner.Start(info, _ => { }));
    }
}
