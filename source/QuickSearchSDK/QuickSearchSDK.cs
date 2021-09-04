using Playnite.SDK;
using QuickSearch.Attributes;
using QuickSearch.SearchItems;
using QuickSearch.SearchItems.Settings;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

[assembly: InternalsVisibleTo("QuickSearch")]
namespace QuickSearch
{
    /// <summary>
    /// Provides methods to add search items to QuickSearch.
    /// </summary>
    public static class QuickSearchSDK
    {
        internal static ConcurrentDictionary<string, ISearchItemSource<string>> searchItemSources = new ConcurrentDictionary<string, ISearchItemSource<string>>();

        internal static ConcurrentDictionary<string, Action<Playnite.SDK.Models.Game>> gameActions = new ConcurrentDictionary<string, Action<Playnite.SDK.Models.Game>>();

        internal static ConcurrentBag<string> registeredAssemblies = new ConcurrentBag<string>();

        /// <summary>
        /// Add action that is shown in the search bar when a game is selected.
        /// </summary>
        /// <param name="name">Short display name of the action.</param>
        /// <param name="action">Action to execute</param>
        /// <returns><see langword="true"/>, if action was added. <see langword="false"/>, if action with <paramref name="name"/> already exists.</returns>
        public static bool AddGameAction(string name, Action<Playnite.SDK.Models.Game> action)
        {
            var assembly = Assembly.GetCallingAssembly();
            string assemblyName = assembly?.GetName()?.Name ?? "Null";
            assemblyName = assemblyName.Replace("_", " ");
            var key = $"{assemblyName}_{name}";
            if (!registeredAssemblies.Contains(assemblyName))
            {
                registeredAssemblies.Add(assemblyName);
            }
            return gameActions.TryAdd(key, action);
        }

        /// <summary>
        /// Remove action that is shown in the search bar when a game is selected.
        /// </summary>
        /// <param name="name"></param>
        /// <returns><see langword="true"/>, if action was removed. <see langword="false"/>, if action with <paramref name="name"/> could not be found.</returns>
        public static bool RemoveGameAction(string name)
        {
            var assembly = Assembly.GetCallingAssembly();
            string assemblyName = assembly?.GetName()?.Name ?? "Null";
            assemblyName = assemblyName.Replace("_", " ");
            var key = $"{assemblyName}_{name}";
            if (!registeredAssemblies.Contains(assemblyName))
            {
                registeredAssemblies.Add(assemblyName);
            }
            return gameActions.TryRemove(name, out var _);
        }

        /// <summary>
        /// Adds an <see cref="ISearchItemSource{TKey}"/> to a dictionary of sources 
        /// which are queried whenever the search window opens.
        /// <paramref name="id"/> is also displayed to the user to enable/disable a given source.
        /// </summary>
        /// <param name="id">ID to uniquely identify the added source. E.g. "MyPlugin Commands".</param>
        /// <param name="source">ISearchItemSource providing ISearchItems.</param>
        /// <returns><see langword="true"/>, if source was added. <see langword="false"/>, if a source with ID <paramref name="id"/> already existed.</returns>
        public static bool AddItemSource(string id, ISearchItemSource<string> source)
        {
            var assembly = Assembly.GetCallingAssembly();
            string assemblyName = assembly?.GetName()?.Name ?? "Null";
            assemblyName = assemblyName.Replace("_", " ");
            var key = $"{assemblyName}_{id}";
            if (!registeredAssemblies.Contains(assemblyName))
            {
                registeredAssemblies.Add(assemblyName);
            }
            if (id is string && source is ISearchItemSource<string>)
            {
                return searchItemSources.TryAdd(key, source);
            }
            return false;
        }

        /// <summary>
        /// Removes the <see cref="ISearchItemSource{TKey}"/> with the given ID 
        /// that it was added with.
        /// </summary>
        /// <param name="id"></param>
        /// <returns><see langword="true"/>, if source was removed. <see langword="false"/>, if no source with ID <paramref name="id"/> was found.</returns>
        public static bool RemoveItemSource(string id)
        {
            var assembly = Assembly.GetCallingAssembly();
            string assemblyName = assembly?.GetName()?.Name ?? "Null";
            assemblyName = assemblyName.Replace("_", " ");
            var key = $"{assemblyName}_{id}";
            if (!registeredAssemblies.Contains(assemblyName))
            {
                registeredAssemblies.Add(assemblyName);
            }
            if (id is string)
            {
                return searchItemSources.TryRemove(key, out var _);
            }
            return false;
        }


