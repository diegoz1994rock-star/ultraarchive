using UltraArchive.Interop.SevenZip;

namespace UltraArchive.Tests.Interop;

/// <summary>Fase 6A, Paso 2: el redactor jamás deja pasar la contraseña.</summary>
public class SevenZipArgRedactorTests
{
    [Fact]
    public void RedactOne_SwitchDeContrasena_QuedaEnmascarado()
    {
        Assert.Equal("-p***", SevenZipArgRedactor.RedactOne("-pMiContraseñaSecreta"));
    }

    [Fact]
    public void RedactOne_NoFiltraNiElValorNiLaLongitud()
    {
        var corta = SevenZipArgRedactor.RedactOne("-pA");
        var larga = SevenZipArgRedactor.RedactOne("-p" + new string('x', 200));

        Assert.Equal("-p***", corta);
        Assert.Equal(corta, larga); // misma salida ⇒ no se puede inferir la longitud
    }

    [Fact]
    public void RedactOne_SwitchPMenosSinValor_SeDejaTalCual()
    {
        Assert.Equal("-p", SevenZipArgRedactor.RedactOne("-p"));
    }

    [Fact]
    public void RedactOne_SwitchNormal_NoSeToca()
    {
        Assert.Equal("-mhe=on", SevenZipArgRedactor.RedactOne("-mhe=on"));
        Assert.Equal("-mx=9", SevenZipArgRedactor.RedactOne("-mx=9"));
        Assert.Equal("a", SevenZipArgRedactor.RedactOne("a"));
    }

    [Fact]
    public void Redact_ListaCompleta_ContieneLosSwitchesPeroNoLaContrasena()
    {
        var password = "Clave-Ultra-Secreta-2026";
        var args = new[]
        {
            "a", "-t7z", "-mx=9", "-mhe=on", "-p" + password, "-y", "-bsp1", "-scsUTF-8",
            @"C:\salida\archivo.7z", @"@C:\Temp\uarsp-abc.txt",
        };

        var redacted = SevenZipArgRedactor.Redact(args);

        Assert.DoesNotContain(password, redacted);
        Assert.Contains("-p***", redacted);
        Assert.Contains("-t7z", redacted);
        Assert.Contains("-mhe=on", redacted);
        Assert.Contains("archivo.7z", redacted);
    }

    [Fact]
    public void SensitiveSwitchPrefixes_EstaExpuestoParaAmpliacionFutura()
    {
        Assert.Contains("-p", SevenZipArgRedactor.SensitiveSwitchPrefixes);
    }
}
