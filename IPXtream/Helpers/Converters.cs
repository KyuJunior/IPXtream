using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using IPXtream.Services;

namespace IPXtream.Helpers;

/// <summary>Returns Visibility.Visible when the boolean is <c>true</c>.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is Visibility.Visible;
}

/// <summary>Returns Visibility.Visible when the boolean is <c>false</c>.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is false ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is Visibility.Collapsed;
}

/// <summary>Inverts a boolean value.</summary>
[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is false;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is false;
}

/// <summary>Returns Visibility.Visible when the string is non-empty.</summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => !string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>Returns Visibility.Collapsed when the string is non-empty (inverse).</summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public class StringEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>
/// Used in the downloads tray progress bar.
/// Takes (double progress 0-1, double containerWidth) and returns the pixel width of the fill.
/// </summary>
public class ProgressToWidthMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object p, CultureInfo c)
    {
        if (values.Length == 2 &&
            values[0] is double progress &&
            values[1] is double width)
            return Math.Max(0, Math.Min(width, progress * width));
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] ts, object p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns a BitmapImage loaded from the local disk cache when the image has been
/// pre-cached by CacheAllBackgroundAsync. Falls back to the original HTTP URL so
/// WPF can download it on-demand when the image isn't cached yet.
/// </summary>
[ValueConversion(typeof(string), typeof(object))]
public class CachedImageConverter : IValueConverter
{
    public object? Convert(object value, Type t, object p, CultureInfo c)
    {
        var url = value as string;
        if (string.IsNullOrWhiteSpace(url)) return null;

        var localPath = XtreamApiService.GetImageCachePath(url);
        if (File.Exists(localPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption    = BitmapCacheOption.OnLoad;
                bmp.UriSource      = new Uri(localPath, UriKind.Absolute);
                bmp.DecodePixelWidth = 80; // matches thumbnail display size, keeps memory low
                bmp.EndInit();
                bmp.Freeze();  // makes it cross-thread safe
                return bmp;
            }
            catch { /* fall through to HTTP */ }
        }

        // Not yet cached — let WPF download from the network as usual
        return url;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
