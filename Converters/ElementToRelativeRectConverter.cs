using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace QuickSearch.Converters
{
    public class ElementToRelativeRectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[1] is Visual relativeTo && values[0] is FrameworkElement element)
            {
                var rect = element.TransformToVisual(relativeTo).TransformBounds(LayoutInformation.GetLayoutSlot(element));
                rect.Height = element.MaxHeight;
                return new Rect(rect.Left, rect.Top / 4, rect.Width, rect.Height);
            }
            throw new ArgumentException();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
