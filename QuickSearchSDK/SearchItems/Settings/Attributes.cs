using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch.SearchItems.Settings
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class GenericOptionAttribute : Attribute
    {
        public GenericOptionAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public string Description { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class FloatOptionAttribute : GenericOptionAttribute
    {
        public FloatOptionAttribute(string name) : base(name)
        {

        }

        public float Min { get; set; } = float.NegativeInfinity;
        public float Max { get; set; } = float.PositiveInfinity;
        public float Ticks { get; set; } = float.NaN;
    }
}
