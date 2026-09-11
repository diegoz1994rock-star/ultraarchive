using System.Windows;
using UltraArchive.App.Views;
using UltraArchive.Core.Interfaces;

namespace UltraArchive.App.Services;

/// <summary>
/// Implementación de <see cref="IPasswordProvider"/> sobre <see cref="PasswordDialog"/>. Los motores de
/// UltraArchive.Archives/UltraArchive.Iso nunca conocen WPF: solo dependen de esta abstracción, definida
/// en UltraArchive.Core, para pedir la contraseña al usuario cuando encuentran una entrada cifrada.
/// </summary>
public sealed class WpfPasswordProvider : IPasswordProvider
{
    public Task<string?> RequestPasswordAsync(string archiveDisplayName, bool isRetryAfterFailure, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // El diálogo de contraseña es modal y debe crearse en el hilo de UI; esta implementación se
        // invoca siempre desde ese hilo (los comandos de MainViewModel usan ConfigureAwait(true)),
        // así que no hace falta Dispatcher.Invoke.
        var dialog = new PasswordDialog(archiveDisplayName, isRetryAfterFailure)
        {
            Owner = DialogOwner.Pick(),
        };

        var accepted = dialog.ShowDialog();
        return Task.FromResult(accepted == true ? dialog.EnteredPassword : null);
    }
}
