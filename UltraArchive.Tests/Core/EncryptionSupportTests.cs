using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;

namespace UltraArchive.Tests.Core;

/// <summary>
/// La matriz de "qué se puede cifrar al crear" (ROADMAP.md §3). Estas pruebas fijan el comportamiento
/// documentado: solo ZIP admite contraseña/AES-256 al crear; el resto falla de forma explícita en vez
/// de generar un archivo sin cifrar.
/// </summary>
public class EncryptionSupportTests
{
    private static CreateArchiveOptions Options(ArchiveFormat format, string? password, EncryptionMethod encryption, bool encryptNames = false) => new()
    {
        SourcePaths = new[] { @"C:\tmp\x.txt" },
        OutputPath = @"C:\tmp\out",
        Format = format,
        Password = password,
        Encryption = encryption,
        EncryptFileNames = encryptNames,
    };

    [Theory]
    [InlineData(ArchiveFormat.Zip, true, true)]
    [InlineData(ArchiveFormat.SevenZip, false, false)]
    [InlineData(ArchiveFormat.Tar, false, false)]
    [InlineData(ArchiveFormat.GZip, false, false)]
    [InlineData(ArchiveFormat.Rar, false, false)]
    public void CapacidadesPorFormato(ArchiveFormat format, bool password, bool aes)
    {
        Assert.Equal(password, EncryptionSupport.SupportsPasswordOnCreate(format));
        Assert.Equal(aes, EncryptionSupport.SupportsAes256OnCreate(format));
        Assert.False(EncryptionSupport.SupportsEncryptedFileNamesOnCreate(format));
    }

    [Fact]
    public void Validate_SinCifrado_NoLanza()
    {
        EncryptionSupport.Validate(Options(ArchiveFormat.Tar, password: null, EncryptionMethod.None));
        EncryptionSupport.Validate(Options(ArchiveFormat.Zip, password: "", EncryptionMethod.None));
    }

    [Fact]
    public void Validate_ZipConAes256YContrasena_NoLanza()
    {
        EncryptionSupport.Validate(Options(ArchiveFormat.Zip, "clave-fuerte-123", EncryptionMethod.Aes256));
    }

    [Theory]
    [InlineData(ArchiveFormat.SevenZip)]
    [InlineData(ArchiveFormat.Tar)]
    [InlineData(ArchiveFormat.GZip)]
    [InlineData(ArchiveFormat.Rar)]
    public void Validate_ContrasenaEnFormatoQueNoLaSoporta_Lanza(ArchiveFormat format)
    {
        Assert.Throws<EncryptionNotSupportedException>(() =>
            EncryptionSupport.Validate(Options(format, "clave-123", EncryptionMethod.Aes256)));
    }

    [Fact]
    public void Validate_EstadoIncoherente_ContrasenaSinMetodo_Lanza()
    {
        Assert.Throws<EncryptionNotSupportedException>(() =>
            EncryptionSupport.Validate(Options(ArchiveFormat.Zip, "clave-123", EncryptionMethod.None)));
    }

    [Fact]
    public void Validate_EstadoIncoherente_MetodoSinContrasena_Lanza()
    {
        Assert.Throws<EncryptionNotSupportedException>(() =>
            EncryptionSupport.Validate(Options(ArchiveFormat.Zip, password: null, EncryptionMethod.Aes256)));
    }

    [Fact]
    public void Validate_ZipCryptoAlCrear_Lanza()
    {
        Assert.Throws<EncryptionNotSupportedException>(() =>
            EncryptionSupport.Validate(Options(ArchiveFormat.Zip, "clave-123", EncryptionMethod.ZipCrypto)));
    }

    [Fact]
    public void Validate_CifrarNombresEnZip_Lanza()
    {
        Assert.Throws<EncryptionNotSupportedException>(() =>
            EncryptionSupport.Validate(Options(ArchiveFormat.Zip, "clave-123", EncryptionMethod.Aes256, encryptNames: true)));
    }

    // ---- Fase 6A, Paso 4: la capacidad de 7Z cifrado depende del binario 7zr ----

    private sealed class FakeCapability(bool available) : ISevenZipCapability
    {
        public bool CanCreateEncryptedSevenZip => available;
        public string? VerifiedExecutablePath => available ? @"C:\app\tools\7zr.exe" : null;
        public string StatusExplanation => available ? "verificado." : "7zr no disponible.";
    }

    [Fact]
    public void SevenZip_SinCapacidad_NoAdmiteContraseña()
    {
        Assert.False(EncryptionSupport.SupportsPasswordOnCreate(ArchiveFormat.SevenZip, sevenZip: null));
        Assert.False(EncryptionSupport.SupportsPasswordOnCreate(ArchiveFormat.SevenZip, new FakeCapability(false)));
        Assert.False(EncryptionSupport.SupportsAes256OnCreate(ArchiveFormat.SevenZip, new FakeCapability(false)));
        Assert.False(EncryptionSupport.SupportsEncryptedFileNamesOnCreate(ArchiveFormat.SevenZip, new FakeCapability(false)));
    }

    [Fact]
    public void SevenZip_ConCapacidad_AdmiteContraseñaAes256YCifradoDeNombres()
    {
        var cap = new FakeCapability(true);
        Assert.True(EncryptionSupport.SupportsPasswordOnCreate(ArchiveFormat.SevenZip, cap));
        Assert.True(EncryptionSupport.SupportsAes256OnCreate(ArchiveFormat.SevenZip, cap));
        Assert.True(EncryptionSupport.SupportsEncryptedFileNamesOnCreate(ArchiveFormat.SevenZip, cap));
    }

    [Fact]
    public void Validate_SevenZipCifrado_ConCapacidad_NoLanza()
    {
        EncryptionSupport.Validate(
            Options(ArchiveFormat.SevenZip, "clave-7z-123", EncryptionMethod.Aes256, encryptNames: true),
            new FakeCapability(true));
    }

    [Fact]
    public void Validate_SevenZipCifrado_SinCapacidad_Lanza()
    {
        Assert.Throws<EncryptionNotSupportedException>(() =>
            EncryptionSupport.Validate(
                Options(ArchiveFormat.SevenZip, "clave-7z-123", EncryptionMethod.Aes256),
                new FakeCapability(false)));
    }

    [Fact]
    public void Zip_ConOSinCapacidad7z_SiempreAdmiteCifrado()
    {
        Assert.True(EncryptionSupport.SupportsPasswordOnCreate(ArchiveFormat.Zip, new FakeCapability(false)));
        Assert.True(EncryptionSupport.SupportsPasswordOnCreate(ArchiveFormat.Zip, sevenZip: null));
    }

    [Theory]
    [InlineData(ArchiveFormat.Tar)]
    [InlineData(ArchiveFormat.GZip)]
    [InlineData(ArchiveFormat.Rar)]
    public void TarGzipRar_NuncaAdmitenCifrado_AunConCapacidad7z(ArchiveFormat format)
    {
        Assert.False(EncryptionSupport.SupportsPasswordOnCreate(format, new FakeCapability(true)));
    }
}
