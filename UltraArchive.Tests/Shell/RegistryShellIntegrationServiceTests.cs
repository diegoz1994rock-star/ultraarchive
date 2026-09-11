using Microsoft.Win32;
using UltraArchive.Shell;

namespace UltraArchive.Tests.Shell;

/// <summary>
/// Fase 6B/6C: la integración con el Explorador escribe SOLO bajo el árbol indicado (HKCU en
/// producción para el usuario actual) y es totalmente reversible. Los tests usan una subclave
/// aislada bajo <c>HKCU\Software\UltraArchive.Tests\...</c>.
///
/// Estructura que se comprueba: un único submenú "Ultra Archive" (<c>*\shell\UltraArchive</c> y
/// <c>Directory\shell\UltraArchive</c>) con <c>SubCommands=""</c> y sub-verbos bajo <c>\shell\NN_*</c>,
/// más "Abrir con" (<c>Applications\UltraArchive.exe</c> + <c>&lt;.ext&gt;\OpenWithList</c>).
/// </summary>
public sealed class RegistryShellIntegrationServiceTests : IDisposable
{
    private readonly string _testRootRelative;
    private readonly string _classesRoot;
    private const string Exe = @"C:\Archivos de programa\UltraArchive\UltraArchive.exe";

    public RegistryShellIntegrationServiceTests()
    {
        _testRootRelative = $@"Software\UltraArchive.Tests\{Guid.NewGuid():N}";
        _classesRoot = $@"{_testRootRelative}\Classes";
    }

