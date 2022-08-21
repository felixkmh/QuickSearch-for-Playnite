using Playnite.SDK;
using Playnite.SDK.Plugins;
using QuickSearch.Attributes;
using QuickSearch.SearchItems.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch.SearchItems
{
    public class ExtensionItemSource : ISearchItemSource<string>
    {
        private SearchPlugin searchPlugin;

        private List<ISearchItem<string>> mainMenuSearchItems = new List<ISearchItem<string>>();
        private List<ISearchItem<string>> pluginSettingsItems = new List<ISearchItem<string>>();

        public ExtensionItemSource(SearchPlugin searchPlugin)
        {
            this.searchPlugin = searchPlugin;

            var playniteApi = searchPlugin.PlayniteApi;

            var deserializer = new YamlDotNet.Serialization.Deserializer();

            var pluginsWithSettings = playniteApi.Addons.Plugins
                .OfType<GenericPlugin>()
                .Where(pl => pl?.Properties?.HasSettings ?? false)
                .Cast<Plugin>();

            pluginsWithSettings = pluginsWithSettings
                .Concat(playniteApi.Addons.Plugins
                    .OfType<LibraryPlugin>()
                    .Where(pl => pl?.Properties?.HasSettings ?? false)
                    .Cast<Plugin>()
            );

            pluginsWithSettings = pluginsWithSettings
                .Concat(playniteApi.Addons.Plugins
                    .OfType<MetadataPlugin>()
                    .Where(pl => pl?.Properties?.HasSettings ?? false)
                    .Cast<Plugin>()
            );

            var pluginsWithSettingsSet = pluginsWithSettings.ToHashSet();

            var plugins = playniteApi.Addons.Plugins;

            foreach (var plugin in plugins)
            {
                var installDir = System.IO.Path.GetDirectoryName(plugin.GetType().Assembly.Location);
                var extensionYaml = System.IO.Path.Combine(installDir, "extension.yaml");
                Uri iconUri = null;
                if (System.IO.File.Exists(extensionYaml))
                {
                    var text = System.IO.File.ReadAllText(extensionYaml);
                    var config = deserializer.Deserialize<Dictionary<string, object>>(text);
                    if (config != null)
                    {
                        string name = config.TryGetValue("Name", out var nameObject) ? nameObject as string : plugin.GetType().Name;
                        string icon = null;
                        if (config.TryGetValue("Icon", out var iconObject)) icon = iconObject as string;
                        if (config.TryGetValue("icon", out var iconObject2)) icon = iconObject2 as string;
                        // string addonId = config["Id"] as string;
                        if (name is string && pluginsWithSettingsSet.Contains(plugin))
                        {
                            var item = CreatePluginSettings(name, plugin.GetSettings(false), plugin.OpenSettingsView);
                            item.TopRight = config["Version"] as string;
                            if (icon is string && Uri.TryCreate(System.IO.Path.Combine(installDir, icon), UriKind.RelativeOrAbsolute, out var uri))
                            {
                                iconUri = uri;
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
                            pluginSettingsItems.Add(item);
                        }
                    }
                }
                var mainMenuItems = plugin.GetMainMenuItems(new GetMainMenuItemsArgs()) ?? new List<MainMenuItem>();
                foreach (var mainMenuItem in mainMenuItems)
                {
                    var path = (mainMenuItem.MenuSection ?? "")
                        .Replace("@", "")
                        .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    var title = string.Join(" > ", path);
                    var command = new CommandItem(
                        name: mainMenuItem.Description,
                        action: () => mainMenuItem.Action?.Invoke(new MainMenuItemActionArgs { SourceItem = mainMenuItem }),
                        actionName: ResourceProvider.GetString("LOC_QS_RunAction"),
                        description: title,
                        iconPath: null
                    );
                    if (mainMenuItem.Icon?.Length > 0)
                    {
                        if (mainMenuItem.Icon.Length == 1)
                        {
                            command.IconChar = mainMenuItem.Icon[0];
                        }
                        else
                        {
                            command.Icon = new Uri(mainMenuItem.Icon);
                        }
                    }
                    else
                    {
                        command.Icon = iconUri;
                    }
                    mainMenuSearchItems.Add(command);
                }
            }
        }

        public static CommandItem CreatePluginSettings<TSettings>(string pluginName, TSettings settings, Func<bool> openSettingsViewAction)
        {
            var settingsCommand = new CommandItem(
                pluginName + " " + ResourceProvider.GetString("LOCSettingsLabel"), 
                () => { openSettingsViewAction?.Invoke(); }, 
                string.Format(ResourceProvider.GetString("LOC_QS_OpenSettings"), pluginName), 
                ResourceProvider.GetString("LOC_QS_OpenAction")
            )
            {
                IconChar = IconChars.Settings
            };
            if (settings?.GetType().GetProperties().Any(prop => prop.GetCustomAttribute<GenericOptionAttribute>(true) != null) ?? false)
            {
                var subItemsSource = new SettingsItemSource<TSettings>() { Prefix = pluginName + " " + ResourceProvider.GetString("LOCSettingsLabel") as string, Settings = settings };
                var subItemsAction = new SubItemsAction() { Action = () => { }, Name = ResourceProvider.GetString("LOC_QS_ShowAction"), SubItemSource = subItemsSource, CloseAfterExecute = false };
                subItemsAction.SubItemSource = subItemsSource;
                settingsCommand.Actions.Add(subItemsAction);
            }
            foreach (CommandItemKey k in settingsCommand.Keys.ToArray())
            {
                settingsCommand.Keys.Add(new CommandItemKey() { Key = "> " + k.Key, Weight = 1 });
            }
            return settingsCommand;
        }

        public IEnumerable<ISearchItem<string>> GetItems()
        {
            IEnumerable<ISearchItem<string>> items = new List<ISearchItem<string>>();

            if (searchPlugin.Settings.AddMainMenuItemsAsCommands)
            {
                items = items.Concat(mainMenuSearchItems);
            }

            if (searchPlugin.Settings.AddExtensionSettingsItems)
            {
                items = items.Concat(pluginSettingsItems);
            }

            return items;
        }

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            return null;
        }

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return null;
        }
    }
}
