using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClaudeStats.Views.Converters;

/// <summary>
/// Converts a pack/URI string to a frozen <see cref="ImageSource"/> for the tray icon, so the
/// ViewModel can expose the icon as a plain string (keeping it free of WPF media types).
/// </summary>
public sealed class PathToImageSourceConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
        {
            return DependencyProperty.UnsetValue;
        }

        BitmapImage image = new();
        image.BeginInit();
        image.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
