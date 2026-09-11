using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using UltraArchive.App.Commands;
using UltraArchive.App.Resources;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;

namespace UltraArchive.App.ViewModels;

/// <summary>
/// Una entrada del ComboBox de formato. <see cref="CanCreate"/> es false para formatos que
/// UltraArchive sabe leer pero <b>no</b> puede crear (hoy: RAR, por la restricción legal de su
/// formato propietario). Se muestran igualmente en la lista, desactivados, para que quede claro por
/// qué no se pueden elegir en vez de que "desaparezcan" sin explicación.
/// </summary>
public sealed record CompressFormatOption(ArchiveFormat Format, string Display, bool CanCreate = true)
{
    public override string ToString() => Display;
}

/// <summary>
/// Una opción del desplegable "Dividir archivo". <see cref="Bytes"/> fijo para los presets;
/// <see cref="Custom"/> pide un tamaño al usuario; <see cref="SplitMethod.NumberOfParts"/> pide un
/// número de partes. <see cref="SplitMethod.None"/> = compresión normal, sin cambios.
/// </summary>
public sealed record SplitChoice(string Display, SplitMethod Method, long? Bytes = null, bool Custom = false)
{
    public override string ToString() => Display;
}

/// <summary>
/// ViewModel del diálogo "Comprimir". Construye un <see cref="CreateArchiveOptions"/> a partir de la
/// selección del usuario y lo pasa al escritor real resuelto vía <see cref="IArchiveEngineFactory"/>.
///
/// Cifrado (Fase 4): solo se ofrece contraseña/AES-256 para los formatos que
/// <see cref="EncryptionSupport"/> declara compatibles (ZIP siempre; 7Z solo si hay un 7zr.exe
/// verificado, vía <see cref="ISevenZipCapability"/>). La contraseña y su
/// confirmación se reciben del code-behind (un <c>PasswordBox</c> no se vincula a un ViewModel) y
/// solo viven en memoria mientras dura la ventana: se borran al terminar la operación y al cerrar.
/// División en volúmenes: pendiente de una fase posterior.
/// </summary>
public sealed class CompressViewModel : ViewModelBase
{
    public static readonly IReadOnlyList<CompressFormatOption> Formats = new[]
    {
        new CompressFormatOption(ArchiveFormat.Zip, "ZIP"),
        new CompressFormatOption(ArchiveFormat.SevenZip, "7Z"),
        new CompressFormatOption(ArchiveFormat.Tar, "TAR"),
        new CompressFormatOption(ArchiveFormat.GZip, "GZIP (o TAR.GZ si hay varios orígenes)"),
        // RAR: solo lectura. El formato de compresión RAR es propietario y su licencia (UnRAR)
        // prohíbe usar el código para crear un compresor compatible; ninguna librería libre
        // (SharpCompress incluido) trae un escritor RAR. Se lista desactivado, no oculto.
        new CompressFormatOption(ArchiveFormat.Rar, "RAR — solo lectura (no se puede crear: restricción legal del formato)", CanCreate: false),
    };

    /// <summary>
    /// Opciones del desplegable "Dividir archivo". La primera ("No dividir") deja la compresión
    /// exactamente como está hoy. Los tamaños son binarios (MiB/GiB).
    /// </summary>
    public static readonly IReadOnlyList<SplitChoice> SplitChoices = new[]
    {
        new SplitChoice("No dividir", SplitMethod.None),
        new SplitChoice("100 MB", SplitMethod.MaxSizePerVolume, 100L * SplitCalculator.BytesPerMB),
        new SplitChoice("500 MB", SplitMethod.MaxSizePerVolume, 500L * SplitCalculator.BytesPerMB),
        new SplitChoice("1 GB", SplitMethod.MaxSizePerVolume, 1L * SplitCalculator.BytesPerGB),
        new SplitChoice("2 GB", SplitMethod.MaxSizePerVolume, 2L * SplitCalculator.BytesPerGB),
        new SplitChoice("4 GB", SplitMethod.MaxSizePerVolume, 4L * SplitCalculator.BytesPerGB),
        new SplitChoice("5 GB", SplitMethod.MaxSizePerVolume, 5L * SplitCalculator.BytesPerGB),
        new SplitChoice("10 GB", SplitMethod.MaxSizePerVolume, 10L * SplitCalculator.BytesPerGB),
        new SplitChoice("20 GB", SplitMethod.MaxSizePerVolume, 20L * SplitCalculator.BytesPerGB),
        new SplitChoice("Personalizado…", SplitMethod.MaxSizePerVolume, Custom: true),
        new SplitChoice("Número de partes…", SplitMethod.NumberOfParts),
    };

