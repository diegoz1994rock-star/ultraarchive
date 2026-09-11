using System.Globalization;
using System.Windows.Data;
using UltraArchive.App.Resources;

namespace UltraArchive.App.Converters;

/// <summary>Convierte <c>ArchiveEntry.IsEncrypted</c> en el texto localizado "Sí"/"No".</summary>
[ValueConversion(typeof(bool), typeof(string))]
public sealed class ProtectedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Strings.ProtectedYes : Strings.ProtectedNo;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
