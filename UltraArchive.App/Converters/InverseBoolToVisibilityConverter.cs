using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UltraArchive.App.Converters;

/// <summary>
/// <c>true</c> → <see cref="Visibility.Collapsed"/>, <c>false</c> → <see cref="Visibility.Visible"/>.
/// El inverso de <c>BooleanToVisibilityConverter</c>, para mostrar algo cuando una condición es falsa
/// (p. ej. una pista de "arrastra aquí" solo cuando la lista está vacía).
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