    private readonly IArchiveEngineFactory _engineFactory;
    private readonly IFileDialogService _fileDialogService;
    private readonly ISevenZipCapability _sevenZipCapability;
    private CancellationTokenSource? _cts;

    private string _outputPath = string.Empty;
    private CompressFormatOption _selectedFormat = Formats[0];
    private CompressionLevel _selectedLevel = CompressionLevel.Normal;
    private bool _preserveFolderStructure = true;
    private bool _deleteSourceAfterCompress;
    private bool _encryptionEnabled;

    private SplitChoice _selectedSplitChoice = SplitChoices[0];
    private double _customSplitValue = 5;
    private SizeUnit _customSplitUnit = SizeUnit.GB;
    private int _splitPartCount = 4;

    // Contraseña y confirmación: solo en memoria mientras la ventana está abierta. Nunca se persisten
    // ni se registran; se limpian en ClearSensitiveData() al terminar o cancelar.
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;

    private string _statusMessage;
    private bool _isBusy;
    private double _progressPercent;
    private bool _isProgressIndeterminate;
    private bool _succeeded;

    public CompressViewModel(IArchiveEngineFactory engineFactory, IFileDialogService fileDialogService, ISevenZipCapability sevenZipCapability)
    {
        _engineFactory = engineFactory;
        _fileDialogService = fileDialogService;
        _sevenZipCapability = sevenZipCapability;
        _statusMessage = Strings.CompressStatusReady;

        Sources.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSources));

        AddFilesCommand = new RelayCommand(AddFiles, () => !IsBusy);
        AddFolderCommand = new RelayCommand(AddFolder, () => !IsBusy);
        BrowseOutputCommand = new RelayCommand(BrowseOutput, () => !IsBusy);
        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsBusy, HandleUnexpectedError);
        CancelOperationCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
    }

    public ObservableCollection<string> Sources { get; } = new();

    /// <summary>True si aún no se ha añadido ningún origen (para mostrar la zona de "arrastra aquí").</summary>
    public bool HasSources => Sources.Count > 0;

    public IReadOnlyList<CompressFormatOption> AvailableFormats => Formats;

    public IReadOnlyList<CompressionLevel> AvailableLevels { get; } = Enum.GetValues<CompressionLevel>();

    public RelayCommand AddFilesCommand { get; }
    public RelayCommand AddFolderCommand { get; }
    public RelayCommand BrowseOutputCommand { get; }
    public AsyncRelayCommand StartCommand { get; }
    public RelayCommand CancelOperationCommand { get; }

    /// <summary>Se dispara cuando la compresión termina con éxito, para que la vista cierre la ventana.</summary>
    public event EventHandler? RequestClose;

    public bool Succeeded
    {
        get => _succeeded;
        private set => SetProperty(ref _succeeded, value);
    }

    public string OutputPath
    {
        get => _outputPath;
        set => SetProperty(ref _outputPath, value);
    }

    public CompressFormatOption SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (SetProperty(ref _selectedFormat, value))
            {
                OnPropertyChanged(nameof(FormatSupportsEncryption));
                OnPropertyChanged(nameof(ShowPasswordFields));
                OnPropertyChanged(nameof(ShowEncryptionUnavailableNote));

                OnPropertyChanged(nameof(SplitAvailable));
                OnPropertyChanged(nameof(ShowSplitUnavailableNote));
                if (!SplitAvailable && _selectedSplitChoice.Method != SplitMethod.None)
                {
                    SelectedSplitChoice = SplitChoices[0]; // formato no soporta división → volver a "No dividir"
                }
            }
        }
    }

    // ---------- División en volúmenes ----------

    public IReadOnlyList<SplitChoice> AvailableSplitChoices => SplitChoices;

    public IReadOnlyList<SizeUnit> AvailableSizeUnits { get; } = new[] { SizeUnit.MB, SizeUnit.GB };

    public SplitChoice SelectedSplitChoice
    {
        get => _selectedSplitChoice;
        set
        {
            if (SetProperty(ref _selectedSplitChoice, value ?? SplitChoices[0]))
            {
                OnPropertyChanged(nameof(ShowCustomSplitSize));
                OnPropertyChanged(nameof(ShowSplitPartCount));
                OnPropertyChanged(nameof(ShowSplitUnavailableNote));
            }
        }
    }

    /// <summary>Tamaño para "Personalizado…".</summary>
    public double CustomSplitValue
    {
        get => _customSplitValue;
        set => SetProperty(ref _customSplitValue, value);
    }

    public SizeUnit CustomSplitUnit
    {
        get => _customSplitUnit;
        set => SetProperty(ref _customSplitUnit, value);
    }

    /// <summary>Número de partes para "Número de partes…".</summary>
    public int SplitPartCount
    {
        get => _splitPartCount;
        set => SetProperty(ref _splitPartCount, value);
    }

    /// <summary>True si el formato seleccionado admite división y hay un 7zr.exe verificado.</summary>
    public bool SplitAvailable =>
        SplitSupport.SupportsSplitOnCreate(SelectedFormat.Format) && _sevenZipCapability.CanCreateEncryptedSevenZip;

    public bool ShowCustomSplitSize => _selectedSplitChoice.Custom;

    public bool ShowSplitPartCount => _selectedSplitChoice.Method == SplitMethod.NumberOfParts;

    /// <summary>Se muestra si el usuario pidió dividir pero el formato/entorno no lo permite.</summary>
    public bool ShowSplitUnavailableNote => _selectedSplitChoice.Method != SplitMethod.None && !SplitAvailable;

    /// <summary>
    /// Convierte la selección de la UI en el tamaño (bytes) por volumen para
    /// <see cref="CreateArchiveOptions.SplitVolumeSizeBytes"/>, o null si "No dividir".
    /// <paramref name="totalSourceBytes"/> solo se usa en el modo "Número de partes".
    /// Lanza <see cref="ArgumentException"/> con un mensaje claro si los valores no son válidos.
    /// </summary>
    public long? ResolveSplitVolumeBytes(long totalSourceBytes)
    {
        var choice = _selectedSplitChoice;
        switch (choice.Method)
        {
            case SplitMethod.None:
                return null;

            case SplitMethod.MaxSizePerVolume when !choice.Custom:
                return choice.Bytes;

            case SplitMethod.MaxSizePerVolume: // personalizado
                try
                {
                    var bytes = SplitCalculator.ToBytes(_customSplitValue, _customSplitUnit);
                    SplitCalculator.EnsureValidVolumeSize(bytes);
                    return bytes;
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new ArgumentException(ex.Message);
                }

            case SplitMethod.NumberOfParts:
                if (_splitPartCount < 2)
                {
                    throw new ArgumentException("El número de partes debe ser al menos 2.");
                }

                if (totalSourceBytes <= 0)
                {
                    throw new ArgumentException(
                        "No se puede dividir por número de partes: no se pudo determinar el tamaño de los orígenes. " +
                        "Usa un tamaño máximo por parte.");
                }

                return SplitCalculator.BytesPerVolumeForPartCount(totalSourceBytes, _splitPartCount);

            default:
                return null;
        }
    }

    /// <summary>True si el formato seleccionado admite protección con contraseña al crearlo (ZIP siempre; 7Z si hay 7zr verificado).</summary>
    public bool FormatSupportsEncryption => EncryptionSupport.SupportsPasswordOnCreate(SelectedFormat.Format, _sevenZipCapability);

    /// <summary>Casilla "Proteger con contraseña". Solo tiene efecto si <see cref="FormatSupportsEncryption"/>.</summary>
    public bool EncryptionEnabled
    {
        get => _encryptionEnabled;
        set
        {
            if (SetProperty(ref _encryptionEnabled, value))
            {
                OnPropertyChanged(nameof(ShowPasswordFields));
                OnPropertyChanged(nameof(ShowEncryptionUnavailableNote));
            }
        }
    }

    public bool ShowPasswordFields => EncryptionEnabled && FormatSupportsEncryption;

    public bool ShowEncryptionUnavailableNote => EncryptionEnabled && !FormatSupportsEncryption;

    /// <summary>Recibe la contraseña desde el <c>PasswordBox</c> del code-behind (no vinculable a un ViewModel).</summary>
    public void UpdatePassword(string value) => _password = value ?? string.Empty;

    /// <summary>Recibe la confirmación de contraseña desde el <c>PasswordBox</c> del code-behind.</summary>
    public void UpdateConfirmPassword(string value) => _confirmPassword = value ?? string.Empty;

    /// <summary>Borra de memoria la contraseña y su confirmación. Lo llama también el code-behind al cerrar la ventana.</summary>
    public void ClearSensitiveData()
    {
        _password = string.Empty;
        _confirmPassword = string.Empty;
    }

    public CompressionLevel SelectedLevel
    {
        get => _selectedLevel;
        set => SetProperty(ref _selectedLevel, value);
    }

    public bool PreserveFolderStructure
    {
        get => _preserveFolderStructure;
        set => SetProperty(ref _preserveFolderStructure, value);
    }

    public bool DeleteSourceAfterCompress
    {
        get => _deleteSourceAfterCompress;
        set => SetProperty(ref _deleteSourceAfterCompress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    /// <summary>Quita de <see cref="Sources"/> las rutas seleccionadas en la vista (reenviadas desde el code-behind del ListBox).</summary>
    public void RemoveSources(IEnumerable<string> paths)
    {
        foreach (var path in paths.ToList())
        {
            Sources.Remove(path);
        }
    }

    private void AddFiles() => AddPaths(_fileDialogService.ShowOpenFilesDialog(Strings.CompressButtonAddFiles));

    private void AddFolder()
    {
        var folder = _fileDialogService.ShowSelectFolderDialog(Strings.CompressButtonAddFolder);
        if (folder is not null)
        {
            AddPaths(new[] { folder });
        }
    }

    /// <summary>
    /// Añade rutas de archivos y/o carpetas a la lista de orígenes. Usado por los botones "Añadir" y
    /// por el arrastrar-y-soltar. Ignora rutas vacías, duplicadas (sin distinguir mayúsculas) o que no
    /// existen en disco. Devuelve cuántas se añadieron realmente.
    /// </summary>
    public int AddPaths(IEnumerable<string>? paths)
    {
        if (paths is null)
        {
            return 0;
        }

        var added = 0;
        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var path = raw.Trim().Trim('"');
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                continue;
            }

            if (Sources.Any(s => string.Equals(s, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            Sources.Add(path);
            added++;
        }

        return added;
    }

    /// <summary>
    /// True si la ventana debe lanzar la compresión sola nada más abrirse (verbos "Comprimir aquí",
    /// "…como .ZIP", "…como .7Z" del menú contextual). Lo consulta el code-behind en <c>Loaded</c>.
    /// </summary>
    public bool AutoStartRequested { get; private set; }

    /// <summary>
    /// Prepara la ventana "Comprimir" a partir de una invocación del menú contextual del Explorador.
    /// <b>No</b> duplica lógica: solo rellena los mismos campos que rellenaría el usuario a mano
    /// (orígenes, formato, ruta de salida) y, si <paramref name="autoStart"/>, marca que se lance sola.
    /// </summary>
    /// <param name="sources">Archivos y/o carpetas seleccionados en el Explorador.</param>
    /// <param name="format">Formato forzado (ZIP/7Z) o null para dejar el predeterminado.</param>
    /// <param name="preferSplit">Si es true y el formato admite división, deja visible la sección de división.</param>
    /// <param name="autoStart">Si es true, se calcula una ruta de salida junto al primer origen y se comprime al abrir.</param>
    public void InitializeFromShell(IEnumerable<string> sources, ArchiveFormat? format, bool preferSplit, bool autoStart)
    {
        AddPaths(sources);

        if (format is { } wanted)
        {
            var option = Formats.FirstOrDefault(o => o.Format == wanted && o.CanCreate);
            if (option is not null)
            {
                SelectedFormat = option;
            }
        }

        if (preferSplit && SplitAvailable && SelectedSplitChoice.Method == SplitMethod.None)
        {
            // Deja preseleccionado "Número de partes…": la sección de división queda a la vista y el
            // usuario solo tiene que confirmar/ajustar. No se auto-lanza (necesita su decisión).
            SelectedSplitChoice = SplitChoices.First(c => c.Method == SplitMethod.NumberOfParts);
        }

        if (autoStart && Sources.Count > 0)
        {
            OutputPath = SuggestShellOutputPath(Sources[0], SelectedFormat.Format);
            AutoStartRequested = true;
        }
    }

    /// <summary>
    /// Ruta de salida por defecto para los verbos "Comprimir aquí / como ZIP / como 7Z": junto al
    /// primer origen, con su nombre y la extensión del formato, evitando sobrescribir un archivo ya
    /// existente ("nombre (2).zip").
    /// </summary>
    private static string SuggestShellOutputPath(string firstSource, ArchiveFormat format)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(firstSource))
                        ?? Directory.GetCurrentDirectory();

        var baseName = Directory.Exists(firstSource)
            ? new DirectoryInfo(firstSource).Name
            : Path.GetFileNameWithoutExtension(firstSource);

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "archivo";
        }

        var extension = format switch
        {
            ArchiveFormat.Zip => ".zip",
            ArchiveFormat.SevenZip => ".7z",
            ArchiveFormat.Tar => ".tar",
            ArchiveFormat.GZip => ".tar.gz",
            _ => ".zip",
        };

        return UniquePath.NextAvailable(Path.Combine(directory, baseName + extension));
    }

    private void BrowseOutput()
    {
        var (filter, suggestedName) = SuggestedOutputFor(SelectedFormat.Format);
        var path = _fileDialogService.ShowSaveArchiveDialog(Strings.CompressLabelOutput, suggestedName, filter);
        if (path is not null)
        {
            OutputPath = path;
        }
    }

    /// <summary>Resultado de la comprobación previa a lanzar la compresión (ver <see cref="ValidateBeforeStart"/>).</summary>
    public enum StartValidation
    {
        Ok,
        NoSources,
        NoOutput,
        PasswordEmpty,
        PasswordMismatch,
        FormatNotCreatable,
        SplitNotAvailable,
        SplitSettingsInvalid,
    }

    /// <summary>
    /// Comprueba, sin efectos secundarios, si la operación puede lanzarse: hay orígenes y destino, y
    /// —si el cifrado está activo y el formato lo admite— la contraseña no está vacía y coincide con
    /// su confirmación. Método puro y público para poder cubrirlo con pruebas sin UI.
    /// </summary>
    public StartValidation ValidateBeforeStart()
    {
        if (!SelectedFormat.CanCreate)
        {
            return StartValidation.FormatNotCreatable;
        }

        if (Sources.Count == 0)
        {
            return StartValidation.NoSources;
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            return StartValidation.NoOutput;
        }

        if (EncryptionEnabled && FormatSupportsEncryption)
        {
            if (string.IsNullOrEmpty(_password))
            {
                return StartValidation.PasswordEmpty;
            }

            if (!string.Equals(_password, _confirmPassword, StringComparison.Ordinal))
            {
                return StartValidation.PasswordMismatch;
            }
        }

        if (_selectedSplitChoice.Method != SplitMethod.None)
        {
            if (!SplitAvailable)
            {
                return StartValidation.SplitNotAvailable;
            }

            // Comprobaciones que no dependen del tamaño de origen (eso se valida al arrancar).
            if (_selectedSplitChoice.Custom && (double.IsNaN(_customSplitValue) || _customSplitValue <= 0))
            {
                return StartValidation.SplitSettingsInvalid;
            }

            if (_selectedSplitChoice.Method == SplitMethod.NumberOfParts && _splitPartCount < 2)
            {
                return StartValidation.SplitSettingsInvalid;
            }
        }

        return StartValidation.Ok;
    }

    private async Task StartAsync()
    {
        switch (ValidateBeforeStart())
        {
            case StartValidation.FormatNotCreatable:
                StatusMessage = Strings.CompressErrorFormatNotCreatable;
                return;
            case StartValidation.NoSources:
                StatusMessage = Strings.CompressErrorNoSources;
                return;
            case StartValidation.NoOutput:
                StatusMessage = Strings.CompressErrorNoOutput;
                return;
            case StartValidation.PasswordEmpty:
                StatusMessage = Strings.CompressErrorPasswordEmpty;
                return;
            case StartValidation.PasswordMismatch:
                StatusMessage = Strings.CompressErrorPasswordMismatch;
                return;
            case StartValidation.SplitNotAvailable:
                StatusMessage = Strings.CompressNoteSplitUnavailable;
                return;
            case StartValidation.SplitSettingsInvalid:
                StatusMessage = Strings.CompressErrorSplitInvalid;
                return;
        }

        var useEncryption = EncryptionEnabled && FormatSupportsEncryption;

        if (DeleteSourceAfterCompress)
        {
            var confirmed = MessageBox.Show(
                Strings.Format(Strings.CompressConfirmDeleteSource, Sources.Count),
                Strings.CompressWindowTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;

            if (!confirmed)
            {
                return;
            }
        }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        ProgressPercent = 0;
        IsProgressIndeterminate = true;
        StatusMessage = Strings.StatusCompressing;

        try
        {
            var sourcePaths = Sources.ToList();

            long? splitBytes = null;
            if (_selectedSplitChoice.Method != SplitMethod.None)
            {
                var totalSourceBytes = await Task.Run(() => SourcePathSize.TotalBytes(sourcePaths), _cts.Token).ConfigureAwait(true);
                try
                {
                    splitBytes = ResolveSplitVolumeBytes(totalSourceBytes);
                }
                catch (ArgumentException ex)
                {
                    StatusMessage = ex.Message;
                    return;
                }
            }

            var writer = _engineFactory.CreateWriter(SelectedFormat.Format);
            var options = new CreateArchiveOptions
            {
                SourcePaths = sourcePaths,
                OutputPath = OutputPath,
                Format = SelectedFormat.Format,
                CompressionLevel = SelectedLevel,
                PreserveFolderStructure = PreserveFolderStructure,
                DeleteSourceAfterCompress = DeleteSourceAfterCompress,
                Password = useEncryption ? _password : null,
                Encryption = useEncryption ? EncryptionMethod.Aes256 : EncryptionMethod.None,
                // El 7Z cifrado protege siempre también la lista de nombres (-mhe=on). En ZIP no es posible.
                EncryptFileNames = useEncryption && SelectedFormat.Format == ArchiveFormat.SevenZip,
                SplitVolumeSizeBytes = splitBytes,
            };

            var progress = new Progress<OperationProgress>(p =>
            {
                IsProgressIndeterminate = p.TotalBytes <= 0;
                ProgressPercent = p.PercentComplete;
                StatusMessage = string.IsNullOrEmpty(p.CurrentEntryName)
                    ? Strings.StatusCompressing
                    : $"{Strings.StatusCompressing} {p.CurrentEntryName} ({p.PercentComplete:0}%)";
            });

            await writer.CreateAsync(options, progress, _cts.Token).ConfigureAwait(true);

            Succeeded = true;

            if (splitBytes is { } volumeBytes)
            {
                // Con división se muestra el resumen (partes, tamaño, verificación) y NO se cierra
                // automáticamente: el usuario lee el resultado y cierra cuando quiera.
                StatusMessage = await BuildSplitResultAsync(volumeBytes, _cts.Token).ConfigureAwait(true);
            }
            else
            {
                StatusMessage = Strings.Format(Strings.StatusCompressComplete, OutputPath);
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Strings.StatusCompressCancelled;
        }
        catch (ArchiveException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;

            // La contraseña ya se ha pasado al escritor (que la usa solo durante CreateAsync). No
            // debe seguir en memoria del ViewModel más tiempo del necesario.
            if (Succeeded)
            {
                ClearSensitiveData();
            }
        }
    }

    /// <summary>
    /// Tras crear un 7Z dividido: cuenta las partes, y verifica el conjunto abriéndolo por la primera
    /// parte y comprobando la integridad (CRC por fichero que guarda el propio 7z). Devuelve el
    /// mensaje-resumen para mostrar. Si la verificación falla o no se puede hacer, se indica.
    /// </summary>
    private async Task<string> BuildSplitResultAsync(long volumeBytes, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(OutputPath)) ?? ".";
        var baseName = Path.GetFileName(OutputPath);
        var parts = Directory.GetFiles(directory, baseName + ".*")
            .Where(p =>
            {
                var suffix = Path.GetFileName(p)[(baseName.Length + 1)..];
                return suffix.Length >= 1 && suffix.All(char.IsDigit);
            })
            .OrderBy(p => p)
            .ToList();

        var totalBytes = parts.Sum(p => new FileInfo(p).Length);
        var firstPart = parts.Count > 0 ? parts[0] : OutputPath + ".001";

        string verification;
        try
        {
            StatusMessage = Strings.CompressStatusVerifying;
            IsProgressIndeterminate = false;
            ProgressPercent = 0;

            using var reader = _engineFactory.CreateReader(SelectedFormat.Format);
            await reader.OpenAsync(firstPart, _password.Length > 0 ? _password : null, cancellationToken).ConfigureAwait(true);

            var verifyProgress = new Progress<OperationProgress>(p =>
            {
                ProgressPercent = p.PercentComplete;
                StatusMessage = $"{Strings.CompressStatusVerifying} ({p.PercentComplete:0}%)";
            });

            var ok = await reader.TestIntegrityAsync(verifyProgress, cancellationToken).ConfigureAwait(true);
            verification = ok ? Strings.CompressSplitVerifyOk : Strings.CompressSplitVerifyFailed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            verification = Strings.Format(Strings.CompressSplitVerifyError, ex.Message);
        }

        return Strings.Format(
            Strings.CompressStatusSplitComplete,
            FormatBytes(totalBytes),
            parts.Count,
            FormatBytes(volumeBytes),
            verification);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "bytes", "KB", "MB", "GB", "TB" };
        double size = bytes;
        var i = 0;
        while (size >= 1024 && i < units.Length - 1)
        {
            size /= 1024;
            i++;
        }

        return i == 0 ? $"{size:0} {units[i]}" : $"{size:0.##} {units[i]}";
    }

    private void HandleUnexpectedError(Exception ex)
    {
        StatusMessage = $"No se pudo completar la operación: {ex.Message}";
        IsBusy = false;
    }

    private static (string Filter, string SuggestedName) SuggestedOutputFor(ArchiveFormat format) => format switch
    {
        ArchiveFormat.Zip => ("Archivo ZIP (*.zip)|*.zip", "archivo.zip"),
        ArchiveFormat.SevenZip => ("Archivo 7Z (*.7z)|*.7z", "archivo.7z"),
        ArchiveFormat.Tar => ("Archivo TAR (*.tar)|*.tar", "archivo.tar"),
        ArchiveFormat.GZip => ("Archivo GZIP (*.gz;*.tar.gz)|*.gz;*.tar.gz", "archivo.tar.gz"),
        _ => ("Todos los archivos (*.*)|*.*", "archivo"),
    };
}
