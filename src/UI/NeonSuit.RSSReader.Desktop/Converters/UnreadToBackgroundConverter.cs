using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NeonSuit.RSSReader.Desktop.Converters
{
    public class UnreadToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isUnread && isUnread)
            {
                // Return a highlighted background for unread items
                // Using a light accent color that works with dark themes
                return new SolidColorBrush(Color.FromArgb(30, 0, 251, 255)); // Semi-transparent neon blue
            }
            // Return transparent for read items
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}