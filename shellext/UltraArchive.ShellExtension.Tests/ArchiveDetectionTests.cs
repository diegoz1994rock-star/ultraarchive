namespace UltraArchive.ShellExtension;

public class ArchiveDetectionTests
{
    [Theory]
    [InlineData(@"C:\docs\backup.zip")]
    [InlineData(@"C:\docs\backup.7z")]
    [InlineData(@"C:\docs\backup.rar")]
    [InlineData(@"C:\docs\backup.tar")]
    [InlineData(@"C:\docs\backup.gz")]
    [InlineData(@"C:\docs\backup.tgz")]
    [InlineData(@"C:\docs\backup.bz2")]
    [InlineData(@"C:\docs\image.iso")]
    [InlineData(@"C:\docs\BACKUP.ZIP")] // sin distinguir mayúsculas/minúsculas
    public void IsArchive_ExtensionReconocida_DevuelveTrue(string path)
    {
        Assert.True(ArchiveDetection.IsArchive(path));
    }

    [Theory]
    [InlineData(@"C:\docs\informe.docx")]
    [InlineData(@"C:\docs\foto.png")]
    [InlineData(@"C:\docs\sin_extension")]
    [InlineData(@"C:\docs\carpeta.")]
    public void IsArchive_ExtensionNoReconocida_DevuelveFalse(string path)
    {
        Assert.False(ArchiveDetection.IsArchive(path));
    }

    // Debe ser exactamente la misma lista que RegistryShellIntegrationService.OpenWithExtensions
    // y el AppliesTo del submenú clásico (Package.wxs) — si alguna cambia, esta prueba lo detecta.
    [Theory]
    [InlineData(".zip")]
    [InlineData(".7z")]
    [InlineData(".rar")]
    [InlineData(".tar")]
    [InlineData(".gz")]
    [InlineData(".tgz")]
    [InlineData(".bz2")]
    [InlineData(".iso")]
    public void IsArchive_CubreLasOchoExtensionesDelSubmenuClasico(string ext)
    {
        Assert.True(ArchiveDetection.IsArchive("archivo" + ext));
    }
}
