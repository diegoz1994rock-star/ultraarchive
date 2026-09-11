using System.Globalization;
using System.Windows.Data;
using UltraArchive.App.Resources;

namespace UltraArchive.App.Converters;

/// <summary>Convierte <c>ArchiveEntry.IsDirectory</c> en el texto localizado "Carpeta"/"Archivo".</summary>
[ValueConversion(typeof(bool), typeof(string))]
public sealed class EntryTypeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Strings.TypeFolder : Strings.TypeFile;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
