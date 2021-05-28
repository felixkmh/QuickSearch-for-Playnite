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
using System.Collections.Concurrent;
using System.Diagnostics;

[assembly: InternalsVisibleTo("QuickSearch")]
namespace QuickSearch
{
    /// <summary>
    /// Interaktionslogik für SearchWindow.xaml
    /// </summary>
    public partial class SearchWindow : UserControl
    {
        SearchPlugin searchPlugin;
        CancellationTokenSource textChangedTokeSource = new CancellationTokenSource();
        CancellationTokenSource openedSearchTokeSource = new CancellationTokenSource();
        Task backgroundTask = Task.CompletedTask;
        IList<ISearchItem<string>> searchItems = Array.Empty<ISearchItem<string>>();

        private double heightSelected = double.NaN;
        private double heightNotSelected = double.NaN;

        public Boolean IsLoadingResults { 
            get => (Boolean)GetValue(IsLoadingResultsProperty); 
            set => SetValue(IsLoadingResultsProperty, value); 
        }
        public static DependencyProperty IsLoadingResultsProperty =
            DependencyProperty.Register(nameof(IsLoadingResults), typeof(Boolean), typeof(SearchWindow), new PropertyMetadata(false));

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
                SearchResults.ScrollIntoView(e.AddedItems[0]);
            }
        }

        private string GetAssemblyName(string name)
        {
            var sep = name.IndexOf('_');
            return name.Substring(0, sep);
        }

        public void QueueIndexUpdate()
        {
            openedSearchTokeSource.Cancel();
            var oldSource = textChangedTokeSource;
            openedSearchTokeSource = new CancellationTokenSource();
            var cancellationToken = textChangedTokeSource.Token;
            backgroundTask = backgroundTask.ContinueWith((t) =>
            {
                t.Dispose();
                var itemsEnumerable = SearchPlugin.Instance.searchItemSources.Values.AsEnumerable();

                if (SearchPlugin.Instance.settings.EnableExternalItems)
                {
                    var externalItems = QuickSearchSDK.searchItemSources
                    .Where(e => SearchPlugin.Instance.settings.EnabledAssemblies[GetAssemblyName(e.Key)].Items)
                    .Select(e => e.Value);
                    itemsEnumerable = itemsEnumerable.Concat(externalItems);
                }

                searchItems = itemsEnumerable
                    .Where(source => !source.DependsOnQuery)
                    .Select(source => source.GetItems(null))
                    .Where(items => items != null)
                    .SelectMany(items => items)
                    .ToList();
            });
        }

        float Clamp01(float f) => Math.Min(Math.Max(0f, f), 1f);

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

        string lastInput = string.Empty;

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                PlaceholderText.Text = (string)Application.Current.FindResource("LOCSearchLabel");
                PlaceholderText.Visibility = string.IsNullOrEmpty(textBox.Text) ? Visibility.Visible : Visibility.Hidden;
                string input = textBox.Text.ToLower().Trim();
                if (lastInput != input)
                {
                    var startTime = DateTime.Now;
                    lastInput = input;
                    textChangedTokeSource.Cancel();
                    var oldSource = textChangedTokeSource;
                    textChangedTokeSource = new CancellationTokenSource();
                    var cancellationToken = textChangedTokeSource.Token;
                    backgroundTask = backgroundTask.ContinueWith(t => {
                        t.Dispose();
                        Dispatcher.Invoke(() => IsLoadingResults = true);

                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        int maxResults = 0;
                        int addedItems = 0;
                        int firstUninstalledIdx = 0;
                        int prioritizedGames = 0;
                        List<Candidate> addedCandidates = new List<Candidate>();
                        if (!string.IsNullOrEmpty(input))
                        {
                            var sources = SearchPlugin.Instance.searchItemSources.Values.AsEnumerable();
                            if (SearchPlugin.Instance.settings.EnableExternalItems)
                            {
                                sources = sources
                                .Concat(
                                    QuickSearchSDK.searchItemSources
                                    .Where(entry => SearchPlugin.Instance.settings.EnabledAssemblies[GetAssemblyName(entry.Key)].Items)
                                    .Select(entry => entry.Value)
                                );
                            }
                            var queryDependantItems = sources
                            .Where(source => source.DependsOnQuery)
                            .Select(source => source.GetItems(input))
                            .Where(items => items != null)
                            .SelectMany(items => items);

                            var canditates = searchItems.Concat(queryDependantItems).AsParallel()
                            .Where(item => ComputePreliminaryScore(item, input) >= searchPlugin.settings.Threshold)
                            .Select(item => new Candidate { Marked = false, Item = item, Score = ComputeScore(item, input) }).ToArray();
                           
                            maxResults = canditates.Count();
                            if (SearchPlugin.Instance.settings.MaxNumberResults > 0)
                            {
                                maxResults = Math.Min(SearchPlugin.Instance.settings.MaxNumberResults, maxResults);
                            }

                            addedCandidates.Capacity = maxResults;

                            while(addedItems < maxResults)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    return;
                                }

                                var maxIdx = FindMax(canditates, cancellationToken);

                                if (maxIdx >= 0)
                                {
                                    canditates[maxIdx].Marked = true;
                                    addedCandidates.Add(canditates[maxIdx]);

                                    //bool prioritized = false;
                                    //if (prioritizedGames < SearchPlugin.Instance.settings.MaxNumberResults)
                                    //{
                                    //    if (canditates[maxIdx].Item is GameSearchItem gameItem)
                                    //    {
                                    //        if (gameItem.game.IsInstalled)
                                    //        {
                                    //            if (ComputePreliminaryScore(gameItem, input) >= SearchPlugin.Instance.settings.PrioritizationThreshold)
                                    //            {
                                    //                addedCandidates.Insert(firstUninstalledIdx, canditates[maxIdx]);
                                    //                prioritizedGames += 1;
                                    //                prioritized = true;
                                    //            }
                                    //            firstUninstalledIdx += 1;
                                    //        }
                                    //    }
                                    //} 
                                    //if (!prioritized)
                                    //{
                                    //    addedCandidates.Add(canditates[maxIdx]);
                                    //}

                                    Dispatcher.Invoke(() =>
                                    {

                                        if (ListDataContext.Count > addedItems)
                                        {
                                            ListDataContext[addedItems] = canditates[maxIdx].Item;
                                        } else
                                        {
                                            ListDataContext.Add(canditates[maxIdx].Item);
                                        }
#if DEBUG
                                        PlaceholderText.Text = SearchBox.Text + " - " + (int)((DateTime.Now - startTime).TotalMilliseconds) + "ms";
                                        PlaceholderText.Visibility = Visibility.Visible;
#endif
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
                                } else
                                {
                                    break;
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

                        }, searchPlugin.settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Background);

                        Dispatcher.Invoke(() =>
                        {
                            UpdateListBox(ListDataContext.Count);
                        }, searchPlugin.settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Render);

                        var elapsedMs = (int)(DateTime.Now - startTime).TotalMilliseconds;

                        var remainingWaitTime = Math.Max(SearchPlugin.Instance.settings.AsyncItemsDelay - elapsedMs, 0);
                        if (SpinWait.SpinUntil(() => cancellationToken.IsCancellationRequested, remainingWaitTime))
                        {
                            return;
                        }
                        
                        InsertDelayedDependentItems(cancellationToken, input, addedCandidates, (float)SearchPlugin.Instance.settings.Threshold, SearchPlugin.Instance.settings.MaxNumberResults);


                        Dispatcher.Invoke(() =>
                        {
                            UpdateListBox(ListDataContext.Count);
                        }, searchPlugin.settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Render);

                    }, textChangedTokeSource.Token);
                    backgroundTask = backgroundTask.ContinueWith(t => {
                        t.Dispose();
                        Dispatcher.Invoke(() => { 
                            IsLoadingResults = false;
                            oldSource.Dispose();
                        });
                    });
                }
            }
        }

        private List<Task> allTasksList = new List<Task>();

        private void InsertDelayedDependentItems(CancellationToken cancellationToken, string input, IList<Candidate> addedCandidates, float threshold, int maxItems)
        {
            var queue = new ConcurrentQueue<ISearchItem<string>>();

            TaskFactory factory = new TaskFactory();

            var sources = SearchPlugin.Instance.searchItemSources.Values.AsEnumerable();

            if (SearchPlugin.Instance.settings.EnableExternalItems)
            {
                sources = sources
                .Concat(
                    QuickSearchSDK.searchItemSources
                    .Where(entry => SearchPlugin.Instance.settings.EnabledAssemblies[GetAssemblyName(entry.Key)].Items)
                    .Select(entry => entry.Value)
                );
            }

            var highestScore = 0.0f;
            if (addedCandidates.Count > 0)
            {
                highestScore = ComputePreliminaryScore(addedCandidates.First().Item, input);
            }

            List<Candidate> addedItems = addedCandidates.ToList();
            var tasks = sources
                .AsParallel()
                .Select(source =>
                {
                    return source.GetItemsTask(input, addedItems);
                })
                .Where(task => task != null);

            if (!tasks.Any())
            {
                return;
            }

            var itemTasks = tasks.Select(task =>
            {
                return task.ContinueWith(t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        var items = t.Result;
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                queue.Enqueue(item);
                            }
                        }
                    }
                    t.Dispose();
                });
            });

            var allTasks = factory.ContinueWhenAll(itemTasks.ToArray(), completed => {
                foreach(var t in completed)
                {
                    t.Dispose();
                }
            });

            for(int i = allTasksList.Count -1; i >= 0; --i)
            {
                var task = allTasksList[i];
                if (task.IsCompleted || task.IsCanceled || task.IsFaulted)
                {
                    task.Dispose();
                }
                allTasksList.RemoveAt(i);
            }

            allTasksList.Add(allTasks);

            while(!cancellationToken.IsCancellationRequested)
            {
                if (allTasks.IsCompleted && queue.IsEmpty)
                {
                    break;
                }
                SpinWait.SpinUntil(() => !queue.IsEmpty || cancellationToken.IsCancellationRequested || allTasks.IsCompleted);

                while (queue.TryDequeue(out var item) && !cancellationToken.IsCancellationRequested)
                {
                    var preliminaryScore = ComputePreliminaryScore(item, input); 
                    if (preliminaryScore > threshold)
                    {
                        var score = ComputeScore(item, input);
                        int insertionIdx = addedCandidates.Count;
                        for(int i = 0; i < addedCandidates.Count; ++i)
                        {
                            if (score > addedCandidates[i].Score)
                            {
                                insertionIdx = i;
                                break;
                            } else if (score == addedCandidates[i].Score)
                            {
                                if (CompareSearchItems(item, addedCandidates[i].Item) < 0)
                                {
                                    insertionIdx = i;
                                    break;
                                }
                            }
                        }

                        if (insertionIdx >= 0 && (insertionIdx < maxItems || maxItems < 1))
                        {
                            addedCandidates.Insert(insertionIdx, new Candidate() { Item = item, Score = score });

                            if (addedCandidates.Count > maxItems && maxItems > 0)
                            {
                                addedCandidates.RemoveAt(maxItems);
                            }
                            Dispatcher.Invoke(() =>
                            {
                                ListDataContext.Insert(insertionIdx, item);

                                if (ListDataContext.Count > maxItems && maxItems > 0)
                                {
                                    ListDataContext.RemoveAt(maxItems);
                                }

                                if (SearchResults.SelectedIndex == -1)
                                {
                                    SearchResults.SelectedIndex = 0;
                                } else
                                {
                                    var selected = SearchResults.SelectedIndex;
                                    SearchResults.SelectedIndex = -1;
                                    SearchResults.SelectedIndex = selected;
                                }
                            }, searchPlugin.settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Normal);
                        }
                    }
                }
            }


        }

        private int FindMax(in IList<Candidate> canditates, in CancellationToken cancellationToken)
        {
            float maxScore = float.NegativeInfinity;
            int maxIdx = -1;

            for (int i = 0; i < canditates.Count; ++i)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return -1;
                }

                if (canditates[i].Marked)
                {
                    continue;
                }

                var item = canditates[i].Item;
                float score = canditates[i].Score;

                if (score >= maxScore)
                {
                    bool updateMax = false;
                    if (score > maxScore)
                    {
                        updateMax = true;
                    }
                    else
                    {
                        updateMax = CompareSearchItems(item, canditates[maxIdx].Item) < 0;
                    }

                    if (updateMax)
                    {
                        maxIdx = i;
                        maxScore = score;
                    }
                }
            }

            return maxIdx;
        }

        private int CompareSearchItems(in ISearchItem<string> a, in ISearchItem<string> b)
        {
            if (a is GameSearchItem gameItem)
            {
                if (b is GameSearchItem maxGameItem)
                {
                    var name = RemoveDiacritics(gameItem.game.Name).CompareTo(RemoveDiacritics(maxGameItem.game.Name));
                    var lastPlayed = (gameItem.game.LastActivity ?? DateTime.MinValue).CompareTo(maxGameItem.game.LastActivity ?? DateTime.MinValue);
                    var installed = gameItem.game.IsInstalled.CompareTo(maxGameItem.game.IsInstalled);

                    if (SearchPlugin.Instance.settings.InstallationStatusFirst)
                    {
                        if (installed > 0)
                        {
                            return -1;
                        }
                        else if (installed == 0 && name < 0)
                        {
                            return -1;
                        }
                        else if (name == 0 && installed == 0 && lastPlayed > 0)
                        {
                            return -1;
                        }
                    } else
                    {
                        if (name < 0)
                        {
                            return -1;
                        }
                        else if (name == 0 && lastPlayed > 0)
                        {
                            return -1;
                        }
                        else if (name == 0 && lastPlayed == 0 && installed > 0)
                        {
                            return -1;
                        }
                    }                   
                }
            }

            var aIsGame = a is GameSearchItem;
            var bIsGame = b is GameSearchItem;

            return bIsGame.CompareTo(aIsGame);
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
                                if (action.CanExecute(item))
                                {
                                    action.Execute(item);
                                }
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
