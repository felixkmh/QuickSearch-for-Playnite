﻿using Playnite.SDK;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

[assembly: InternalsVisibleTo("QuickSearch")]

namespace QuickSearch.SearchItems
{
    /// <inheritdoc cref="ISearchItem{TKey}"/>
    public class CommandItem : ISearchItem<string>
    {
        /// <summary>
        /// Create a empty simple command item.
        /// </summary>
        public CommandItem()
        {

        }

        /// <summary>
        /// Create a simple command item.
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <param name="action">Action to execute.</param>
        /// <param name="description">Item description.</param>
        /// <param name="actionName">Short display name of the action.</param>
        /// <param name="iconPath">Path to an icon.</param>
        public CommandItem(string name, Action action, string description = null, string actionName = null, string iconPath = null)
        {
            TopLeft = name;
            BottomLeft = description;
            if (actionName == null) actionName = ResourceProvider.GetString("LOC_QS_RunAction");

            if (!string.IsNullOrWhiteSpace(name))
            {
                Keys.Add(new CommandItemKey { Key = name, Weight = 1f });
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                Keys.Add(new CommandItemKey { Key = description, Weight = 1f });
            }

            if (action is Action)
            {
                Actions.Add(new CommandAction { Action = action, Name = actionName });
            }

            if (!string.IsNullOrEmpty(iconPath))
            {
                if (Uri.TryCreate(iconPath, UriKind.RelativeOrAbsolute, out var uri))
                {
                    Icon = uri;
                }
            }
        }

        /// <summary>
        /// Create a simple command item with multiple commands.
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <param name="actions">Actions to execute.</param>
        /// <param name="description">Item description.</param>
        /// <param name="iconPath">Path to icon.</param>
        public CommandItem(string name, IList<CommandAction> actions, string description = null, string iconPath = null)
        {
            TopLeft = name;
            BottomLeft = description;

            if (!string.IsNullOrWhiteSpace(name))
            {
                Keys.Add(new CommandItemKey { Key = name, Weight = 1f });
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                Keys.Add(new CommandItemKey { Key = description, Weight = 0.9f });
            }

            if (actions is IList<CommandAction>)
            {
                foreach(var action in actions)
                {
                    Actions.Add(action);
                }
            }

            if (!string.IsNullOrEmpty(iconPath))
            {
                if (Uri.TryCreate(iconPath, UriKind.RelativeOrAbsolute, out var uri))
                {
                    Icon = uri;
                }
            }
        }
        /// <summary>
        /// Create a simple command item.
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <param name="action">Action to execute.</param>
        /// <param name="description">Item description.</param>
        /// <param name="iconPath">Path to icon.</param>
        public CommandItem(string name, ISearchAction<string> action, string description = null, string iconPath = null)
        {
            TopLeft = name;
            BottomLeft = description;

            if (!string.IsNullOrWhiteSpace(name))
            {
                Keys.Add(new CommandItemKey { Key = name, Weight = 1f });
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                Keys.Add(new CommandItemKey { Key = description, Weight = 0.9f });
            }

            if (action is ISearchAction<string>)
            {
                Actions.Add(action);
            }

            if (!string.IsNullOrEmpty(iconPath))
            {
                if (Uri.TryCreate(iconPath, UriKind.RelativeOrAbsolute, out var uri))
                {
                    Icon = uri;
                }
            }
        }

