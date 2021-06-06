﻿using Newtonsoft.Json;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            foreach(var property in type.GetProperties())
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

        public Hotkey SearchShortcut { get; set; } = new Hotkey(Key.F, ModifierKeys.Control);
        public Hotkey SearchShortcutGlobal { get; set; } = new Hotkey(Key.F, ModifierKeys.Control | ModifierKeys.Alt);
        public string HotkeyText { get; set; } = string.Empty;
        public string HotkeyTextGlobal { get; set; } = string.Empty;

        public double Threshold { get; set; } = 0.55;

        public bool ExpandAllItems { get; set; } = true;
        public bool ShowSeperator { get; set; } = false;
        public bool IncrementalUpdate { get; set; } = false;
        public int MaxNumberResults { get; set; } = 20;
        public bool EnableExternalGameActions { get; set; } = true;
        public bool EnableExternalItems { get; set; } = true;
        public int AsyncItemsDelay { get; set; } = 500;
        public SortedDictionary<string, AssemblyOptions> EnabledAssemblies { get; set; } = new SortedDictionary<string, AssemblyOptions>();
        public float PrioritizationThreshold { get; set; } = 0.55f;
        public int MaxPrioritizedGames { get; set; } = 1;
        public bool InstallationStatusFirst { get; set; } = true;
        public float ITADThreshold { get; set; } = 0.75f;
        public string ITADOverride { get; set; } = "+";
        public bool ITADEnabled { get; set; } = true;
        public SortedDictionary<string, ITADShopOption> EnabledITADShops { get; set; } = new SortedDictionary<string, ITADShopOption>();
        public bool EnableGlassEffect { get; set; } = true;
        public int OuterBorderThickness { get; set; } = 12;
        public bool EnableGlobalHotkey { get; set; } = false;
        

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
            changed |= EnabledAssemblies.Keys
                .Concat(previousSettings.EnabledAssemblies.Keys)
                .Aggregate(false, (v, key) => v || !(EnabledAssemblies
                    .Where(p => p.Key == key).Select(p => p.Value).FirstOrDefault()?.Equals( 
                    previousSettings.EnabledAssemblies.Where(p => p.Key == key).Select(p => p.Value).FirstOrDefault())??true));
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