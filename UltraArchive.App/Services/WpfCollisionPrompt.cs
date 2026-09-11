using System.IO;
using System.Windows;
using UltraArchive.App.Views;
using UltraArchive.Core.Interfaces;
using UltraArchive.Core.Models;

namespace UltraArchive.App.Services;

/// <summary>
/// Implementación de <see cref="ICollisionPrompt"/> sobre <see cref="CollisionDialog"/>.
///
/// Los motores de extracción llaman a esto de forma síncrona y potencialmente desde un hilo del
/// <c>ThreadPool</c> (los lectores usan <c>ConfigureAwait(false)</c> internamente), así que se
/// marshaliza siempre al hilo de UI con <see cref="System.Windows.Threading.Dispatcher.Invoke{T}"/>.
/// </summary>
public sealed class WpfCollisionPrompt : ICollisionPrompt
{
    public CollisionResolution Resolve(string existingFilePath)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            // Sin UI disponible: comportamiento histórico (sobrescribir).
            return CollisionResolution.Overwrite;
        }

        return dispatcher.Invoke(() =>
        {
            var dialog = new CollisionDialog(Path.GetFileName(existingFilePath))
            {
                Owner = DialogOwner.Pick(),
            };
            dialog.ShowDialog();
            return dialog.Result;
        });
    }
}
