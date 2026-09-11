using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using UltraArchive.App.Commands;
using UltraArchive.App.Resources;
using UltraArchive.App.Services;
using UltraArchive.App.Views;
using UltraArchive.Core.Exceptions;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using Wpf.Ui.Appearance;

namespace UltraArchive.App.ViewModels;

/// <summary>
/// ViewModel de la ventana principal. Abrir/Explorar/Extraer/Probar operan sobre los motores reales de
/// ZIP/7Z/TAR/GZIP (Fase 2), RAR (Fase 3, solo lectura) e imágenes ISO9660 (Fase 5, solo lectura)
/// resueltos vía <see cref="IArchiveEngineFactory"/>; Comprimir/Añadir/Eliminar solo aplican a los
/// formatos con escritor. El botón "ISO" (<see cref="IsoCommand"/>) es un atajo para abrir una imagen
/// .iso; también se puede abrir por el botón "Abrir" normal.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private static readonly IReadOnlyDictionary<ArchiveFormat, string> FormatDisplayNames = new Dictionary<ArchiveFormat, string>
    {
        [ArchiveFormat.Zip] = "ZIP",
        [ArchiveFormat.SevenZip] = "7Z",
        [ArchiveFormat.Rar] = "RAR",
        [ArchiveFormat.Tar] = "TAR",
        [ArchiveFormat.GZip] = "GZIP",
        [ArchiveFormat.BZip2] = "BZIP2",
        [ArchiveFormat.Iso9660] = "ISO",
    };

    private readonly IArchiveFormatDetector _formatDetector;
    private readonly IArchiveEngineFactory _engineFactory;
    private readonly IFileDialogService _fileDialogService;
    private readonly IPasswordProvider _passwordProvider;
    private readonly ISevenZipCapability _sevenZipCapability;
    private readonly ICollisionPrompt _collisionPrompt;
    private readonly List<string> _selectedEntryPaths = new();

    /// <summary>Lista COMPLETA y plana de entradas del archivo abierto (fuente de la que se derivan el árbol y la tabla).</summary>
    private readonly List<ArchiveEntry> _allEntries = new();
    private ArchiveTreeNode? _folderRoot;
    private ArchiveTreeNode? _selectedFolder;

    private string? _currentArchivePath;
    private ArchiveFormat _currentFormat;
    /// <summary>
    /// Contraseña recordada SOLO en memoria para la sesión de este archivo concreto, para no tener que
    /// volver a pedirla en cada operación. Nunca se escribe a disco ni se registra en logs; se pierde
    /// al cerrar el archivo o la aplicación (ver requisitos de seguridad de contraseñas del proyecto).
    /// </summary>
    private string? _currentPassword;
    private CancellationTokenSource? _cts;

    private string _windowTitle = Strings.AppTitle;
    private string _statusMessage = Strings.StatusReady;
    private bool _isBusy;
    private bool _isDarkTheme;
    private double _progressPercent;
    private bool _isProgressIndeterminate;
    private string _progressElapsedText = string.Empty;

    private readonly IShellIntegrationService _shellIntegration;

    public MainViewModel(IArchiveFormatDetector formatDetector, IArchiveEngineFactory engineFactory,
        IFileDialogService fileDialogService, IPasswordProvider passwordProvider, ISevenZipCapability sevenZipCapability,
        IShellIntegrationService shellIntegration, ICollisionPrompt collisionPrompt)
    {
        _formatDetector = formatDetector;
        _engineFactory = engineFactory;
        _fileDialogService = fileDialogService;
        _sevenZipCapability = sevenZipCapability;
        _passwordProvider = passwordProvider;
        _shellIntegration = shellIntegration;
        _collisionPrompt = collisionPrompt;

        OpenCommand = new AsyncRelayCommand(OpenAsync, () => !IsBusy, HandleUnexpectedError);
        AddCommand = new AsyncRelayCommand(AddAsync, () => !IsBusy, HandleUnexpectedError);
        ExtractCommand = new AsyncRelayCommand(ExtractAsync, () => !IsBusy && HasArchiveOpen, HandleUnexpectedError);
        CompressCommand = new RelayCommand(OpenCompressWindow, () => !IsBusy);
        PasswordCommand = new AsyncRelayCommand(SetPasswordAsync, () => !IsBusy && HasArchiveOpen, HandleUnexpectedError);
        TestCommand = new AsyncRelayCommand(TestAsync, () => !IsBusy && HasArchiveOpen, HandleUnexpectedError);
        IsoCommand = new AsyncRelayCommand(OpenIsoAsync, () => !IsBusy, HandleUnexpectedError);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !IsBusy && HasArchiveOpen, HandleUnexpectedError);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        CancelOperationCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        ShellIntegrationCommand = new RelayCommand(ToggleShellIntegration, () => !IsBusy);
    }

    /// <summary>Filas visibles en la tabla: subcarpetas + ficheros de la carpeta seleccionada en el árbol.</summary>
    public ObservableCollection<ArchiveEntry> Entries { get; } = new();

    /// <summary>Raíz(es) del árbol de carpetas (una sola: el nodo raíz del archivo). Vacío si no hay archivo abierto.</summary>
    public ObservableCollection<ArchiveTreeNode> FolderRoots { get; } = new();

    /// <summary>Carpeta seleccionada en el árbol. Al cambiarla se recalcula <see cref="Entries"/>.</summary>
    public ArchiveTreeNode? SelectedFolder
    {
        get => _selectedFolder;
        private set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                RefreshVisibleEntries();
            }
        }
    }

    /// <summary>Ruta (dentro del archivo) de la carpeta mostrada, para la barra de navegación / breadcrumb.</summary>
    public string CurrentFolderPath => _selectedFolder is null or { IsRoot: true } ? string.Empty : _selectedFolder.FullPath;

    public AsyncRelayCommand OpenCommand { get; }
    public AsyncRelayCommand AddCommand { get; }
    public AsyncRelayCommand ExtractCommand { get; }
    public RelayCommand CompressCommand { get; }
    public AsyncRelayCommand PasswordCommand { get; }
    public AsyncRelayCommand TestCommand { get; }
    public AsyncRelayCommand IsoCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand CancelOperationCommand { get; }
    public RelayCommand ShellIntegrationCommand { get; }

    public string WindowTitle
    {
        get => _windowTitle;
        private set => SetProperty(ref _windowTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        private set => SetProperty(ref _isDarkTheme, value);
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

    /// <summary>Texto "Tiempo transcurrido: mm:ss" para la ventana compacta de progreso del Explorador.</summary>
    public string ProgressElapsedText
    {
        get => _progressElapsedText;
        private set => SetProperty(ref _progressElapsedText, value);
    }

    /// <summary>
    /// Se dispara (en el hilo de UI) cuando termina <see cref="RunStartupCommandAsync"/> — con éxito,
    /// error o cancelación. Lo usa <see cref="App"/> para cerrar la ventana compacta de progreso y
    /// salir cuando UltraArchive se lanzó solo para una operación del menú contextual.
    /// </summary>
    public event EventHandler? ShellOperationFinished;

    public bool HasArchiveOpen => _currentArchivePath is not null;

    /// <summary>
    /// Sincroniza el estado del interruptor de tema con el tema ya aplicado a la aplicación
    /// (p. ej. tras detectar el tema del sistema operativo al arrancar), sin volver a aplicarlo.
    /// </summary>
    public void SetInitialTheme(bool isDarkTheme) => IsDarkTheme = isDarkTheme;

    /// <summary>Reenvía desde el code-behind del DataGrid qué entradas están seleccionadas (para "Eliminar").</summary>
    public void UpdateSelectedEntries(IEnumerable<string> fullPaths)
    {
        _selectedEntryPaths.Clear();
        _selectedEntryPaths.AddRange(fullPaths);
        RelayCommand.RaiseCanExecuteChanged();
    }

    private async Task OpenAsync()
    {
        var path = _fileDialogService.ShowOpenArchiveDialog();
        if (path is not null)
        {
            await OpenPathAsync(path).ConfigureAwait(true);
        }
    }

    /// <summary>Atajo del botón "ISO": abre directamente el selector filtrado a imágenes .iso.</summary>
    private async Task OpenIsoAsync()
    {
        var path = _fileDialogService.ShowOpenIsoDialog();
        if (path is not null)
        {
            await OpenPathAsync(path).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Detecta el formato de <paramref name="path"/> y, si hay un motor de lectura, carga su contenido.
    /// Compartido por "Abrir" (cualquier formato) y por el atajo "ISO".
    /// </summary>
    private async Task OpenPathAsync(string path)
    {
        IsBusy = true;
        ClearOpenArchiveState();
        _currentPassword = null;

        try
        {
            var format = await _formatDetector.DetectAsync(path).ConfigureAwait(true);

            if (format == ArchiveFormat.Unknown)
            {
                _currentArchivePath = null;
                WindowTitle = Strings.AppTitle;
                StatusMessage = Strings.Format(Strings.StatusUnknownFormat, Path.GetFileName(path));
                return;
            }

            _currentArchivePath = path;
            _currentFormat = format;
            WindowTitle = $"{Strings.AppTitle} — {Path.GetFileName(path)}";

            if (_engineFactory.CanRead(format))
            {
                var entries = await WithPasswordRetryAsync(
                    path, format,
                    (reader, ct) => reader.GetEntriesAsync(ct),
                    CancellationToken.None).ConfigureAwait(true);

                LoadEntries(entries, Path.GetFileName(path));

                var fileCount = entries.Count(e => !e.IsDirectory);
                StatusMessage = Strings.Format(Strings.StatusArchiveOpened, DisplayNameFor(format), fileCount);
            }
            else
            {
                StatusMessage = Strings.Format(Strings.StatusFormatDetectedNoEngine, DisplayNameFor(format));
            }
        }
        catch (OperationCanceledException)
        {
            ClearOpenArchiveState();
            WindowTitle = Strings.AppTitle;
            StatusMessage = Strings.StatusOpenCancelled;
        }
        catch (ArchiveException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseArchiveDependentCommandsChanged();
        }
    }

    /// <summary>Vacía el árbol de carpetas, la tabla y la lista plana (al abrir otro archivo o al fallar).</summary>
    private void ClearOpenArchiveState()
    {
        _currentArchivePath = null;
        _allEntries.Clear();
        _folderRoot = null;
        _selectedEntryPaths.Clear();
        FolderRoots.Clear();
        Entries.Clear();
        SetProperty(ref _selectedFolder, null, nameof(SelectedFolder));
        OnPropertyChanged(nameof(CurrentFolderPath));
    }

    /// <summary>Carga una lista de entradas: guarda la lista plana, construye el árbol y selecciona la raíz.</summary>
    private void LoadEntries(IReadOnlyList<ArchiveEntry> entries, string archiveFileName)
    {
        var previouslySelected = CurrentFolderPath;

        _allEntries.Clear();
        _allEntries.AddRange(entries);

        _folderRoot = ArchiveFolderTree.Build(_allEntries, archiveFileName);
        FolderRoots.Clear();
        FolderRoots.Add(_folderRoot);

        // Tras recargar (p. ej. después de Añadir/Eliminar) se intenta conservar la carpeta abierta.
        var target = string.IsNullOrEmpty(previouslySelected)
            ? _folderRoot
            : ArchiveFolderTree.Find(_folderRoot, previouslySelected) ?? _folderRoot;

        target.IsSelected = true;
        SelectedFolder = target;
    }

    private void RefreshVisibleEntries()
    {
        Entries.Clear();
        if (_folderRoot is not null && _selectedFolder is not null)
        {
            foreach (var entry in ArchiveFolderTree.EntriesIn(_allEntries, _selectedFolder))
            {
                Entries.Add(entry);
            }
        }

        _selectedEntryPaths.Clear();
        OnPropertyChanged(nameof(CurrentFolderPath));
        RelayCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Selecciona una carpeta del árbol (lo llama el code-behind del TreeView).</summary>
    public void SelectFolder(ArchiveTreeNode? node)
    {
        if (node is not null && !ReferenceEquals(node, _selectedFolder))
        {
            SelectedFolder = node;
        }
    }

    /// <summary>Navega a la carpeta <paramref name="folderFullPath"/> (doble clic en una fila de tipo carpeta).</summary>
    public void NavigateInto(string folderFullPath)
    {
        if (_folderRoot is null)
        {
            return;
        }

        var node = ArchiveFolderTree.Find(_folderRoot, folderFullPath);
        if (node is null)
        {
            return;
        }

        // Expande la cadena de ancestros para que el nodo sea visible en el árbol.
        var current = node;
        while (current is not null)
        {
            current.IsExpanded = true;
            if (current.IsRoot)
            {
                break;
            }

            current = ArchiveFolderTree.Find(_folderRoot, ParentPath(current.FullPath));
        }

        node.IsSelected = true;
        SelectedFolder = node;
    }

    private static string ParentPath(string fullPath)
    {
        var lastSlash = fullPath.LastIndexOf('/');
        return lastSlash < 0 ? string.Empty : fullPath[..lastSlash];
    }

    private async Task ExtractAsync()
    {
        if (_currentArchivePath is null)
        {
            return;
        }

        var destination = _fileDialogService.ShowSelectFolderDialog(Strings.DialogSelectFolderTitle);
        if (destination is null)
        {
            return;
        }

        await ExtractToPathAsync(destination).ConfigureAwait(true);
    }

    /// <summary>Extrae todo el archivo abierto a <paramref name="destination"/>. Compartido por el botón y por los verbos del Explorador.</summary>
    private async Task ExtractToPathAsync(string destination)
    {
        if (_currentArchivePath is null)
        {
            return;
        }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        StatusMessage = Strings.StatusExtracting;
        ProgressPercent = 0;
        IsProgressIndeterminate = true;

        try
        {
            var progress = new Progress<OperationProgress>(ReportProgress(Strings.StatusExtracting));

            var extractOptions = new ExtractOptions
            {
                DestinationPath = destination,
                CollisionPolicy = CollisionPolicy.Ask,
                OnCollision = _collisionPrompt.Resolve,
            };

            var result = await WithPasswordRetryAsync(
                _currentArchivePath, _currentFormat,
                (reader, ct) => reader.ExtractAsync(extractOptions, progress, ct),
                _cts.Token).ConfigureAwait(true);

            var message = result.BlockedEntries.Count > 0
                ? Strings.Format(Strings.StatusExtractCompleteWithBlocked, result.ExtractedCount, result.BlockedEntries.Count)
                : Strings.Format(Strings.StatusExtractComplete, result.ExtractedCount);
            if (result.SkippedEntries.Count > 0)
            {
                message += Strings.Format(Strings.StatusExtractSkippedSuffix, result.SkippedEntries.Count);
            }
            StatusMessage = message;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Strings.StatusExtractCancelled;
        }
        catch (ArchiveException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
            RaiseArchiveDependentCommandsChanged();
        }
    }

    /// <summary>
    /// Ejecuta la acción con la que se lanzó UltraArchive desde el Explorador / línea de comandos
    /// (abrir, extraer aquí, extraer en…). La instala/desinstala del shell la gestiona <see cref="App"/>
    /// antes de mostrar la ventana.
    /// </summary>
    public async Task RunStartupCommandAsync(UltraArchive.Shell.ShellCommand command)
    {
        if (command.Path is null)
        {
            ShellOperationFinished?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            await DispatchStartupCommandAsync(command).ConfigureAwait(true);
        }
        finally
        {
            ShellOperationFinished?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task DispatchStartupCommandAsync(UltraArchive.Shell.ShellCommand command)
    {
        if (command.Path is null)
        {
            return;
        }

        switch (command.Action)
        {
            case UltraArchive.Shell.ShellAction.OpenFile:
                await OpenPathAsync(command.Path).ConfigureAwait(true);
                break;

            case UltraArchive.Shell.ShellAction.ExtractHere:
                await OpenPathAsync(command.Path).ConfigureAwait(true);
                if (HasArchiveOpen)
                {
                    var here = Path.Combine(
                        Path.GetDirectoryName(command.Path) ?? Directory.GetCurrentDirectory(),
                        Path.GetFileNameWithoutExtension(command.Path));
                    await ExtractToPathAsync(here).ConfigureAwait(true);
                }

                break;

            case UltraArchive.Shell.ShellAction.ExtractHereFlat:
                await OpenPathAsync(command.Path).ConfigureAwait(true);
                if (HasArchiveOpen)
                {
                    var parent = Path.GetDirectoryName(command.Path) ?? Directory.GetCurrentDirectory();
                    await ExtractToPathAsync(parent).ConfigureAwait(true);
                }

                break;

            case UltraArchive.Shell.ShellAction.ExtractTo:
                await OpenPathAsync(command.Path).ConfigureAwait(true);
                if (HasArchiveOpen)
                {
                    var chosen = _fileDialogService.ShowSelectFolderDialog(Strings.DialogSelectFolderTitle);
                    if (chosen is not null)
                    {
                        await ExtractToPathAsync(chosen).ConfigureAwait(true);
                    }
                }

                break;

            case UltraArchive.Shell.ShellAction.Compress:
                OpenCompressWindow(command.AllPaths);
                break;

            case UltraArchive.Shell.ShellAction.CompressHere:
                OpenCompressWindow(command.AllPaths, ArchiveFormat.Zip, autoStart: true);
                break;

            case UltraArchive.Shell.ShellAction.CompressToZip:
                OpenCompressWindow(command.AllPaths, ArchiveFormat.Zip, autoStart: true);
                break;

            case UltraArchive.Shell.ShellAction.CompressTo7z:
                OpenCompressWindow(command.AllPaths, ArchiveFormat.SevenZip, autoStart: true);
                break;

            case UltraArchive.Shell.ShellAction.CompressSplit:
                OpenCompressWindow(command.AllPaths, ArchiveFormat.SevenZip, preferSplit: true);
                break;
        }
    }

    private async Task TestAsync()
    {
        if (_currentArchivePath is null)
        {
            return;
        }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        StatusMessage = Strings.StatusTesting;
        ProgressPercent = 0;
        IsProgressIndeterminate = true;

        try
        {
            var progress = new Progress<OperationProgress>(ReportProgress(Strings.StatusTesting));

            var ok = await WithPasswordRetryAsync(
                _currentArchivePath, _currentFormat,
                (reader, ct) => reader.TestIntegrityAsync(progress, ct),
                _cts.Token).ConfigureAwait(true);

            StatusMessage = ok ? Strings.StatusTestOk : Strings.StatusTestFailed;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Strings.StatusOpenCancelled;
        }
        catch (ArchiveException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
            RaiseArchiveDependentCommandsChanged();
        }
    }

    private async Task AddAsync()
    {
        if (_currentArchivePath is null)
        {
            StatusMessage = Strings.StatusNoFileOpen;
            return;
        }

        if (!_engineFactory.CanWrite(_currentFormat) || _engineFactory.CreateWriter(_currentFormat) is not IMutableArchiveWriter mutableWriter)
        {
            StatusMessage = Strings.StatusAddNotMutable;
            return;
        }

        var filesToAdd = _fileDialogService.ShowOpenFilesDialog(Strings.ButtonAdd);
        if (filesToAdd.Count == 0)
        {
            return;
        }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        StatusMessage = Strings.StatusAdding;
        ProgressPercent = 0;
        IsProgressIndeterminate = true;

        try
        {
            var progress = new Progress<OperationProgress>(ReportProgress(Strings.StatusAdding));
            await mutableWriter.AddEntriesAsync(_currentArchivePath, filesToAdd, _currentPassword, progress, _cts.Token).ConfigureAwait(true);

            StatusMessage = Strings.Format(Strings.StatusAddComplete, filesToAdd.Count);
            await ReloadEntriesAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Strings.StatusOpenCancelled;
        }
        catch (ArchiveException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
            RaiseArchiveDependentCommandsChanged();
        }
    }

    private async Task DeleteAsync()
    {
        if (_currentArchivePath is null)
        {
            StatusMessage = Strings.StatusNoFileOpen;
            return;
        }

        if (_selectedEntryPaths.Count == 0)
        {
            StatusMessage = Strings.StatusDeleteNoSelection;
            return;
        }

        if (!_engineFactory.CanWrite(_currentFormat) || _engineFactory.CreateWriter(_currentFormat) is not IMutableArchiveWriter mutableWriter)
        {
            StatusMessage = Strings.StatusDeleteNotMutable;
            return;
        }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        StatusMessage = Strings.StatusDeleting;

        try
        {
            var toDelete = _selectedEntryPaths.ToList();
            await mutableWriter.DeleteEntriesAsync(_currentArchivePath, toDelete, cancellationToken: _cts.Token).ConfigureAwait(true);

            StatusMessage = Strings.Format(Strings.StatusDeleteComplete, toDelete.Count);
            _selectedEntryPaths.Clear();
            await ReloadEntriesAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Strings.StatusOpenCancelled;
        }
        catch (ArchiveException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
            RaiseArchiveDependentCommandsChanged();
        }
    }

    private async Task SetPasswordAsync()
    {
        if (_currentArchivePath is null)
        {
            StatusMessage = Strings.StatusNoFileOpen;
            return;
        }

        var password = await _passwordProvider.RequestPasswordAsync(Path.GetFileName(_currentArchivePath), isRetryAfterFailure: false).ConfigureAwait(true);
        if (password is null)
        {
            StatusMessage = Strings.StatusPasswordCancelled;
            return;
        }

        _currentPassword = password;
        StatusMessage = Strings.StatusPasswordSet;
    }

    private void OpenCompressWindow() => OpenCompressWindow(null);

    /// <summary>
    /// Abre la ventana "Comprimir". Con <paramref name="initialSources"/> (invocación desde el menú
    /// contextual del Explorador) precarga esos orígenes y, opcionalmente, fuerza formato / división /
    /// arranque automático. Reutiliza íntegramente <see cref="CompressViewModel"/>: no hay una segunda
    /// ruta de compresión.
    /// </summary>
    private void OpenCompressWindow(
        IReadOnlyList<string>? initialSources,
        ArchiveFormat? format = null,
        bool preferSplit = false,
        bool autoStart = false)
    {
        var viewModel = new CompressViewModel(_engineFactory, _fileDialogService, _sevenZipCapability);

        if (initialSources is { Count: > 0 })
        {
            viewModel.InitializeFromShell(initialSources, format, preferSplit, autoStart);
        }

        var window = new CompressWindow(viewModel) { Owner = Application.Current?.MainWindow };
        window.ShowDialog();

        if (viewModel.Succeeded)
        {
            StatusMessage = Strings.Format(Strings.StatusCompressComplete, viewModel.OutputPath);
        }
    }

    private async Task ReloadEntriesAsync()
    {
        if (_currentArchivePath is null)
        {
            return;
        }

        var entries = await WithPasswordRetryAsync(
            _currentArchivePath, _currentFormat,
            (reader, ct) => reader.GetEntriesAsync(ct),
            CancellationToken.None).ConfigureAwait(true);

        LoadEntries(entries, Path.GetFileName(_currentArchivePath));
    }

    /// <summary>
    /// Abre un lector para <paramref name="path"/> y ejecuta <paramref name="operation"/>, pidiendo la
    /// contraseña al usuario (vía <see cref="IPasswordProvider"/>) y reintentando desde cero si el
    /// archivo resulta estar protegido o la contraseña usada era incorrecta. Reutiliza automáticamente
    /// la última contraseña válida de la sesión para este archivo (<see cref="_currentPassword"/>) antes
    /// de preguntar, para no pedirla más de una vez por archivo mientras dure la sesión.
    /// </summary>
    private async Task<TResult> WithPasswordRetryAsync<TResult>(
        string path,
        ArchiveFormat format,
        Func<IArchiveReader, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var password = _currentPassword;
        var isRetry = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var reader = _engineFactory.CreateReader(format);

            try
            {
                await reader.OpenAsync(path, password, cancellationToken).ConfigureAwait(true);
                var result = await operation(reader, cancellationToken).ConfigureAwait(true);
                _currentPassword = password;
                return result;
            }
            catch (InvalidPasswordException)
            {
                password = await _passwordProvider.RequestPasswordAsync(Path.GetFileName(path), isRetry, cancellationToken).ConfigureAwait(true);
                if (password is null)
                {
                    throw new OperationCanceledException();
                }

                isRetry = true;
            }
        }
    }

    private Action<OperationProgress> ReportProgress(string statusPrefix) => progress =>
    {
        IsProgressIndeterminate = progress.TotalBytes <= 0;
        ProgressPercent = progress.PercentComplete;
        ProgressElapsedText = Strings.Format(Strings.ShellProgressElapsed, FormatElapsed(progress.Elapsed));
        StatusMessage = string.IsNullOrEmpty(progress.CurrentEntryName)
            ? statusPrefix
            : $"{statusPrefix} {progress.CurrentEntryName} ({progress.PercentComplete:0}%)";
    };

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1 ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"mm\:ss");


    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplicationThemeManager.Apply(IsDarkTheme ? ApplicationTheme.Dark : ApplicationTheme.Light);
    }

    /// <summary>
    /// Instala o desinstala la integración con el Explorador (menú contextual + "Abrir con"), todo
    /// bajo HKCU y reversible. Pide confirmación.
    /// </summary>
    private void ToggleShellIntegration()
    {
        var installed = _shellIntegration.IsInstalled;
        var question = installed ? Strings.ShellIntegrationConfirmUninstall : Strings.ShellIntegrationConfirmInstall;

        if (MessageBox.Show(question, Strings.AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            if (installed)
            {
                _shellIntegration.Uninstall();
                StatusMessage = Strings.ShellIntegrationUninstalled;
            }
            else
            {
                _shellIntegration.Install();
                StatusMessage = Strings.ShellIntegrationInstalled;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Format(Strings.ShellIntegrationError, ex.Message);
        }
    }

    private void HandleUnexpectedError(Exception ex)
    {
        // Los detalles técnicos completos se registran en el sistema de logging (a partir de la Fase 4/6);
        // aquí solo se muestra al usuario un mensaje comprensible.
        StatusMessage = $"No se pudo completar la operación: {ex.Message}";
        IsBusy = false;
    }

    private static string DisplayNameFor(ArchiveFormat format) =>
        FormatDisplayNames.TryGetValue(format, out var name) ? name : format.ToString();

    private void RaiseArchiveDependentCommandsChanged()
    {
        OnPropertyChanged(nameof(HasArchiveOpen));
        RelayCommand.RaiseCanExecuteChanged();
    }
}
