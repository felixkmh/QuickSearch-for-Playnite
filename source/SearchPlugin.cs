using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using QuickSearch.SearchItems;
using QuickSearch.SearchItems.Settings;
using QuickSearch.ViewModels;
using QuickSearch.Views;
using StartPage.SDK;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

[assembly: InternalsVisibleTo("QuickSearch")]
namespace QuickSearch
{
    public class SearchPlugin : GenericPlugin, StartPage.SDK.IStartPageExtension
    {
        internal static readonly ILogger logger = LogManager.GetLogger();

        public static SearchPlugin Instance { get => instance; }
        private static SearchPlugin instance;

        public SearchSettings Settings { get; set; }

        private InputBinding HotkeyBinding { get; set; }

        internal CommandItemSource simpleCommands = new CommandItemSource();
        internal Dictionary<string, ISearchItemSource<string>> searchItemSources = new Dictionary<string, ISearchItemSource<string>>();
        
        public override Guid Id { get; } = Guid.Parse("6a604592-7001-4b4e-a3be-91073b459e2b");

        public SearchPlugin(IPlayniteAPI api) : base(api)
        {
            Settings = new SearchSettings(this);
            instance = this;
            Properties = new GenericPluginProperties { HasSettings = true };
            api.Database.Games.ItemUpdated += Games_ItemUpdated;
            api.Database.Games.ItemCollectionChanged += Games_ItemCollectionChanged;
        }

        private void InitializeUsedFields(IPlayniteAPI api)
        {
            Task.Run(() =>
            {
                UsedSources.Clear();
                UsedGenres.Clear();
                UsedFeatures.Clear();
                UsedPlatforms.Clear();
                UsedTags.Clear();
                UsedCategories.Clear();
                UsedCompanies.Clear();
                foreach (var game in api.Database.Games)
                {
                    if (game.Source is GameSource source) { if (UsedSources.ContainsKey(source)) { UsedSources[source]++; } else { UsedSources[source] = 1; } }
                    game.Genres?.ForEach(g => { if (UsedGenres.ContainsKey(g)) { UsedGenres[g]++; } else { UsedGenres[g] = 1; } });
                    game.Features?.ForEach(f => { if (UsedFeatures.ContainsKey(f)) { UsedFeatures[f]++; } else { UsedFeatures[f] = 1; } });
                    game.Platforms?.ForEach(p => { if (UsedPlatforms.ContainsKey(p)) { UsedPlatforms[p]++; } else { UsedPlatforms[p] = 1; } });
                    game.Tags?.ForEach(p => { if (UsedTags.ContainsKey(p)) { UsedTags[p]++; } else { UsedTags[p] = 1; } });
                    game.Categories?.ForEach(c => { if (UsedCategories.ContainsKey(c)) { UsedCategories[c]++; } else { UsedCategories[c] = 1; } });
                    game.Developers?.ForEach(d => { if (UsedCompanies.ContainsKey(d)) { UsedCompanies[d]++; } else { UsedCompanies[d] = 1; } });
                    game.Publishers?.ForEach(p => { if (UsedCompanies.ContainsKey(p)) { UsedCompanies[p]++; } else { UsedCompanies[p] = 1; } });
                }
            });
        }

        private void UpdateUsedFields (IPlayniteAPI api, IEnumerable<Game> added, IEnumerable<Game> removed)
        {
            foreach (var game in removed)
            {
                if (game.Source is GameSource source) { if (UsedSources.ContainsKey(source)) { UsedSources[source]--; } }
                game.Genres?.ForEach(g => { if (UsedGenres.ContainsKey(g)) { UsedGenres[g]--; } });
                game.Features?.ForEach(f => { if (UsedFeatures.ContainsKey(f)) { UsedFeatures[f]--; } });
                game.Platforms?.ForEach(p => { if (UsedPlatforms.ContainsKey(p)) { UsedPlatforms[p]--; } });
                game.Tags?.ForEach(p => { if (UsedTags.ContainsKey(p)) { UsedTags[p]--; } });
                game.Categories?.ForEach(c => { if (UsedCategories.ContainsKey(c)) { UsedCategories[c]--; } });
                game.Developers?.ForEach(d => { if (UsedCompanies.ContainsKey(d)) { UsedCompanies[d]--; } });
                game.Publishers?.ForEach(p => { if (UsedCompanies.ContainsKey(p)) { UsedCompanies[p]--; } });
            }
            foreach (var game in added)
            {
                if (game.Source is GameSource source) { if (UsedSources.ContainsKey(source)) { UsedSources[source]++; } else { UsedSources[source] = 1; } }
                game.Genres?.ForEach(g => { if (UsedGenres.ContainsKey(g)) { UsedGenres[g]++; } else { UsedGenres[g] = 1; } });
                game.Features?.ForEach(f => { if (UsedFeatures.ContainsKey(f)) { UsedFeatures[f]++; } else { UsedFeatures[f] = 1; } });
                game.Platforms?.ForEach(p => { if (UsedPlatforms.ContainsKey(p)) { UsedPlatforms[p]++; } else { UsedPlatforms[p] = 1; } });
                game.Tags?.ForEach(p => { if (UsedTags.ContainsKey(p)) { UsedTags[p]++; } else { UsedTags[p] = 1; } });
                game.Categories?.ForEach(c => { if (UsedCategories.ContainsKey(c)) { UsedCategories[c]++; } else { UsedCategories[c] = 1; } });
                game.Developers?.ForEach(d => { if (UsedCompanies.ContainsKey(d)) { UsedCompanies[d]++; } else { UsedCompanies[d] = 1; } });
                game.Publishers?.ForEach(p => { if (UsedCompanies.ContainsKey(p)) { UsedCompanies[p]++; } else { UsedCompanies[p] = 1; } });
            }
            UsedSources.AsParallel().Where(p => p.Value <= 0).Select(p => p.Key).ToList().ForEach(i => UsedSources.Remove(i));
            UsedGenres.AsParallel().Where(p => p.Value <= 0).Select(p => p.Key).ToList().ForEach(i => UsedGenres.Remove(i));
            UsedFeatures.AsParallel().Where(p => p.Value <= 0).Select(p => p.Key).ToList().ForEach(i => UsedFeatures.Remove(i));
            UsedPlatforms.AsParallel().Where(p => p.Value <= 0).Select(p => p.Key).ToList().ForEach(i => UsedPlatforms.Remove(i));
            UsedTags.AsParallel().Where(p => p.Value <= 0).Select(p => p.Key).ToList().ForEach(i => UsedTags.Remove(i));
            UsedCategories.AsParallel().Where(p => p.Value <= 0).Select(p => p.Key).ToList().ForEach(i => UsedCategories.Remove(i));
            UsedCompanies.AsParallel().Where(p => p.Value <= 0).Select(p => p.Key).ToList().ForEach(i => UsedCompanies.Remove(i));
        }

