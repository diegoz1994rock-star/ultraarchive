namespace UltraArchive.Core.Interfaces;

/// <summary>
/// Abstrae los diálogos nativos de selección de archivo/carpeta para que los ViewModels no dependan
/// directamente de WPF (Microsoft.Win32.OpenFileDialog vive solo en la implementación, en UltraArchive.App).
/// </summary>
public interface IFileDialogService
{
    /// <summary>Muestra un diálogo "Abrir archivo". Devuelve la ruta elegida o null si se cancela.</summary>
    string? ShowOpenArchiveDialog();

    /// <summary>Muestra un diálogo "Abrir" filtrado a imágenes de disco (.iso). Devuelve la ruta elegida o null si se cancela.</summary>
    string? ShowOpenIsoDialog();

    /// <summary>Muestra un diálogo de selección de carpeta de destino. Devuelve la ruta elegida o null si se cancela.</summary>
    string? ShowSelectFolderDialog(string title);

    /// <summary>Muestra un diálogo de selección múltiple de archivos (para añadir a un archivo comprimido). Devuelve las rutas elegidas, o una colección vacía si se cancela.</summary>
    IReadOnlyList<string> ShowOpenFilesDialog(string title);

    /// <summary>Muestra un diálogo "Guardar como" para elegir la ruta de salida de un nuevo archivo comprimido. Devuelve la ruta elegida o null si se cancela.</summary>
    string? ShowSaveArchiveDialog(string title, string suggestedFileName, string filter);
}
