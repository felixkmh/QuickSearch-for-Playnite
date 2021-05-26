using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using QuickSearch.SearchItems;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace QuickSearch
{
    public class SearchPlugin : Plugin
    {
        internal static readonly ILogger logger = LogManager.GetLogger();

        public static SearchPlugin Instance { get => instance; }
        private static SearchPlugin instance;

        public SearchSettings settings { get; set; }

        private InputBinding HotkeyBinding { get; set; }

        internal CommandItemSource simpleCommands = new CommandItemSource();
        internal Dictionary<string, ISearchItemSource<string>> searchItemSources = new Dictionary<string, ISearchItemSource<string>>();

        public override Guid Id { get; } = Guid.Parse("6a604592-7001-4b4e-a3be-91073b459e2b");

        public SearchPlugin(IPlayniteAPI api) : base(api)
        {
            settings = new SearchSettings(this);
            instance = this;
        }

        private void OnSettingsChanged(SearchSettings newSettings, SearchSettings oldSettings)
        {
            var window = Application.Current.MainWindow;
            window.Dispatcher.Invoke(() => { 
                window.InputBindings.Remove(HotkeyBinding);
                popup?.InputBindings.Remove(HotkeyBinding);
                HotkeyBinding = new InputBinding(new ActionCommand(ToggleSearch), new KeyGesture(newSettings.SearchShortcut.Key, newSettings.SearchShortcut.Modifiers));
                window.InputBindings.Add(HotkeyBinding);
                popup?.InputBindings.Add(HotkeyBinding);
                searchWindow?.Reset();
            });
        }

        public override void OnGameInstalled(Game game)
        {
            // Add code to be executed when game is finished installing.
        }

        public override void OnGameStarted(Game game)
        {
            // Add code to be executed when game is started running.
        }

        public override void OnGameStarting(Game game)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameStopped(Game game, long elapsedSeconds)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameUninstalled(Game game)
        {
            // Add code to be executed when game is uninstalled.
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
            private Action action;

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                action?.Invoke();
            }
        }

        public override void OnApplicationStarted()
        {
            instance = this;
            searchItemSources.Add("Games", new GameSearchSource());
            // searchItemSources.Add("Commands", simpleCommands);
            // QuickSearchSDK.AddItemSource("External_Commands", QuickSearchSDK.simpleCommands);
            // Add code to be executed when Playnite is initialized.
            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                settings.SettingsChanged += OnSettingsChanged;
                var window = Application.Current.MainWindow;
                HotkeyBinding = new InputBinding(new ActionCommand(ToggleSearch), new KeyGesture(settings.SearchShortcut.Key, settings.SearchShortcut.Modifiers));
                window.InputBindings.Add(HotkeyBinding);
                CommandItem addGameCommand = new CommandItem((string)Application.Current.FindResource("LOCAddGames"), new List<CommandAction>(), "Add Games");
                addGameCommand.IconChar = IconChars.GameConsole;
                foreach(InputBinding binding in window.InputBindings)
                {
                    if (binding.Gesture is KeyGesture keyGesture)
                    {
                        if (keyGesture.Key == Key.F4 && keyGesture.Modifiers == ModifierKeys.None)
                        {
                            string name = (string)Application.Current.FindResource("LOCSettingsWindowTitle");
                            var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Playnite Settings", "Open");
                            item.IconChar = IconChars.Settings;
                            foreach(CommandItemKey key in item.Keys)
                            {
                                key.Key = "> " + key.Key;
                            }
                        }
                        if (keyGesture.Key == Key.F5 && keyGesture.Modifiers == ModifierKeys.None)
                        {
                            string name = (string)Application.Current.FindResource("LOCUpdateAll") + " " + (string)Application.Current.FindResource("LOCLibraries");
                            var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Update All Libraries", (string)Application.Current.FindResource("LOCUpdateAll"));
                            item.IconChar = IconChars.Refresh;
                            foreach (CommandItemKey key in item.Keys)
                            {
                                key.Key = "> " + key.Key;
                            }
                        }
                        if (keyGesture.Key == Key.Q && keyGesture.Modifiers == ModifierKeys.Alt)
                        {
                            string name = (string)Application.Current.FindResource("LOCExitPlaynite");
                            var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Exit Playnite", (string)Application.Current.FindResource("LOCExitAppLabel"));
                            item.IconChar = IconChars.Exit;
                            foreach (CommandItemKey key in item.Keys)
                            {
                                key.Key = "> " + key.Key;
                            }
                        }
                        if (keyGesture.Key == Key.F11 && keyGesture.Modifiers == ModifierKeys.None)
                        {
                            string name = (string)Application.Current.FindResource("LOCMenuOpenFullscreen");
                            var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Switch to Fullscreen Mode", "Switch");
                            item.IconChar = IconChars.Maximize;
                            foreach (CommandItemKey key in item.Keys)
                            {
                                key.Key = "> " + key.Key;
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
                            var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Library Manager", "Open");
                            item.IconChar = IconChars.Settings;
                            foreach (CommandItemKey key in item.Keys)
                            {
                                key.Key = "> " + key.Key;
                            }
                        }
                        if (keyGesture.Key == Key.T && keyGesture.Modifiers == ModifierKeys.Control)
                        {
                            string name = (string)Application.Current.FindResource("LOCEmulatorsWindowTitle");
                            name = name.Replace("…", "");
                            var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Configure Emulators", "Configure");
                            item.IconChar = IconChars.Settings;
                            foreach (CommandItemKey key in item.Keys)
                            {
                                key.Key = "> " + key.Key;
                            }
                        }
                        if (keyGesture.Key == Key.D && keyGesture.Modifiers == ModifierKeys.Control)
                        {
                            string name = (string)Application.Current.FindResource("LOCMenuDownloadMetadata");
                            name = name.Replace("…", "");
                            var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Download Metadata", "Open");
                            item.IconChar = IconChars.Copy;
                            foreach (CommandItemKey key in item.Keys)
                            {
                                key.Key = "> " + key.Key;
                            }
                        }
                        if (keyGesture.Key == Key.F3 && keyGesture.Modifiers == ModifierKeys.None)
                        {
                            QuickSearchSDK.AddGameAction("Edit", g => binding.Command.Execute(g));
                        }
                    }
                }
                QuickSearchSDK.AddCommand(addGameCommand);
                foreach (CommandItemKey key in addGameCommand.Keys)
                {
                    key.Key = "> " + key.Key;
                }
                addGameCommand.Keys.Add(new CommandItemKey { Key = ">", Weight = 1 });
                var settingsCommand = new CommandItem("Open QuickSearch settings", () => OpenSettingsView(), "Open the QuickSearch settings view.", "Open") { IconChar = IconChars.Settings };
                foreach (CommandItemKey key in settingsCommand.Keys)
                {
                    key.Key = "> " + key.Key;
                }
                QuickSearchSDK.AddCommand(settingsCommand);
            }
            // QuickSearchSDK.AddGameAction("Show", g => PlayniteApi.Dialogs.ShowMessage(g.Name));

        }

        public Popup popup;
        SearchWindow searchWindow;

        private void ToggleSearch()
        {
            //PlayniteApi.Dialogs.ShowMessage("Shortcut Pressed!");

            if (popup is null)
            {
                popup = new Popup();
                popup.PlacementTarget = Application.Current.MainWindow;
                popup.Placement = PlacementMode.Center;
                popup.StaysOpen = false;
                searchWindow = new SearchWindow(this);
                searchWindow.DataContext = searchWindow;
                popup.Child = searchWindow;
                popup.InputBindings.Add(HotkeyBinding);
            }
            popup.IsOpen = !popup.IsOpen;
            if (popup.IsOpen)
            {
                foreach(var assembly in QuickSearchSDK.registeredAssemblies)
                {
                    if (!settings.EnabledAssemblies.ContainsKey(assembly))
                    {
                        settings.EnabledAssemblies.Add(assembly, new SearchSettings.AssemblyOptions());
                    }
                }
                
                searchWindow.Dispatcher.Invoke(() => {
                    searchWindow.QueueIndexUpdate();
                    searchWindow.SearchBox.SelectAll();
                    searchWindow.SearchBox.Focus();
                });
            }
        }

        public override void OnApplicationStopped()
        {
            // Add code to be executed when Playnite is shutting down.
        }

        public override void OnLibraryUpdated()
        {
            // Add code to be executed when library is updated.
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SearchSettingsView();
        }

    }
}