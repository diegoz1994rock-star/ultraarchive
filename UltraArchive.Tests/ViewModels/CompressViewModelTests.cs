using System.IO;
using UltraArchive.App.ViewModels;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;

namespace UltraArchive.Tests.ViewModels;

/// <summary>
/// Fase 4: la ventana "Comprimir" no debe lanzar la operación si la confirmación de contraseña no
/// coincide (o falta), ni retener la contraseña en memoria más de lo necesario.
/// </summary>
public class CompressViewModelTests
{
    private static CompressViewModel NewViewModel(bool sevenZipAvailable = false)
    {
        var vm = new CompressViewModel(new StubEngineFactory(), new StubFileDialogService(), new StubSevenZipCapability(sevenZipAvailable));
        vm.Sources.Add(@"C:\tmp\algo.txt");
        vm.OutputPath = @"C:\tmp\salida.zip";
        return vm;
    }

    [Fact]
    public void ValidateBeforeStart_ZipCifradoConContrasenasQueNoCoinciden_DevuelvePasswordMismatch()
    {
        var vm = NewViewModel();
        vm.SelectedFormat = CompressViewModel.Formats.Single(f => f.Format == ArchiveFormat.Zip);
        vm.EncryptionEnabled = true;
        vm.UpdatePassword("clave-uno");
        vm.UpdateConfirmPassword("clave-DOS");

        Assert.Equal(CompressViewModel.StartValidation.PasswordMismatch, vm.ValidateBeforeStart());
    }

    [Fact]
    public void ValidateBeforeStart_ZipCifradoSinContrasena_DevuelvePasswordEmpty()
    {
        var vm = NewViewModel();
        vm.EncryptionEnabled = true;

        Assert.Equal(CompressViewModel.StartValidation.PasswordEmpty, vm.ValidateBeforeStart());
    }

    [Fact]
    public void ValidateBeforeStart_ContrasenasCoinciden_DevuelveOk()
    {
        var vm = NewViewModel();
        vm.EncryptionEnabled = true;
        vm.UpdatePassword("clave-igual");
        vm.UpdateConfirmPassword("clave-igual");

        Assert.Equal(CompressViewModel.StartValidation.Ok, vm.ValidateBeforeStart());
    }

    [Fact]
    public void ValidateBeforeStart_SinCifrado_IgnoraLasContrasenas()
    {
        var vm = NewViewModel();
        vm.EncryptionEnabled = false;
        vm.UpdatePassword("algo");
        vm.UpdateConfirmPassword("distinto");

        Assert.Equal(CompressViewModel.StartValidation.Ok, vm.ValidateBeforeStart());
    }

    [Fact]
    public void FormatoSinSoporteDeCifrado_NoMuestraCamposDeContrasena()
    {
        var vm = NewViewModel();
        vm.SelectedFormat = CompressViewModel.Formats.Single(f => f.Format == ArchiveFormat.Tar);
        vm.EncryptionEnabled = true;

        Assert.False(vm.FormatSupportsEncryption);
        Assert.False(vm.ShowPasswordFields);
        Assert.True(vm.ShowEncryptionUnavailableNote);
        // Aunque la casilla esté marcada, al no soportarlo el formato la validación no exige contraseña.
        Assert.Equal(CompressViewModel.StartValidation.Ok, vm.ValidateBeforeStart());
    }

    [Fact]
    public void SevenZip_SinBinarioVerificado_NoOfreceCifrado()
    {
        var vm = NewViewModel(sevenZipAvailable: false);
        vm.SelectedFormat = CompressViewModel.Formats.Single(f => f.Format == ArchiveFormat.SevenZip);
        vm.EncryptionEnabled = true;

        Assert.False(vm.FormatSupportsEncryption);
        Assert.False(vm.ShowPasswordFields);
        Assert.True(vm.ShowEncryptionUnavailableNote);
    }

    [Fact]
    public void SevenZip_ConBinarioVerificado_OfreceCifrado()
    {
        var vm = NewViewModel(sevenZipAvailable: true);
        vm.SelectedFormat = CompressViewModel.Formats.Single(f => f.Format == ArchiveFormat.SevenZip);
        vm.EncryptionEnabled = true;
        vm.UpdatePassword("clave-7z");
        vm.UpdateConfirmPassword("clave-7z");

        Assert.True(vm.FormatSupportsEncryption);
        Assert.True(vm.ShowPasswordFields);
        Assert.False(vm.ShowEncryptionUnavailableNote);
        Assert.Equal(CompressViewModel.StartValidation.Ok, vm.ValidateBeforeStart());
    }

    [Fact]
    public void Zip_OfreceCifradoIndependientementeDe7zr()
    {
        var vm = NewViewModel(sevenZipAvailable: false);
        vm.SelectedFormat = CompressViewModel.Formats.Single(f => f.Format == ArchiveFormat.Zip);

        Assert.True(vm.FormatSupportsEncryption);
    }

