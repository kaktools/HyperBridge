using System.Globalization;
using System.Windows;
using System.Windows.Data;
using HyperBridge.App.ViewModels;

namespace HyperBridge.App.Infrastructure;

public sealed class AppPaneVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AppPane pane || parameter is not string expected)
        {
            return Visibility.Collapsed;
        }

        if (!Enum.TryParse<AppPane>(expected, out var expectedPane))
        {
            return Visibility.Collapsed;
        }

        return pane == expectedPane ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

