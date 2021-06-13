using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch.SearchItems.Settings
{
    /// <summary>
    /// ItemSource that provides items to display and change options by exposing read/write properties of <typeparamref name="TSettings"/>.
    /// To expose an option (which must be a property), one of the following
    /// Attributes needs to be attached: <see cref="GenericOptionAttribute"/> (allows to set bools and enums, will only display option for other types),
    /// <see cref="NumberOptionAttribute"/> (allows to set a number option, like float or int) or 
    /// <see cref="SelectionOptionAttribute"/> (allows to set an option to a value from a specified list of values).
    /// </summary>
    /// <typeparam name="TSettings">The settings object type.</typeparam>
    public class SettingsItemSource<TSettings> : ISearchSubItemSource<string>
    {
        internal SettingsItemSource() {}
        /// <summary>
        /// Constructs a <see cref="SettingsItemSource{TSettings}"/> from a 
        /// settings object.
        /// </summary>
        /// <param name="settings">The settings object containing option properties.</param>
        public SettingsItemSource(TSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings), "Settings object cannot be null.");
            Settings = settings;
        }
        internal TSettings Settings { get; set; }
        /// <inheritdoc cref="ISearchSubItemSource{TKey}.Prefix"/>
        public virtual string Prefix { get; set; } = "Settings";
        /// <inheritdoc cref="ISearchSubItemSource{TKey}.DisplayAllIfQueryIsEmpty"/>
        public virtual bool DisplayAllIfQueryIsEmpty => true;
        /// <inheritdoc cref="ISearchItemSource{TKey}.DependsOnQuery"/>
        public virtual bool DependsOnQuery => true;
        /// <inheritdoc cref="ISearchItemSource{TKey}.GetItems(string)"/>
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
                            var last = query.Split(' ').LastOrDefault() ?? string.Empty;
                            if (double.TryParse(last, out double value))
                            {
                                items.Add(new DoubleSettingsItem<TSettings>(prop, Settings) { NewValue = value });
                            } else
                            {
                                items.Add(new DoubleSettingsItem<TSettings>(prop, Settings));
                            }
                        } else if (attr is SelectionOptionAttribute)
                        {
                            items.Add(new SelectionSettingsItem<TSettings>(prop, Settings));
                        } else
                        {
                            if (prop.PropertyType == typeof(bool))
                            {
                                items.Add(new BoolSettingsItem<TSettings>(prop, Settings));
                            } else
                            {
                                items.Add(new EnumSettingsItem<TSettings>(prop, Settings));
                            }
                        }
                    }
                }
            }
            return items.OrderBy(item => item.TopLeft);
        }
        /// <inheritdoc cref="ISearchItemSource{TKey}.GetItemsTask(string, IReadOnlyList{Candidate})"/>
        public virtual Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return null;
        }
    }
    /// <summary>
    /// Action to toggle a bool option.
    /// Only works for <see cref="BoolSettingsItem{TSettings}"/>.
    /// </summary>
    /// <typeparam name="TSettings">The settings object type.</typeparam>
    public class BoolSettingsAction<TSettings> : ISearchAction<string>
    {
        /// <summary>
        /// Static instance of a <see cref="BoolSettingsAction{TSettings}"/>.
        /// Can be used instead of creating a new object for each item. 
        /// Only works for <see cref="BoolSettingsItem{TSettings}"/>.
        /// </summary>
        public static readonly BoolSettingsAction<TSettings> Instance = new BoolSettingsAction<TSettings>();
        /// <inheritdoc cref="ISearchAction{TKey}.Name"/>
        public string Name { get; set; } = "Toggle";
        /// <inheritdoc cref="ISearchAction{TKey}.CloseAfterExecute"/>
        public bool CloseAfterExecute => false;

#pragma warning disable CS0067
        /// <inheritdoc cref="System.Windows.Input.ICommand.CanExecuteChanged"/>
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067
        /// <inheritdoc cref="System.Windows.Input.ICommand.CanExecute(object)"/>
        public bool CanExecute(object parameter)
        {
            return true;
        }
        /// <inheritdoc cref="System.Windows.Input.ICommand.Execute(object)"/>
        public void Execute(object parameter)
        {
            if (parameter is BoolSettingsItem<TSettings> item)
            {
                item.Property.SetValue(item.Settings, !(bool)item.Property.GetValue(item.Settings));
            }
        }
    }
    /// <summary>
    /// Sets a value for an <see cref="EnumSettingsItem{TSettings}"/> or
    /// <see cref="SelectionSettingsItem{TSettings}"/>.
    /// </summary>
    /// <typeparam name="TSettings">The settings object type.</typeparam>
    public class ValueSetAction<TSettings> : ISearchAction<string>
    {
        internal ValueSetAction() { }
        /// <summary>
        /// Construct a <see cref="ValueSetAction{TSettings}"/> that sets the value of a 
        /// <see cref="EnumSettingsItem{TSettings}"/> or <see cref="SelectionSettingsItem{TSettings}"/>.
        /// </summary>
        /// <param name="value">Value to set.</param>
        public ValueSetAction(object value)
        {
            Value = value;
        }
        /// <inheritdoc cref="ISearchAction{TKey}.Name"/>
        public string Name { get; set; }
        /// <summary>
        /// Value to set the property to.
        /// </summary>
        public object Value { get; set; }
        /// <inheritdoc cref="ISearchAction{TKey}.CloseAfterExecute"/>
        public bool CloseAfterExecute => false;

#pragma warning disable CS0067
        /// <inheritdoc cref="System.Windows.Input.ICommand.CanExecuteChanged"/>
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067
        /// <inheritdoc cref="System.Windows.Input.ICommand.CanExecute(object)"/>
        public bool CanExecute(object parameter)
        {
            return true;
        }
        /// <inheritdoc cref="System.Windows.Input.ICommand.Execute(object)"/>
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
    /// <summary>
    /// Increments, decrements or sets the value of an <see cref="DoubleSettingsItem{TSettings}"/>.
    /// </summary>
    /// <typeparam name="TSettings">The settings object type.</typeparam>
    public class FloatSettingsAction<TSettings> : ISearchAction<string>
    {
        /// <summary>
        /// Action that increments the value by one Tick.
        /// </summary>
        public static readonly FloatSettingsAction<TSettings> IncrementAction = new FloatSettingsAction<TSettings>() { floatAction = FloatAction.Add, Name = " + " };
        /// <summary>
        /// Action that decrements the value by one Tick.
        /// </summary>
        public static readonly FloatSettingsAction<TSettings> DecrementAction = new FloatSettingsAction<TSettings>() { floatAction = FloatAction.Subtract, Name = " - " };
        internal FloatSettingsAction() {}
        /// <summary>
        /// Construct a <see cref="FloatSettingsAction{TSettings}"/> as a Set-Action that sets
        /// the value of a <see cref="DoubleSettingsItem{TSettings}"/>.
        /// </summary>
        /// <param name="value">Value to set.</param>
        public FloatSettingsAction(double value)
        {
            NewValue = value;
            floatAction = FloatAction.Set;
        }
        /// <summary>
        /// kind of action that is performed.
        /// </summary>
        public enum FloatAction
        {
            /// <summary>
            /// Add a tick.
            /// </summary>
            Add,
            /// <summary>
            /// Subctract a tick.
            /// </summary>
            Subtract,
            /// <summary>
            /// Set to specific value.
            /// </summary>
            Set
        }
        /// <summary>
        /// Value to set the option to when executing this action.
        /// </summary>
        public double NewValue { get; set; }

        internal FloatAction floatAction;
        /// <inheritdoc cref="ISearchAction{TKey}.Name"/>
        public string Name { get; set; } = "Set";
        /// <inheritdoc cref="ISearchAction{TKey}.CloseAfterExecute"/>
        public bool CloseAfterExecute => false;

#pragma warning disable CS0067
        /// <inheritdoc cref="System.Windows.Input.ICommand.CanExecuteChanged"/>
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067
        /// <inheritdoc cref="System.Windows.Input.ICommand.CanExecute(object)"/>
        public bool CanExecute(object parameter)
        {
            return true;
        }
        /// <inheritdoc cref="System.Windows.Input.ICommand.Execute(object)"/>
        public void Execute(object parameter)
        {
            if (parameter is DoubleSettingsItem<TSettings> item)
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
    /// <summary>
    /// <see langword="abstract"/> base class for settings items.
    /// </summary>
    /// <typeparam name="TSettings"></typeparam>
    public abstract class SettingsItem<TSettings> : ISearchItem<string>
    {
        internal PropertyInfo Property { get; set; }
        internal TSettings Settings { get; set; }

        /// <inheritdoc cref="ISearchItem{TKey}.Keys"/> 
        public virtual IList<ISearchKey<string>> Keys
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

        /// <inheritdoc cref="ISearchItem{TKey}.ScoreMode"/> 
        public ScoreMode ScoreMode => ScoreMode.WeightedMaxScore;
        /// <inheritdoc cref="ISearchItem{TKey}.Icon"/> 
        public Uri Icon { get; set; } = null;
        /// <inheritdoc cref="ISearchItem{TKey}.TopLeft"/> 
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
        /// <inheritdoc cref="ISearchItem{TKey}.TopRight"/> 
        public virtual string TopRight => Property.GetValue(Settings).ToString();
        /// <inheritdoc cref="ISearchItem{TKey}.BottomLeft"/> 
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
        /// <inheritdoc cref="ISearchItem{TKey}.BottomCenter"/> 
        public string BottomCenter => null;
        /// <inheritdoc cref="ISearchItem{TKey}.BottomRight"/> 
        public string BottomRight => null;
        /// <inheritdoc cref="ISearchItem{TKey}.IconChar"/> 
        public char? IconChar { get; set; } = IconChars.Settings;
        /// <inheritdoc cref="ISearchItem{TKey}.Actions"/>
        public virtual IList<ISearchAction<string>> Actions => throw new NotImplementedException();
    }

    /// <summary>
    /// Item that sets the value of a bool property with <see cref="GenericOptionAttribute"/> attached to it.
    /// </summary>
    /// <typeparam name="TSettings">The settings object type.</typeparam>
    public class BoolSettingsItem<TSettings> : SettingsItem<TSettings>
    {
        internal BoolSettingsItem() {}
        /// <summary>
        /// Constructs a <see cref="BoolSettingsItem{TSettings}"/> using a given <see cref="PropertyInfo"/> and
        /// <typeparamref name="TSettings"/> settings object. None of which can be <see langword="null"/>.
        /// </summary>
        /// <param name="property">Bool property to set.</param>
        /// <param name="settings">Settings object that has property <paramref name="property"/>.</param>
        public BoolSettingsItem(PropertyInfo property, TSettings settings) {
            if (property == null || settings == null) throw new ArgumentNullException();
            Property = property;
            Settings = settings;
        }
        
        /// <inheritdoc cref="ISearchItem{TKey}.Actions"/> 
        public override IList<ISearchAction<string>> Actions
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
    }
    /// <summary>
    /// Item that sets the value of a enum property with <see cref="GenericOptionAttribute"/> attached to it.
    /// </summary>
    /// <typeparam name="TSettings">The settings object type.</typeparam>
    public class EnumSettingsItem<TSettings> : SettingsItem<TSettings>
    {
        internal EnumSettingsItem() { }
        /// <summary>
        /// Constructs a <see cref="EnumSettingsItem{TSettings}"/> using a given <see cref="PropertyInfo"/> and
        /// <typeparamref name="TSettings"/> settings object. None of which can be <see langword="null"/>.
        /// </summary>
        /// <param name="property">Enum property to set.</param>
        /// <param name="settings">Settings object that has property <paramref name="property"/>.</param>
        public EnumSettingsItem(PropertyInfo property, TSettings settings)
        {
            if (property == null || settings == null) throw new ArgumentNullException();
            Property = property;
            Settings = settings;
        }

        /// <inheritdoc cref="ISearchItem{TKey}.Actions"/>
        public override IList<ISearchAction<string>> Actions
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
    }
    /// <summary>
    /// Item that sets the value of a enum property with <see cref="GenericOptionAttribute"/> attached to it.
    /// </summary>
    /// <typeparam name="TSettings">The settings object type.</typeparam>
    public class SelectionSettingsItem<TSettings> : SettingsItem<TSettings>
    {
        private SelectionSettingsItem() { }
        /// <summary>
        /// Constructs a <see cref="SelectionSettingsItem{TSettings}"/> using a given <see cref="PropertyInfo"/> and
        /// <typeparamref name="TSettings"/> settings object. None of which can be <see langword="null"/>.
        /// </summary>
        /// <param name="property">Property to set.</param>
        /// <param name="settings">Settings object that has property <paramref name="property"/>.</param>
        public SelectionSettingsItem(PropertyInfo property, TSettings settings)
        {
            if (property == null || settings == null) throw new ArgumentNullException();
            Property = property;
            Settings = settings;
        }
        
        /// <inheritdoc cref="ISearchItem{TKey}.Actions"/>
        public override IList<ISearchAction<string>> Actions
        {
            get
            {
                var actions = new List<ISearchAction<string>>();
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
    }
    /// <summary>
    /// Item that sets the value of a number property with <see cref="NumberOptionAttribute"/> attached to it.
    /// </summary>
    /// <typeparam name="TSettings">The settings object type.</typeparam>
    public class DoubleSettingsItem<TSettings> : SettingsItem<TSettings>
    {
        private DoubleSettingsItem() { }
        /// <summary>
        /// The value to set the property to.
        /// </summary>
        public double NewValue { get; set; } = float.NaN;
        /// <summary>
        /// Constructs a <see cref="DoubleSettingsItem{TSettings}"/> using a given <see cref="PropertyInfo"/> and
        /// <typeparamref name="TSettings"/> settings object. None of which can be <see langword="null"/>.
        /// </summary>
        /// <param name="property">Property to set.</param>
        /// <param name="settings">Settings object that has property <paramref name="property"/>.</param>
        public DoubleSettingsItem(PropertyInfo property, TSettings settings)
        {
            if (property == null || settings == null) throw new ArgumentNullException();
            Property = property;
            Settings = settings;
        }

        public override string TopRight => Convert.ToDouble(Property.GetValue(Settings)).ToString("0.###");

        /// <inheritdoc cref="ISearchItem{TKey}"/>
        public override IList<ISearchKey<string>> Keys
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

        /// <inheritdoc cref="ISearchItem{TKey}.Actions"/>
        public override IList<ISearchAction<string>> Actions
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
                            actions.Add(FloatSettingsAction<TSettings>.IncrementAction);
                            actions.Add(FloatSettingsAction<TSettings>.DecrementAction);
                        } else
                        {
                            actions.Add(new FloatSettingsAction<TSettings> { Name = $"Set to {NewValue}", NewValue = NewValue, floatAction = FloatSettingsAction<TSettings>.FloatAction.Set });
                        }
                    }
                }
                return actions;
            }
        }
    }
    /// <summary>
    /// Simple key with weight 1.
    /// </summary>
    public class SettingsKey : ISearchKey<string>
    {
        internal SettingsKey() { }
        /// <summary>
        /// Construct <see cref="SettingsKey"/> with a key and weight.
        /// </summary>
        /// <param name="key">Key to match.</param>
        /// <param name="weight">Weight of the key.</param>
        public SettingsKey(string key, float weight = 1f)
        {
            Key = key;
            Weight = weight;
        }
        /// <inheritdoc cref="ISearchKey{TKey}.Key"/>
        public string Key { get; set; }
        /// <inheritdoc cref="ISearchKey{TKey}.Weight"/>
        public float Weight { get; } = 1f;
    }
}
