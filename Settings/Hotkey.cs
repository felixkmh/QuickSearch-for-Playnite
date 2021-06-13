using System;
using System.Windows.Input;

namespace QuickSearch
{
    public partial class SearchSettings
    {
        public class Hotkey : IEquatable<Hotkey>
        {
            public Hotkey(Key key, ModifierKeys modifier)
            {
                this.Key = key;
                this.Modifiers = modifier;
            }
            public Key Key { get; set; }
            public ModifierKeys Modifiers { get; set; }

            public bool Equals(Hotkey other)
            {
                return Key == other.Key && Modifiers == other.Modifiers;
            }

            public override string ToString()
            {
                return $"{Modifiers} + {Key}";
            }
        }

    }
}