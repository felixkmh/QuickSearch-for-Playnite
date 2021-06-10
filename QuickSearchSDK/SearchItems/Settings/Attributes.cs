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
    public class SelectionOptionAttribute : GenericOptionAttribute
    {
        public SelectionOptionAttribute(string name, object[] options) : base(name)
        {
            Options = options;
        }

        public object[] Options { get; }

    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class NumberOptionAttribute : GenericOptionAttribute
    {
        public NumberOptionAttribute(string name) : base(name)
        {

        }

        public double Min { get; set; } = double.NegativeInfinity;
        public double Max { get; set; } = double.PositiveInfinity;
        public double Tick { get; set; } = 1;
    }
}
