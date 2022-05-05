using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace QuickSearch.Converters
{
    public class ICollectionNullOrEmptyToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ICollection collection)
            {
                if (targetType == typeof(bool))
                {
                    return collection.Count > 0;
                } else if (targetType == typeof(Visibility))
                {
                    return collection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                }
            } else if (value is null)
            {
                if (targetType == typeof(bool))
                {
                    return false;
                }
                else if (targetType == typeof(Visibility))
                {
                    return Visibility.Collapsed;
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
