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

        public virtual string Prefix { get; set; } = "Settings";

        public virtual bool DisplayAllIfQueryIsEmpty => true;

        public virtual bool DependsOnQuery => true;

        public virtual IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            var items = new List<ISearchItem<string>>();
            foreach(var prop in typeof(TSettings).GetProperties())
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    if (prop.GetCustomAttributes(true).OfType<GenericOptionAttribute>().FirstOrDefault() is GenericOptionAttribute attr)
                    {
                        if (attr is NumberOptionAttribute)
                        {
                            var value = double.NaN;
                            var last = query.Split(' ').LastOrDefault()??string.Empty;
                            if (double.TryParse(last, out value))
                            {
                                items.Add(new FloatSettingsItem<TSettings>(prop, Settings) { NewValue = value });
                            } else
                            {
                                items.Add(new FloatSettingsItem<TSettings>(prop, Settings));
                            }
                        } else if (attr is SelectionOptionAttribute)
                        {
                            items.Add(new SelectionSettingsItem<TSettings>(prop, Settings));
                        } else
                        {
                            if (prop.PropertyType == typeof(bool))
                            {
                                items.Add(new BoolSettingsItem<TSettings>(prop, Settings));
                            } else if (prop.PropertyType.IsEnum)
                            {
                                items.Add(new EnumSettingsItem<TSettings>(prop, Settings));
                            }
                        }
                    }
                }
            }
            return items;
        }

        public virtual Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return null;
        }
    }

    public class BoolSettingsAction<TSettings> : ISearchAction<string>
    {
        public static BoolSettingsAction<TSettings> Instance { get; private set; } = new BoolSettingsAction<TSettings>();
        public string Name { get; set; } = "Toggle";

        public bool CloseAfterExecute => false;

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

    public class ValueSetAction<TSettings> : ISearchAction<string>
    {
        public string Name { get; set; }

        public object Value { get; set; }

        public bool CloseAfterExecute => false;

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            if (parameter is EnumSettingsItem<TSettings> item)
            {
                item.Property.SetValue(item.Settings, Value);
            }
            if (parameter is SelectionSettingsItem<TSettings> item2)
            {
                item2.Property.SetValue(item2.Settings, Value);
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

        public double NewValue { get; set; }

        internal FloatAction floatAction;

        public string Name { get; set; } = "Set";

        public bool CloseAfterExecute => false;

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            if (parameter is FloatSettingsItem<TSettings> item)
            {
                if (item.Property.GetCustomAttributes(true).OfType<NumberOptionAttribute>().FirstOrDefault() is NumberOptionAttribute attr)
                {
                    var oldValue = Convert.ToDouble(item.Property.GetValue(item.Settings));
                    
                    var newValue = oldValue;
                    switch (floatAction)
                    {
                        case FloatAction.Add:
                            newValue = oldValue + attr.Tick;
                            break;
                        case FloatAction.Subtract:
                            newValue = oldValue - attr.Tick;
                            break;
                        case FloatAction.Set:
                            newValue = NewValue;
                            break;
                    }

                    newValue = Math.Min(attr.Max, Math.Max(attr.Min, newValue));

                    item.Property.SetValue(item.Settings, Convert.ChangeType(newValue, item.Property.PropertyType));
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

    public class EnumSettingsItem<TSettings> : ISearchItem<string>
    {
        internal PropertyInfo Property { get; set; }
        internal TSettings Settings { get; set; }

        private EnumSettingsItem() { }

        public EnumSettingsItem(PropertyInfo property, TSettings settings)
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
                if (type.IsEnum)
                {
                    if (attr is GenericOptionAttribute)
                    {
                        var values = type.GetEnumValues();
                        foreach(var value in values)
                        {
                            actions.Add(new ValueSetAction<TSettings> { Name = type.GetEnumName(value), Value = value});
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

    public class SelectionSettingsItem<TSettings> : ISearchItem<string>
    {
        internal PropertyInfo Property { get; set; }
        internal TSettings Settings { get; set; }

        private SelectionSettingsItem() { }

        public SelectionSettingsItem(PropertyInfo property, TSettings settings)
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
                if (attr is SelectionOptionAttribute selectionAttr)
                {
                    if (selectionAttr.Options != null)
                    {
                        foreach (var value in selectionAttr.Options)
                        {
                            if (value.GetType() == Property.PropertyType)
                            {
                                actions.Add(new ValueSetAction<TSettings> { Name = value.ToString(), Value = value });
                            }
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

        public string TopRight => Property.GetValue(Settings)?.ToString()??"Null";

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

        public double NewValue { get; set; } = float.NaN;

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
                var attr = Property.GetCustomAttributes(true).OfType<NumberOptionAttribute>().FirstOrDefault();
                if (attr is NumberOptionAttribute)
                {
                    if (!string.IsNullOrWhiteSpace(attr.Name)) keys.Add(new SettingsKey { Key = attr.Name });
                    if (!string.IsNullOrWhiteSpace(attr.Description)) keys.Add(new SettingsKey { Key = attr.Description });
                }
                if (!double.IsNaN(NewValue))
                {
                    var length = keys.Count;
                    for (int i = 0; i < length; ++i)
                    {
                        keys.Add(new SettingsKey { Key = keys[i].Key + " " + NewValue.ToString() });
                    }
                    if (keys.Count > 0) keys.Add(new SettingsKey { Key = NewValue.ToString() });
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
                var attr = Property.GetCustomAttributes(true).OfType<NumberOptionAttribute>().FirstOrDefault();
                if (type.IsNumberType())
                {
                    if (attr is NumberOptionAttribute)
                    {
                        if (double.IsNaN(NewValue)) 
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

        public string TopRight => Convert.ToDouble(Property.GetValue(Settings)).ToString("0.####");

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
