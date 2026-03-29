using System.Globalization;
using System.Windows;
using System.Windows.Data;
using IPXtream.ViewModels;

namespace IPXtream.Helpers;

/// <summary>
/// Converts the active <see cref="MediaSection"/> to NavBtnActive or NavBtn
/// by looking in Application.Current.Resources (both styles are registered globally).
/// </summary>
public class SectionToStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MediaSection activeSection
            && parameter is string targetSection
            && Enum.TryParse<MediaSection>(targetSection, out var target))
        {
            var key = activeSection == target ? "NavBtnActive" : "NavBtn";
            return Application.Current.Resources[key]!;
        }

        return Application.Current.Resources["NavBtn"]!;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
