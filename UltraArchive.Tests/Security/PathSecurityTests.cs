using UltraArchive.Core.Exceptions;
using UltraArchive.Security;

namespace UltraArchive.Tests.Security;

public class PathSecurityTests
{
    [Fact]
    public void ResolveSafeDestinationPath_RutaRelativaNormal_DevuelveRutaDentroDelDestino()
    {
        var root = Path.Combine(Path.GetTempPath(), "ultraarchive-pathsec-" + Guid.NewGuid().ToString("N"));

        var result = PathSecurity.ResolveSafeDestinationPath(root, "sub/archivo.txt");

        Assert.StartsWith(Path.GetFullPath(root), result, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("sub", "archivo.txt"), result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("../../evil.txt")]
    [InlineData("sub/../../evil.txt")]
    [InlineData(@"C:\Windows\System32\evil.dll")]
    [InlineData(@"\\servidor\recurso\evil.txt")]
    public void ResolveSafeDestinationPath_IntentoDeEscapeDelDestino_LanzaPathTraversalException(string maliciousEntry)
    {
        var root = Path.Combine(Path.GetTempPath(), "ultraarchive-pathsec-" + Guid.NewGuid().ToString("N"));

        Assert.Throws<PathTraversalException>(() => PathSecurity.ResolveSafeDestinationPath(root, maliciousEntry));
    }

    [Theory]
    [InlineData("archivo\u0001.txt")]     // carácter de control
    [InlineData("na|me.txt")]              // barra vertical
    [InlineData("qu?e.txt")]               // interrogante
    [InlineData("as*terisco.txt")]         // asterisco
    [InlineData("carpeta/CON")]            // nombre de dispositivo reservado
    [InlineData("COM1.txt")]               // dispositivo reservado con extensión
    [InlineData("sub/NUL.log")]
    [InlineData("carpeta./archivo.txt")]  // segmento que termina en punto
    [InlineData("termina_en_espacio /x")] // segmento que termina en espacio
    public void ResolveSafeDestinationPath_NombreNoValidoEnWindows_LanzaUnsafeEntryNameException(string entry)
    {
        var root = Path.Combine(Path.GetTempPath(), "ultraarchive-pathsec-" + Guid.NewGuid().ToString("N"));

        Assert.Throws<UnsafeEntryNameException>(() => PathSecurity.ResolveSafeDestinationPath(root, entry));
    }

    [Theory]
    [InlineData("normal.txt")]
    [InlineData("con-guion.txt")]          // "con" solo es reservado si es el nombre exacto
    [InlineData("carpeta/CONSOLA.txt")]
    [InlineData("mi archivo con espacios.txt")]
    public void ResolveSafeDestinationPath_NombreValido_NoLanza(string entry)
    {
        var root = Path.Combine(Path.GetTempPath(), "ultraarchive-pathsec-" + Guid.NewGuid().ToString("N"));

        var result = PathSecurity.ResolveSafeDestinationPath(root, entry);

        Assert.StartsWith(Path.GetFullPath(root), result, StringComparison.OrdinalIgnoreCase);
    }
}
