using Microsoft.Win32;
using UltraArchive.App.Resources;
using UltraArchive.Core.Interfaces;

namespace UltraArchive.App.Services;

/// <summary>Implementación de <see cref="IFileDialogService"/> sobre los diálogos nativos de WPF/.NET.</summary>
public sealed class WpfFileDialogService : IFileDialogService
{
    public string? ShowOpenArchiveDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.DialogOpenArchiveTitle,
            Filter = Strings.DialogOpenArchiveFilter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowOpenIsoDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.DialogOpenIsoTitle,
            Filter = Strings.DialogOpenIsoFilter,
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSelectFolderDialog(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = string.IsNullOrWhiteSpace(title) ? Strings.DialogSelectFolderTitle : title
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public IReadOnlyList<string> ShowOpenFilesDialog(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Multiselect = true,
            CheckFileExists = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : Array.Empty<string>();
    }

    public string? ShowSaveArchiveDialog(string title, string suggestedFileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            FileName = suggestedFileName,
            Filter = filter,
            OverwritePrompt = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
