using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch
{
    public static class Extensions
    {
        internal static readonly HashSet<Type> NumberTypes = new HashSet<Type>
        {
            typeof(byte),
            typeof(sbyte),
            typeof(UInt16),
            typeof(UInt32),
            typeof(UInt64),
            typeof(Int16),
            typeof(Int32),
            typeof(Int64),
            typeof(decimal),
            typeof(double),
            typeof(float)
        };

        public static bool IsNumberType(this Type type)
        {
            return NumberTypes.Contains(type);
        }
    }
}
