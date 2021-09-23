using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


[assembly: InternalsVisibleTo("QuickSearchSDK")]
namespace QuickSearch.SearchItems
{
    /// <summary>
    /// A candidate is a potential search result that might be 
    /// displayed, depending on the <see cref="Score"/>.
    /// </summary>
    public class Candidate
    {
        /// <summary>
        /// The <see cref="ISearchItem{TKey}"/> associated with this candidate
        /// </summary>
        public ISearchItem<string> Item { get; internal set; }
        /// <summary>
        /// The final combined score for this candidate used to sort in descending order.
        /// </summary>
        public float Score { get; internal set; }
        internal bool Marked;
    }

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

        /// <summary>
        /// Indicate whether the search window should
        /// close after executing this action.
        /// </summary>
        bool CloseAfterExecute { get; }
    }
    /// <summary>
    /// <see cref="ISearchAction{TKey}"/> with additional SubItemSource that
    /// is loaded in when action is executed. While a SubItemSource is active,
    /// all search requests are only directed towards this ItemSource.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public interface ISubItemsAction<TKey> : ISearchAction<TKey>
    {
        /// <summary>
        /// The ItemSource to be used as the only ItemSource
        /// after this action was executed.
        /// </summary>
        ISearchSubItemSource<TKey> SubItemSource { get; }
    }
    /// <summary>
    /// Source that provides <see cref="ISearchItem{TKey}"/>.
    /// </summary>
    /// <typeparam name="TKey">Type used for comparison.</typeparam>
    public interface ISearchItemSource<TKey>
    {
        /// <summary>
        /// Returns query independent items.
        /// </summary>
        /// <returns><see cref="IEnumerable{T}"/> of <see cref="ISearchItem{TKey}"/> or <see langword="null"/>.</returns>
        IEnumerable<ISearchItem<TKey>> GetItems();

        /// <summary>
        /// Returns query dependent items.
        /// </summary>
        /// <param name="query">The current search query, neither leading nor trailing spaces and in lower case.</param>
        /// <returns><see cref="IEnumerable{T}"/> of <see cref="ISearchItem{TKey}"/> or <see langword="null"/>.</returns>
        IEnumerable<ISearchItem<TKey>> GetItems(string query);

        /// <summary>
        /// Used to return search items asynchronously. Should not add same items as <see cref="ISearchItemSource{TKey}.GetItems(string)"/>.
        /// This would create duplicate entries. Uses this for items that depend on asynchronous data, like requesting data from a web api.
        /// <see cref="ISearchItemSource{TKey}.GetItemsTask(string, IReadOnlyList{Candidate})"/> is only called after a short time in which the query was not changed.
        /// </summary>
        /// <param name="query">The current search query, neither leading nor trailing spaces and in lower case.</param>
        /// <param name="addedItems">List of already added search item candicates sorted by score.</param>
        /// <returns><see cref="Task{TResult}"/> returning <see cref="IEnumerable{T}"/> of <see cref="ISearchItem{TKey}"/>, or <see langword="null"/></returns>
        Task<IEnumerable<ISearchItem<TKey>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems);
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
        /// <summary>
        /// Optional details view that is shown on to the right of the search window
        /// after a short delay if the item is selected.
        /// </summary>
        FrameworkElement DetailsView { get; }
    }
    /// <summary>
    /// A <see cref="ISearchItemSource{TKey}"/> that also supplies a 
    /// prefix that is displayed in front of the search query if active.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public interface ISearchSubItemSource<TKey> : ISearchItemSource<TKey>
    {
        /// <summary>
        /// String displayed in front of sub search query followed by a space.
        /// </summary>
        string Prefix { get; }
        /// <summary>
        /// If <see langword="true"/> is returned, all items of this source
        /// will be shown if the sub search query is empty.
        /// Otherwise items are displayed only if the sub query is not empty
        /// and their score is above the threshold set by the user.
        /// </summary>
        bool DisplayAllIfQueryIsEmpty { get; }
    }
}
