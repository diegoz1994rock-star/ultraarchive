using UltraArchive.Core.Interfaces;

namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Implementación de <see cref="ISevenZipCapability"/>: consulta a <see cref="SevenZipLocator"/> y
/// solo declara disponible el 7Z cifrado cuando el localizador devuelve
/// <see cref="SevenZipToolStatus.Available"/> (binario presente, SHA-256 correcto, versión compatible).
///
/// El resultado del localizador se evalúa <b>una vez</b> (perezosamente) por sesión: el binario no
/// aparece ni cambia en caliente durante la ejecución de la app.
/// </summary>
public sealed class SevenZipCapability : ISevenZipCapability
{
    private readonly Lazy<SevenZipLocateResult> _located;

    public SevenZipCapability(SevenZipLocator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        _located = new Lazy<SevenZipLocateResult>(locator.Locate);
    }

    public bool CanCreateEncryptedSevenZip => _located.Value.Status == SevenZipToolStatus.Available;

    public string? VerifiedExecutablePath =>
        _located.Value.Status == SevenZipToolStatus.Available ? _located.Value.Tool?.ExecutablePath : null;

    public string StatusExplanation => _located.Value.Message;
}
