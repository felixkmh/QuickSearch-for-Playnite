using QuickSearch.SearchItems;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;


[assembly: InternalsVisibleTo("QuickSearch")]
namespace QuickSearch
{
    /// <summary>
    /// Provides methods to add search items to QuickSearch.
    /// </summary>
    public static class QuickSearchSDK
    {
        internal static CommandItemSource simpleCommands = new CommandItemSource();

        internal static ConcurrentDictionary<string, ISearchItemSource<string>> searchItemSources = new ConcurrentDictionary<string, ISearchItemSource<string>>();

        internal static ConcurrentDictionary<string, Action<Playnite.SDK.Models.Game>> gameActions = new ConcurrentDictionary<string, Action<Playnite.SDK.Models.Game>>();

        /// <summary>
        /// Add action that is shown in the search bar when a game is selected.
        /// </summary>
        /// <param name="name">Short display name of the action.</param>
        /// <param name="action">Action to execute</param>
        /// <returns><see langword="true"/>, if action was added. <see langword="false"/>, if action with <paramref name="name"/> already exists.</returns>
        public static bool AddGameAction(string name, Action<Playnite.SDK.Models.Game> action)
        {
            return gameActions.TryAdd(name, action);
        }

        /// <summary>
        /// Remove action that is shown in the search bar when a game is selected.
        /// </summary>
        /// <param name="name"></param>
        /// <returns><see langword="true"/>, if action was removed. <see langword="false"/>, if action with <paramref name="name"/> could not be found.</returns>
        public static bool RemoveGameAction(string name)
        {
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
            if (id is string && source is ISearchItemSource<string>)
            {
                return searchItemSources.TryAdd(id, source);
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
            if (id is string)
            {
                return searchItemSources.TryRemove(id, out var _);
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
        /// <returns>The <see cref="ISearchItemSource{TKey}"/> that was added. Can be used to remove it.</returns>
        public static ISearchItem<string> AddCommand(string name, Action action, string descripton = null, string actionName = "Run", string iconPath = null)
        {
            var item = new CommandItem(name, action, descripton, actionName, iconPath);
            simpleCommands.Items.Add(item);
            return item;
        }

        /// <summary>
        /// Add a simple executable search entry with multiple actions that can be searched
        /// for by its <paramref name="name"/> and its <paramref name="descripton"/>.
        /// </summary>
        /// <param name="name">Name displayed on the top left. Always visible.</param>
        /// <param name="actions">List of actions shown in the search box when this item is selected.</param>
        /// <param name="descripton">Description displayed on the bottem left. Not visible when collapsed.</param>
        /// <param name="iconPath">Path to an image file. Used like a game icon on the left.</param>
        /// <returns>The <see cref="ISearchItemSource{TKey}"/> that was added. Can be used to remove it.</returns>
        public static ISearchItem<string> AddCommand(string name, IList<CommandAction> actions, string descripton = null, string iconPath = null)
        {
            var item = new CommandItem(name, actions, descripton, iconPath);
            simpleCommands.Items.Add(item);
            return item;
        }

        /// <summary>
        /// Adds a <see cref="ISearchItem{TKey}"/>.
        /// </summary>
        /// <param name="item"><see cref="ISearchItem{TKey}"/> to add.</param>
        /// <returns><see langword="true"/>, if item was added. <see langword="false"/>, if item already existed.</returns>
        public static bool AddCommand(ISearchItem<string> item) 
        {
            if (simpleCommands.Items.Contains(item))
            {
                return false;
            }
            simpleCommands.Items.Add(item);
            return true;
        }

        /// <summary>
        /// Removes the <see cref="ISearchItem{TKey}"/>.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns><see langword="true"/>, if <paramref name="item"/> was removed. <see langword="false"/>, if <paramref name="item"/> was not found.</returns>
        public static bool RemoveCommand(ISearchItem<string> item)
        {
            return simpleCommands.Items.Remove(item);
        }
    }
}
