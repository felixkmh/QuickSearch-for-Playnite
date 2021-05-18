using DuoVia.FuzzyStrings;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading;
using System;
using Playnite.SDK.Models;
using System.Collections.Generic;
using System.Collections;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;

namespace QuickSearch
{
    /// <summary>
    /// Interaktionslogik für SearchWindow.xaml
    /// </summary>
    public partial class SearchWindow : UserControl
    {
        SearchPlugin searchPlugin;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        Task backgroundTask = Task.CompletedTask;
        static UnboundedCache<GameResult> gameResults;

        public SearchWindow(SearchPlugin plugin)
        {
            InitializeComponent();
            searchPlugin = plugin;
            if (gameResults is null)
            {
                gameResults = new UnboundedCache<GameResult>(() => 
                { 
                    var result = new GameResult(); 
                    result.MouseLeftButtonDown += ItemClicked;
                    result.MouseDoubleClick += ItemDoubleClick;
                    result.AlwaysExpand = searchPlugin.settings.ExpandAllItems;
                    result.Seperator.Height = searchPlugin.settings.ShowSeperator ? 5 : 0;
                    return result; 
                });
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string input = textBox.Text.ToLower().Trim();
                tokenSource.Cancel();
                var oldSource = tokenSource;
                tokenSource = new CancellationTokenSource();
                var cancellationToken = tokenSource.Token;
                backgroundTask = backgroundTask.ContinueWith(_ => {

                    if (cancellationToken.IsCancellationRequested)
                    {
                        oldSource.Dispose();
                        return;
                    }

                    var results = new List<Game>().AsEnumerable();
                    if (!string.IsNullOrEmpty(input))
                    {
                        results = searchPlugin.PlayniteApi.Database.Games
                        .Where(g => Matching.GetScore(input, g.Name) / input.Replace(" ", "").Length >= searchPlugin.settings.Threshold)
                        .OrderByDescending(g => Matching.GetScore(input, g.Name) + input.LongestCommonSubsequence(Matching.RemoveDiacritics(g.Name.ToLower())).Item1.Length + Matching.MatchingWords(input, g.Name))
                        .ThenBy(g => Matching.RemoveDiacritics(g.Name))
                        .ThenByDescending(g => g.LastActivity ?? DateTime.MinValue)
                        .ThenByDescending(g => g.IsInstalled)
                        .Take(20);
                    }
                    int count = 0;
                    foreach(var result in results)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            oldSource.Dispose();
                            return;
                        }

                        Dispatcher.Invoke(() => { 
                            if (count < SearchResults.Items.Count)
                            {
                                GameResult item;
                                if (SearchResults.Items[count] is GameResult existing)
                                {
                                    item = existing;
                                } else
                                {
                                    item = gameResults.Get();
                                    SearchResults.Items[count] = item;
                                }
                                item.SetGame(result);
                                item.AlwaysExpand = searchPlugin.settings.ExpandAllItems;
                                item.Seperator.Height = searchPlugin.settings.ShowSeperator ? 5 : 0;
                            } else
                            {
                                var newItem = gameResults.Get();
                                newItem.SetGame(result);
                                newItem.AlwaysExpand = searchPlugin.settings.ExpandAllItems;
                                newItem.Seperator.Height = searchPlugin.settings.ShowSeperator ? 5 : 0;
                                SearchResults.Items.Add(newItem);
                            }
                            if (SearchResults.Items.Count > 0 && count == 0)
                            {
                                SearchResults.SelectedIndex = 0;
                            }
                        }, searchPlugin.settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Normal, cancellationToken);
                        count++;
                    }
                    Dispatcher.Invoke(() =>
                    {
                        for(int i = SearchResults.Items.Count - 1; i >= count; --i)
                        {
                            if (SearchResults.Items[i] is GameResult result)
                            {
                                gameResults.Push(result);
                            }
                            SearchResults.Items.RemoveAt(i);
                        }
                        if (SearchResults.Items.Count > 0)
                        {
                            SearchResults.SelectedIndex = 0;
                        }
                        if (SearchResults.Items.Count > 1)
                        {
                            var first = (UIElement)SearchResults.Items[0];
                            var second = (UIElement)SearchResults.Items[1];
                            var heightSelected = first.RenderSize.Height;
                            var heightNotSelected = second.RenderSize.Height;
                            var availableHeight = WindowGrid.Height;
                            availableHeight -= SearchBox.RenderSize.Height;
                            availableHeight -= heightSelected;
                            availableHeight -= SearchResults.Padding.Top + SearchResults.Padding.Bottom;
                            var maxItems = Math.Floor(availableHeight / heightNotSelected);
                            var maxHeight = heightSelected + maxItems * heightNotSelected + SearchResults.Padding.Top + SearchResults.Padding.Bottom + 2;
                            Decorator border = VisualTreeHelper.GetChild(SearchResults, 0) as Decorator;
                            ScrollViewer scrollViewer = border.Child as ScrollViewer;
                            if (maxItems + 1 >= SearchResults.Items.Count)
                            {
                                scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                            } else
                            {
                                scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                            }
                            SearchResults.MaxHeight = maxHeight;
                        } else
                        {
                            Decorator border = VisualTreeHelper.GetChild(SearchResults, 0) as Decorator;
                            ScrollViewer scrollViewer = border.Child as ScrollViewer;
                            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                        }
                    }, searchPlugin.settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Normal, cancellationToken);
                    oldSource.Dispose();
                }, tokenSource.Token);
            }
        }

        private void ItemClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item)
            {
                SearchResults.SelectedItem = item;
            }
        }

        private void ItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (sender is ListBoxItem item)
                {
                    SearchResults.SelectedItem = item;
                    if (item.DataContext is Game game)
                    {
                        searchPlugin.PlayniteApi.StartGame(game.Id);
                    }
                }
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                searchPlugin.popup.IsOpen = false;
            }
            var count = SearchResults.Items.Count;
            if (count > 0)
            {
                var idx = SearchResults.SelectedIndex;
                if (e.Key == Key.Down)
                {
                    if (SearchResults.SelectedIndex == -1)
                        SearchResults.SelectedIndex = 0;
                    idx = idx + 1;
                }
                if (e.Key == Key.Up)
                {
                    if (SearchResults.SelectedIndex == -1)
                        SearchResults.SelectedIndex = 0;
                    idx = idx - 1;
                }
                if(e.IsRepeat)
                {
                    idx = Math.Min(count - 1, Math.Max(0, idx));
                } else
                {
                    idx = (idx + count) % count;
                }
                SearchResults.ScrollIntoView(SearchResults.Items[idx]);
                SearchResults.SelectedIndex = idx;
                if (e.Key == Key.Enter || e.Key == Key.Return)
                {
                    if (SearchResults.SelectedIndex != -1)
                    {
                        if (((FrameworkElement)SearchResults.SelectedItem).DataContext is Game game)
                        {
                            searchPlugin.PlayniteApi.StartGame(game.Id);
                        }
                    }
                    searchPlugin.popup.IsOpen = false;
                }
            }
        }
    }

    class UnboundedCache<T> : Stack<T>
    {
        public bool HasItems { get => Count > 0; }

        protected readonly Func<T> generate;

        public virtual new void Push(T item)
        {
            base.Push(item);
        }

        public virtual void Consume<Collection>(Collection collection)
            where Collection : IList, ICollection, IEnumerable
        {
            for (int i = 0; i < collection.Count; ++i)
            {
                if (collection[i] is T item)
                {
                    base.Push(item);
                }
            }
            collection.Clear();
        }

        internal int Recycled = 0;
        internal int Generated = 0;

        public readonly Func<T> Get;

        internal T GetOrGenerate()
        {
            T item = default(T);
            if (Count > 0)
            {
                ++Recycled;
                item = Pop();
            }
            else if (generate != null)
            {
                ++Generated;
                item = generate.Invoke();
            }
            return item;
        }

        public UnboundedCache(Func<T> generator = null) : base()
        {
            this.generate = generator;
            if (generate != null)
            {
                Get = GetOrGenerate;
            }
            else
            {
                Get = () =>
                {
                    if (Count > 0)
                        return Pop();
                    return default(T);
                };
            }
        }
    }
}
