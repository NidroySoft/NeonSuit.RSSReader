using System;
using System.Globalization;
using System.Windows.Data;

namespace NeonSuit.RSSReader.Desktop.Converters
{
    /// <summary>
    /// Converts an integer to boolean (true if greater than zero)
    /// </summary>
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue > 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