        public Dictionary<GameSource, int> UsedSources { get; private set; } = new Dictionary<GameSource, int>();
        public Dictionary<Genre, int> UsedGenres { get; private set; } = new Dictionary<Genre, int>();
        public Dictionary<GameFeature, int> UsedFeatures { get; private set; } = new Dictionary<GameFeature, int>();
        public Dictionary<Platform, int> UsedPlatforms { get; private set; } = new Dictionary<Platform, int>();
        public Dictionary<Tag, int> UsedTags { get; private set; } = new Dictionary<Tag, int>();
        public Dictionary<Category, int> UsedCategories { get; private set; } = new Dictionary<Category, int>();
        public Dictionary<Company, int> UsedCompanies { get; private set; } = new Dictionary<Company, int>();

        private void Games_ItemCollectionChanged(object sender, ItemCollectionChangedEventArgs<Game> e)
        {
            if (PlayniteApi is IPlayniteAPI api)
            {
                UpdateUsedFields(api, e.AddedItems, e.RemovedItems);
            }
        }

        private void Games_ItemUpdated(object sender, ItemUpdatedEventArgs<Game> e)
        {
            if (PlayniteApi is IPlayniteAPI api)
            {
                UpdateUsedFields(api, e.UpdatedItems.Select(i => i.NewData), e.UpdatedItems.Select(i => i.OldData));
            }
        }

        internal bool RegisterGlobalHotkey()
        {
            if (!globalHotkeyRegistered)
            {
                var window = mainWindow;
                var handle = mainWindowHandle;
                source.AddHook(GlobalHotkeyCallback);
                globalHotkeyRegistered = true;
                return HotkeyHelper.RegisterHotKey(handle, HOTKEY_ID, Settings.SearchShortcutGlobal.Modifiers.ToVK(), (uint)KeyInterop.VirtualKeyFromKey(Settings.SearchShortcutGlobal.Key));
            }
            return true;
        }

        internal bool UnregisterGlobalHotkey()
        {
            if (globalHotkeyRegistered)
            {
                var window = mainWindow;
                var handle = mainWindowHandle;
                var success = HotkeyHelper.UnregisterHotKey(handle, HOTKEY_ID);
                source.RemoveHook(GlobalHotkeyCallback);
                globalHotkeyRegistered = false;
                return success;
            }
            return true;
        }

        private void OnSettingsChanged(SearchSettings newSettings, SearchSettings oldSettings)
        {
            var window = mainWindow;
            window.Dispatcher.Invoke(() => {
                if (searchWindow != null)
                {
                    searchWindow.WindowGrid.Width = newSettings.SearchWindowWidth;
                    searchWindow.DetailsBorder.Width = newSettings.DetailsViewMaxWidth;
                }

                UpdateBorder(newSettings.OuterBorderThickness);

                if (newSettings.EnableGlassEffect)
                {
                    DisableGlassEffect();
                    EnableGlassEffect();
                }
                else
                {
                    EnableGlassEffect();
                    DisableGlassEffect();
                }
                if (globalHotkeyRegistered)
                {
                    UnregisterGlobalHotkey();
                }

                if (newSettings.EnableGlobalHotkey)
                {
                    RegisterGlobalHotkey();
                }

                window.InputBindings.Remove(HotkeyBinding);
                popup?.InputBindings.Remove(HotkeyBinding);
                HotkeyBinding = new InputBinding(new ActionCommand(ToggleSearch), new KeyGesture(newSettings.SearchShortcut.Key, newSettings.SearchShortcut.Modifiers));
                window.InputBindings.Add(HotkeyBinding);
                popup?.InputBindings.Add(HotkeyBinding);
                searchWindow?.Reset();
            });
        }

