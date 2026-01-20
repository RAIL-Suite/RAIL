using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace RailStudio
{
    /// <summary>
    /// Converter that returns a yellow brush if value is true (duplicate), otherwise transparent.
    /// </summary>
    public class DuplicateToBackgroundConverter : IValueConverter
    {
        private static readonly Brush YellowBrush = new SolidColorBrush(Color.FromRgb(255, 243, 205)); // Light yellow
        private static readonly Brush TransparentBrush = Brushes.Transparent;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDuplicate && isDuplicate)
            {
                return YellowBrush;
            }
            return TransparentBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}




