using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch.SearchItems.Settings
{
    public class SettingsItemSource<TSettings> : ISearchSubItemSource<string>
    {
        internal TSettings Settings { get; set; }

        public string Prefix { get; set; } = "Settings";

        public bool DisplayAllIfQueryIsEmpty => true;

        public bool DependsOnQuery => true;

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            var items = new List<ISearchItem<string>>();
            foreach(var prop in typeof(TSettings).GetProperties())
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    if (prop.GetCustomAttributes(true).OfType<GenericOptionAttribute>().FirstOrDefault() is GenericOptionAttribute attr)
                    {
                        if (attr is FloatOptionAttribute)
                        {
                            var value = float.NaN;
                            var last = query.Split(' ').LastOrDefault()??string.Empty;
                            if (float.TryParse(last, out value))
                            {
                                items.Add(new FloatSettingsItem<TSettings>(prop, Settings) { NewValue = value });
                            } else
                            {
                                items.Add(new FloatSettingsItem<TSettings>(prop, Settings));
                            }
                        } else
                        {
                            items.Add(new BoolSettingsItem<TSettings>(prop, Settings));
                        }
                    }
                }
            }
            return items;
        }

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return null;
        }
    }

    public class BoolSettingsAction<TSettings> : ISearchAction<string>
    {
        public static BoolSettingsAction<TSettings> Instance { get; private set; } = new BoolSettingsAction<TSettings>();
        public string Name { get; set; } = "Toggle";

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            if (parameter is BoolSettingsItem<TSettings> item)
            {
                item.Property.SetValue(item.Settings, !(bool)item.Property.GetValue(item.Settings));
            }
        }
    }

    public class FloatSettingsAction<TSettings> : ISearchAction<string>
    {
        public static readonly FloatSettingsAction<TSettings> AddAction = new FloatSettingsAction<TSettings>() { floatAction = FloatAction.Add, Name = "+" };
        public static readonly FloatSettingsAction<TSettings> SubtractAction = new FloatSettingsAction<TSettings>() { floatAction = FloatAction.Subtract, Name = "-" };

        public enum FloatAction
        {
            Add, Subtract, Set
        }

        public float NewValue { get; set; }

        internal FloatAction floatAction;

        public string Name { get; set; } = "Set";

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            if (parameter is FloatSettingsItem<TSettings> item)
            {
                if (item.Property.GetCustomAttributes(true).OfType<FloatOptionAttribute>().FirstOrDefault() is FloatOptionAttribute attr)
                {
                    var oldValue = (float)item.Property.GetValue(item.Settings);
                    var newValue = oldValue;
                    switch (floatAction)
                    {
                        case FloatAction.Add:
                            newValue = oldValue + attr.Ticks;
                            break;
                        case FloatAction.Subtract:
                            newValue = oldValue - attr.Ticks;
                            break;
                        case FloatAction.Set:
                            newValue = NewValue;
                            break;
                    }
                    var fullTicks = (int)Math.Round(newValue / attr.Ticks);
                    newValue = attr.Ticks * fullTicks;
                    item.Property.SetValue(item.Settings, Math.Min(attr.Max, Math.Max(attr.Min, newValue)));
                }
            }
        }
    }

    public class BoolSettingsItem<TSettings> : ISearchItem<string>
    {
        internal PropertyInfo Property { get; set; }
        internal TSettings Settings { get; set; }

        private BoolSettingsItem() {}

        public BoolSettingsItem(PropertyInfo property, TSettings settings) {
            if (property == null || settings == null) throw new ArgumentNullException();
            Property = property;
            Settings = settings;
        }
         
        public IList<ISearchKey<string>> Keys
        {
            get
            {
                var keys = new List<ISearchKey<string>>();
                var attr = Property.GetCustomAttributes(true).OfType<GenericOptionAttribute>().FirstOrDefault();
                if (attr is GenericOptionAttribute)
                {
                    if (attr.Name is string) keys.Add(new SettingsKey { Key = attr.Name });
                    if (attr.Description is string) keys.Add(new SettingsKey { Key = attr.Description });
                }
                return keys;
            }
        }
        public IList<ISearchAction<string>> Actions
        {
            get
            {
                var actions = new List<ISearchAction<string>>();
                var type = Property.PropertyType;
                var attr = Property.GetCustomAttributes(true).OfType<GenericOptionAttribute>().FirstOrDefault();
                if (type == typeof(bool))
                {
                    if (attr is GenericOptionAttribute)
                    {
                        actions.Add(BoolSettingsAction<TSettings>.Instance);
                    }
                }
                return actions;
            }
        }

        public ScoreMode ScoreMode => ScoreMode.WeightedMaxScore;

        public Uri Icon { get; set; } = null;

        public string TopLeft
        {
            get
            {
                var attr = Property.GetCustomAttributes(true).OfType<GenericOptionAttribute>().FirstOrDefault();
                if (attr is GenericOptionAttribute)
                {
                    if (attr.Name is string) return attr.Name;
                }
                return null;
            }
        }

        public string TopRight => Property.GetValue(Settings).ToString();

        public string BottomLeft
        {
            get
            {
                var attr = Property.GetCustomAttributes(true).OfType<GenericOptionAttribute>().FirstOrDefault();
                if (attr is GenericOptionAttribute)
                {
                    if (attr.Name is string) return attr.Description;
                }
                return null;
            }
        }

        public string BottomCenter => null;

        public string BottomRight => null;

        public char? IconChar { get; set; } = IconChars.Settings;

    }

    public class FloatSettingsItem<TSettings> : ISearchItem<string>
    {
        internal PropertyInfo Property { get; set; }
        internal TSettings Settings { get; set; }

        private FloatSettingsItem() { }

        public float NewValue { get; set; } = float.NaN;

        public FloatSettingsItem(PropertyInfo property, TSettings settings)
        {
            if (property == null || settings == null) throw new ArgumentNullException();
            Property = property;
            Settings = settings;
        }

        public IList<ISearchKey<string>> Keys
        {
            get
            {
                var keys = new List<ISearchKey<string>>();
                var attr = Property.GetCustomAttributes(true).OfType<FloatOptionAttribute>().FirstOrDefault();
                if (attr is FloatOptionAttribute)
                {
                    if (attr.Name is string) keys.Add(new SettingsKey { Key = attr.Name });
                    if (attr.Description is string) keys.Add(new SettingsKey { Key = attr.Description });
                }
                return keys;
            }
        }
        public IList<ISearchAction<string>> Actions
        {
            get
            {
                var actions = new List<ISearchAction<string>>();
                var type = Property.PropertyType;
                var attr = Property.GetCustomAttributes(true).OfType<FloatOptionAttribute>().FirstOrDefault();
                if (type == typeof(float))
                {
                    if (attr is FloatOptionAttribute)
                    {
                        if (float.IsNaN(NewValue)) 
                        {
                            actions.Add(FloatSettingsAction<TSettings>.AddAction);
                            actions.Add(FloatSettingsAction<TSettings>.SubtractAction);
                        } else
                        {
                            actions.Add(new FloatSettingsAction<TSettings> { Name = $"Set to {NewValue}", NewValue = NewValue, floatAction = FloatSettingsAction<TSettings>.FloatAction.Set });
                        }
                    }
                }
                return actions;
            }
        }

        public ScoreMode ScoreMode => ScoreMode.WeightedMaxScore;

        public Uri Icon { get; set; } = null;

        public string TopLeft
        {
            get
            {
                var attr = Property.GetCustomAttributes(true).OfType<GenericOptionAttribute>().FirstOrDefault();
                if (attr is GenericOptionAttribute)
                {
                    if (attr.Name is string) return attr.Name;
                }
                return null;
            }
        }

        public string TopRight => Property.GetValue(Settings).ToString();

        public string BottomLeft
        {
            get
            {
                var attr = Property.GetCustomAttributes(true).OfType<GenericOptionAttribute>().FirstOrDefault();
                if (attr is GenericOptionAttribute)
                {
                    if (attr.Name is string) return attr.Description;
                }
                return null;
            }
        }

        public string BottomCenter => null;

        public string BottomRight => null;

        public char? IconChar { get; set; } = IconChars.Settings;

    }

    public class SettingsKey : ISearchKey<string>
    {
        public string Key { get; set; }

        public float Weight => 1;
    }
}
