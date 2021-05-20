﻿using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
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

        internal IDictionary<string, ISearchItemSource<string>> searchItemSources = new Dictionary<string, ISearchItemSource<string>>();
        internal static SearchItems.SimpleCommandItemSource simpleCommands = new SearchItems.SimpleCommandItemSource();

        public SearchSettings settings { get; set; }

        private InputBinding HotkeyBinding { get; set; }

        public override Guid Id { get; } = Guid.Parse("6a604592-7001-4b4e-a3be-91073b459e2b");

        public SearchPlugin(IPlayniteAPI api) : base(api)
        {
            settings = new SearchSettings(this);
        }

        public static SearchItems.SimpleCommandItem AddCommand(string name, Action action, string descripton = null, string absoluteIconPath = null)
        {
            var item = new SearchItems.SimpleCommandItem(name, action, descripton, absoluteIconPath);
            simpleCommands.Items.Add(item);
            return item;
        }

        public static bool RemoveCommand(SearchItems.SimpleCommandItem item)
        {
            return simpleCommands.Items.Remove(item);
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
            searchItemSources.Add("QuickSearch_Games", new SearchItems.GameSearchSource());
            searchItemSources.Add("QuickSearch_Commands", simpleCommands);
            // Add code to be executed when Playnite is initialized.
            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                settings.SettingsChanged += OnSettingsChanged;
                var window = Application.Current.MainWindow;
                HotkeyBinding = new InputBinding(new ActionCommand(ToggleSearch), new KeyGesture(settings.SearchShortcut.Key, settings.SearchShortcut.Modifiers));
                window.InputBindings.Add(HotkeyBinding);
            }

            AddCommand("Open QuickSearch settings", () => OpenSettingsView(), "Open the QuickSearch settings view.");
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