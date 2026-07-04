using System.Globalization;
using System.Windows.Data;

namespace ClaudeStats.Views.Converters;

/// <summary>
/// Converts a fraction (0–1) to a pixel height by multiplying by the converter parameter
/// (the maximum bar height). Used by the hand-rolled trend bar chart.
/// </summary>
public sealed class FractionToHeightConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double fraction = value is double d ? d : 0.0;
        double maxHeight = parameter is not null
            && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double max)
            ? max
            : 120.0;

        // Keep a minimum sliver so non-zero days are visible.
        double height = fraction * maxHeight;
        return fraction > 0 && height < 2 ? 2.0 : height;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
