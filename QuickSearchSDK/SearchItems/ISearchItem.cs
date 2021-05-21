using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

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
        /// <returns><see cref="IEnumerable{T}"/> of <see cref="ISearchItem{TKey}"/>.</returns>
        IEnumerable<ISearchItem<TKey>> GetItems();
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
    }
}
