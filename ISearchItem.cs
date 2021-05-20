using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace QuickSearch
{
    public interface ISearchKey<TKey>
    {
        TKey Key { get; }
        float Weight { get; }
    }

    public interface ISearchAction<TKey> : ICommand
    {
        string Name { get; }
    }

    public interface ISearchResultView
    {
        UIElement view { get; }
        void SetResult(object result);
    }

    public interface ISearchItemSource<TKey>
    {
        IEnumerable<ISearchItem<TKey>> GetItems();
    }

    public interface ISearchItem<TKey>
    {
        IList<ISearchKey<TKey>> Keys { get; }
        float TotalKeyWeight { get; }
        IList<ISearchAction<TKey>> Actions { get; }

        Uri Icon { get; }
        string TopLeft { get; }
        string TopRight { get; }
        string BottomLeft { get; }
        string BottomCenter { get; }
        string BottomRight { get; }
    }
}
