using System;

namespace QuickSearch
{

    public class AssemblyOptions : IEquatable<AssemblyOptions>
    {
        public bool Items { get; set; } = true;
        public bool Actions { get; set; } = true;

        public bool Equals(AssemblyOptions other)
        {
            if (other == null)
            {
                return false;
            }
            return Items == other.Items && Actions == other.Actions;
        }

        public override bool Equals(object obj)
        {
            if (obj is AssemblyOptions options)
            {
                return Equals(options);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

}