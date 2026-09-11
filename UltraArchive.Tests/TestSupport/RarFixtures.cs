namespace UltraArchive.Tests.TestSupport;

/// <summary>
/// Archivos RAR de muestra para las pruebas del lector (UltraArchive no puede crear RAR, así que
/// estos fixtures se generaron una vez con WinRAR/Rar.exe y se incrustan aquí en Base64 para que la
/// suite sea autónoma y no dependa de tener WinRAR instalado).
///
/// Contenido común de los tres: una carpeta "src" con "src/readme.txt" ("hola rar") y
/// "src/sub/data.bin" ("contenido binario rar"). El cifrado usa la contraseña <see cref="Password"/>.
/// Todos son formato RAR5 (el único que Rar.exe 8.x sabe crear); la detección y lectura de RAR4
/// dependen de SharpCompress y no se cubren aquí por no disponer de un generador de RAR4.
/// </summary>
internal static class RarFixtures
{
    public const string Password = "Secreto123";

    public const string ReadmeContent = "hola rar";
    public const string DataBinContent = "contenido binario rar";

    /// <summary>RAR5 sin cifrar.</summary>
    private const string PlainBase64 =
        "UmFyIRoHAQAzkrXlCgEFBgAFAQGAgADBe1FUKgIDC4gABIgAIEGQHPWAAAAOc3JjL3JlYWRtZS50eHQKAwITZVyJQzvdAWhvbGEgcmFyi7UR0CwCAwuVAASVACCw+vDugAAAEHNyYy9zdWIvZGF0YS5iaW4KAwITZVyJQzvdAWNvbnRlbmlkbyBiaW5hcmlvIHJhcopr7hIdAgMLAAEAEIAAAAdzcmMvc3ViCgMCE2VciUM73QFIvCoAGQIDCwABABCAAAADc3JjCgMCE2VciUM73QEdd1ZRAwUEAA==";

    /// <summary>RAR5 con datos y cabecera cifrados (Rar.exe -hp), contraseña <see cref="Password"/>.</summary>
    private const string EncryptedBase64 =
        "UmFyIRoHAQCxHiGlIQQAAAEPXxKza4L4iCwQGC3jrFJqXG9ZZve4cFUSQ2UJjflPcc2F28jZRBtLsy7Ese5chV+d/hC3C4k0KvgADbDEi2KC+fCXrCocr3Ohi4ausLdvs8wzQbNNhpkQyiJ4N+zEgZjqmlXpdI+UwwAfkKNBnH/w8dO2ZrQDO/PsxWbxF0+s9HZXH0kCIAk7ezuYAj+6CGyTb32i6N9f2BaM5Wfeajj2grpUAX953zdBGKT5N4zf575b11js7ZEth05QAhQLLw22X/MacKGKi2oDfgko01W0yvHVFww0gd0VwGtaBZqKp+3WZalB3CWVdU4PDIN5hpTTqGEeYSm+GfOsCi7sE6BL1vIvWDmIj27xlzfcvdfAHKtVwjOqI8WPdh3gTFg65f/FwdIEVEdAmEG3UKS5KDTRHjqYLm8ySewZTrGmOtWa/hnTHhnBX6vqptfzD2ZEPWFfNeJ5iIztgj2UK6gNGnewvvGC0+ocVnK+wUBgJXfYMrCjcY+K01L7FaJaSD5TfmhcjHPWybUUOfJfoVQfiduSnEIddmX9PPVCoYIS69cndVUsWNvsDVNDj0th/lgwSYnUYHxurHKnnEVehdFY8hXPTJ5FUieDGOFLrHQai1IiNx5JwAXsuRi8um8+PdbHr3NQk8qgCymdDOBFhEpW4uWraPjrOBIwJOjnJ3sDbg==";

    /// <summary>Escribe el RAR5 sin cifrar en <paramref name="path"/> y devuelve esa ruta.</summary>
    public static string WritePlain(string path)
    {
        File.WriteAllBytes(path, Convert.FromBase64String(PlainBase64));
        return path;
    }

    /// <summary>Escribe el RAR5 cifrado en <paramref name="path"/> y devuelve esa ruta.</summary>
    public static string WriteEncrypted(string path)
    {
        File.WriteAllBytes(path, Convert.FromBase64String(EncryptedBase64));
        return path;
    }
}
