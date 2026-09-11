namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Watchdog de <b>inactividad</b> (no de duración total): mide el tiempo transcurrido desde el último
/// progreso válido. Si supera el límite, el orquestador debe detener 7zr.
///
/// El reloj es inyectable (<c>Func&lt;DateTimeOffset&gt;</c>) para poder probarlo de forma determinista
/// sin <c>Thread.Sleep</c>.
/// </summary>
public sealed class SevenZipInactivityWatchdog
{
    private readonly TimeSpan _limit;
    private readonly Func<DateTimeOffset> _now;
    private DateTimeOffset _lastActivity;

    public SevenZipInactivityWatchdog(TimeSpan limit, Func<DateTimeOffset>? now = null)
    {
        if (limit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "El límite de inactividad debe ser positivo.");
        }

        _limit = limit;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _lastActivity = _now();
    }

    /// <summary>Registra que ha habido progreso: reinicia el contador de inactividad.</summary>
    public void NotifyProgress() => _lastActivity = _now();

    /// <summary>Tiempo transcurrido sin progreso.</summary>
    public TimeSpan IdleTime => _now() - _lastActivity;

    /// <summary>True si el proceso lleva demasiado tiempo sin reportar progreso.</summary>
    public bool ShouldAbort() => IdleTime > _limit;
}
