using System.Windows;
using UltraArchive.App.Resources;
using UltraArchive.Core.Models;
using Wpf.Ui.Controls;

namespace UltraArchive.App.Views;

/// <summary>
/// Diálogo modal para resolver un conflicto de ficheros durante la extracción
/// (<see cref="CollisionPolicy.Ask"/>). No decide nada por su cuenta: devuelve la elección del
/// usuario como <see cref="CollisionResolution"/>. Cerrar la ventana equivale a "Cancelar".
/// </summary>
public partial class CollisionDialog : FluentWindow
{
    public CollisionDialog(string existingFileName)
    {
        InitializeComponent();
        PromptText.Text = Strings.Format(Strings.CollisionDialogPrompt, existingFileName);
    }

    /// <summary>Elección del usuario. Por defecto, cancelar (si cierra la ventana sin pulsar un botón).</summary>
    public CollisionResolution Result { get; private set; } = CollisionResolution.Cancel;

    private bool ApplyToAll => ApplyToAllCheck.IsChecked == true;

    private void Overwrite_Click(object sender, RoutedEventArgs e) => Finish(CollisionAction.Overwrite);
    private void Skip_Click(object sender, RoutedEventArgs e) => Finish(CollisionAction.Skip);
    private void Rename_Click(object sender, RoutedEventArgs e) => Finish(CollisionAction.Rename);

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = CollisionResolution.Cancel;
        DialogResult = false;
        Close();
    }

    private void Finish(CollisionAction action)
    {
        Result = new CollisionResolution(action, ApplyToAll);
        DialogResult = true;
        Close();
    }
}