        /// <inheritdoc cref="ISearchItem{TKey}.Keys"/>
        public IList<ISearchKey<string>> Keys { get; set; } = new List<ISearchKey<string>>();
        /// <inheritdoc cref="ISearchItem{TKey}.Actions"/>
        public IList<ISearchAction<string>> Actions { get; set; } = new List<ISearchAction<string>>();
        /// <inheritdoc cref="ISearchItem{TKey}.Icon"/>
        public Uri Icon { get; set; } = null;
        /// <inheritdoc cref="ISearchItem{TKey}.TopLeft"/>
        public string TopLeft { get; set; } = null;
        /// <inheritdoc cref="ISearchItem{TKey}.TopRight"/>
        public string TopRight { get; set; } = null;
        /// <inheritdoc cref="ISearchItem{TKey}.BottomLeft"/>
        public string BottomLeft { get; set; } = null;
        /// <inheritdoc cref="ISearchItem{TKey}.BottomCenter"/>
        public string BottomCenter { get; set; } = null;
        /// <inheritdoc cref="ISearchItem{TKey}.BottomRight"/>
        public string BottomRight { get; set; } = null;
        /// <inheritdoc cref="ISearchItem{TKey}.ScoreMode"/>
        public ScoreMode ScoreMode => ScoreMode.WeightedMaxScore;
        /// <inheritdoc cref="ISearchItem{TKey}.IconChar"/>
        public char? IconChar { get; set; } = null;
        /// <inheritdoc cref="ISearchItem{TKey}.DetailsView"/>
        public FrameworkElement DetailsView { get; set; } = null;
    }
    /// <inheritdoc cref="ISearchItemSource{TKey}"/>
    public class CommandItemSource : ISearchItemSource<string>
    {
        /// <summary>
        /// 
        /// </summary>
        public CommandItemSource() { }

        /// <summary>
        /// List holding the search items.
        /// </summary>
        public List<ISearchItem<string>> Items { get; set; } = new List<ISearchItem<string>>();


        /// <inheritdoc cref="ISearchItemSource{TKey}.GetItems(string)"/>
        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            return null;
        }

        /// <inheritdoc cref="ISearchItemSource{TKey}.GetItems()"/>
        public IEnumerable<ISearchItem<string>> GetItems()
        {
            return Items;
        }

        /// <inheritdoc cref="ISearchItemSource{TKey}.GetItemsTask(string, IReadOnlyList{Candidate})"/>
        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return null;
        }
    }
    /// <inheritdoc cref="ISearchKey{TKey}"/>
    public class CommandItemKey : ISearchKey<string>
    {
        /// <inheritdoc cref="ISearchKey{TKey}.Key"/>
        public string Key { get; set; }
        /// <inheritdoc cref="ISearchKey{TKey}.Weight"/>
        public float Weight { get; set; } = 1f;
    }
    /// <inheritdoc cref="ISearchAction{TKey}"/>
    public class CommandAction : ISearchAction<string>
    {
        /// <inheritdoc cref="ISearchAction{TKey}.Name"/>
        public string Name { get; set; }
        /// <summary>
        /// Action to be executed.
        /// </summary>
        public Action Action { get; set; }
        /// <inheritdoc cref="ISearchAction{TKey}"/>
        public bool CloseAfterExecute { get; set; } = true;

#pragma warning disable CS0067
        /// <inheritdoc cref="System.Windows.Input.ICommand.CanExecuteChanged"/>
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067
        /// <inheritdoc cref="System.Windows.Input.ICommand.CanExecute(object)"/>
        public bool CanExecute(object parameter)
        {
            return Action is Action;
        }
        /// <inheritdoc cref="System.Windows.Input.ICommand.Execute(object)"/>
        public void Execute(object parameter)
        {
            Action?.Invoke();
        }
    }
    /// <inheritdoc cref="ISubItemsAction{TKey}"/>
    public class SubItemsAction : CommandAction, ISubItemsAction<string>
    {
        /// <inheritdoc cref="ISubItemsAction{TKey}.SubItemSource"/>
        public ISearchSubItemSource<string> SubItemSource { get; set; } = new SubItemsSource();
    }
    /// <inheritdoc cref="ISearchItemSource{TKey}"/>
    public class SubItemsSource : CommandItemSource, ISearchSubItemSource<string>
    {
        /// <inheritdoc cref="ISearchSubItemSource{TKey}.Prefix"/>
        public string Prefix { get; set; }
        /// <inheritdoc cref="ISearchSubItemSource{TKey}.DisplayAllIfQueryIsEmpty"/>
        public bool DisplayAllIfQueryIsEmpty => true;
    }

    internal class ExternalCommandItemSource : ISearchItemSource<string>
    {
        public Dictionary<string, ISearchItem<string>> entries = new Dictionary<string, ISearchItem<string>>();

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            return null;
        }

        public IEnumerable<ISearchItem<string>> GetItems()
        {
            return entries.Values;
        }

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return null;
        }
    }
}
