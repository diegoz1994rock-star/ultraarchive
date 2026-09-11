using UltraArchive.Core.Models;
using UltraArchive.Interop.SevenZip;

namespace UltraArchive.Tests.Interop;

/// <summary>Fase 6A, Paso 2: validación de la petición (rutas, contraseña, nivel).</summary>
public class SevenZipRequestTests
{
    private const string Pwd = "Clave-Valida-123";
    private static readonly string[] OneSource = { @"C:\datos\a.txt" };
    private const string Output = @"C:\salida\archivo.7z";

    [Fact]
    public void Create_PeticionValida_RellenaTodo()
    {
        var request = SevenZipRequest.Create(OneSource, Output, Pwd, CompressionLevel.Maximum, encryptHeaders: true);

        Assert.Single(request.SourcePaths);
        Assert.Equal(Path.GetFullPath(Output), request.OutputPath);
        Assert.Equal(Pwd, request.Password);
        Assert.Equal(CompressionLevel.Maximum, request.Level);
        Assert.True(request.EncryptHeaders);
    }

    [Fact]
    public void Create_ListaDeOrigenesVacia_Rechazada()
    {
        Assert.Throws<ArgumentException>(() => SevenZipRequest.Create(Array.Empty<string>(), Output, Pwd));
    }

    [Fact]
    public void Create_ContrasenaVacia_Rechazada()
    {
        Assert.Throws<ArgumentException>(() => SevenZipRequest.Create(OneSource, Output, string.Empty));
    }

    [Theory]
    [InlineData("-mhe=off")]      // parece un switch
    [InlineData("--password")]    // parece un switch largo
    [InlineData("@otracosa")]     // parece un listfile
    [InlineData("relativa/x.txt")] // no absoluta
    [InlineData("x.txt")]          // no absoluta
    public void Create_RutaDeOrigenPeligrosa_Rechazada(string malicious)
    {
        Assert.Throws<ArgumentException>(() => SevenZipRequest.Create(new[] { malicious }, Output, Pwd));
    }

    [Fact]
    public void Create_RutaDeOrigenConSaltoDeLinea_Rechazada()
    {
        Assert.Throws<ArgumentException>(() =>
            SevenZipRequest.Create(new[] { "C:\\datos\\a.txt\r\nC:\\Windows\\System32\\evil" }, Output, Pwd));
    }

    [Fact]
    public void Create_RutaDeSalidaPeligrosa_Rechazada()
    {
        Assert.Throws<ArgumentException>(() => SevenZipRequest.Create(OneSource, "-salida.7z", Pwd));
    }

    [Fact]
    public void Create_NivelDeCompresionNoValido_Rechazado()
    {
        Assert.Throws<ArgumentException>(() =>
            SevenZipRequest.Create(OneSource, Output, Pwd, (CompressionLevel)999));
    }

    [Fact]
    public void Create_RutaConGuionEnUnSegmentoIntermedio_SeAcepta()
    {
        // "C:\carpeta\-archivo.txt" es absoluta y no empieza por '-' → válida; irá al response file.
        var request = SevenZipRequest.Create(new[] { @"C:\carpeta\-archivo.txt" }, Output, Pwd);
        Assert.Single(request.SourcePaths);
    }

    // ---- 14. ausencia de datos sensibles en mensajes de error / ToString ----

    [Fact]
    public void Create_ErrorConContrasenaPresente_NoFiltraLaContrasenaEnElMensaje()
    {
        var secret = "SuperSecretoQueNoDebeAparecer";

        var ex = Assert.Throws<ArgumentException>(() =>
            SevenZipRequest.Create(new[] { "ruta-relativa-invalida" }, Output, secret));

        Assert.DoesNotContain(secret, ex.Message);
        Assert.DoesNotContain(secret, ex.ToString());
    }

    [Fact]
    public void ToString_NoContieneLaContrasena()
    {
        var secret = "OtroSecreto-987";
        var request = SevenZipRequest.Create(OneSource, Output, secret);

        Assert.DoesNotContain(secret, request.ToString());
        Assert.Contains("***", request.ToString());
    }
}
