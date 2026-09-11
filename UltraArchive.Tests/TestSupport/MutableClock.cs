namespace UltraArchive.Tests.TestSupport;

/// <summary>Reloj controlable para tests deterministas (sin Thread.Sleep).</summary>
internal sealed class MutableClock
{
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public Func<DateTimeOffset> Now => () => _now;

    public void Advance(TimeSpan by) => _now += by;
}
