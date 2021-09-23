using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;

namespace QuickSearch.Converters
{
    public class ListToStringConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
            {
                return string.Empty;
            }

            string sep = ", ";

            if (parameter is string )
            {
                sep = parameter as string;
            }

            return string.Join(sep, ((IEnumerable<object>)value).ToArray());
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var stringVal = (string)value;

            if (string.IsNullOrEmpty(stringVal))
            {
                return null;
            }
            else
            {
                string sep = ", ";

                if (parameter is string)
                {
                    sep = parameter as string;
                }

                var converted = stringVal.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries);
                if (targetType == typeof(ComparableList<string>))
                {
                    return new ComparableList<string>(converted);
                }
                else
                {
                    return converted.ToList();
                }
            }
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