        /// <summary>
        /// Add a simple executable search entry that can be searched
        /// for by its <paramref name="name"/> and its <paramref name="descripton"/>.
        /// </summary>
        /// <param name="name">Name displayed on the top left. Always visible.</param>
        /// <param name="action"><see cref="Action"/> executed when user confirms the search result.</param>
        /// <param name="descripton">Description displayed on the bottem left. Not visible when collapsed.</param>
        /// <param name="actionName">Very short name for the action. Displayed next to the search query.</param>
        /// <param name="iconPath">Path to an image file. Used like a game icon on the left.</param>
        /// <returns>The <see cref="CommandItem"/> that was added or already existed and has the same <paramref name="name"/>. Can be used to remove it.</returns>
        public static CommandItem AddCommand(string name, Action action, string descripton = null, string actionName = "Run", string iconPath = null)
        {
            var assembly = Assembly.GetCallingAssembly();
            string assemblyName = assembly?.GetName()?.Name ?? "Null";
            assemblyName = assemblyName.Replace("_", " ");
            var key = $"{assemblyName}_{name}";
            if (!registeredAssemblies.Contains(assemblyName))
            {
                registeredAssemblies.Add(assemblyName);
            }

            var source = GetOrCreateSource(assemblyName);

            var item = new CommandItem(name, action, descripton, actionName, iconPath);
            if (!source.entries.ContainsKey(key))
            {
                source.entries.Add(key, item);
            }
            return source.entries[key] as CommandItem;
        }

        /// <summary>
        /// Add a simple executable search entry with multiple actions that can be searched
        /// for by its <paramref name="name"/> and its <paramref name="descripton"/>.
        /// </summary>
        /// <param name="name">Name displayed on the top left. Always visible.</param>
        /// <param name="actions">List of actions shown in the search box when this item is selected.</param>
        /// <param name="descripton">Description displayed on the bottem left. Not visible when collapsed.</param>
        /// <param name="iconPath">Path to an image file. Used like a game icon on the left.</param>
        /// <returns>The <see cref="CommandItem"/> that was added. Can be used to remove it.</returns>
        public static CommandItem AddCommand(string name, IList<CommandAction> actions, string descripton = null, string iconPath = null)
        {
            var assembly = Assembly.GetCallingAssembly();
            string assemblyName = assembly?.GetName()?.Name ?? "Null";
            assemblyName = assemblyName.Replace("_", " "); ;
            var key = $"{assemblyName}_{name}";
            if (!registeredAssemblies.Contains(assemblyName))
            {
                registeredAssemblies.Add(assemblyName);
            }
            var item = new CommandItem(name, actions, descripton, iconPath);

            var source = GetOrCreateSource(assemblyName);

            if (!source.entries.ContainsKey(key))
            {
                source.entries.Add(key, item);
            }
            return source.entries[key] as CommandItem;
        }

