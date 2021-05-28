using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;


[assembly: InternalsVisibleTo("QuickSearchSDK")]
namespace QuickSearch.SearchItems
{
    /// <summary>
    /// Controls how scores of multiple keys are combined.
    /// </summary>
    public enum ScoreMode
    {   /// <summary>
        /// Sums up KeyScore * KeyWeight and divides by sum of KeyWeights.
        /// </summary>
        WeightedAverage,
        /// <summary>
        /// Score is the maximum KeyScore * KeyWeight.
        /// </summary>
        WeightedMaxScore,
        /// <summary>
        /// Score is the minimum KeyScore * KeyWeight.
        /// </summary>
        WeightedMinScore
    }
    /// <summary>
    /// Key used to compute score by comparing it to the query.
    /// </summary>
    /// <typeparam name="TKey">Type used for comparison.</typeparam>
    public interface ISearchKey<TKey>
    {
        /// <summary>
        /// The key that is compared to the query.
        /// </summary>
        TKey Key { get; }
        /// <summary>
        /// Weight of this key, usually between 0 and 1
        /// </summary>
        float Weight { get; }
    }
    /// <summary>
    /// Action associated with a search result. 
    /// One result can have multiple actions.
    /// The <see cref="ICommand.Execute(object)"/> and <see cref="ICommand.CanExecute(object)"/> methods
    /// are called with the <see cref="ISearchItem{TKey}"/>
    /// they belong to as its argument.
    /// </summary>
    /// <typeparam name="TKey">Type used for comparison.</typeparam>
    public interface ISearchAction<TKey> : ICommand
    {
        /// <summary>
        /// Display name of the action.
        /// </summary>
        string Name { get; }
    }
    /// <summary>
    /// Source that provides <see cref="ISearchItem{TKey}"/>.
    /// </summary>
    /// <typeparam name="TKey">Type used for comparison.</typeparam>
    public interface ISearchItemSource<TKey>
    {
        /// <summary>
        /// Returns <see cref="ISearchItem{TKey}"/>s.
        /// </summary>
        /// <param name="query">The current search query, neither leading nor trailing spaces and in lower case. Only supplied if <see cref="DependsOnQuery"/> returns <see langword="true"/>, otherwise it is <see langword="null"/>.</param>
        /// <returns><see cref="IEnumerable{T}"/> of <see cref="ISearchItem{TKey}"/> or <see langword="null"/>.</returns>
        IEnumerable<ISearchItem<TKey>> GetItems(string query);

        /// <summary>
        /// Used to return search items asynchronously. Should not add same items as <see cref="ISearchItemSource{TKey}.GetItems(string)"/>.
        /// This would create duplicate entries. Uses this for items that depend on asynchronous data, like requesting data from a web api.
        /// <see cref="ISearchItemSource{TKey}.GetItemsTask(string,float)"/> is only called after a short time in which the query was not changed.
        /// </summary>
        /// <param name="query">The current search query, neither leading nor trailing spaces and in lower case.</param>
        /// <param name="highestScore">Score of the first item in the list (between 0.0 and 1.0) after processing all non-asynchronous items.</param>
        /// <returns><see cref="Task{TResult}"/> returning <see cref="IEnumerable{T}"/> of <see cref="ISearchItem{TKey}"/>, or <see langword="null"/></returns>
        Task<IEnumerable<ISearchItem<TKey>>> GetItemsTask(string query, float highestScore);

        /// <summary>
        /// Indicates whether this source supplies items depending on the current search query.
        /// Returning true unnecessarily can slow down the search.
        /// </summary>
        bool DependsOnQuery { get; }
    }
    /// <summary>
    /// Item that holds data on how to search for it,
    /// which actions it supports and how it is presented.
    /// </summary>
    /// <typeparam name="TKey">Type used for comparison.</typeparam>
    public interface ISearchItem<TKey>
    {
        /// <summary>
        /// Keys that can be searched for.
        /// </summary>
        IList<ISearchKey<TKey>> Keys { get; }
        /// <summary>
        /// Actions associated with this item.
        /// </summary>
        IList<ISearchAction<TKey>> Actions { get; }
        /// <summary>
        /// Determines how the score is computed.
        /// </summary>
        ScoreMode ScoreMode { get; }
        /// <summary>
        /// Uri to the icon.
        /// </summary>
        Uri Icon { get; }
        /// <summary>
        /// Text on the top left. Always visible.
        /// </summary>
        string TopLeft { get; }
        /// <summary>
        /// Text on the top right. Always visible.
        /// </summary>
        string TopRight { get; }
        /// <summary>
        /// Text on the bottom left. Hidden if collapsed.
        /// </summary>
        string BottomLeft { get; }
        /// <summary>
        /// Text in the bottom center. Hidden if collapsed.
        /// </summary>
        string BottomCenter { get; }
        /// <summary>
        /// Text on bottom right. Hidden if collapsed.
        /// </summary>
        string BottomRight { get; }
        /// <summary>
        /// Alternative way to set an icon. Set a Unicode Icon 
        /// using the "icofont.ttf" that is included with Playnite by default.
        /// </summary>
        char? IconChar { get; }
    }
}
