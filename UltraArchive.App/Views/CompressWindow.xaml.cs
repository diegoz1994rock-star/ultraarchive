using System.Linq;
using System.Windows;
using UltraArchive.App.ViewModels;
using Wpf.Ui.Controls;

namespace UltraArchive.App.Views;

/// <summary>
/// Ventana "Comprimir". Igual que <see cref="MainWindow"/>, deliberadamente tonta: toda la lógica vive
/// en <see cref="CompressViewModel"/>. El único código aquí es reenviar la selección múltiple del
/// ListBox (no vinculable directamente a un ViewModel en WPF sin un behavior adicional) y cerrar la
/// ventana cuando la compresión termina con éxito.
/// </summary>
public partial class CompressWindow : FluentWindow
{
    public CompressWindow(CompressViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();

        // La contraseña nunca debe sobrevivir al cierre de la ventana: se borra del ViewModel y de
        // los propios PasswordBox al cerrar, pase lo que pase.
        Closed += (_, _) =>
        {
            viewModel.ClearSensitiveData();
            PasswordBox.Clear();
            ConfirmPasswordBox.Clear();
        };

        // Verbos "Comprimir aquí / como .ZIP / como .7Z" del menú contextual: la ventana se abre con
        // todo ya configurado y lanza la compresión sola. Usa exactamente el mismo StartCommand que
        // el botón "Comprimir"; si termina con éxito, RequestClose cierra la ventana.
        if (viewModel.AutoStartRequested)
        {
            Loaded += (_, _) =>
            {
                if (viewModel.StartCommand.CanExecute(null))
                {
                    viewModel.StartCommand.Execute(null);
                }
            };
        }
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CompressViewModel viewModel)
        {
            return;
        }

        var selected = SourcesListBox.SelectedItems.Cast<string>().ToList();
        viewModel.RemoveSources(selected);
    }

    // ----------- Arrastrar y soltar archivos/carpetas -----------

    private void Window_DragOver(object sender, DragEventArgs e) => HandleDragOver(e, showOverlay: false);
    private void Window_DragLeave(object sender, DragEventArgs e) => DropOverlay.Visibility = Visibility.Collapsed;
    private void Window_Drop(object sender, DragEventArgs e) => HandleDrop(e);

    private void DropZone_DragOver(object sender, DragEventArgs e) => HandleDragOver(e, showOverlay: true);
    private void DropZone_DragLeave(object sender, DragEventArgs e) => DropOverlay.Visibility = Visibility.Collapsed;
    private void DropZone_Drop(object sender, DragEventArgs e) => HandleDrop(e);

    private void HandleDragOver(DragEventArgs e, bool showOverlay)
    {
        var accepts = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = accepts ? DragDropEffects.Copy : DragDropEffects.None;
        DropOverlay.Visibility = accepts && showOverlay ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void HandleDrop(DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;

        if (DataContext is not CompressViewModel viewModel || !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            viewModel.AddPaths(paths);
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is CompressViewModel viewModel)
        {
            viewModel.UpdatePassword(PasswordBox.Password);
        }
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is CompressViewModel viewModel)
        {
            viewModel.UpdateConfirmPassword(ConfirmPasswordBox.Password);
        }
    }
}
