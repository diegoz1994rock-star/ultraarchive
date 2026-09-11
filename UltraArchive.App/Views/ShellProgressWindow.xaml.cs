using System.Windows;
using UltraArchive.App.Resources;
using UltraArchive.App.ViewModels;
using Wpf.Ui.Controls;

namespace UltraArchive.App.Views;

/// <summary>
/// Ventana compacta de progreso (estilo WinRAR) para las operaciones lanzadas desde el menú
/// contextual del Explorador — "Extraer aquí", "Extraer ficheros…", etc. — cuando UltraArchive se
/// abre solo para esa operación (sin la ventana principal). Deliberadamente tonta: se enlaza al
/// mismo <see cref="MainViewModel"/> que ejecuta la extracción; el único código aquí es "Segundo
/// plano" (minimizar) y cerrarse cuando la operación termina.
/// </summary>
public partial class ShellProgressWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private bool _finished;

    public ShellProgressWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.ShellOperationFinished += OnOperationFinished;
        Closed += (_, _) => viewModel.ShellOperationFinished -= OnOperationFinished;
    }

    private void OnOperationFinished(object? sender, System.EventArgs e)
    {
        // Ya en el hilo de UI (el ViewModel lo dispara desde el dispatcher). Deja el resultado
        // visible un instante y cierra; App se encarga de terminar el proceso.
        if (_finished)
        {
            return;
        }

        _finished = true;
        Title = Strings.AppTitle;
        BackgroundButton.IsEnabled = false;

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(1400) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Close();
        };
        timer.Start();
    }

    private void BackgroundButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
}
