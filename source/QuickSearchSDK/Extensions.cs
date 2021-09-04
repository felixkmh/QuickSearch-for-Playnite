using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch
{
    /// <summary>
    /// Some utility extensions.
    /// </summary>
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

        /// <summary>
        /// Checks whether a type is primitve number type.
        /// </summary>
        /// <param name="type">Type to check.</param>
        /// <returns><see langword="true"/>, if <paramref name="type"/> is a primitive number type. <see langword="false"/> otherwise.</returns>
        public static bool IsNumberType(this Type type)
        {
            return NumberTypes.Contains(type);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string ExpandString(this string input)
        {
            if (input is string)
            {
                if (input.StartsWith("LOC", StringComparison.OrdinalIgnoreCase))
                {
                    var loc = ResourceProvider.GetString(input);
                    if (!string.IsNullOrEmpty(loc))
                    {
                        return loc;
                    }
                }
            }
            return input;
        }
    }
}
