namespace UltraArchive.Core.Interfaces;

/// <summary>
/// Abstrae la obtención de una contraseña del usuario, para que los motores de archivo
/// (en UltraArchive.Archives/UltraArchive.Iso) no dependan de la capa de UI.
/// La implementación real (en UltraArchive.App) muestra el diálogo de contraseña de WPF.
/// </summary>
public interface IPasswordProvider
{
    /// <summary>
    /// Solicita al usuario la contraseña de <paramref name="archiveDisplayName"/>.
    /// Devuelve null si el usuario cancela el diálogo.
    /// </summary>
    /// <param name="isRetryAfterFailure">True si ya se intentó una contraseña anterior y era incorrecta.</param>
    Task<string?> RequestPasswordAsync(string archiveDisplayName, bool isRetryAfterFailure, CancellationToken cancellationToken = default);
}
