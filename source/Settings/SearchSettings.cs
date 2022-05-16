using Newtonsoft.Json;
using Playnite.SDK;
using QuickSearch.Attributes;
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
    public partial class SearchSettings : ISettings
    {
        private readonly SearchPlugin plugin;

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

        public bool EnableTagCreation { get; set; } = false;

        public Guid IgnoreTagId { get; set; } = Guid.Empty;

        [GenericOption("LOC_QS_LocalHotkeyShort")]
        public Hotkey SearchShortcut { get; set; } = new Hotkey(Key.F, ModifierKeys.Control);
        [GenericOption("LOC_QS_GlobalHotkeyShort")]
        public Hotkey SearchShortcutGlobal { get; set; } = new Hotkey(Key.F, ModifierKeys.Control | ModifierKeys.Alt);
        public string HotkeyText { get; set; } = string.Empty;
        public string HotkeyTextGlobal { get; set; } = string.Empty;

        [NumberOption("LOC_QS_SearchThresholdShort", Min = 0, Max = 1, Tick = 0.01f)]
        public double Threshold { get; set; } = 0.55;
        [GenericOption("LOC_QS_ExpandAllItems")]
        public bool ExpandAllItems
        {
            get => expandAllItems;
            set { expandAllItems = value; plugin?.searchWindow?.UpdateListBox(plugin.searchWindow.SearchResults.Items.Count, true); }
        }
        [GenericOption("LOC_QS_ShowSeparator")]
        public bool ShowSeperator { get; set; } = false;
        [GenericOption("LOC_QS_LowerPriorityAddingShort")]
        public bool IncrementalUpdate { get; set; } = false;
        [NumberOption("LOC_QS_MaxResults", Min = 0, Tick = 1)]
        public int MaxNumberResults { get; set; } = 20;
        [GenericOption("LOC_QS_AllowExternalGameActions")]
        public bool EnableExternalGameActions { get; set; } = true;
        [GenericOption("LOC_QS_AllowExternalItems")]
        public bool EnableExternalItems { get; set; } = true;
        [NumberOption("LOC_QS_AsyncDelayShort", Min = 10, Max = 1000, Tick = 10f)]
        public int AsyncItemsDelay { get; set; } = 500;
        public SortedDictionary<string, AssemblyOptions> EnabledAssemblies { get; set; } = new SortedDictionary<string, AssemblyOptions>();
        public float PrioritizationThreshold { get; set; } = 0.55f;
        public int MaxPrioritizedGames { get; set; } = 1;
        [GenericOption("LOC_QS_PrioritizeInstallationStatusShort")]
        public bool InstallationStatusFirst { get; set; } = true;
        [NumberOption("LOC_QS_ITADThresholdShort", Min = 0, Max = 1, Tick = 0.01f)]
        public float ITADThreshold { get; set; } = 0.75f;
        public string ITADOverride { get; set; } = "+";
        [GenericOption("LOC_QS_EnableITAD")]
        public bool ITADEnabled { get; set; } = true;
        public SortedDictionary<string, ITADShopOption> EnabledITADShops { get; set; } = new SortedDictionary<string, ITADShopOption>();
        [GenericOption("LOC_QS_GlassEffectShort", Description = "LOC_QS_GlassEffect")]
        public bool EnableGlassEffect
        {
            get => enableGlassEffect;
            set
            {
                if (value)
                {
                    SearchPlugin.Instance?.EnableGlassEffect();
                }
                else
                {
                    SearchPlugin.Instance?.DisableGlassEffect();
                }
                enableGlassEffect = value;
            }
        }
        private bool enableGlassEffect = true;
        [NumberOption("LOC_QS_BorderWidthShort", Min = 0, Max = 30, Tick = 1)]
        public int OuterBorderThickness
        {
            get => outerBorderThickness;
            set 
            { 
                plugin?.searchWindow?.UpdateListBox(plugin.searchWindow.SearchResults.Items.Count, true);
                outerBorderThickness = value; plugin?.UpdateBorder(value);
                if (EnableGlassEffect)
                {
                    plugin?.DisableGlassEffect();
                    plugin?.EnableGlassEffect();
                }
                else
                {
                    plugin?.EnableGlassEffect();
                    plugin?.DisableGlassEffect();
                }
            }
        }
        [GenericOption("LOC_QS_FilterSubItems")]
        public bool EnableFilterSubSources { get; set; } = true;
        [GenericOption("LOC_QS_GlobalHotkeyToggle")]
        public bool EnableGlobalHotkey { get => enableGlobalHotkey; set { enableGlobalHotkey = value; if (value) SearchPlugin.Instance?.RegisterGlobalHotkey(); else SearchPlugin.Instance?.UnregisterGlobalHotkey(); } }
        private bool enableGlobalHotkey = false;
        [GenericOption("LOC_QS_PreferCoverArt")]
        public bool PreferCoverArt { get; set; } = false;
        [GenericOption("LOC_QS_EnableDetailsView")]
        public bool EnableDetailsView { get; set; } = true;
        [NumberOption("LOC_QS_SearchWindowWidth", Min = 300, Max = 800, Tick = 5)]
        public int SearchWindowWidth { get => searchWindowWidth; 
            set
            {
                if (plugin?.searchWindow != null)
                {
                    plugin.searchWindow.WindowGrid.Width = value;
                    plugin.searchWindow.UpdateListBox(plugin.searchWindow.SearchResults.Items.Count, true);
                    plugin.UpdateBorder(OuterBorderThickness); 
                    if (EnableGlassEffect)
                    {
                        plugin.DisableGlassEffect();
                        plugin.EnableGlassEffect();
                    } else
                    {
                        plugin.EnableGlassEffect();
                        plugin.DisableGlassEffect();
                    }
                }
                searchWindowWidth = value;
            } 
        }
        private int searchWindowWidth = 660;
        [NumberOption("LOC_QS_DetailsMaxWidth", Min = 200, Max = 500, Tick = 5)]
        public int DetailsViewMaxWidth { get => detailsViewMaxWidth; 
            set 
            {
                if ((plugin?.searchWindow ?? null) != null)
                {
                    plugin.searchWindow.DetailsBorder.Width = value;
                    plugin.searchWindow.UpdateListBox(plugin.searchWindow.SearchResults.Items.Count, true);
                    plugin.UpdateBorder(OuterBorderThickness);
                    if (EnableGlassEffect)
                    {
                        plugin.DisableGlassEffect();
                        plugin.EnableGlassEffect();
                    }
                    else
                    {
                        plugin.EnableGlassEffect();
                        plugin.DisableGlassEffect();
                    }
                }
                detailsViewMaxWidth = value;
            } 
        }
        private int detailsViewMaxWidth = 400;

        [GenericOption("LOC_QS_IgnoreHiddenGames")]
        public bool IgnoreHiddenGames { get; set; } = false;

        [NumberOption("LOC_QS_MinAcronmLength", Min = 0, Max = 10, Tick = 1, Description = "LOC_QS_MinAcronmLengthTooltip")]
        public int MinAcronmLength { get; set; } = 3;

        [GenericOption("LOC_QS_SwapGameActions")]
        public bool SwapGameActions { get; set; } = false;

        [GenericOption("LOC_QS_KeepIndexInMemory")]
        public bool KeepGamesInMemory { get; set; } = false;

        public bool EnableInFullscreenMode { get; set; } = false;

        public string GitHubAccessToken { get; set; } = null;

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
        private bool expandAllItems = true;

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