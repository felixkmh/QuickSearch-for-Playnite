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
using static QuickSearch.Matching;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using QuickSearch.Controls;
using QuickSearch.SearchItems;

[assembly: InternalsVisibleTo("QuickSearch")]
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
        List<ISearchItem<string>> searchItems = new List<ISearchItem<string>>();

        private double heightSelected = double.NaN;
        private double heightNotSelected = double.NaN;

        public void Reset()
        {
            heightSelected = double.NaN;
            heightNotSelected = double.NaN;
            SearchBox.Text = string.Empty;
        }

        public ObservableCollection<ISearchItem<string>> ListDataContext { get; private set; } = new ObservableCollection<ISearchItem<string>>();

        public SearchWindow(SearchPlugin plugin)
        {
            InitializeComponent();
            searchPlugin = plugin;
            SearchResults.SelectionChanged += SearchResults_SelectionChanged;
            ListDataContext.CollectionChanged += ListDataContext_CollectionChanged;
        }

        private void ListDataContext_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (ListDataContext.Count > 0 && ActionsListBox.Visibility == Visibility.Hidden)
            {
                ActionsListBox.Visibility = Visibility.Visible;
                SearchResults.Visibility = Visibility.Visible;
            }
            if (ListDataContext.Count == 0 && ActionsListBox.Visibility == Visibility.Visible)
            {
                ActionsListBox.Visibility = Visibility.Hidden;
                SearchResults.Visibility = Visibility.Hidden;
            }

        }

        private void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (e.AddedItems.Count > 0)
            {
                if (ActionsListBox.Items.Count > 0)
                {
                    ActionsListBox.Dispatcher.BeginInvoke((Action<int>) SelectActionButton, DispatcherPriority.Normal, 0);
                }
            }
        }

        public void QueueIndexUpdate()
        {
            backgroundTask = backgroundTask.ContinueWith((t) =>
            {
                if (SearchPlugin.Instance.settings.EnableExternalItems)
                {
                    searchItems = QuickSearchSDK.searchItemSources.Values
                    .Concat(SearchPlugin.Instance.searchItemSources.Values)
                    .Where(source => !source.DependsOnQuery)
                    .AsParallel()
                    .SelectMany(source => source.GetItems(null))
                    .ToList();
                } else
                {
                    searchItems = SearchPlugin.Instance.searchItemSources.Values
                    .AsParallel()
                    .Where(source => !source.DependsOnQuery)
                    .SelectMany(source => source.GetItems(null))
                    .ToList();
                }
            });
        }

        struct Candidate {
            public ISearchItem<string> Item;
            public float Score;
            public bool Marked;
        }

        internal float ComputeScore(ISearchItem<string> item, string input)
        {
            var scores = item.Keys.Where(k => k.Weight > 0).Select(k => new { Score = GetCombinedScore(input, k.Key), Key = k});
            var weightSum = scores.Sum(s => s.Key.Weight);
            if (weightSum <= 0) weightSum = 1;
            switch (item.ScoreMode)
            {
                case ScoreMode.WeightedAverage:
                    return scores.Sum(s => s.Score * s.Key.Weight) / weightSum;
                case ScoreMode.WeightedMaxScore:
                    return scores.Max(s => s.Score * s.Key.Weight);
                case ScoreMode.WeightedMinScore:
                    return scores.Min(s => s.Score * s.Key.Weight);
                default:
                    return scores.Sum(s => s.Score * s.Key.Weight) / weightSum;
            }
        }

        internal float ComputePreliminaryScore(ISearchItem<string> item, string input)
        {
            var scores = item.Keys.Where(k => k.Weight > 0).Select(k => new { Score = MatchingLetterPairs(input, k.Key, ScoreNormalization.Str1), Key = k });
            var weightSum = scores.Sum(s => s.Key.Weight);
            if (weightSum <= 0) weightSum = 1;
            switch (item.ScoreMode)
            {
                case ScoreMode.WeightedAverage:
                    return scores.Sum(s => s.Score * s.Key.Weight) / weightSum;
                case ScoreMode.WeightedMaxScore:
                    return scores.Max(s => s.Score * s.Key.Weight);
                case ScoreMode.WeightedMinScore:
                    return scores.Min(s => s.Score * s.Key.Weight);
                default:
                    return scores.Sum(s => s.Score * s.Key.Weight) / weightSum;
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

                    int addedItems = 0;
                    if (!string.IsNullOrEmpty(input))
                    {
                        IEnumerable<ISearchItem<string>> queryDependantItems;
                        if (SearchPlugin.Instance.settings.EnableExternalItems)
                        {
                            queryDependantItems = QuickSearchSDK.searchItemSources.Values
                            .Concat(SearchPlugin.Instance.searchItemSources.Values)
                            .Where(source => source.DependsOnQuery)
                            .SelectMany(source => source.GetItems(input));
                        }
                        else
                        {
                            queryDependantItems = SearchPlugin.Instance.searchItemSources.Values
                            .Where(source => source.DependsOnQuery)
                            .SelectMany(source => source.GetItems(input));
                        }
                        Candidate[] canditates = Array.Empty<Candidate>();
                        if (queryDependantItems.Any())
                        {
                            canditates = searchItems.Concat(queryDependantItems).AsParallel()
                            .Where(item => ComputePreliminaryScore(item, input) >= searchPlugin.settings.Threshold)
                            .Select(item => new Candidate { Marked = false, Item = item, Score = ComputeScore(item, input) }).ToArray();
                        } else
                        {
                            canditates = searchItems.AsParallel()
                            .Where(item => ComputePreliminaryScore(item, input) >= searchPlugin.settings.Threshold)
                            .Select(item => new Candidate{ Marked = false, Item = item, Score = ComputeScore(item, input)}).ToArray();
                        }
                        var maxResults = canditates.Length;
                        if (SearchPlugin.Instance.settings.MaxNumberResults > 0)
                        {
                            maxResults = Math.Min(SearchPlugin.Instance.settings.MaxNumberResults, maxResults);
                        }


                        bool unmarkedLeft = true;

                        while(unmarkedLeft && addedItems < maxResults)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                oldSource.Dispose();
                                return;
                            }

                            unmarkedLeft = false;

                            float maxScore = float.NegativeInfinity;
                            int maxIdx = -1;

                            for (int i = 0; i < canditates.Length; ++i)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    oldSource.Dispose();
                                    return;
                                }

                                if (canditates[i].Marked)
                                {
                                    continue;
                                }
                                unmarkedLeft = true;
                                var item = canditates[i].Item;
                                float score = canditates[i].Score;

                                if (score >= maxScore)
                                {
                                    bool updateMax = false;
                                    if (score > maxScore)
                                    {
                                        updateMax = true;
                                    } else
                                    {
                                        if (item is SearchItems.GameSearchItem gameItem)
                                        {
                                            if (canditates[maxIdx].Item is SearchItems.GameSearchItem maxGameItem)
                                            {
                                                if (RemoveDiacritics(gameItem.game.Name).CompareTo(RemoveDiacritics(maxGameItem.game.Name)) < 0)
                                                {
                                                    updateMax = true;
                                                } else if ((gameItem.game.LastActivity ?? DateTime.MinValue).CompareTo(maxGameItem.game.LastActivity ?? DateTime.MinValue) > 0)
                                                {
                                                    updateMax = true;
                                                } else if (gameItem.game.IsInstalled.CompareTo(maxGameItem.game.IsInstalled) > 0)
                                                {
                                                    updateMax = true;
                                                }
                                            } 
                                        }
                                    }

                                    if (updateMax)
                                    {
                                        maxIdx = i;
                                        maxScore = score;
                                    }
                                }
                            }

                            if (maxIdx >= 0)
                            {
                                canditates[maxIdx].Marked = true;
                                Dispatcher.Invoke(() =>
                                {
                                    if(ListDataContext.Count > addedItems)
                                    {
                                        ListDataContext[addedItems] = canditates[maxIdx].Item;
                                    } else
                                    {
                                        ListDataContext.Add(canditates[maxIdx].Item);
                                    }

                                    if (ListDataContext.Count > 0 && addedItems == 0)
                                    {
                                        SearchResults.SelectedIndex = 0;
                                    }

                                    if (addedItems == 2)
                                    {
                                        UpdateListBox(maxResults);
                                    }
                                }, searchPlugin.settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Normal);

                                addedItems += 1;
                            }

                        }

                        
                    }

                    Dispatcher.Invoke(() =>
                    {
                        for (int i = ListDataContext.Count - 1; i >= addedItems; --i)
                        {
                            ListDataContext.RemoveAt(i);
                        }
                        if (ListDataContext.Count > 0)
                        {
                            SearchResults.SelectedIndex = 0;
                        }
                        UpdateListBox(ListDataContext.Count);
                    }, searchPlugin.settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Normal);
                    oldSource.Dispose();

                }, tokenSource.Token);
            }
        }

        private void UpdateListBox(int items)
        {
            if (SearchResults.Items.Count > 1)
            {
                if (double.IsNaN(heightSelected) || double.IsNaN(heightNotSelected))
                {
                    var first = SearchResults.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                    var second = SearchResults.ItemContainerGenerator.ContainerFromIndex(1) as ListBoxItem;
                    if(first != null && second != null)
                    {
                        heightSelected = first.RenderSize.Height;
                        heightNotSelected = second.RenderSize.Height;
                    }
                }
                if (!double.IsNaN(heightSelected) && !double.IsNaN(heightNotSelected))
                {
                    var availableHeight = WindowGrid.Height;
                    availableHeight -= SearchBox.RenderSize.Height;
                    availableHeight -= heightSelected;
                    availableHeight -= SearchResults.Padding.Top + SearchResults.Padding.Bottom;
                    var maxItems = Math.Floor(availableHeight / heightNotSelected);
                    var maxHeight = heightSelected + maxItems * heightNotSelected + SearchResults.Padding.Top + SearchResults.Padding.Bottom + 2;
                    Decorator border = VisualTreeHelper.GetChild(SearchResults, 0) as Decorator;
                    ScrollViewer scrollViewer = border.Child as ScrollViewer;
                    if (maxItems + 1 >= items)
                    {
                        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    }
                    else
                    {
                        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                    }
                    SearchResults.MaxHeight = maxHeight;
                }
            }
            else
            {
                Decorator border = VisualTreeHelper.GetChild(SearchResults, 0) as Decorator;
                ScrollViewer scrollViewer = border.Child as ScrollViewer;
                scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }
        }

        private void ItemClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 1)
                {
                    if (sender is SearchResult searchResult)
                    {
                        SearchResults.SelectedItem = searchResult;
                    }
                }
                if (e.ClickCount == 2)
                {
                    if (sender is SearchResult searchResult)
                    {
                        SearchResults.SelectedItem = searchResult;
                        searchPlugin.popup.IsOpen = false;
                        if (SearchResults.SelectedIndex != -1)
                        {
                            if (ActionsListBox.SelectedItem is ISearchAction<string> action)
                            {
                                action.Execute(searchResult.DataContext);
                            }
                        }
                    }
                }
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                int currentIdx = ActionsListBox.SelectedIndex;
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (currentIdx >= 0)
                    {
                        SelectActionButton((currentIdx + ActionsListBox.Items.Count - 1) % ActionsListBox.Items.Count);
                    }
                } else
                {
                    if (currentIdx >= 0)
                    {
                        SelectActionButton((currentIdx + 1) % ActionsListBox.Items.Count);
                    }
                }
                e.Handled = true;
            }
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
                    searchPlugin.popup.IsOpen = false;
                    if (SearchResults.SelectedIndex != -1)
                    {
                        if (ActionsListBox.SelectedItem is ISearchAction<string> action)
                        {
                            if (SearchResults.SelectedItem is ISearchItem<string> item)
                            {
                                action.Execute(item);
                            }
                        }
                    }
                }
            }
        }

        private void SelectActionButton(int idx)
        {
            ActionsListBox.SelectedIndex = idx;
            var containter = ActionsListBox.ItemContainerGenerator.ContainerFromIndex(idx);
            ActionsListBox.ScrollIntoView(ActionsListBox.SelectedItem);
        }

        private void ActionButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is ActionButton bt)
            {
                if (bt.Command is ISearchAction<string> action)
                {
                    var lbi = ActionsListBox.ContainerFromElement(bt);
                    if (lbi is ListBoxItem)
                    {
                        SelectActionButton(ActionsListBox.ItemContainerGenerator.IndexFromContainer(lbi));
                    }
                }
            }
        }

        private void ActionsListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Decorator border = VisualTreeHelper.GetChild(ActionsListBox, 0) as Decorator;
            ScrollViewer scrollViewer = border.Child as ScrollViewer;
            if (e.Delta > 0)
            {
                scrollViewer.LineLeft();
            }
            else
            {
                scrollViewer.LineRight();
            }
            e.Handled = true;
        }
    }

    // Fix for CanExecute being called with null value
    // https://stackoverflow.com/a/24669638
    public static class ButtonHelper
    {
        public static DependencyProperty CommandParameterProperty = DependencyProperty.RegisterAttached(
            "CommandParameter",
            typeof(object),
            typeof(ButtonHelper),
            new PropertyMetadata(CommandParameter_Changed));

        private static void CommandParameter_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = d as ButtonBase;
            if (target == null)
                return;

            target.CommandParameter = e.NewValue;
            var temp = target.Command;
            // Have to set it to null first or CanExecute won't be called.
            target.Command = null;
            target.Command = temp;
        }

        public static object GetCommandParameter(ButtonBase target)
        {
            return target.GetValue(CommandParameterProperty);
        }

        public static void SetCommandParameter(ButtonBase target, object value)
        {
            target.SetValue(CommandParameterProperty, value);
        }
    }
}
