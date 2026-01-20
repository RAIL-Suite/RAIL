using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfRagApp.Converters
{
    /// <summary>
    /// Converts a boolean value to a color brush.
    /// True = Green (#4CAF50), False = Red (#F44336).
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return new SolidColorBrush(boolValue 
                    ? Color.FromRgb(0x4C, 0xAF, 0x50)  // Green
                    : Color.FromRgb(0xF4, 0x43, 0x36)); // Red
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}