        private class ActionCommand : ICommand
        {
            public ActionCommand(Action action)
            {
                this.action = action;
            }
#pragma warning disable CS0067
            public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067
            private readonly Action action;

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                action?.Invoke();
            }
        }

        private class InputBindingWrapper : ISearchAction<string>
        {
            public InputBindingWrapper(string name, InputBinding binding)
            {
                Name = name;
                Binding = binding;
            }

            public string Name { get; set; }
            public InputBinding Binding { get; set; }

            public bool CloseAfterExecute => true;
#pragma warning disable CS0067
            public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

            public bool CanExecute(object parameter)
            {
                if (Binding?.Command != null)
                {
                    return Binding.Command.CanExecute(Binding.CommandParameter);
                }
                return false;
            }

            public void Execute(object parameter)
            {
                if (Binding?.Command != null)
                {
                    if (CanExecute(null)) {
                        Binding.Command.Execute(Binding.CommandParameter);
                    }
                }
            }
        }

        private IntPtr GlobalHotkeyCallback(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (Settings.EnableGlobalHotkey)
            {
                const int WM_HOTKEY = 0x0312;
                switch (msg)
                {
                    case WM_HOTKEY:
                        switch (wParam.ToInt32())
                        {
                            case HOTKEY_ID:
                                uint vkey = ((uint)lParam >> 16) & 0xFFFF;
                                if (vkey == (uint)KeyInterop.VirtualKeyFromKey(Settings.SearchShortcutGlobal.Key))
                                {
                                    ToggleSearch();
                                    if (popup.IsOpen)
                                    {
                                        searchWindow.SearchBox.Focus();
                                    }
                                }
                                handled = true;
                                break;
                        }
                        break;
                }
            }
            return IntPtr.Zero;
        }

        private const int HOTKEY_ID = 1337;

        HwndSource source = null;
        Window mainWindow;
        IntPtr mainWindowHandle;
        WindowInteropHelper windowInterop;
        bool globalHotkeyRegistered = false;

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            InitializeUsedFields(PlayniteApi);
            instance = this;
            searchItemSources.Add("Games", new GameSearchSource());

            if (!string.IsNullOrWhiteSpace(Settings.GitHubAccessToken))
            {
                AddonManifestBase.gitHub.Credentials = new Octokit.Credentials(Settings.GitHubAccessToken);
            }

            // create tags
            if (Settings.EnableTagCreation)
            {
                if (Settings.IgnoreTagId == Guid.Empty || PlayniteApi.Database.Tags.Get(Settings.IgnoreTagId) == null)
                {
                    Settings.IgnoreTagId = PlayniteApi.Database.Tags.Add("[QS] Ignored").Id;
                }
            }

            AddPluginSettings();

