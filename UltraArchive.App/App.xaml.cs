using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using UltraArchive.App.Services;
using UltraArchive.App.ViewModels;
using UltraArchive.App.Views;
using UltraArchive.Archives.Gzip;
using UltraArchive.Archives.Rar;
using UltraArchive.Archives.SevenZip;
using UltraArchive.Archives.Tar;
using UltraArchive.Archives.Zip;
using UltraArchive.Interop.SevenZip;
using UltraArchive.Iso;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;
using UltraArchive.Core.Services;
using UltraArchive.Shell;
using Wpf.Ui.Appearance;

namespace UltraArchive.App;

/// <summary>
/// Composition root de UltraArchive. Registra por inyección de dependencias los servicios de dominio
/// (UltraArchive.Core), los motores (ZIP/7Z/TAR/GZIP/RAR/ISO, resueltos por
/// <see cref="CreateEngineFactory"/>), el interop 7zr (Fase 6A) y la integración con el Explorador
/// (Fase 6B). También interpreta los argumentos de línea de comandos con los que se lanza desde el
/// Explorador antes de mostrar la ventana.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private SingleInstance? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var command = ShellCommandLineParser.Parse(e.Args);

        // --install-shell(-allusers) / --uninstall-shell(-allusers): acción sin ventana.
        if (command.Action is ShellAction.InstallShell or ShellAction.UninstallShell
            or ShellAction.InstallShellAllUsers or ShellAction.UninstallShellAllUsers)
        {
            RunShellSetup(command.Action);
            Shutdown();
            return;
        }

        // Instancia única: si ya hay una en marcha, reenviarle los argumentos y cerrar esta.
        _singleInstance = new SingleInstance();
        if (!_singleInstance.IsPrimaryInstance)
        {
            SingleInstance.TrySendToPrimary(e.Args);
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // UltraArchive tiene identidad visual propia: tema oscuro (azul marino/grafito) fijo.
        // La capa de estilos (Styles/Theme.xaml) se mezcla la última y manda sobre WPF-UI.
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        mainViewModel.SetInitialTheme(isDarkTheme: true);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

        // Verbos de EXTRACCIÓN y COMPRESIÓN del menú contextual: UltraArchive se abrió solo para esa
        // operación, así que no hace falta mostrar la ventana principal detrás. Al terminar, la
        // aplicación se cierra sola. Si una segunda instancia reenvía otro comando mientras tanto, el
        // manejador del pipe muestra la ventana principal y el proceso sigue vivo (se cancela el cierre).
        var isShellExtraction = command.Action is ShellAction.ExtractHere
            or ShellAction.ExtractHereFlat or ShellAction.ExtractTo;
        var isShellCompress = command.Action is ShellAction.Compress or ShellAction.CompressHere
            or ShellAction.CompressToZip or ShellAction.CompressTo7z or ShellAction.CompressSplit;

        if (isShellExtraction)
        {
            // Ventana compacta de progreso (estilo WinRAR): fichero + %, tiempo, Cancelar.
            var progressWindow = new ShellProgressWindow(mainViewModel);
            progressWindow.Closed += (_, _) =>
            {
                if (!mainWindow.IsVisible)
                {
                    Shutdown();
                }
            };
            progressWindow.Show();
            RunShellCommand(progressWindow, mainViewModel, command);
        }
        else if (isShellCompress)
        {
            // OpenCompressWindow (llamado dentro de RunStartupCommandAsync) ya muestra su propia
            // ventana "Comprimir" con barra de progreso; aquí solo evitamos abrir la principal detrás
            // y cerramos la aplicación cuando termine (a menos que una instancia reenviada la muestre).
            mainViewModel.ShellOperationFinished += (_, _) =>
            {
                if (!mainWindow.IsVisible)
                {
                    Shutdown();
                }
            };
            RunShellCommand(mainWindow, mainViewModel, command);
        }
        else
        {
            mainWindow.Show();

            if (command.Action != ShellAction.None)
            {
                RunShellCommand(mainWindow, mainViewModel, command);
            }
        }

        // A partir de aquí, una segunda instancia nos reenviará sus argumentos por el pipe.
        _singleInstance.ArgumentsReceived += forwarded =>
            mainWindow.Dispatcher.InvokeAsync(() =>
            {
                BringToFront(mainWindow);
                var forwardedCommand = ShellCommandLineParser.Parse(forwarded);
                if (forwardedCommand.Action != ShellAction.None
                    && forwardedCommand.Action is not (ShellAction.InstallShell or ShellAction.UninstallShell
                        or ShellAction.InstallShellAllUsers or ShellAction.UninstallShellAllUsers))
                {
                    RunShellCommand(mainWindow, mainViewModel, forwardedCommand);
                }
            });
        _singleInstance.StartListening();
    }

    private static void RunShellCommand(Window mainWindow, MainViewModel mainViewModel, ShellCommand command)
    {
        _ = mainWindow.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await mainViewModel.RunStartupCommandAsync(command);
            }
            catch
            {
                // El propio ViewModel ya traduce los errores de dominio a StatusMessage.
            }
        });
    }

    private static void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void RunShellSetup(ShellAction action)
    {
        var allUsers = action is ShellAction.InstallShellAllUsers or ShellAction.UninstallShellAllUsers;
        var install = action is ShellAction.InstallShell or ShellAction.InstallShellAllUsers;
        try
        {
            var service = allUsers
                ? RegistryShellIntegrationService.ForAllUsers()
                : RegistryShellIntegrationService.ForCurrentProcess();

            if (install)
            {
                service.Install();
            }
            else
            {
                service.Uninstall();
            }
        }
        catch (Exception ex)
        {
            // Silencioso para los verbos "-allusers": los ejecuta el instalador/desinstalador y no debe
            // haber diálogos. Para la acción manual desde la app, avisar.
            if (allUsers)
            {
                Environment.ExitCode = 1;
            }
            else
            {
                MessageBox.Show($"No se pudo cambiar la integración con el Explorador: {ex.Message}",
                    "UltraArchive", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // --- Núcleo de dominio (UltraArchive.Core) ---
        services.AddSingleton<IArchiveFormatDetector, ArchiveFormatDetector>();

        // --- Interop 7zr (Fase 6A): localizador + capacidad + ejecutor ---
        services.AddSingleton<SevenZipLocator>();
        services.AddSingleton<ISevenZipCapability, SevenZipCapability>();
        services.AddSingleton<ISevenZipProcessRunner, SevenZipProcessRunner>();
        services.AddSingleton<ISevenZipCli, SevenZipCli>();

        services.AddSingleton<IArchiveEngineFactory>(sp => CreateEngineFactory(
            sp.GetRequiredService<ISevenZipCli>(),
            sp.GetRequiredService<ISevenZipCapability>()));

        // --- Integración con el Explorador (Fase 6B), solo HKCU ---
        services.AddSingleton<IShellIntegrationService>(_ => RegistryShellIntegrationService.ForCurrentProcess());

        // --- Infraestructura de UI ---
        services.AddSingleton<IFileDialogService, WpfFileDialogService>();
        services.AddSingleton<IPasswordProvider, WpfPasswordProvider>();
        services.AddSingleton<ICollisionPrompt, WpfCollisionPrompt>();

        // --- ViewModels y ventanas ---
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static IArchiveEngineFactory CreateEngineFactory(ISevenZipCli sevenZipCli, ISevenZipCapability sevenZipCapability)
    {
        var factory = new ArchiveEngineFactory();

        // ZIP: único formato mutable en esta fase (permite añadir/quitar entradas de un archivo existente).
        factory.RegisterReader(ArchiveFormat.Zip, () => new ZipArchiveReader());
        factory.RegisterWriter(ArchiveFormat.Zip, () => new ZipArchiveWriter());

        // 7Z: lectura sobre SharpCompress. Creación enrutada (Fase 6A):
        //   - sin contraseña → SevenZipArchiveWriter gestionado (SharpCompress), sin cambios;
        //   - con contraseña → SevenZipCli (7zr.exe), solo si hay un binario verificado disponible.
        factory.RegisterReader(ArchiveFormat.SevenZip, () => new SevenZipArchiveReader());
        factory.RegisterWriter(ArchiveFormat.SevenZip, () =>
            new RoutingSevenZipWriter(new SevenZipArchiveWriter(), sevenZipCli, sevenZipCapability));

        // TAR sin comprimir.
        factory.RegisterReader(ArchiveFormat.Tar, () => new TarArchiveReader());
        factory.RegisterWriter(ArchiveFormat.Tar, () => new TarArchiveWriter());

        // GZIP: archivo suelto o TAR.GZ combinado (ver GZipArchiveReader/Writer).
        factory.RegisterReader(ArchiveFormat.GZip, () => new GZipArchiveReader());
        factory.RegisterWriter(ArchiveFormat.GZip, () => new GZipArchiveWriter());

        // RAR (Fase 3): solo lectura/extracción/prueba (RAR4 y RAR5). No se registra escritor: el
        // formato de compresión RAR es propietario y su licencia prohíbe crear un compresor compatible.
        factory.RegisterReader(ArchiveFormat.Rar, () => new RarArchiveReader());

        // ISO9660 (Fase 5): solo lectura/exploración/extracción (incl. Joliet y Rock Ridge). Sin
        // escritor: la creación de imágenes ISO queda fuera del alcance de esta fase.
        factory.RegisterReader(ArchiveFormat.Iso9660, () => new IsoArchiveReader());

        return factory;
    }
}
