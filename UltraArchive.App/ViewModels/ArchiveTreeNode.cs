using System.Collections.ObjectModel;

namespace UltraArchive.App.ViewModels;

/// <summary>
/// Un nodo de carpeta del árbol lateral. Representa <b>solo carpetas</b> (los ficheros se muestran en
/// la tabla de la derecha según la carpeta seleccionada). <see cref="FullPath"/> usa '/' como
/// separador, igual que <see cref="Core.Models.ArchiveEntry.FullPath"/>; la raíz tiene <c>FullPath = ""</c>.
/// </summary>
public sealed class ArchiveTreeNode : ViewModelBase
{
    private bool _isExpanded;
    private bool _isSelected;

    public required string Name { get; init; }

    /// <summary>Ruta de la carpeta dentro del archivo ("" para la raíz, "docs", "docs/sub"…).</summary>
    public required string FullPath { get; init; }

    public ObservableCollection<ArchiveTreeNode> Children { get; } = new();

    public bool IsRoot => FullPath.Length == 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
