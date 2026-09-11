using UltraArchive.Core.Models;
using UltraArchive.Interop.SevenZip;

namespace UltraArchive.Tests.Interop;

/// <summary>
/// Fase 6A, Paso 2: construcción de argumentos. Ningún valor del usuario puede convertirse en un
/// switch ni alterar las opciones fijadas por UltraArchive.
/// </summary>
public class SevenZipArgumentBuilderTests
{
    private const string Pwd = "Clave-Fuerte-2026!";
    private const string Output = @"C:\salida\archivo.7z";
    private static readonly string[] Sources = { @"C:\datos\a.txt", @"C:\datos\sub\b.bin" };

    private static (SevenZipArguments Args, SevenZipResponseFile Rf) Build(
        IEnumerable<string>? sources = null,
        string? output = null,
        CompressionLevel level = CompressionLevel.Normal,
        bool mhe = false,
        string password = Pwd,
        long? volumeSizeBytes = null)
    {
        var request = SevenZipRequest.Create(sources ?? Sources, output ?? Output, password, level, mhe, volumeSizeBytes);
        var rf = SevenZipResponseFile.Create(request.SourcePaths);
        var args = new SevenZipArgumentBuilder().Build(request, rf);
        return (args, rf);
    }

    // ---- 1. construcción básica ----

    [Fact]
    public void Build_PeticionBasica_GeneraLosArgumentosEsperados()
    {
        var (args, rf) = Build();
        using var _ = rf;

        var list = args.ArgumentList;
        Assert.Equal("a", list[0]);
        Assert.Equal("-t7z", list[1]);
        Assert.Equal("-mx=5", list[2]);
        Assert.Contains("-p" + Pwd, list);
        Assert.Contains("-y", list);
        Assert.Contains("-bsp1", list);
        Assert.Contains("-scsUTF-8", list);
        Assert.Contains(Path.GetFullPath(Output), list);
        Assert.Contains("@" + rf.Path, list);
        Assert.DoesNotContain("-mhe=on", list); // mhe=false
    }

    // ---- división en volúmenes ----