            // searchItemSources.Add("Commands", simpleCommands);
            // QuickSearchSDK.AddItemSource("External_Commands", QuickSearchSDK.simpleCommands);
            // Add code to be executed when Playnite is initialized.
            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop || Settings.EnableInFullscreenMode)
            {
                Settings.SettingsChanged += OnSettingsChanged;
                mainWindow = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.Name.Equals("WindowMain", StringComparison.InvariantCultureIgnoreCase));
                if (mainWindow == null) mainWindow = Application.Current.MainWindow;
                if (mainWindow is Window)
                {
                    windowInterop = new WindowInteropHelper(mainWindow);
                    mainWindowHandle = windowInterop.Handle;
                    source = HwndSource.FromHwnd(mainWindowHandle);
                    if (Settings.EnableGlobalHotkey)
                    {
                        RegisterGlobalHotkey();
                    }

                    HotkeyBinding = new InputBinding(new ActionCommand(ToggleSearch), new KeyGesture(Settings.SearchShortcut.Key, Settings.SearchShortcut.Modifiers));
                    mainWindow.InputBindings.Add(HotkeyBinding);
                    AddBuiltInCommands();
                } else
                {
                    logger.Error("Could not find main window. Shortcuts could not be registered.");
                }
            }

        }

        private void AddBuiltInCommands()
        {
            CommandItem addGameCommand = new CommandItem((string)Application.Current.FindResource("LOCAddGames"), new List<CommandAction>(), "Add Games")
            {
                IconChar = IconChars.GameConsole
            };

            try 
            {
                var mainMenuBorder = Helper.UiHelper.FindVisualChildren<Border>(mainWindow, "PART_ElemMainMenu").FirstOrDefault();
                if (mainMenuBorder != null)
                {
                    if (mainMenuBorder.ContextMenu is ContextMenu mainMenu)
                    {
                        var description = ResourceProvider.GetString("LOCMenuReloadLibrary");
                        if (mainMenu.Items.OfType<MenuItem>().FirstOrDefault(item => item.Header.ToString() == description ) is MenuItem updateItem)
                        {
                            var items = updateItem.Items.OfType<MenuItem>().Skip(2).ToList();
                            foreach(var libraryUpdateItem in items)
                            {
                                string libraryName = libraryUpdateItem.Header.ToString();
                                var name = string.Format(ResourceProvider.GetString("LOC_QS_UpdateLibraryX"), libraryName);
                                var icon = PlayniteApi.Addons.Plugins.OfType<LibraryPlugin>().FirstOrDefault(p => p.Name.Contains(libraryName))?.LibraryIcon;
                                var actionName = ResourceProvider.GetString("LOC_QS_UpdateAction");
                                Action action = () => libraryUpdateItem.Command.Execute(libraryUpdateItem.CommandParameter);
                                var command = QuickSearchSDK.AddCommand(name, action, description, actionName, icon);
                                command.IconChar = QuickSearch.IconChars.Refresh;
                                var englishKey = $"Update {libraryName} library";
                                if (!command.Keys.Any(k => k.Key == englishKey))
                                {
                                    command.Keys.Add(new CommandItemKey() { Key = englishKey, Weight = 1 });
                                }
                            }
                        }
                    }

                }
            
            } catch (Exception e)
            {
                logger.Error(e, "Could not create library update commands.");
            }



            foreach (InputBinding binding in mainWindow.InputBindings)
            {
                if (binding.Gesture is KeyGesture keyGesture)
                {
                    if (keyGesture.Key == Key.F9 && keyGesture.Modifiers == ModifierKeys.None)
                    {
                        string name = (string)Application.Current.FindResource("LOC_QS_Addons");
                        var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), (string)Application.Current.FindResource("LOC_QS_OpenAddons"), ResourceProvider.GetString("LOC_QS_OpenAction"));
                        item.IconChar = '\uEEA0';
                        item.Actions[0] = new InputBindingWrapper(item.Actions[0].Name, binding);
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            var clean = SearchItems.CleanNameKey.regex.Replace(key.Key, "");
                            if (clean != key.Key)
                            {
                                item.Keys.Add(new CommandItemKey() { Key = clean, Weight = 1 });
                            }
                        }
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            item.Keys.Add(new CommandItemKey() { Key = "> " + key.Key, Weight = 1 });
                        }
                    }
                    if (keyGesture.Key == Key.F4 && keyGesture.Modifiers == ModifierKeys.None)
                    {
                        string name = (string)Application.Current.FindResource("LOCSettingsWindowTitle");
                        var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Playnite Settings", ResourceProvider.GetString("LOC_QS_OpenAction"));
                        item.IconChar = IconChars.Settings;
                        item.Actions[0] = new InputBindingWrapper(item.Actions[0].Name, binding);
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            var clean = SearchItems.CleanNameKey.regex.Replace(key.Key, "");
                            if (clean != key.Key)
                            {
                                item.Keys.Add(new CommandItemKey() { Key = clean, Weight = 1 });
                            }
                        }
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            item.Keys.Add(new CommandItemKey() { Key = "> " + key.Key, Weight = 1 });
                        }
                    }
                    if (keyGesture.Key == Key.F5 && keyGesture.Modifiers == ModifierKeys.None)
                    {
                        string name = ResourceProvider.GetString("LOC_QS_UpdateAllLibraries");
                        var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Update All Libraries", (string)Application.Current.FindResource("LOCUpdateAll"));
                        item.IconChar = IconChars.Refresh;
                        item.Actions[0] = new InputBindingWrapper(item.Actions[0].Name, binding);
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            var clean = SearchItems.CleanNameKey.regex.Replace(key.Key, "");
                            if (clean != key.Key)
                            {
                                item.Keys.Add(new CommandItemKey() { Key = clean, Weight = 1 });
                            }
                        }
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            item.Keys.Add(new CommandItemKey() { Key = "> " + key.Key, Weight = 1 });
                        }
                    }
                    if (keyGesture.Key == Key.Q && keyGesture.Modifiers == ModifierKeys.Alt)
                    {
                        string name = (string)Application.Current.FindResource("LOCExitPlaynite");
                        var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Exit Playnite", (string)Application.Current.FindResource("LOCExitAppLabel"));
                        item.IconChar = IconChars.Exit;
                        item.Actions[0] = new InputBindingWrapper(item.Actions[0].Name, binding);
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            var clean = SearchItems.CleanNameKey.regex.Replace(key.Key, "");
                            if (clean != key.Key)
                            {
                                item.Keys.Add(new CommandItemKey() { Key = clean, Weight = 1 });
                            }
                        }
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            item.Keys.Add(new CommandItemKey() { Key = "> " + key.Key, Weight = 1 });
                        }
                    }
                    if (keyGesture.Key == Key.F11 && keyGesture.Modifiers == ModifierKeys.None)
                    {
                        string name = (string)Application.Current.FindResource("LOCMenuOpenFullscreen");
                        var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Switch to Fullscreen Mode", ResourceProvider.GetString("LOC_QS_SwitchAction"));
                        item.IconChar = IconChars.Maximize;
                        item.Actions[0] = new InputBindingWrapper(item.Actions[0].Name, binding);
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            var clean = SearchItems.CleanNameKey.regex.Replace(key.Key, "");
                            if (clean != key.Key)
                            {
                                item.Keys.Add(new CommandItemKey() { Key = clean, Weight = 1 });
                            }
                        }
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            item.Keys.Add(new CommandItemKey() { Key = "> " + key.Key, Weight = 1 });
                        }
                    }
                    if (keyGesture.Key == Key.Insert && keyGesture.Modifiers == ModifierKeys.None)
                    {
                        addGameCommand.Actions.Add(new CommandAction() { Name = ((string)Application.Current.FindResource("LOCMenuAddGameManual")).Replace("…", ""), Action = () => binding.Command.Execute(binding.CommandParameter) });
                    }
                    if (keyGesture.Key == Key.Q && keyGesture.Modifiers == ModifierKeys.Control)
                    {
                        addGameCommand.Actions.Add(new CommandAction() { Name = ((string)Application.Current.FindResource("LOCMenuAddGameEmulated")).Replace("…", ""), Action = () => binding.Command.Execute(binding.CommandParameter) });
                    }
                    if (keyGesture.Key == Key.W && keyGesture.Modifiers == ModifierKeys.Control)
                    {
                        string name = (string)Application.Current.FindResource("LOCMenuLibraryManagerTitle");
                        name = name.Replace("…", "");
                        var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Library Manager", ResourceProvider.GetString("LOC_QS_OpenAction"));
                        item.IconChar = IconChars.Settings;
                        item.Actions[0] = new InputBindingWrapper(item.Actions[0].Name, binding);
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            var clean = SearchItems.CleanNameKey.regex.Replace(key.Key, "");
                            if (clean != key.Key)
                            {
                                item.Keys.Add(new CommandItemKey() { Key = clean, Weight = 1 });
                            }
                        }
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            item.Keys.Add(new CommandItemKey() { Key = "> " + key.Key, Weight = 1 });
                        }
                    }
                    if (keyGesture.Key == Key.T && keyGesture.Modifiers == ModifierKeys.Control)
                    {
                        string name = (string)Application.Current.FindResource("LOCEmulatorsWindowTitle");
                        name = name.Replace("…", "");
                        var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Configure Emulators", ResourceProvider.GetString("LOC_QS_ConfigureAction"));
                        item.IconChar = IconChars.Settings;
                        item.Actions[0] = new InputBindingWrapper(item.Actions[0].Name, binding);
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            var clean = SearchItems.CleanNameKey.regex.Replace(key.Key, "");
                            if (clean != key.Key)
                            {
                                item.Keys.Add(new CommandItemKey() { Key = clean, Weight = 1 });
                            }
                        }
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            item.Keys.Add(new CommandItemKey() { Key = "> " + key.Key, Weight = 1 });
                        }
                    }
                    if (keyGesture.Key == Key.D && keyGesture.Modifiers == ModifierKeys.Control)
                    {
                        string name = (string)Application.Current.FindResource("LOCMenuDownloadMetadata");
                        name = name.Replace("…", "");
                        var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Download Metadata", ResourceProvider.GetString("LOC_QS_OpenAction"));
                        item.IconChar = IconChars.Copy;
                        item.Actions[0] = new InputBindingWrapper(item.Actions[0].Name, binding);
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            var clean = SearchItems.CleanNameKey.regex.Replace(key.Key, "");
                            if (clean != key.Key)
                            {
                                item.Keys.Add(new CommandItemKey() { Key = clean, Weight = 1 });
                            }
                        }
                        foreach (CommandItemKey key in item.Keys.ToArray())
                        {
                            item.Keys.Add(new CommandItemKey() { Key = "> " + key.Key, Weight = 1 });
                        }
                    }
                }
            }
            QuickSearchSDK.AddCommand(addGameCommand);
            foreach (CommandItemKey key in addGameCommand.Keys.ToArray())
            {
                addGameCommand.Keys.Add(new CommandItemKey() { Key = "> " + key.Key, Weight = 1 });
            }
            addGameCommand.Keys.Add(new CommandItemKey { Key = ">", Weight = 1 });
            QuickSearchSDK.AddPluginSettings("QuickSearch", Settings, OpenSettingsView);
            QuickSearchSDK.AddItemSource("ITAD", new ITADItemSource());

            //foreach(var metadataPlugin in PlayniteApi.Addons.Plugins.OfType<MetadataPlugin>().Where(p => p.Name == "IGDB"))
            //{
            //    var imdbSubItemsAction = new SubItemsAction() { Action = () => { }, Name = ResourceProvider.GetString("LOC_QS_OpenAction"), CloseAfterExecute = false, SubItemSource = new MetadataSource(metadataPlugin) };
            //    var imdbCommand = new CommandItem(metadataPlugin.Name, new List<CommandAction>(), string.Format(ResourceProvider.GetString("LOC_QS_SearchOnAction"), metadataPlugin.Name));
            //    // imdbCommand.Keys.Add(new CommandItemKey() { Key = "itad", Weight = 1 });
            //    imdbCommand.Actions.Add(imdbSubItemsAction);
            //    QuickSearchSDK.AddCommand(imdbCommand);
            //}


            var itadSubItemsAction = new SubItemsAction() { Action = () => { }, Name = ResourceProvider.GetString("LOC_QS_OpenAction"), CloseAfterExecute = false, SubItemSource = new ITADItemSource() };
            var itadCommand = new CommandItem("IsThereAnyDeal", new List<CommandAction>(), string.Format(ResourceProvider.GetString("LOC_QS_SearchOnAction") , "IsThereAnyDeal.com"), "https://d2uym1p5obf9p8.cloudfront.net/images/banners/150x150.gif");
            itadCommand.Keys.Add(new CommandItemKey() { Key = "itad", Weight = 1 });
            itadCommand.Actions.Add(itadSubItemsAction);
            QuickSearchSDK.AddCommand(itadCommand);

            var cheapSharkSubItemsAction = new SubItemsAction() { Action = () => { }, Name = ResourceProvider.GetString("LOC_QS_OpenAction"), CloseAfterExecute = false, SubItemSource = new CheapSharkItemSource() };
            var cheapSharkCommand = new CommandItem("CheapShark", new List<CommandAction>(), string.Format(ResourceProvider.GetString("LOC_QS_SearchOnAction"), "CheapShark.com"), "https://www.cheapshark.com/img/logo_image.png?v=1.0");
            cheapSharkCommand.Actions.Add(cheapSharkSubItemsAction);
            QuickSearchSDK.AddCommand(cheapSharkCommand);

            var addonSubItemAction = new SubItemsAction() { Action = () => { }, Name = ResourceProvider.GetString("LOC_QS_OpenAction"), CloseAfterExecute = false, SubItemSource = new AddonBrowser() };
            var addonItem = new CommandItem("Add-on Browser", new List<CommandAction>(), string.Format(ResourceProvider.GetString("LOC_QS_AddonBrowserPrefix"))) { IconChar = '\uEEA0' };
            addonItem.Keys.Add(new CommandItemKey { Key = "Addon Browser" });
            addonItem.Actions.Add(addonSubItemAction);
            QuickSearchSDK.AddCommand(addonItem);
        }

        public Popup popup;
        internal SearchWindow searchWindow;
        internal LuceneSearchViewModel luceneSearchViewModel;
        private UIElement placementTarget;
        VisualBrush brush;
        Window dummyWindow;
        bool glassActive = false;

        internal string GetAssemblyName(string name)
        {
            var sep = name.IndexOf('_');
            return name.Substring(0, sep);
        }

        private void AddPluginSettings()
        {
            var deserializer = new YamlDotNet.Serialization.Deserializer();
            var plugins = PlayniteApi.Addons.Plugins
                .OfType<GenericPlugin>()
                .Where(pl => pl?.Properties?.HasSettings ?? false)
                .Cast<Plugin>();

            plugins = plugins
                .Concat(PlayniteApi.Addons.Plugins
                    .OfType<LibraryPlugin>()
                    .Where(pl => pl?.Properties?.HasSettings ?? false)
                    .Cast<Plugin>()
            );

            plugins = plugins
                .Concat(PlayniteApi.Addons.Plugins
                    .OfType<MetadataPlugin>()
                    .Where(pl => pl?.Properties?.HasSettings ?? false)
                    .Cast<Plugin>()
            );

            foreach (var plugin in plugins)
            {
                var installDir = System.IO.Path.GetDirectoryName(plugin.GetType().Assembly.Location);
                var extensionYaml = System.IO.Path.Combine(installDir, "extension.yaml");
                if (System.IO.File.Exists(extensionYaml))
                {
                    var text = System.IO.File.ReadAllText(extensionYaml);
                    var config = deserializer.Deserialize<Dictionary<string, object>>(text);
                    if (config != null)
                    {
                        string name = config["Name"] as string ?? plugin.GetType().Name;
                        string icon = config["Icon"] as string;
                        string addonId = config["Id"] as string;
                        if (name is string)
                        {
                            var item = QuickSearchSDK.AddPluginSettings(name, plugin.GetSettings(false), plugin.OpenSettingsView);
                            if (icon is string && Uri.TryCreate(System.IO.Path.Combine(installDir, icon), UriKind.RelativeOrAbsolute, out var uri))
                            {
                                item.Icon = uri;
                            }
                            item.Actions.Add(new CommandAction()
                            {
                                Name = ResourceProvider.GetString("LOC_QS_UserData"),
                                Action = () => Process.Start(plugin.GetPluginUserDataPath())
                            });
                            item.Actions.Add(new CommandAction()
                            {
                                Name = ResourceProvider.GetString("LOC_QS_InstallationData"),
                                Action = () => Process.Start(installDir)
                            });
                        }
                    }
                }
            }
        }

        private void ToggleSearch()
        {
            // PlayniteApi.Dialogs.ShowMessage("Shortcut Pressed!");

            if (popup is null)
            {
                popup = new Popup();
                popup.Opened += (s, a) =>
                {
#if DEBUG
                    try
                    {
#endif
                        searchWindow.DetailsBorder.Width = Settings.DetailsViewMaxWidth;
                        searchWindow.WindowGrid.Width = Settings.SearchWindowWidth;
                        searchWindow.SearchResults.Items.Refresh();
                        foreach (var assembly in QuickSearchSDK.registeredAssemblies)
                        {
                            if (!Settings.EnabledAssemblies.ContainsKey(assembly))
                            {
                                Settings.EnabledAssemblies.Add(assembly, new AssemblyOptions());
                            }
                        }

                        UpdateBorder(Settings.OuterBorderThickness);

                        
                        searchWindow.SearchBox.SelectAll();
                        searchWindow.SearchBox.Focus();

#if DEBUG
                    } catch (Exception ex)
                    {
                        logger.Error(ex, "Failed to initialize search.");
                    }
#endif
                };

                popup.Closed += (s, e) =>
                {
                    luceneSearchViewModel.QueueIndexClear();
                    dummyWindow.Hide();
                };

                placementTarget = Helper.UiHelper.FindVisualChildren(Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Name == "WindowMain"), "PART_ContentView").FirstOrDefault();
                var p = VisualTreeHelper.GetParent(Application.Current.MainWindow);
                popup.Placement = PlacementMode.Center;
                popup.StaysOpen = false;
                popup.PlacementTarget = placementTarget;
                luceneSearchViewModel = new LuceneSearchViewModel(this);
                searchWindow = new SearchWindow(this, luceneSearchViewModel) { DataContext = luceneSearchViewModel };

                searchWindow.DetailsBorder.Width = Settings.DetailsViewMaxWidth;
                searchWindow.WindowGrid.Width = Settings.SearchWindowWidth;
                popup.Child = searchWindow;
                popup.InputBindings.Add(HotkeyBinding);
                if (Settings.EnableGlassEffect)
                {
                    EnableGlassEffect();
                } else
                {
                    DisableGlassEffect();
                }

                dummyWindow = new PopupWindow() 
                { 
                    Width = 0,
                    Height = 0,
                };
            } 
            if (!popup.IsOpen)
            {
                var sources = searchItemSources.Values.AsEnumerable();

                if (Settings.EnableExternalItems)
                {
                    sources = sources.Concat(QuickSearchSDK.searchItemSources
                    .Where(e => SearchPlugin.Instance.Settings.EnabledAssemblies[GetAssemblyName(e.Key)].Items)
                    .Select(e => e.Value));
                }

                if (luceneSearchViewModel.navigationStack.Count <= 1)
                {
                    luceneSearchViewModel.QueueIndexUpdate(sources);
                }
                else
                {
                    luceneSearchViewModel.QueueIndexUpdate();
                }
            }
            if (mainWindow.IsActive && mainWindow.WindowState != WindowState.Minimized)
            {
                if (Settings.EnableGlassEffect && !glassActive)
                {
                    EnableGlassEffect();
                }
                popup.PlacementTarget = placementTarget;
                popup.Placement = PlacementMode.Center;
                popup.StaysOpen = false;
                popup.VerticalOffset = 0;
                popup.HorizontalOffset = 0;
            } else
            {
                if (glassActive)
                {
                    DisableGlassEffect();
                }
                double primScreenHeight = System.Windows.SystemParameters.FullPrimaryScreenHeight;
                double primScreenWidth = System.Windows.SystemParameters.FullPrimaryScreenWidth;
                dummyWindow.Top = (primScreenHeight - dummyWindow.Height) / 2;
                dummyWindow.Left = (primScreenWidth - dummyWindow.Width) / 2;
                dummyWindow.Show();
                dummyWindow.Activate();
                popup.PlacementTarget = dummyWindow;
                popup.Placement = PlacementMode.Center;
                popup.StaysOpen = false;
            }

            popup.IsOpen = !popup.IsOpen;
        }

        internal void UpdateBorder(int thickness)
        {
            if (searchWindow != null)
            {
                searchWindow.WindowGrid.Margin = new Thickness(thickness);
                searchWindow.OutsideBorder.CornerRadius = new CornerRadius(thickness);
                searchWindow.HeaderBorder.CornerRadius = new CornerRadius(thickness);
                searchWindow.DetailsBackgroundBorder.CornerRadius = new CornerRadius(thickness);
                searchWindow.DetailsBackgroundBorderFallback.CornerRadius = new CornerRadius(thickness);
                var margin = searchWindow.SearchResults.Margin;
                margin.Top = thickness + 8;
                searchWindow.SearchResults.Margin = margin;
                searchWindow.SearchResultsBackground.Viewport = new Rect(
                                        0, 0,
                                        searchWindow.WindowGrid.Width + searchWindow.WindowGrid.Margin.Left + searchWindow.WindowGrid.Margin.Right,
                                        searchWindow.WindowGrid.Height + searchWindow.WindowGrid.Margin.Top + searchWindow.WindowGrid.Margin.Bottom
                                    );

                searchWindow.DetailsBackground.Viewport = new Rect(
                                        -searchWindow.SearchResultsBackground.Viewport.Width - searchWindow.DetailsBorder.Width, 0,
                                        searchWindow.SearchResultsBackground.Viewport.Width + 2 * searchWindow.DetailsBorder.Width,
                                        searchWindow.SearchResultsBackground.Viewport.Height
                                    );
            }
        }

        internal void EnableGlassEffect()
        {
            if (searchWindow != null)
            {
                searchWindow.WindowGrid.Margin = new Thickness(Settings.OuterBorderThickness);
                searchWindow.GlassTint.Visibility = Visibility.Visible;
                searchWindow.Noise.Visibility = Visibility.Visible;
                var margin = searchWindow.SearchResults.Margin;
                margin.Top = Settings.OuterBorderThickness + 8;
                searchWindow.SearchResults.Margin = margin;
                var visual = (Visual)placementTarget;

                brush = new VisualBrush()
                {
                    Stretch = Stretch.None,
                    Visual = placementTarget,
                    AutoLayoutContent = false,
                    TileMode = TileMode.None
                };

                searchWindow.BackgroundBorder.Background = brush;
                searchWindow.DetailsBackgroundVisual.Background = brush;
                searchWindow.DetailsBackgroundBorderFallback.Visibility = Visibility.Hidden;
                RenderOptions.SetCachingHint(brush, CachingHint.Cache);
                RenderOptions.SetCachingHint(searchWindow.SearchResultsBackground, CachingHint.Cache);
                ((Brush)searchWindow.SearchResults.Resources["GlyphBrush"]).Opacity = 0.5f;
                ((Brush)searchWindow.SearchResults.Resources["HoverBrush"]).Opacity = 0.5f;

                int radius = 80;
                searchWindow.BackgroundBorder.Effect = new BlurEffect() { Radius = radius, RenderingBias = RenderingBias.Performance };
                searchWindow.BackgroundBorder.Width = searchWindow.WindowGrid.Width + searchWindow.WindowGrid.Margin.Left + searchWindow.WindowGrid.Margin.Right + (2 * radius) + (2 * searchWindow.DetailsBorder.Width);
                searchWindow.BackgroundBorder.Height = searchWindow.WindowGrid.Height + searchWindow.WindowGrid.Margin.Top + searchWindow.WindowGrid.Margin.Bottom + (2 * radius);
                glassActive = true;
            }
        }

        internal void DisableGlassEffect()
        {
            if (searchWindow != null)
            {
                searchWindow.WindowGrid.Margin = new Thickness(Settings.OuterBorderThickness);
                searchWindow.GlassTint.Visibility = Visibility.Hidden;
                searchWindow.Noise.Visibility = Visibility.Hidden;
                var margin = searchWindow.SearchResults.Margin;
                margin.Top = Settings.OuterBorderThickness + 8;
                searchWindow.SearchResults.Margin = margin;
                searchWindow.BackgroundBorder.Background = Application.Current.TryFindResource("PopupBackgroundBrush") as Brush;
                searchWindow.DetailsBackgroundVisual.Background = Application.Current.TryFindResource("PopupBackgroundBrush") as Brush;
                searchWindow.HeaderBorder.Background = new SolidColorBrush { Color = Colors.Black, Opacity = 0.25 };
                ((Brush)searchWindow.SearchResults.Resources["GlyphBrush"]).Opacity = 1f;
                ((Brush)searchWindow.SearchResults.Resources["HoverBrush"]).Opacity = 1f;
                searchWindow.BackgroundBorder.Effect = null;
                searchWindow.BackgroundBorder.Width = searchWindow.WindowGrid.Width + searchWindow.WindowGrid.Margin.Left + searchWindow.WindowGrid.Margin.Right;
                searchWindow.BackgroundBorder.Height = searchWindow.WindowGrid.Height + searchWindow.WindowGrid.Margin.Top + searchWindow.WindowGrid.Margin.Bottom;
                searchWindow.DetailsBackgroundBorderFallback.Visibility = Visibility.Visible;
                glassActive = false;
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            var gameSearchSource = searchItemSources?.OfType<GameSearchSource>().FirstOrDefault();

            gameSearchSource?.LuceneDirectory?.Dispose();
            luceneSearchViewModel?.indexDir?.Dispose();
            startPageSearchViewModel?.indexDir?.Dispose();

            // Add code to be executed when Playnite is shutting down.
            HotkeyHelper.UnregisterHotKey(mainWindowHandle, HOTKEY_ID);
            SavePluginSettings(Settings);
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            // Add code to be executed when library is updated.
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SearchSettingsView();
        }

        public StartPageExtensionArgs GetAvailableStartPageViews()
        {
            var views = new List<StartPageViewArgsBase> {
                new StartPageViewArgsBase
                {
                    Name = "Search Bar",
                    ViewId = "SearchPopup"
                }
            };
            var args = new StartPageExtensionArgs { ExtensionName = "QuickSearch", Views = views };

            return args;
        }

        LuceneSearchViewModel startPageSearchViewModel = null;

        public object GetStartPageView(string viewId, Guid instanceId)
        {
            if (viewId == "SearchPopup")
            {
                if (startPageSearchViewModel == null)
                {
                    startPageSearchViewModel = new ViewModels.LuceneSearchViewModel(this);
                }
                return new Views.SearchView { DataContext = startPageSearchViewModel };
            }
            return null;
        }

        public Control GetStartPageViewSettings(string viewId, Guid instanceId)
        {
            return null;
        }

        public void OnViewRemoved(string viewId, Guid instanceId)
        {
            if (viewId == "SearchPopup")
            {
                startPageSearchViewModel?.indexDir?.Dispose();
                startPageSearchViewModel = null;
            }
        }
    }
}