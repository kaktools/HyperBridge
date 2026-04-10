using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using HyperBridge.Core.Enums;

namespace HyperBridge.App.Infrastructure;

public sealed class CompatibilityBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not CompatibilityLevel level)
        {
            return Brushes.Gray;
        }

        return level switch
        {
            CompatibilityLevel.Green => new SolidColorBrush(Color.FromRgb(24, 170, 113)),
            CompatibilityLevel.Yellow => new SolidColorBrush(Color.FromRgb(232, 190, 52)),
            CompatibilityLevel.Red => new SolidColorBrush(Color.FromRgb(220, 80, 90)),
            _ => Brushes.Gray,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