    public void Dispose()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(_testRootRelative, throwOnMissingSubKey: false);
            using var parent = Registry.CurrentUser.OpenSubKey(@"Software\UltraArchive.Tests");
            if (parent is { SubKeyCount: 0, ValueCount: 0 })
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\UltraArchive.Tests", throwOnMissingSubKey: false);
            }
        }
        catch { /* best-effort */ }
    }

    private RegistryShellIntegrationService NewService() => new(Exe, _classesRoot);

    private RegistryKey? Open(string subPath) => Registry.CurrentUser.OpenSubKey($@"{_classesRoot}\{subPath}");

    private string? CommandOf(string menuRoot, string verbKey)
    {
        using var key = Open($@"{menuRoot}\shell\{verbKey}\command");
        return key?.GetValue(null) as string;
    }

    [Fact]
    public void Install_CreaElSubmenuUltraArchiveSobreArchivosYCarpetas()
    {
        var service = NewService();
        Assert.False(service.IsInstalled);

        service.Install();

        Assert.True(service.IsInstalled);

        foreach (var menuRoot in new[] { @"*\shell\UltraArchive", @"Directory\shell\UltraArchive" })
        {
            using var menu = Open(menuRoot);
            Assert.NotNull(menu);
            Assert.Equal("Ultra Archive", menu!.GetValue("MUIVerb"));
            Assert.Equal(string.Empty, menu.GetValue("SubCommands"));
            Assert.Contains(Exe, (string)menu.GetValue("Icon")!);

            // Verbos de compresión: presentes en ambos menús, con selección múltiple.
            foreach (var (verb, flag) in new[]
                     {
                         ("30_compress", "--compress"),
                         ("31_compresshere", "--compress-here"),
                         ("32_zip", "--compress-zip"),
                         ("33_7z", "--compress-7z"),
                         ("34_split", "--compress-split"),
                     })
            {
                var command = CommandOf(menuRoot, verb);
                Assert.NotNull(command);
                Assert.Contains(flag, command!);
                Assert.Contains(Exe, command!);
                Assert.Contains("%1", command!);

                using var verbKey = Open($@"{menuRoot}\shell\{verb}");
                Assert.Equal("Player", verbKey!.GetValue("MultiSelectModel"));
            }
        }

        // Verbos de extracción: SOLO en el menú de archivos (*), con AppliesTo a extensiones de archivo.
        foreach (var (verb, flag) in new[]
                 {
                     ("10_open", null),
                     ("11_extractfiles", "--extract-to"),
                     ("12_extracthere", "--extract-here-flat"),
                     ("13_extractsub", "--extract-here"),
                 })
        {
            var command = CommandOf(@"*\shell\UltraArchive", verb);
            Assert.NotNull(command);
            if (flag is not null)
            {
                Assert.Contains(flag, command!);
            }

            using var verbKey = Open($@"*\shell\UltraArchive\shell\{verb}");
            var appliesTo = (string)verbKey!.GetValue("AppliesTo")!;
            Assert.Contains(".zip", appliesTo);
            Assert.Contains(".7z", appliesTo);

            // No aparece sobre carpetas.
            Assert.Null(Open($@"Directory\shell\UltraArchive\shell\{verb}"));
        }
    }

    [Fact]
    public void Install_MantieneAbrirCon()
    {
        var service = NewService();
        service.Install();

        using var cmd = Open(@"Applications\UltraArchive.exe\shell\open\command");
        Assert.NotNull(cmd);
        Assert.Contains(Exe, (string)cmd!.GetValue(null)!);
        Assert.Contains("%1", (string)cmd.GetValue(null)!);

        foreach (var ext in RegistryShellIntegrationService.OpenWithExtensions)
        {
            Assert.NotNull(Open($@"{ext}\OpenWithList\UltraArchive.exe"));
        }
    }

    [Fact]
    public void Uninstall_EliminaTodoLoQueCreoInstall()
    {
        var service = NewService();
        service.Install();
        Assert.True(service.IsInstalled);

        service.Uninstall();

        Assert.False(service.IsInstalled);
        Assert.Null(Open(@"*\shell\UltraArchive"));
        Assert.Null(Open(@"Directory\shell\UltraArchive"));
        Assert.Null(Open(@"Applications\UltraArchive.exe"));

        foreach (var ext in RegistryShellIntegrationService.OpenWithExtensions)
        {
            Assert.Null(Open($@"{ext}\OpenWithList\UltraArchive.exe"));
        }
    }

    [Fact]
    public void Uninstall_TambienLimpiaElEsquemaAntiguoDeVerbosPlanos()
    {
        // Simula una instalación de una versión anterior (verbos planos en SystemFileAssociations).
        using (Registry.CurrentUser.CreateSubKey(
                   $@"{_classesRoot}\SystemFileAssociations\.zip\shell\UltraArchive.ExtractHere\command"))
        {
        }

        using (Registry.CurrentUser.CreateSubKey(
                   $@"{_classesRoot}\SystemFileAssociations\.7z\shell\UltraArchive.ExtractTo\command"))
        {
        }

        var service = NewService();
        service.Uninstall();

        Assert.Null(Open(@"SystemFileAssociations\.zip\shell\UltraArchive.ExtractHere"));
        Assert.Null(Open(@"SystemFileAssociations\.7z\shell\UltraArchive.ExtractTo"));
    }

    [Fact]
    public void Install_EsIdempotente()
    {
        var service = NewService();
        service.Install();
        service.Install(); // no debe lanzar ni duplicar
        Assert.True(service.IsInstalled);
    }

    [Fact]
    public void Uninstall_SinInstalarAntes_NoLanza()
    {
        var service = NewService();
        service.Uninstall();
        Assert.False(service.IsInstalled);
    }

    [Fact]
    public void Uninstall_NoTocaEntradasAjenasDeLaMismaExtension()
    {
        using (Registry.CurrentUser.CreateSubKey($@"{_classesRoot}\.zip\OpenWithList\OtroPrograma.exe"))
        {
        }

        using (var otroVerbo = Registry.CurrentUser.CreateSubKey(
                   $@"{_classesRoot}\SystemFileAssociations\.zip\shell\OtroPrograma.Abrir\command"))
        {
            otroVerbo.SetValue(null, @"C:\otro\prog.exe %1");
        }

        var service = NewService();
        service.Install();
        service.Uninstall();

        Assert.NotNull(Open(@".zip\OpenWithList\OtroPrograma.exe"));
        Assert.NotNull(Open(@"SystemFileAssociations\.zip\shell\OtroPrograma.Abrir"));
    }

    [Fact]
    public void Install_EscribeEnLaRaizIndicada()
    {
        var service = NewService();
        service.Install();

        using var testApp = Open(@"Applications\UltraArchive.exe");
        Assert.NotNull(testApp);
    }

    [Fact]
    public void Constructor_RutaVacia_Lanza()
    {
        Assert.Throws<ArgumentException>(() => new RegistryShellIntegrationService("  "));
    }
}
