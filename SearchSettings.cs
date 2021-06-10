using Newtonsoft.Json;
using Playnite.SDK;
using QuickSearch.SearchItems.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace QuickSearch
{
    public class SearchSettings : ISettings
    {
        private readonly SearchPlugin plugin;

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
        }

        public static void CopyProperties(SearchSettings from, SearchSettings to)
        {
            if (from == null || to == null)
            {
                return;
            }
            var type = typeof(SearchSettings);
            foreach (var property in type.GetProperties())
            {
                if (property.CanRead && property.CanWrite && !Attribute.IsDefined(property, typeof(JsonIgnoreAttribute)))
                {
                    property.SetValue(to, property.GetValue(from));
                }
            }
        }

        public static bool ValuePropertyChanged(SearchSettings a, SearchSettings b)
        {
            if (a == null || b == null)
            {
                return true;
            }
            var type = typeof(SearchSettings);
            foreach (var property in type.GetProperties())
            {
                if (property.PropertyType.IsValueType && property.CanRead && !Attribute.IsDefined(property, typeof(JsonIgnoreAttribute)))
                {
                    if (!property.GetValue(a).Equals(property.GetValue(b)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void callback()
        {

        }

        public Hotkey SearchShortcut { get; set; } = new Hotkey(Key.F, ModifierKeys.Control);
        public Hotkey SearchShortcutGlobal { get; set; } = new Hotkey(Key.F, ModifierKeys.Control | ModifierKeys.Alt);
        public string HotkeyText { get; set; } = string.Empty;
        public string HotkeyTextGlobal { get; set; } = string.Empty;

        [GenericOption("Modfier Enum Option")]
        public Visibility TestOption { get; set; }
        [SelectionOption("Selection Option", new object[] {"First", "Second", "Not Fourth"})]
        public string SelectionOptions { get; set; }
        [NumberOption("Search Threshold", Min = 0, Max = 1, Tick = 0.01f)]
        public double Threshold { get; set; } = 0.55;
        [GenericOption("Expand all items", Description = "If enabled, always show more detailed version of items.")]
        public bool ExpandAllItems { get; set; } = true;
        [GenericOption("Show Seperator", Description = "If enabled, show line inbetween items.")]
        public bool ShowSeperator { get; set; } = false;
        public bool IncrementalUpdate { get; set; } = false;
        public int MaxNumberResults { get; set; } = 20;
        [GenericOption("Enable External GameActions")]
        public bool EnableExternalGameActions { get; set; } = true;
        [GenericOption("Enable External SearchItems")]
        public bool EnableExternalItems { get; set; } = true;
        [NumberOption("Delay until requesting async items", Min = 10, Max = 1000, Tick = 1f)]
        public int AsyncItemsDelay { get; set; } = 500;
        public SortedDictionary<string, AssemblyOptions> EnabledAssemblies { get; set; } = new SortedDictionary<string, AssemblyOptions>();
        public float PrioritizationThreshold { get; set; } = 0.55f;
        public int MaxPrioritizedGames { get; set; } = 1;
        [GenericOption("Prioritize Installation Status for Sorting")]
        public bool InstallationStatusFirst { get; set; } = true;
        [NumberOption("ITAD Threshold", Min = 0, Max = 1, Tick = 0.01f)]
        public float ITADThreshold { get; set; } = 0.75f;
        public string ITADOverride { get; set; } = "+";
        [GenericOption("Enable ITAD Search")]
        public bool ITADEnabled { get; set; } = true;
        public SortedDictionary<string, ITADShopOption> EnabledITADShops { get; set; } = new SortedDictionary<string, ITADShopOption>();
        [GenericOption("Glass Effect", Description = "If enabled, blur background under the search window.")]
        public bool EnableGlassEffect { get => enableGlassEffect; set { enableGlassEffect = value; if (value) SearchPlugin.Instance?.EnableGlassEffect(); else SearchPlugin.Instance?.DisableGlassEffect(); } }
        private bool enableGlassEffect = true;
        [NumberOption("Border Thickness", Min = 0, Max = 30, Tick = 1)]
        public int OuterBorderThickness { get => outerBorderThickness; set { outerBorderThickness = value; plugin?.UpdateBorder(value); } }

        [GenericOption("Enable Global Hotkey")]
        public bool EnableGlobalHotkey { get => enableGlobalHotkey; set { enableGlobalHotkey = value; if (value) SearchPlugin.Instance?.RegisterGlobalHotkey(); else SearchPlugin.Instance?.UnregisterGlobalHotkey(); } }
        private bool enableGlobalHotkey = false;
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

        public class ITADShopOption
        {
            public ITADShopOption(string name)
            {
                Name = name;
            }
            public string Name { get; set; }
            public bool Enabled { get; set; } = true;
        }

        public delegate void SettingsChangedHandler(SearchSettings newSettings, SearchSettings oldSettings);
        public event SettingsChangedHandler SettingsChanged;

        // Parameterless constructor must exist if you want to use LoadPluginSettings method.
        public SearchSettings()
        {

        }

        public SearchSettings(SearchPlugin plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<SearchSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            if (savedSettings != null)
            {
                CopyProperties(savedSettings, this);
            }
        }

        private SearchSettings previousSettings = null;
        private int outerBorderThickness = 12;

        public void BeginEdit()
        {
            previousSettings = JsonConvert.DeserializeObject<SearchSettings>(JsonConvert.SerializeObject(this));
            // Code executed when settings view is opened and user starts editing values.
            HotkeyText = $"{SearchShortcut.Modifiers} + {SearchShortcut.Key}";
            HotkeyTextGlobal = $"{SearchShortcutGlobal.Modifiers} + {SearchShortcutGlobal.Key}";
        }

        public void CancelEdit()
        {
            CopyProperties(previousSettings, this);
            previousSettings = null;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            bool changed = false;
            changed |= ValuePropertyChanged(this, previousSettings);
            changed |= !SearchShortcut.Equals(previousSettings.SearchShortcut);
            changed |= !SearchShortcutGlobal.Equals(previousSettings.SearchShortcutGlobal);
            changed |= EnabledAssemblies.Keys
                .Concat(previousSettings.EnabledAssemblies.Keys)
                .Aggregate(false, (v, key) => v || !(EnabledAssemblies
                    .Where(p => p.Key == key).Select(p => p.Value).FirstOrDefault()?.Equals(
                    previousSettings.EnabledAssemblies.Where(p => p.Key == key).Select(p => p.Value).FirstOrDefault()) ?? true));
            if (changed)
                SettingsChanged?.Invoke(this, previousSettings);
            previousSettings = null;
            plugin.SavePluginSettings(this);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}