    [Fact]
    public void ClearSensitiveData_DejaLaValidacionComoSinContrasena()
    {
        var vm = NewViewModel();
        vm.EncryptionEnabled = true;
        vm.UpdatePassword("secreta");
        vm.UpdateConfirmPassword("secreta");
        Assert.Equal(CompressViewModel.StartValidation.Ok, vm.ValidateBeforeStart());

        vm.ClearSensitiveData();

        Assert.Equal(CompressViewModel.StartValidation.PasswordEmpty, vm.ValidateBeforeStart());
    }

    [Fact]
    public void Formats_IncluyeRarPeroMarcadoComoNoCreable()
    {
        var rar = CompressViewModel.Formats.SingleOrDefault(f => f.Format == ArchiveFormat.Rar);

        Assert.NotNull(rar);
        Assert.False(rar!.CanCreate);
        // Los formatos que sí se crean siguen marcados como creables.
        Assert.All(
            CompressViewModel.Formats.Where(f => f.Format is ArchiveFormat.Zip or ArchiveFormat.SevenZip or ArchiveFormat.Tar or ArchiveFormat.GZip),
            f => Assert.True(f.CanCreate));
    }

    [Fact]
    public void ValidateBeforeStart_ConRarSeleccionado_DevuelveFormatNotCreatable()
    {
        var vm = NewViewModel();
        vm.SelectedFormat = CompressViewModel.Formats.Single(f => f.Format == ArchiveFormat.Rar);

        Assert.Equal(CompressViewModel.StartValidation.FormatNotCreatable, vm.ValidateBeforeStart());
    }

    // ---------------- División en volúmenes ----------------

    private static CompressViewModel SplitVm(ArchiveFormat format, bool sevenZipAvailable = true)
    {
        var vm = NewViewModel(sevenZipAvailable);
        vm.SelectedFormat = CompressViewModel.Formats.Single(f => f.Format == format);
        return vm;
    }

    [Fact]
    public void Split_NoDividir_EsElPredeterminado_YResuelveANull()
    {
        var vm = SplitVm(ArchiveFormat.SevenZip);
        Assert.Equal(SplitMethod.None, vm.SelectedSplitChoice.Method);
        Assert.Null(vm.ResolveSplitVolumeBytes(0));
        Assert.Equal(CompressViewModel.StartValidation.Ok, vm.ValidateBeforeStart());
    }

    [Fact]
    public void Split_7ZDisponible_PresetSeResuelveAlTamanoEnBytes()
    {
        var vm = SplitVm(ArchiveFormat.SevenZip);
        vm.SelectedSplitChoice = CompressViewModel.SplitChoices.First(c => c.Display == "5 GB");

        Assert.True(vm.SplitAvailable);
        Assert.Equal(5L * 1024 * 1024 * 1024, vm.ResolveSplitVolumeBytes(0));
        Assert.Equal(CompressViewModel.StartValidation.Ok, vm.ValidateBeforeStart());
    }

    [Fact]
    public void Split_Personalizado_ConvierteValorYUnidad()
    {
        var vm = SplitVm(ArchiveFormat.SevenZip);
        vm.SelectedSplitChoice = CompressViewModel.SplitChoices.Single(c => c.Custom);
        vm.CustomSplitValue = 250;
        vm.CustomSplitUnit = SizeUnit.MB;

        Assert.True(vm.ShowCustomSplitSize);
        Assert.Equal(250L * 1024 * 1024, vm.ResolveSplitVolumeBytes(0));
    }

    [Fact]
    public void Split_NumeroDePartes_RepartteElTotal()
    {
        var vm = SplitVm(ArchiveFormat.SevenZip);
        vm.SelectedSplitChoice = CompressViewModel.SplitChoices.Single(c => c.Method == SplitMethod.NumberOfParts);
        vm.SplitPartCount = 4;

        Assert.True(vm.ShowSplitPartCount);
        var total = 20L * 1024 * 1024 * 1024;
        Assert.Equal(5L * 1024 * 1024 * 1024, vm.ResolveSplitVolumeBytes(total));
    }

    [Fact]
    public void Split_NumeroDePartes_SinTamanoOrigen_Lanza()
    {
        var vm = SplitVm(ArchiveFormat.SevenZip);
        vm.SelectedSplitChoice = CompressViewModel.SplitChoices.Single(c => c.Method == SplitMethod.NumberOfParts);
        Assert.Throws<ArgumentException>(() => vm.ResolveSplitVolumeBytes(0));
    }

    [Fact]
    public void Split_EnZip_NoDisponible_YValidacionLoRechaza()
    {
        var vm = SplitVm(ArchiveFormat.Zip);
        vm.SelectedSplitChoice = CompressViewModel.SplitChoices.First(c => c.Display == "1 GB");

        Assert.False(vm.SplitAvailable);
        Assert.True(vm.ShowSplitUnavailableNote);
        Assert.Equal(CompressViewModel.StartValidation.SplitNotAvailable, vm.ValidateBeforeStart());
    }

