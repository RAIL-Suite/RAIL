using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WorkflowDemo.Converters;

public class MachineStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value as string;
        return status switch
        {
            "free" => new SolidColorBrush(Color.FromRgb(30, 50, 30)),
            "busy" => new SolidColorBrush(Color.FromRgb(50, 50, 20)),
            "error" => new SolidColorBrush(Color.FromRgb(60, 30, 30)),
            _ => new SolidColorBrush(Colors.Transparent)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class MachineStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value as string;
        return status switch
        {
            "free" => new SolidColorBrush(Colors.Green),
            "busy" => new SolidColorBrush(Colors.Orange),
            "error" => new SolidColorBrush(Colors.Red),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}





