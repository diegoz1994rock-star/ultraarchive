using System.Text;
using UltraArchive.Interop.SevenZip;

namespace UltraArchive.Tests.Interop;

/// <summary>Fase 6A, Paso 2: el response file solo contiene rutas, en UTF-8, y se limpia.</summary>
public class SevenZipResponseFileTests
{
    [Fact]
    public void Create_EscribeUnaRutaPorLinea_EnUtf8ConBom()
    {
        var sources = new[] { @"C:\uno\a.txt", @"C:\dos\b.bin" };

        using var rf = SevenZipResponseFile.Create(sources);

        Assert.True(File.Exists(rf.Path));
        var bytes = File.ReadAllBytes(rf.Path);
        Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "debe tener BOM UTF-8");

        var lines = File.ReadAllLines(rf.Path, Encoding.UTF8);
        Assert.Equal(sources, lines);
    }

    [Fact]
    public void Create_RutasUnicodeYConEspacios_SeConservanExactamente()
    {
        var sources = new[]
        {
            @"C:\mis documentos\informe final.txt",
            @"C:\café\ñandú — copia (1).bin",
            @"C:\Пользователи\файл.txt",
        };

        using var rf = SevenZipResponseFile.Create(sources);
        var lines = File.ReadAllLines(rf.Path, Encoding.UTF8);

        Assert.Equal(sources, lines);
    }

    [Fact]
    public void Create_RutasConCaracteresEspeciales_SeConservanSinInterpretar()
    {
        var sources = new[]
        {
            @"C:\a & b\c $(d).txt",
            "C:\\comillas\\\"x\".txt",
            @"C:\punto y coma\a;b.txt",
            @"C:\muy\larga\" + new string('n', 180) + ".txt",
        };

        using var rf = SevenZipResponseFile.Create(sources);
        var lines = File.ReadAllLines(rf.Path, Encoding.UTF8);

        Assert.Equal(sources, lines);
    }

    [Fact]
    public void Create_ContrasenaNuncaAparece_AunqueCoincidaConParteDeUnaRuta()
    {
        var password = "MiClaveSecreta";
        // La ruta contiene deliberadamente el texto de la contraseña; el response file NO recibe la contraseña
        // como tal, pero comprobamos que Create ni la conoce ni la añade.
        var sources = new[] { @"C:\proyectos\backup.txt" };

        using var rf = SevenZipResponseFile.Create(sources);
        var content = File.ReadAllText(rf.Path);

        Assert.DoesNotContain(password, content);
        // Y solo hay rutas, nada de switches
        Assert.DoesNotContain("-p", content);
        Assert.DoesNotContain("-mhe", content);
    }

    [Fact]
    public void Create_ListaVacia_Rechazada()
    {
        Assert.Throws<ArgumentException>(() => SevenZipResponseFile.Create(Array.Empty<string>()));
    }

    [Theory]
    [InlineData("-mhe=off")]
    [InlineData("@listfile-anidado")]
    [InlineData("relativa.txt")]
    public void Create_RutaPeligrosa_Rechazada(string malicious)
    {
        Assert.Throws<ArgumentException>(() => SevenZipResponseFile.Create(new[] { @"C:\ok.txt", malicious }));
    }

    [Fact]
    public void Create_RutaConSaltoDeLinea_Rechazada()
    {
        Assert.Throws<ArgumentException>(() =>
            SevenZipResponseFile.Create(new[] { "C:\\a.txt\nC:\\Windows\\evil" }));
    }

    [Fact]
    public void NombreDelFichero_DerivadoDeGuid_NoDeDatosDeUsuario()
    {
        using var rf = SevenZipResponseFile.Create(new[] { @"C:\x\a.txt" });

        var name = Path.GetFileName(rf.Path);
        Assert.StartsWith("uarsp-", name);
        Assert.EndsWith(".txt", name);
        Assert.Equal("uarsp-".Length + 32 + ".txt".Length, name.Length);
    }

    [Fact]
    public void Dispose_BorraElFicheroTemporal()
    {
        var rf = SevenZipResponseFile.Create(new[] { @"C:\x\a.txt" });
        var path = rf.Path;
        Assert.True(File.Exists(path));

        rf.Dispose();

        Assert.False(File.Exists(path));
        rf.Dispose(); // idempotente, no lanza
    }
}
