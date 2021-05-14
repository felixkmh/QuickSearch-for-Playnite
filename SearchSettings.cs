using Newtonsoft.Json;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Search
{
    public class SearchSettings : ISettings
    {
        private readonly SearchPlugin plugin;

        public struct Hotkey
        {
            public Hotkey(Key key, ModifierKeys modifier)
            {
                this.Key = key;
                this.Modifiers = modifier;
            }
            public Key Key { get; set; }
            public ModifierKeys Modifiers { get; set; }
        }

        public Hotkey SearchShortcut { get; set; } = new Hotkey(Key.F, ModifierKeys.Control);
        public string Option1 { get; set; } = string.Empty;

        public double Threshold { get; set; } = 0.4;

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
                Option1 = $"{savedSettings.SearchShortcut.Modifiers} + {savedSettings.SearchShortcut.Key}";
                Threshold = savedSettings.Threshold;
            }
        }

        private SearchSettings previousSettings = null; 

        public void BeginEdit()
        {
            previousSettings = JsonConvert.DeserializeObject<SearchSettings>(JsonConvert.SerializeObject(this));
            // Code executed when settings view is opened and user starts editing values.
            Option1 = $"{SearchShortcut.Modifiers} + {SearchShortcut.Key}";
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            this.SearchShortcut = previousSettings.SearchShortcut;
            this.Threshold = previousSettings.Threshold;
            previousSettings = null;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
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