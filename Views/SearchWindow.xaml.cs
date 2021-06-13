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
        readonly SearchPlugin searchPlugin;
        CancellationTokenSource textChangedTokeSource = new CancellationTokenSource();
        CancellationTokenSource openedSearchTokeSource = new CancellationTokenSource();
        Task backgroundTask = Task.CompletedTask;
        internal IList<ISearchItem<string>> searchItems = Array.Empty<ISearchItem<string>>();

        internal Stack<IEnumerable<ISearchItemSource<string>>> navigationStack = new Stack<IEnumerable<ISearchItemSource<string>>>();

        internal IEnumerable<ISearchItemSource<string>> searchItemSources = new List<ISearchItemSource<string>>();

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
                SearchResults.Visibility = Visibility.Collapsed;
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
            SearchBox.Focus();
        }

        private string GetAssemblyName(string name)
        {
            var sep = name.IndexOf('_');
            return name.Substring(0, sep);
        }

        public void QueueIndexUpdate(IEnumerable<ISearchItemSource<string>> itemSources = null, bool isSubSource = false)
        {
            openedSearchTokeSource.Cancel();
            var oldSource = textChangedTokeSource;
            openedSearchTokeSource = new CancellationTokenSource();
            var cancellationToken = textChangedTokeSource.Token;
            backgroundTask = backgroundTask.ContinueWith((t) =>
            {
                t.Dispose();
                
                if (itemSources != null)
                {
                    if (navigationStack.Count == 1 && !isSubSource)
                    {
                        navigationStack.Clear();
                    }
                    navigationStack.Push(itemSources);
                }
                searchItemSources = navigationStack.Peek();

                searchItems = searchItemSources
                    .Select(source => source.GetItems())
                    .Where(items => items != null)
                    .SelectMany(items => items)
                    .ToList();
            });
        }

        public void QueueIndexClear()
        {
            backgroundTask = backgroundTask.ContinueWith((t) =>
            {
                if (searchItems != null)
                {
                    searchItems.Clear();
                    GC.Collect();
                }
                t.Dispose();
            });
        }

        // static float Clamp01(float f) => Math.Min(Math.Max(0f, f), 1f);

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
                    textChangedTokeSource?.Cancel();
                    backgroundTask = backgroundTask.ContinueWith(t =>
                    {
                        t.Dispose();
                        lastInput = input;
                        bool showAll = false;
                        if (navigationStack.Count > 1)
                        {
                            bool popped = false;
                            while (navigationStack.Count > 1 && navigationStack.Peek().First() is ISearchSubItemSource<string> subSource && !input.StartsWith(subSource.Prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                navigationStack.Pop();
                                popped = true;
                            }
                            if (navigationStack.Count > 1 && navigationStack.Peek().First() is ISearchSubItemSource<string> newSubSource) 
                            { 
                                input = input.Substring(newSubSource.Prefix.Length);
                                showAll = newSubSource.DisplayAllIfQueryIsEmpty && string.IsNullOrWhiteSpace(input);
                            } 
                            if (popped)
                            {
                                QueueIndexUpdate();
                            }
                        }
                        QueueSearch(input, showAll);
                    });
                }
            }
        }

        private void QueueSearch(string input, bool showAll = false)
        {
            textChangedTokeSource.Cancel();
            var oldSource = textChangedTokeSource;
            textChangedTokeSource = new CancellationTokenSource();
            var cancellationToken = textChangedTokeSource.Token;
            backgroundTask = backgroundTask.ContinueWith(t =>
            {
                var searchSw = Stopwatch.StartNew();
                t.Dispose();
                Dispatcher.Invoke(() => { IsLoadingResults = true; });

                if (cancellationToken.IsCancellationRequested)
                {
                    searchSw.Stop();
                    return;
                }
                int maxResults = 0;
                int addedItems = 0;
                List<Candidate> addedCandidates = new List<Candidate>();
                if (!string.IsNullOrEmpty(input) || showAll)
                {
                    var sources = searchItemSources;
                    var queryDependantItems = sources
                    .Select(source => source.GetItems(input))
                    .Where(items => items != null)
                    .SelectMany(items => items);

                    Candidate[] canditates;
                    if (showAll)
                    {
                        canditates = searchItems.Concat(queryDependantItems)
                        .Where(item => showAll || (ComputePreliminaryScore(item, input) >= searchPlugin.Settings.Threshold))
                        .Select(item => new Candidate { Marked = false, Item = item, Score = ComputeScore(item, input) }).ToArray();
                    } else
                    {
                        canditates = searchItems.Concat(queryDependantItems).AsParallel()
                        .Where(item => showAll || (ComputePreliminaryScore(item, input) >= searchPlugin.Settings.Threshold))
                        .Select(item => new Candidate { Marked = false, Item = item, Score = ComputeScore(item, input) }).ToArray();
                    }

                    maxResults = canditates.Count();
                    if (SearchPlugin.Instance.Settings.MaxNumberResults > 0)
                    {
                        maxResults = Math.Min(SearchPlugin.Instance.Settings.MaxNumberResults, maxResults);
                    }
                    if (showAll)
                    {
                        maxResults = canditates.Count();
                    }

                    addedCandidates.Capacity = maxResults;
                    
                    var sw = new Stopwatch();
                    bool done = false;
                    var passes = 0;
                    
                    while (addedItems < maxResults && !done)
                    {
                        ++passes;
                        if (cancellationToken.IsCancellationRequested)
                        {
                            searchSw.Stop();
                            sw.Stop();
                            return;
                        }

                        Dispatcher.Invoke(() =>
                        {
                            sw.Restart();
                            do
                            {
                                sw.Start();
                                var maxIdx = showAll ? addedItems : FindMax(canditates, cancellationToken);

                                if (maxIdx >= 0)
                                {
                                    canditates[maxIdx].Marked = true;
                                    addedCandidates.Add(canditates[maxIdx]);

                                    if (ListDataContext.Count > addedItems)
                                    {
                                        ListDataContext[addedItems] = canditates[maxIdx].Item;
                                    }
                                    else
                                    {
                                        ListDataContext.Add(canditates[maxIdx].Item);
                                    }
#if DEBUG
                                    PlaceholderText.Text = SearchBox.Text;
                                    PlaceholderText.Text += " - " + searchSw.ElapsedMilliseconds + "ms (" + passes + " passes)";
                                    PlaceholderText.Visibility = Visibility.Visible;
#endif
                                    if (ListDataContext.Count > 0 && addedItems == 0)
                                    {
                                        SearchResults.SelectedIndex = 0;
                                    }

                                    if (addedItems == 2 || (addedItems > 2 && (double.IsNaN(heightSelected) || double.IsNaN(heightNotSelected))))
                                    {
                                        UpdateListBox(maxResults);
                                    }
                                    ++addedItems;
                                }
                                else
                                {
                                    done = true;
                                    break;
                                }
                                sw.Stop();
                            } while (addedItems < maxResults && !cancellationToken.IsCancellationRequested && sw.ElapsedMilliseconds < 15);
                            sw.Stop();
                        }, searchPlugin.Settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Normal, cancellationToken);
                    }
                    sw.Reset();
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    searchSw.Stop();
                    return;
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

                }, searchPlugin.Settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Normal);

                Dispatcher.Invoke(() =>
                {
                    UpdateListBox(ListDataContext.Count);
                }, searchPlugin.Settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Render);
                searchSw.Stop();
                var elapsedMs = (int)searchSw.ElapsedMilliseconds;

                var remainingWaitTime = Math.Max(SearchPlugin.Instance.Settings.AsyncItemsDelay - elapsedMs, 0);
                if (SpinWait.SpinUntil(() => cancellationToken.IsCancellationRequested, remainingWaitTime))
                {
                    return;
                }

                InsertDelayedDependentItems(input, addedCandidates, (float)SearchPlugin.Instance.Settings.Threshold, SearchPlugin.Instance.Settings.MaxNumberResults, cancellationToken, showAll);

                Dispatcher.Invoke(() =>
                {
                    UpdateListBox(ListDataContext.Count);
                }, searchPlugin.Settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Render);

            }, textChangedTokeSource.Token);
            backgroundTask = backgroundTask.ContinueWith(t =>
            {
                t.Dispose();
                Dispatcher.Invoke(() =>
                {
                    IsLoadingResults = false;
                    oldSource.Dispose();
                });
            });
        }

        private readonly List<Task> allTasksList = new List<Task>();

        private void InsertDelayedDependentItems(string input, IList<Candidate> addedCandidates, float threshold, int maxItems, CancellationToken cancellationToken, bool showAll = false)
        {
            var queue = new ConcurrentQueue<ISearchItem<string>>();

            TaskFactory factory = new TaskFactory();

            var sources = searchItemSources;

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
                    if (preliminaryScore > threshold || showAll)
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
                                if (!showAll && CompareSearchItems(item, addedCandidates[i].Item) < 0)
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
                                    SearchResults.SelectedIndex = -1;
                                    SearchResults.SelectedIndex = 0;
                                } else
                                {
                                    var selected = SearchResults.SelectedIndex;
                                    SearchResults.SelectedIndex = -1;
                                    SearchResults.SelectedIndex = selected;
                                }
                            }, searchPlugin.Settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Normal);
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
                    bool updateMax;
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

        private int CompareSearchItems(ISearchItem<string> a, ISearchItem<string> b)
        {
            if (a is GameSearchItem gameItem)
            {
                if (b is GameSearchItem maxGameItem)
                {
                    var name = RemoveDiacritics(gameItem.game.Name).CompareTo(RemoveDiacritics(maxGameItem.game.Name));
                    var lastPlayed = (gameItem.game.LastActivity ?? DateTime.MinValue).CompareTo(maxGameItem.game.LastActivity ?? DateTime.MinValue);
                    var installed = gameItem.game.IsInstalled.CompareTo(maxGameItem.game.IsInstalled);

                    if (SearchPlugin.Instance.Settings.InstallationStatusFirst)
                    {
                        if (installed != 0)
                        {
                            return -1 * installed;
                        }
                        else if (name != 0)
                        {
                            return name;
                        }
                        else if (lastPlayed != 0)
                        {
                            return -1 * lastPlayed;
                        }
                    } else
                    {
                        if (name != 0)
                        {
                            return -1;
                        }
                        else if (lastPlayed != 0)
                        {
                            return -1 * lastPlayed;
                        }
                        else if (installed != 0)
                        {
                            return -1 * installed;
                        }
                    }                   
                }
            }

            var aIsGame = a is GameSearchItem;
            var bIsGame = b is GameSearchItem;

            if (bIsGame.CompareTo(aIsGame) != 0)
            {
                return bIsGame.CompareTo(aIsGame);
            }

            return a.TopLeft.CompareTo(b.TopLeft);
        } 

        private void UpdateListBox(int items)
        {
            if (SearchResults.Items.Count > 1)
            {
                if (double.IsNaN(heightSelected) || double.IsNaN(heightNotSelected))
                {
                    if (SearchResults.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem first &&
                        SearchResults.ItemContainerGenerator.ContainerFromIndex(1) is ListBoxItem second)
                    {
                        if(first != null && second != null)
                        {
                            heightSelected = first.RenderSize.Height;
                            heightNotSelected = second.RenderSize.Height;
                        }
                    }

                }
                if (!double.IsNaN(heightSelected) && !double.IsNaN(heightNotSelected))
                {
                    var availableHeight = WindowGrid.Height;
                    availableHeight -= SearchBox.RenderSize.Height;
                    availableHeight -= heightSelected;
                    availableHeight -= SearchResults.Padding.Top + SearchResults.Padding.Bottom;
                    availableHeight -= SearchResults.Margin.Top + SearchResults.Margin.Bottom;
                    var maxItems = Math.Floor(availableHeight / heightNotSelected);
                    var maxHeight = heightSelected + maxItems * heightNotSelected + SearchResults.Padding.Top + SearchResults.Padding.Bottom + 2;
                    Decorator border = VisualTreeHelper.GetChild(SearchResults, 0) as Decorator;
                    ScrollViewer scrollViewer = border.Child as ScrollViewer;
                    if (maxItems + 1 >= items)
                    {
                        var margin = SearchResults.Margin;
                        margin.Right = 0;
                        SearchResults.Margin = margin;
                        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    }
                    else
                    {
                        var margin = SearchResults.Margin;
                        margin.Right = -SystemParameters.VerticalScrollBarWidth;
                        SearchResults.Margin = margin;
                        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                    }
                    SearchResults.MaxHeight = maxHeight;
                }
            }
            else
            {
                if (VisualTreeHelper.GetChildrenCount(SearchResults) > 0)
                {
                    Decorator border = VisualTreeHelper.GetChild(SearchResults, 0) as Decorator;
                    ScrollViewer scrollViewer = border.Child as ScrollViewer;
                    var margin = SearchResults.Margin;
                    margin.Right = items > 5 ? -SystemParameters.VerticalScrollBarWidth : 0;
                    SearchResults.Margin = margin;
                    scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                }
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
                        if (SearchResults.SelectedIndex != -1)
                        {
                            if (ActionsListBox.SelectedItem is ISearchAction<string> action)
                            {
                                ExecuteAction(action);
                            }
                        }
                    }
                }
            }
            SearchBox.Focus();
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
                    idx++;
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        idx = count - 1;
                    }
                    e.Handled = true;
                }
                if (e.Key == Key.Up)
                {
                    if (SearchResults.SelectedIndex == -1)
                        SearchResults.SelectedIndex = 0;
                    idx--;
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        idx = 0;
                    }
                    e.Handled = true;
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
                    if (SearchResults.SelectedIndex != -1)
                    {
                        if (ActionsListBox.SelectedItem is ISearchAction<string> action)
                        {
                            ExecuteAction(action);
                        }
                    }
                }
            }
        }

        private void ExecuteAction(ISearchAction<string> action)
        {
            if (action is ISubItemsAction<string> subItemsAction)
            {
                var source = subItemsAction.SubItemSource;
                QueueIndexUpdate(new ISearchItemSource<string>[] { source }, true);
                if (source != null)
                {
                    SearchBox.Text = source.Prefix + " ";
                    SearchBox.CaretIndex = SearchBox.Text.Length;
                    PlaceholderText.Text = SearchBox.Text;
                    QueueSearch(source.Prefix + " ", source.DisplayAllIfQueryIsEmpty);
                }
            }
            else if (action.CloseAfterExecute)
            {
                searchPlugin.popup.IsOpen = false;
            }
            if (SearchResults.SelectedItem is ISearchItem<string> item)
            {
                if (action.CanExecute(item))
                {
                    action.Execute(item);
                }
            }
            SearchResults.Items.Refresh();
        }

        private void SelectActionButton(int idx)
        {
            ActionsListBox.SelectedIndex = idx;
            //var containter = ActionsListBox.ItemContainerGenerator.ContainerFromIndex(idx);
            ActionsListBox.ScrollIntoView(ActionsListBox.SelectedItem);
        }

        private void ActionButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is ActionButton bt)
            {
                var lbi = ActionsListBox.ContainerFromElement(bt);
                if (lbi is ListBoxItem)
                {
                    SelectActionButton(ActionsListBox.ItemContainerGenerator.IndexFromContainer(lbi));
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

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ActionButton bt)
            {
                if (bt.DataContext is ISearchAction<string> action)
                {
                    ExecuteAction(action);
                    SearchBox.Focus();
                }
            }
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
            if (d is ButtonBase target)
            {
                target.CommandParameter = e.NewValue;
                var temp = target.Command;
                // Have to set it to null first or CanExecute won't be called.
                target.Command = null;
                target.Command = temp;
            }
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
