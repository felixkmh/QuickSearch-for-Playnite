using System;

namespace QuickSearch.Attributes
{
    /// <summary>
    /// Marks a property as an option with a name and a description.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class GenericOptionAttribute : Attribute
    {
        /// <summary>
        /// Marks a property as an option with a name <paramref name="name"/> and an optional description <see cref="GenericOptionAttribute.Description"/>.
        /// </summary>
        /// <param name="name">Name of the option.</param>
        public GenericOptionAttribute(string name)
        {
            Name = name;
        }
        /// <summary>
        /// Name of the option.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// Short desctiption of the option.
        /// </summary>
        public string Description { get; set; }
    }
    /// <summary>
    /// Marks a property as an option with a name, a description and an array of possible values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class SelectionOptionAttribute : GenericOptionAttribute
    {
        /// <summary>
        /// Marks a property as an option with a name <paramref name="name"/> and an optional description <see cref="GenericOptionAttribute.Description"/>.
        /// Values that this property can be set to are the items in <see cref="SelectionOptionAttribute.Options"/>.
        /// </summary>
        /// <param name="name">Name of the option.</param>
        /// <param name="options">Possible values this option can be set to. Need to have the same type as the property.</param>
        public SelectionOptionAttribute(string name, object[] options) : base(name)
        {
            Options = options;
        }
        /// <summary>
        /// Possible values for this option. Items need to have the same type as the property.
        /// </summary>
        public object[] Options { get; }

    }
    /// <summary>
    /// Marks a property as an option with a name, a description, min/max values and a a tick.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class NumberOptionAttribute : GenericOptionAttribute
    {
        /// <inheritdoc cref="GenericOptionAttribute"/>
        public NumberOptionAttribute(string name) : base(name)
        {

        }
        /// <summary>
        /// Minimum value the option can be set to.
        /// </summary>
        public double Min { get; set; } = double.NegativeInfinity;
        /// <summary>
        /// Maximum value the option can be set to.
        /// </summary>
        public double Max { get; set; } = double.PositiveInfinity;
        /// <summary>
        /// Amount by which the value is incremented/decremented with each tick.
        /// </summary>
        public double Tick { get; set; } = 1;
    }
}

