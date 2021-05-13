using Playnite.SDK;
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

namespace Search
{
    public class SearchPlugin : Plugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private SearchSettings settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("6a604592-7001-4b4e-a3be-91073b459e2b");

        public SearchPlugin(IPlayniteAPI api) : base(api)
        {
            settings = new SearchSettings(this);
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

        public override void OnApplicationStarted()
        {
            // Add code to be executed when Playnite is initialized.
            var window = Application.Current.MainWindow;
            var cmd = new RoutedCommand();
            cmd.InputGestures.Add(new KeyGesture(Key.Space, ModifierKeys.Alt));
            var binding = new CommandBinding(cmd);
            binding.Executed += ShortutPressed;
            window.CommandBindings.Add(binding);
        }
        public Popup popup;
        SearchWindow searchWindow;
        private void ShortutPressed(object sender, ExecutedRoutedEventArgs e)
        {
            //PlayniteApi.Dialogs.ShowMessage("Shortcut Pressed!");

            if (popup is null)
            {
                popup = new Popup();
                popup.PlacementTarget = Application.Current.MainWindow;
                popup.Placement = PlacementMode.Center;
                popup.StaysOpen = false;
            }
            searchWindow = new SearchWindow(this);
            popup.Child = searchWindow;
            popup.IsOpen = !popup.IsOpen;
            searchWindow.SearchBox.Focus();
            e.Handled = true;
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