        /// <summary>
        /// Adds a <see cref="ISearchItem{TKey}"/>.
        /// </summary>
        /// <param name="item"><see cref="ISearchItem{TKey}"/> to add.</param>
        /// <returns><see langword="true"/>, if item was added. <see langword="false"/>, if item already existed.</returns>
        public static bool AddCommand(ISearchItem<string> item) 
        {
            var assembly = Assembly.GetCallingAssembly();
            string assemblyName = assembly?.GetName()?.Name ?? "Null";
            assemblyName = assemblyName.Replace("_", " ");
            if (!registeredAssemblies.Contains(assemblyName))
            {
                registeredAssemblies.Add(assemblyName);
            }
            var key = $"{assemblyName}_{item.TopLeft}";

            var source = GetOrCreateSource(assemblyName);

            if (!source.entries.ContainsKey(key))
            {
                source.entries.Add(key, item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes the <see cref="ISearchItem{TKey}"/>.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns><see langword="true"/>, if <paramref name="item"/> was removed. <see langword="false"/>, if <paramref name="item"/> was not found.</returns>
        public static bool RemoveCommand(ISearchItem<string> item)
        {
            var assembly = Assembly.GetCallingAssembly();
            string assemblyName = assembly?.GetName()?.Name ?? "Null";
            assemblyName = assemblyName.Replace("_", " ");

            var sourceName = "___UNNAMEDITEMSOURCE___";
            var combined = $"{assemblyName}_{sourceName}";

            if (searchItemSources.TryGetValue(combined, out var existing))
            {
                ExternalCommandItemSource source = (ExternalCommandItemSource)existing;
                var key = source.entries.Where(e => e.Value == item).Select(e => e.Key).FirstOrDefault();
                if (key != null)
                {
                    source.entries.Remove(key);
                    return true;
                }
                if (source.entries.Count == 0)
                {
                    searchItemSources.TryRemove(combined, out var _);
                }
            }
            return false;
        }

        /// <summary>
        /// Delegate that should open the settings view of a plugin.
        /// Every <see cref="Playnite.SDK.Plugins.Plugin"/> has the 
        /// <see cref="Playnite.SDK.Plugins.Plugin.OpenSettingsView"/> method
        /// that should be used.
        /// </summary>
        /// <returns>Whether settings were saved or not.</returns>
        public delegate bool OpenSettingsViewDelegate();

        /// <summary>
        /// Add a command that opens the settings for a plugin or shows some options as a result using a <see cref="SubItemsSource"/>.
        /// In order for options to be added to the search results, <typeparamref name="TSettings"/> needs to have at least one public property with a getter
        /// and a setter with either a <see cref="GenericOptionAttribute"/>, <see cref="SelectionOptionAttribute"/> or <see cref="NumberOptionAttribute"/>
        /// attached to it.
        /// </summary>
        /// <typeparam name="TSettings">Type of the settings object which has some properties 
        /// with a <see cref="GenericOptionAttribute"/>/<see cref="SelectionOptionAttribute"/>/<see cref="NumberOptionAttribute"/>
        /// atteched to it.
        /// </typeparam>
        /// <param name="pluginName">Name of the plugin.</param>
        /// <param name="settings">The settings object used by the plugin or a proxy object for it.</param>
        /// <param name="openSettingsViewAction">
        /// Every <see cref="Playnite.SDK.Plugins.Plugin"/> has the 
        /// <see cref="Playnite.SDK.Plugins.Plugin.OpenSettingsView"/> method
        /// that should be used here.
        /// </param>
        /// <returns>The item that was added. Can be used to remove it.</returns>
        public static SettingsItem<TSettings> AddPluginSettings<TSettings>(string pluginName, TSettings settings, OpenSettingsViewDelegate openSettingsViewAction)
        {
            var assembly = Assembly.GetCallingAssembly();
            string assemblyName = assembly?.GetName()?.Name ?? "Null";
            assemblyName = assemblyName.Replace("_", " "); ;
            var key = $"{assemblyName}_{pluginName}_SETTINGS";
            if (!registeredAssemblies.Contains(assemblyName))
            {
                registeredAssemblies.Add(assemblyName);
            }

            var settingsCommand = new CommandItem(pluginName + " " + Application.Current.FindResource("LOCSettingsLabel"), () => { openSettingsViewAction?.Invoke(); }, string.Format(ResourceProvider.GetString("LOC_QS_OpenSettings"), pluginName), ResourceProvider.GetString("LOC_QS_OpenAction"))
            {
                IconChar = IconChars.Settings
            };
            if (settings?.GetType().GetProperties().Any(prop => prop.GetCustomAttribute<GenericOptionAttribute>(true) != null) ?? false)
            {
                var subItemsSource = new SettingsItemSource<TSettings>() { Prefix = pluginName + " " + Application.Current.FindResource("LOCSettingsLabel") as string, Settings = settings };
                var subItemsAction = new SubItemsAction() { Action = () => { }, Name = ResourceProvider.GetString("LOC_QS_ShowAction"), SubItemSource = subItemsSource, CloseAfterExecute = false };
                subItemsAction.SubItemSource = subItemsSource;
                settingsCommand.Actions.Add(subItemsAction);
            }
            foreach (CommandItemKey k in settingsCommand.Keys.ToArray())
            {
                settingsCommand.Keys.Add(new CommandItemKey() { Key = "> " + k.Key, Weight = 1 });
            }
            var source = GetOrCreateSource(assemblyName);

            if (!source.entries.ContainsKey(key))
            {
                source.entries.Add(key, settingsCommand);
            }
            return source.entries[key] as SearchItems.Settings.SettingsItem<TSettings>;
        }

        private static ExternalCommandItemSource GetOrCreateSource(string assemblyName)
        {
            var sourceName = "___UNNAMEDITEMSOURCE___";
            var combined = $"{assemblyName}_{sourceName}";

            ExternalCommandItemSource source;
            if (searchItemSources.TryGetValue(combined, out var existing))
            {
                source = (ExternalCommandItemSource)existing;
            }
            else
            {
                source = new ExternalCommandItemSource();
                searchItemSources.TryAdd(combined, source);
            }

            return source;
        }
    }
}
