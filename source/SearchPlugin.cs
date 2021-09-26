using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using QuickSearch.SearchItems;
using QuickSearch.SearchItems.Settings;
using QuickSearch.Views;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
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

[assembly: InternalsVisibleTo("QuickSearch")]
namespace QuickSearch
{
    public class SearchPlugin : GenericPlugin
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
            instance = this;
            searchItemSources.Add("Games", new GameSearchSource());



            // searchItemSources.Add("Commands", simpleCommands);
            // QuickSearchSDK.AddItemSource("External_Commands", QuickSearchSDK.simpleCommands);
            // Add code to be executed when Playnite is initialized.
            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                Settings.SettingsChanged += OnSettingsChanged;
                mainWindow = Application.Current.MainWindow;
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
            }

        }

        private void AddBuiltInCommands()
        {
            CommandItem addGameCommand = new CommandItem((string)Application.Current.FindResource("LOCAddGames"), new List<CommandAction>(), "Add Games")
            {
                IconChar = IconChars.GameConsole
            };
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
                            item.Keys.Add(new CommandItemKey() { Key = "> " + key.Key, Weight = 1 });
                        }
                    }
                    if (keyGesture.Key == Key.F5 && keyGesture.Modifiers == ModifierKeys.None)
                    {
                        string name = (string)Application.Current.FindResource("LOCUpdateAll") + " " + (string)Application.Current.FindResource("LOCLibraries");
                        var item = QuickSearchSDK.AddCommand(name, () => binding.Command.Execute(binding.CommandParameter), "Update All Libraries", (string)Application.Current.FindResource("LOCUpdateAll"));
                        item.IconChar = IconChars.Refresh;
                        item.Actions[0] = new InputBindingWrapper(item.Actions[0].Name, binding);
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

            var itadSubItemsAction = new SubItemsAction() { Action = () => { }, Name = ResourceProvider.GetString("LOC_QS_OpenAction"), CloseAfterExecute = false, SubItemSource = new ITADItemSource() };
            var itadCommand = new CommandItem("IsThereAnyDeal", new List<CommandAction>(), string.Format(ResourceProvider.GetString("LOC_QS_SearchOnAction") , "IsThereAnyDeal.com"), "https://d2uym1p5obf9p8.cloudfront.net/images/banners/150x150.gif");
            itadCommand.Keys.Add(new CommandItemKey() { Key = "itad", Weight = 1 });
            itadCommand.Actions.Add(itadSubItemsAction);
            QuickSearchSDK.AddCommand(itadCommand);

            var cheapSharkSubItemsAction = new SubItemsAction() { Action = () => { }, Name = ResourceProvider.GetString("LOC_QS_OpenAction"), CloseAfterExecute = false, SubItemSource = new CheapSharkItemSource() };
            var cheapSharkCommand = new CommandItem("CheapShark", new List<CommandAction>(), string.Format(ResourceProvider.GetString("LOC_QS_SearchOnAction"), "CheapShark.com"), "https://www.cheapshark.com/img/logo_image.png?v=1.0");
            cheapSharkCommand.Actions.Add(cheapSharkSubItemsAction);
            QuickSearchSDK.AddCommand(cheapSharkCommand);
        }

        public Popup popup;
        internal SearchWindow searchWindow;
        private UIElement placementTarget;
        VisualBrush brush;
        Window dummyWindow;
        bool glassActive = false;

        private string GetAssemblyName(string name)
        {
            var sep = name.IndexOf('_');
            return name.Substring(0, sep);
        }

        private void ToggleSearch()
        {
            //PlayniteApi.Dialogs.ShowMessage("Shortcut Pressed!");

            if (popup is null)
            {
                var deserializer = new YamlDotNet.Serialization.Deserializer();
                var plugins = PlayniteApi.Addons.Plugins
                .OfType<GenericPlugin>()
                .Where(pl => pl?.Properties?.HasSettings ?? false);

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

                            if (name is string)
                            {
                                QuickSearchSDK.AddPluginSettings(name, plugin.GetSettings(false), plugin.OpenSettingsView);
                            }
                        }
                    }
                }

                popup = new Popup();
                popup.Opened += (s, a) =>
                {
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

                    var sources = searchItemSources.Values.AsEnumerable();

                    if (Settings.EnableExternalItems)
                    {
                        sources = sources.Concat(QuickSearchSDK.searchItemSources
                        .Where(e => SearchPlugin.Instance.Settings.EnabledAssemblies[GetAssemblyName(e.Key)].Items)
                        .Select(e => e.Value));
                    }

                    if (searchWindow.navigationStack.Count <= 1)
                    {
                        searchWindow.QueueIndexUpdate(sources);
                    }
                    else
                    {
                        searchWindow.QueueIndexUpdate();
                    }
                    searchWindow.SearchBox.SelectAll();
                    searchWindow.SearchBox.Focus();
                };

                popup.Closed += (s, e) =>
                {
                    searchWindow.QueueIndexClear();
                    dummyWindow.Hide();
                };

                placementTarget = Helper.UiHelper.FindVisualChildren(Application.Current.MainWindow, "PART_ContentView").FirstOrDefault();
                var p = VisualTreeHelper.GetParent(Application.Current.MainWindow);
                popup.Placement = PlacementMode.Center;
                popup.StaysOpen = false;
                popup.PlacementTarget = placementTarget;
                searchWindow = new SearchWindow(this);
                searchWindow.DataContext = searchWindow;
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

    }
}