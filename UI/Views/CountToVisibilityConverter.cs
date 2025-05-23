using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CSVGenerator.UI.Views
{
    /// <summary>
    /// Converts an integer count to a Visibility value.
    /// Returns Collapsed if count is 0 or 1, otherwise returns Visible.
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count <= 1 ? Visibility.Collapsed : Visibility.Visible;
            }
            
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