    [Fact]
    public void Split_7ZSin7zr_NoDisponible()
    {
        var vm = SplitVm(ArchiveFormat.SevenZip, sevenZipAvailable: false);
        vm.SelectedSplitChoice = CompressViewModel.SplitChoices.First(c => c.Display == "1 GB");

        Assert.False(vm.SplitAvailable);
        Assert.Equal(CompressViewModel.StartValidation.SplitNotAvailable, vm.ValidateBeforeStart());
    }

    [Fact]
    public void Split_CambiarFormatoAUnoSinSoporte_ReseteaANoDividir()
    {
        var vm = SplitVm(ArchiveFormat.SevenZip);
        vm.SelectedSplitChoice = CompressViewModel.SplitChoices.First(c => c.Display == "2 GB");
        Assert.NotEqual(SplitMethod.None, vm.SelectedSplitChoice.Method);

        vm.SelectedFormat = CompressViewModel.Formats.Single(f => f.Format == ArchiveFormat.Tar);

        Assert.Equal(SplitMethod.None, vm.SelectedSplitChoice.Method);
    }

    [Fact]
    public void Split_PartCountMenorQue2_ValidacionInvalida()
    {
        var vm = SplitVm(ArchiveFormat.SevenZip);
        vm.SelectedSplitChoice = CompressViewModel.SplitChoices.Single(c => c.Method == SplitMethod.NumberOfParts);
        vm.SplitPartCount = 1;

        Assert.Equal(CompressViewModel.StartValidation.SplitSettingsInvalid, vm.ValidateBeforeStart());
    }

    // ---------------- Añadir orígenes / arrastrar y soltar ----------------

    [Fact]
    public void AddPaths_AnadeSoloRutasExistentes_SinDuplicados()
    {
        var vm = new CompressViewModel(new StubEngineFactory(), new StubFileDialogService(), new StubSevenZipCapability(false));
        Assert.False(vm.HasSources);

        var dir = Directory.GetCurrentDirectory();
        var file = System.Reflection.Assembly.GetExecutingAssembly().Location;

        var added = vm.AddPaths(new[] { dir, file, @"C:\no\existe\nada-12345.xyz", "", "   ", dir /* duplicado */ });

        Assert.Equal(2, added);
        Assert.True(vm.HasSources);
        Assert.Equal(2, vm.Sources.Count);
        Assert.Contains(dir, vm.Sources);
        Assert.Contains(file, vm.Sources);
    }

    [Fact]
    public void AddPaths_QuitaComillasYEspacios()
    {
        var vm = new CompressViewModel(new StubEngineFactory(), new StubFileDialogService(), new StubSevenZipCapability(false));
        var dir = Directory.GetCurrentDirectory();

        vm.AddPaths(new[] { $"  \"{dir}\"  " });

        Assert.Contains(dir, vm.Sources);
    }

    [Fact]
    public void AddPaths_Null_NoLanza()
    {
        var vm = new CompressViewModel(new StubEngineFactory(), new StubFileDialogService(), new StubSevenZipCapability(false));
        Assert.Equal(0, vm.AddPaths(null));
    }

    [Fact]
    public void HasSources_SeActualizaAlAnadirYQuitar()
    {
        var vm = new CompressViewModel(new StubEngineFactory(), new StubFileDialogService(), new StubSevenZipCapability(false));
        var changed = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.HasSources)) changed++; };

        vm.AddPaths(new[] { Directory.GetCurrentDirectory() });
        Assert.True(vm.HasSources);

        vm.RemoveSources(new[] { Directory.GetCurrentDirectory() });
        Assert.False(vm.HasSources);
        Assert.True(changed >= 2);
    }

    private sealed class StubEngineFactory : IArchiveEngineFactory
    {
        public void RegisterReader(ArchiveFormat format, Func<IArchiveReader> readerFactory) { }
        public void RegisterWriter(ArchiveFormat format, Func<IArchiveWriter> writerFactory) { }
        public bool CanRead(ArchiveFormat format) => false;
        public bool CanWrite(ArchiveFormat format) => true;
        public IReadOnlyCollection<ArchiveFormat> SupportedReadFormats => Array.Empty<ArchiveFormat>();
        public IReadOnlyCollection<ArchiveFormat> SupportedWriteFormats => Array.Empty<ArchiveFormat>();
        public IArchiveReader CreateReader(ArchiveFormat format) => throw new NotSupportedException();
        public IArchiveWriter CreateWriter(ArchiveFormat format) => throw new NotSupportedException();
    }

    private sealed class StubFileDialogService : IFileDialogService
    {
        public IReadOnlyList<string> ShowOpenFilesDialog(string title) => Array.Empty<string>();
        public string? ShowSelectFolderDialog(string title) => null;
        public string? ShowOpenArchiveDialog() => null;
        public string? ShowOpenIsoDialog() => null;
        public string? ShowSaveArchiveDialog(string title, string suggestedFileName, string filter) => null;
    }

    private sealed class StubSevenZipCapability(bool available) : ISevenZipCapability
    {
        public bool CanCreateEncryptedSevenZip => available;
        public string? VerifiedExecutablePath => available ? @"C:\app\tools\7zr.exe" : null;
        public string StatusExplanation => available ? "7zr verificado." : "7zr.exe no disponible.";
    }
}
