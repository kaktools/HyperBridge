using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HyperBridge.App.Infrastructure;

public sealed class StepVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int step || parameter is not string text)
        {
            return Visibility.Collapsed;
        }

        return int.TryParse(text, out var expected) && expected == step
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

