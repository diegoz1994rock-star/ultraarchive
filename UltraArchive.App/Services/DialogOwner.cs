using System.Linq;
using System.Windows;

namespace UltraArchive.App.Services;

/// <summary>
/// Elige una ventana propietaria <b>visible</b> para los diálogos modales (contraseña, colisión).
///
/// No se puede usar sin más <c>Application.Current.MainWindow</c>: cuando UltraArchive se lanza desde
/// el menú contextual del Explorador solo para una operación (p. ej. "Extraer aquí"), la ventana
/// principal existe pero <b>no se muestra</b> — solo está la ventana compacta de progreso
/// (<see cref="Views.ShellProgressWindow"/>) —, y asignar <c>Owner</c> a una ventana no mostrada
/// lanza <see cref="System.InvalidOperationException"/>.
/// </summary>
internal static class DialogOwner
{
    public static Window? Pick()
    {
        var app = Application.Current;
        if (app is null)
        {
            return null;
        }

        if (app.MainWindow is { IsVisible: true } main)
        {
            return main;
        }

        return app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
               ?? app.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible);
    }
}
