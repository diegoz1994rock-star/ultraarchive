using UltraArchive.Interop.SevenZip;

namespace UltraArchive.Tests.Interop;

/// <summary>Fase 6A, Paso 3: watchdog de inactividad (reloj inyectado, sin Thread.Sleep).</summary>
public class SevenZipInactivityWatchdogTests
{
    private sealed class TestClock
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public Func<DateTimeOffset> Now => () => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    [Fact]
    public void ReciénCreado_NoDebeAbortar()
    {
        var clock = new TestClock();
        var watchdog = new SevenZipInactivityWatchdog(TimeSpan.FromMinutes(10), clock.Now);

        Assert.False(watchdog.ShouldAbort());
    }

    [Fact]
    public void TrasElLímiteSinProgreso_DebeAbortar()
    {
        var clock = new TestClock();
        var watchdog = new SevenZipInactivityWatchdog(TimeSpan.FromMinutes(10), clock.Now);

        clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        Assert.True(watchdog.ShouldAbort());
    }

    [Fact]
    public void ProgresoAntesDelLímite_ReiniciaElContador()
    {
        var clock = new TestClock();
        var watchdog = new SevenZipInactivityWatchdog(TimeSpan.FromMinutes(10), clock.Now);

        clock.Advance(TimeSpan.FromMinutes(9));
        watchdog.NotifyProgress();          // reinicia
        clock.Advance(TimeSpan.FromMinutes(9));

        Assert.False(watchdog.ShouldAbort()); // solo llevan 9 min desde el último progreso
    }

    [Fact]
    public void ProgresoQueNoLlegaAlLímite_YLuegoSilencio_AcabaAbortando()
    {
        var clock = new TestClock();
        var watchdog = new SevenZipInactivityWatchdog(TimeSpan.FromMinutes(10), clock.Now);

        clock.Advance(TimeSpan.FromMinutes(5));
        watchdog.NotifyProgress();
        clock.Advance(TimeSpan.FromMinutes(11));

        Assert.True(watchdog.ShouldAbort());
    }

    [Fact]
    public void Constructor_LímiteNoPositivo_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SevenZipInactivityWatchdog(TimeSpan.Zero));
    }

    [Fact]
    public void ValorPorDefecto_SonDiezMinutos()
    {
        Assert.Equal(TimeSpan.FromMinutes(10), SevenZipExecutionDefaults.InactivityLimit);
    }
}