    [Fact]
    public void Build_ConVolumen_IncluyeSwitchVConTamanoEnBytes()
    {
        var (args, rf) = Build(volumeSizeBytes: 5L * 1024 * 1024 * 1024);
        using var _ = rf;

        Assert.Contains("-v5368709120b", args.ArgumentList);
        Assert.Single(args.ArgumentList, a => a.StartsWith("-v", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_SinVolumen_NoIncluyeSwitchV()
    {
        var (args, rf) = Build();
        using var _ = rf;
        Assert.DoesNotContain(args.ArgumentList, a => a.StartsWith("-v", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_DivisionSinContrasena_NoIncluyeP_NiMhe()
    {
        var (args, rf) = Build(password: "", volumeSizeBytes: 100L * 1024 * 1024);
        using var _ = rf;

        Assert.DoesNotContain(args.ArgumentList, a => a.StartsWith("-p", StringComparison.Ordinal));
        Assert.DoesNotContain(args.ArgumentList, a => a.StartsWith("-mhe", StringComparison.Ordinal));
        Assert.Contains("-v104857600b", args.ArgumentList);
    }

    [Fact]
    public void Build_DivisionConContrasena_IncluyeAmbos()
    {
        var (args, rf) = Build(mhe: true, volumeSizeBytes: 100L * 1024 * 1024);
        using var _ = rf;

        Assert.Contains("-p" + Pwd, args.ArgumentList);
        Assert.Contains("-mhe=on", args.ArgumentList);
        Assert.Contains("-v104857600b", args.ArgumentList);
    }

    // ---- 9. -mhe=on no manipulable ----

    [Fact]
    public void Build_ConEncryptHeaders_IncluyeMheOnExactamente()
    {
        var (args, rf) = Build(mhe: true);
        using var _ = rf;

        Assert.Contains("-mhe=on", args.ArgumentList);
        Assert.DoesNotContain("-mhe=off", args.ArgumentList);
        Assert.Single(args.ArgumentList, a => a.StartsWith("-mhe", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_RutaConAparienciaDeMheOff_NoDesactivaElCifradoDeCabecera()
    {
        // La "ruta" C:\proyecto\-mhe=off.txt es absoluta y no empieza por '-' → válida, va al response file.
        var (args, rf) = Build(sources: new[] { @"C:\proyecto\-mhe=off.txt" }, mhe: true);
        using var _ = rf;

        Assert.Contains("-mhe=on", args.ArgumentList);
        Assert.DoesNotContain("-mhe=off", args.ArgumentList);
        // La ruta peligrosa NO está en argv: está en el response file.
        Assert.DoesNotContain(args.ArgumentList, a => a.Contains("-mhe=off"));
        Assert.Contains(@"C:\proyecto\-mhe=off.txt", File.ReadAllLines(rf.Path));
    }

    // ---- 7. intento de convertir una ruta en switch ----

    [Theory]
    [InlineData("-y")]
    [InlineData("-mx=0")]
    [InlineData("--")]
    [InlineData("--password")]
    [InlineData("@evil-listfile")]
    [InlineData("$(rm -rf)")]
    [InlineData("foo & malicious.exe")]
    public void Build_OrigenQueIntentaSerSwitchOInyeccion_EsRechazadoEnLaValidacion(string malicious)
    {
        // '$(...)' y 'foo & malicious.exe' no son rutas absolutas → rechazados por no cualificados;
        // '-y', '--', '@...' → rechazados por carácter reservado.
        Assert.Throws<ArgumentException>(() =>
            SevenZipRequest.Create(new[] { malicious }, Output, Pwd));
    }

    // ---- 8. "--" / posición de los datos de usuario ----

    [Fact]
    public void Build_LosUnicosArgumentosConGuionSonLosSwitchesFijos()
    {
        var (args, rf) = Build(sources: new[] { @"C:\x\a.txt", @"C:\x\b.txt" }, level: CompressionLevel.Ultra, mhe: true);
        using var _ = rf;

        var switchLike = args.ArgumentList.Where(a => a.StartsWith('-')).ToList();

        // Exactamente estos, con valores fijos puestos por UltraArchive:
        Assert.Equal(new[] { "-t7z", "-mx=9", "-mhe=on", "-p" + Pwd, "-y", "-bsp1", "-scsUTF-8" }, switchLike);
    }

    [Fact]
    public void Build_LaRutaDeSalidaYElResponseFileNoEmpiezanPorGuionNiArroba()
    {
        var (args, rf) = Build();
        using var _ = rf;

        var output = args.ArgumentList.Single(a => a.EndsWith("archivo.7z"));
        var listRef = args.ArgumentList.Single(a => a.StartsWith('@'));

        Assert.False(output.StartsWith('-'));
        Assert.False(output.StartsWith('@'));
        Assert.StartsWith("@", listRef);
        Assert.False(listRef[1..].StartsWith('-')); // la ruta tras la '@' tampoco
    }

    // ---- 10. nivel de compresión ----

    [Theory]
    [InlineData(CompressionLevel.Store, "-mx=0")]
    [InlineData(CompressionLevel.Fastest, "-mx=1")]
    [InlineData(CompressionLevel.Fast, "-mx=3")]
    [InlineData(CompressionLevel.Normal, "-mx=5")]
    [InlineData(CompressionLevel.Maximum, "-mx=7")]
    [InlineData(CompressionLevel.Ultra, "-mx=9")]
    public void Build_CadaNivel_MapeaAlSwitchMx(CompressionLevel level, string expected)
    {
        var (args, rf) = Build(level: level);
        using var _ = rf;

        Assert.Contains(expected, args.ArgumentList);
        Assert.Single(args.ArgumentList, a => a.StartsWith("-mx=", StringComparison.Ordinal));
    }

    // ---- 12. ArgumentList, no concatenación ----

    [Fact]
    public void SevenZipArguments_ExponeUnaListaDeTokens_NoUnaLineaDeComandos()
    {
        var (args, rf) = Build();
        using var _ = rf;

        Assert.IsAssignableFrom<IReadOnlyList<string>>(args.ArgumentList);
        Assert.True(args.ArgumentList.Count >= 9);
        // Cada switch es su propio token: no hay un elemento que combine dos switches.
        Assert.Contains("-t7z", args.ArgumentList);
        Assert.Contains("-mx=5", args.ArgumentList);
        Assert.DoesNotContain(args.ArgumentList, a => a.Contains(" -", StringComparison.Ordinal));
        Assert.DoesNotContain("a -t7z", args.ArgumentList);
    }

    // ---- 2 / 14. contraseña redactada, nunca en diagnóstico ----

    [Fact]
    public void SevenZipArguments_Redacted_OcultaLaContrasena()
    {
        var (args, rf) = Build();
        using var _ = rf;

        Assert.DoesNotContain(Pwd, args.Redacted);
        Assert.DoesNotContain(Pwd, args.ToString());
        Assert.Contains("-p***", args.Redacted);
    }

    // ---- 3. contraseña nunca en el response file ----

    [Fact]
    public void Build_LaContrasenaNuncaLlegaAlResponseFile()
    {
        var (args, rf) = Build();
        using var _ = rf;

        var content = File.ReadAllText(rf.Path);
        Assert.DoesNotContain(Pwd, content);
        // la contraseña sí está en argv (es necesaria para ejecutar), pero solo ahí:
        Assert.Contains("-p" + Pwd, args.ArgumentList);
    }

    // ---- 4 / 5 / 6. rutas con espacios / unicode / especiales van al response file, no a argv ----

    [Fact]
    public void Build_RutasComplicadas_VanAlResponseFileNoAArgv()
    {
        var tricky = new[]
        {
            @"C:\mis documentos\a b c.txt",
            @"C:\café\ñandú.bin",
            @"C:\a & b\c $(x).txt",
        };

        var (args, rf) = Build(sources: tricky);
        using var _ = rf;

        var lines = File.ReadAllLines(rf.Path, System.Text.Encoding.UTF8);
        Assert.Equal(tricky, lines);

        foreach (var t in tricky)
        {
            Assert.DoesNotContain(args.ArgumentList, a => a == t);
        }
    }

    // ---- 11. lista vacía ----

    [Fact]
    public void Build_SinOrigenes_RechazadoAntesDeConstruirNada()
    {
        Assert.Throws<ArgumentException>(() => SevenZipRequest.Create(Array.Empty<string>(), Output, Pwd));
    }
}
