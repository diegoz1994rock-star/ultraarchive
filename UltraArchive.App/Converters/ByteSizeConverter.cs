using System.Globalization;
using System.Windows.Data;

namespace UltraArchive.App.Converters;

/// <summary>Convierte un tamaño en bytes (long) a una cadena legible ("1,42 GB", "384 KB"...).</summary>
[ValueConversion(typeof(long), typeof(string))]
public sealed class ByteSizeConverter : IValueConverter
{
    private static readonly string[] Units = { "bytes", "KB", "MB", "GB", "TB" };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes || bytes < 0)
        {
            return string.Empty;
        }

        if (bytes == 0)
        {
            return "0 bytes";
        }

        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        var format = unitIndex == 0 ? "N0" : "N2";
        return $"{size.ToString(format, culture)} {Units[unitIndex]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
