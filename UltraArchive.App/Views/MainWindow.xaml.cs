using System.Windows;
using System.Windows.Controls;
using UltraArchive.App.ViewModels;
using UltraArchive.Core.Models;
using Wpf.Ui.Controls;

namespace UltraArchive.App.Views;

/// <summary>
/// Ventana principal de UltraArchive. Deliberadamente "tonta": toda la lógica vive en
/// <see cref="MainViewModel"/>, resuelto por inyección de dependencias en el constructor. El único
/// código aquí es reenviar la selección múltiple del DataGrid al ViewModel (no vinculable directamente
/// sin un behavior adicional, ver el mismo patrón en <see cref="CompressWindow"/>).
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void EntriesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not System.Windows.Controls.DataGrid grid)
        {
            return;
        }

        viewModel.UpdateSelectedEntries(grid.SelectedItems.Cast<ArchiveEntry>().Select(entry => entry.FullPath));
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel && e.NewValue is ArchiveTreeNode node)
        {
            viewModel.SelectFolder(node);
        }
    }

    /// <summary>Doble clic sobre una fila de tipo carpeta: navegar dentro de ella.</summary>
    private void EntriesGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not System.Windows.Controls.DataGrid grid)
        {
            return;
        }

        if (grid.SelectedItem is ArchiveEntry { IsDirectory: true } folder)
        {
            viewModel.NavigateInto(folder.FullPath);
        }
    }
}
