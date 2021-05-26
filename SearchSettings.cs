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

        public struct Hotkey : IEquatable<Hotkey>
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

        public Hotkey SearchShortcut { get; set; } = new Hotkey(Key.F, ModifierKeys.Control);
        public string HotkeyText { get; set; } = string.Empty;

        public double Threshold { get; set; } = 0.55;

        public bool ExpandAllItems { get; set; } = false;
        public bool ShowSeperator { get; set; } = false;
        public bool IncrementalUpdate { get; set; } = false;
        public int MaxNumberResults { get; set; } = 20;
        public bool EnableExternalGameActions { get; set; } = false;
        public bool EnableExternalItems { get; set; } = false;
        public int AsyncItemsDelay { get; set; } = 500;
        public Dictionary<string, AssemblyOptions> EnabledAssemblies { get; set; } = new Dictionary<string, AssemblyOptions>();
        public float PrioritizationThreshold { get; set; } = 0.55f;
        public int MaxPrioritizedGames { get; set; } = 1;
        public bool InstallationStatusFirst { get; set; } = false;

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

        public delegate void SettingsChangedHandler(SearchSettings newSettings, SearchSettings oldSettings);
        public event SettingsChangedHandler SettingsChanged;

        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonIgnore` ignore attribute.
        [JsonIgnore]
        public bool OptionThatWontBeSaved { get; set; } = false;

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
                SearchShortcut = savedSettings.SearchShortcut;
                HotkeyText = $"{savedSettings.SearchShortcut.Modifiers} + {savedSettings.SearchShortcut.Key}";
                Threshold = savedSettings.Threshold;
                ExpandAllItems = savedSettings.ExpandAllItems;
                ShowSeperator = savedSettings.ShowSeperator;
                IncrementalUpdate = savedSettings.IncrementalUpdate;
                MaxNumberResults = savedSettings.MaxNumberResults;
                EnableExternalGameActions = savedSettings.EnableExternalGameActions;
                EnableExternalItems = savedSettings.EnableExternalItems;
                AsyncItemsDelay = savedSettings.AsyncItemsDelay;
                EnabledAssemblies = savedSettings.EnabledAssemblies;
                PrioritizationThreshold = savedSettings.PrioritizationThreshold;
                MaxPrioritizedGames = savedSettings.MaxPrioritizedGames;
                InstallationStatusFirst = savedSettings.InstallationStatusFirst;
            }
        }

        private SearchSettings previousSettings = null; 

        public void BeginEdit()
        {
            previousSettings = JsonConvert.DeserializeObject<SearchSettings>(JsonConvert.SerializeObject(this));
            // Code executed when settings view is opened and user starts editing values.
            HotkeyText = $"{SearchShortcut.Modifiers} + {SearchShortcut.Key}";
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            this.SearchShortcut = previousSettings.SearchShortcut;
            this.Threshold = previousSettings.Threshold;
            this.ExpandAllItems = previousSettings.ExpandAllItems;
            this.ShowSeperator = previousSettings.ShowSeperator;
            this.IncrementalUpdate = previousSettings.IncrementalUpdate;
            this.MaxNumberResults = previousSettings.MaxNumberResults;
            this.EnableExternalGameActions = previousSettings.EnableExternalGameActions;
            this.EnableExternalItems = previousSettings.EnableExternalItems;
            this.AsyncItemsDelay = previousSettings.AsyncItemsDelay;
            this.EnabledAssemblies = previousSettings.EnabledAssemblies;
            this.MaxPrioritizedGames = previousSettings.MaxPrioritizedGames;
            this.PrioritizationThreshold = previousSettings.PrioritizationThreshold;
            this.InstallationStatusFirst = previousSettings.InstallationStatusFirst;
            previousSettings = null;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            bool changed = false;
            changed |= !SearchShortcut.Equals(previousSettings.SearchShortcut);
            changed |= Threshold                 != previousSettings.Threshold;
            changed |= ExpandAllItems            != previousSettings.ExpandAllItems;
            changed |= ShowSeperator             != previousSettings.ShowSeperator;
            changed |= IncrementalUpdate         != previousSettings.IncrementalUpdate;
            changed |= MaxNumberResults          != previousSettings.MaxNumberResults;
            changed |= EnableExternalGameActions != previousSettings.EnableExternalGameActions;
            changed |= EnableExternalItems       != previousSettings.EnableExternalItems;
            changed |= AsyncItemsDelay           != previousSettings.AsyncItemsDelay;
            changed |= EnabledAssemblies.Count   != previousSettings.EnabledAssemblies.Count;
            changed |= PrioritizationThreshold   != previousSettings.PrioritizationThreshold;
            changed |= MaxPrioritizedGames       != previousSettings.MaxPrioritizedGames;
            changed |= InstallationStatusFirst != previousSettings.InstallationStatusFirst;
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