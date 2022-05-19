using QuickSearch.SearchItems;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using QuickSearch.Models;
using static QuickSearch.Matching;
using System.Collections.Concurrent;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using LuceneDirectory = Lucene.Net.Store.Directory;
using System.IO;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search.Similar;
using Lucene.Net.Analysis.Snowball;
using Lucene.Net.Analysis.Ext;
using Lucene.Net.Analysis.NGram;
using Lucene.Net.Analysis.Position;
using SpellChecker.Net.Search.Spell;
using Lucene.Net.Search.Spans;

namespace QuickSearch.ViewModels
{
    public class LuceneSearchViewModel : ObservableObject, StartPage.SDK.IStartPageControl
    {
        public class CustomAnalyzer : Analyzer
        {
            public override TokenStream TokenStream(string fieldName, TextReader reader)
            {
                TokenStream t = null;
                t = new WhitespaceTokenizer(reader);
                t = new ASCIIFoldingFilter(t);
                t = new LowerCaseFilter(t);
                t = new NGramTokenFilter(t, 1, 10);

                return t;
            }
        }

        const Lucene.Net.Util.Version luceneVersion = Lucene.Net.Util.Version.LUCENE_30;

        Analyzer analyzer;

        internal LuceneDirectory indexDir = null;

        List<ISearchItem<string>> cachedItems = new List<ISearchItem<string>>();

        public LuceneSearchViewModel(SearchPlugin plugin)
        {
            searchPlugin = plugin;

            SearchResults.CollectionChanged += SearchResults_CollectionChanged;

            analyzer = new CustomAnalyzer();
            
            indexDir = new RAMDirectory();
        }

        private void SearchResults_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            HasItems = SearchResults.Count > 0;
        }

        private SearchPlugin searchPlugin;
        public SearchPlugin SearchPlugin => searchPlugin;

        private CancellationTokenSource textChangedTokenSource = new CancellationTokenSource();
        private CancellationTokenSource openedSearchTokenSource = new CancellationTokenSource();
        private Task backgroundTask = Task.CompletedTask;

        private IList<ISearchItem<string>> searchItems = new List<ISearchItem<string>>();
        public IList<ISearchItem<string>> SearchItems { get => searchItems; set => SetValue(ref searchItems, value); }

        internal Stack<IEnumerable<ISearchItemSource<string>>> navigationStack = new Stack<IEnumerable<ISearchItemSource<string>>>();
        private IEnumerable<ISearchItemSource<string>> searchItemSources = new List<ISearchItemSource<string>>();

        private bool isSearching = false;
        public bool IsSearching { get => isSearching; set => SetValue(ref isSearching, value); }

        private bool hasItems = false;
        public bool HasItems { get => hasItems; set => SetValue(ref hasItems, value); }

        private bool isLoadingResults = false;
        public bool IsLoadingResults { get => isLoadingResults; set => SetValue(ref isLoadingResults, value); }

        private string lastInput = string.Empty;
        public string LastInput { get => lastInput; set => SetValue(ref lastInput, value); }

        private Models.Candidate selectedItem = null;
        public Models.Candidate SelectedItem { get => selectedItem; set => SetValue(ref selectedItem, value); }

        private int selectedIndex = -1;
        public int SelectedIndex { get => selectedIndex; set => SetValue(ref selectedIndex, value); }

        private int actionIndex = 0;
        public int ActionIndex { get => actionIndex; set { if (value > 0) SetValue(ref actionIndex, value); } }

        private string input = string.Empty;
        public string Input
        {
            get => input;
            set
            {
                OnInputChanged(input, value);
                SetValue(ref input, value);
            }
        }

        private readonly List<Task> allTasksList = new List<Task>();

        private void OnInputChanged(string oldValue, string input)
        {
            if (!EnableSearch) return;
            var trimmedInput = input.ToLower().Trim();

            if (LastInput != trimmedInput)
            {
                textChangedTokenSource?.Cancel();
                if (navigationStack.Count > 1 && navigationStack.Peek().First() is ISearchSubItemSource<string> subSource2 && !trimmedInput.StartsWith(subSource2.Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (!(openedSearchTokenSource?.IsCancellationRequested ?? true))
                    {
                        openedSearchTokenSource.Cancel();
                    }
                }
                backgroundTask = backgroundTask.ContinueWith(t =>
                {
                    t.Dispose();
                    LastInput = trimmedInput;
                    bool showAll = false;
                    if (navigationStack.Count > 1)
                    {
                        bool popped = false;
                        while (navigationStack.Count > 1 && navigationStack.Peek().First() is ISearchSubItemSource<string> subSource && !trimmedInput.StartsWith(subSource.Prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            navigationStack.Pop();
                            popped = true;
                        }
                        if (navigationStack.Count > 1 && navigationStack.Peek().First() is ISearchSubItemSource<string> newSubSource)
                        {
                            trimmedInput = trimmedInput.Substring(newSubSource.Prefix.Length);
                            showAll = newSubSource.DisplayAllIfQueryIsEmpty && string.IsNullOrWhiteSpace(trimmedInput);
                        }
                        if (popped)
                        {
                            QueueIndexUpdate();
                        }
                    }
                    QueueSearch(trimmedInput, showAll);
                });
            }
        }

        private bool enableSearch = true;
        public bool EnableSearch { get => enableSearch; set => SetValue(ref enableSearch, value); }

        public ObservableCollection<Models.Candidate> SearchResults { get; private set; } = new ObservableCollection<Models.Candidate>();

        public IList<ISearchItem<string>> StartIndexUpdate(IEnumerable<ISearchItemSource<string>> itemSources = null, bool isSubSource = false)
        {
            if (itemSources != null)
            {
                if (navigationStack.Count == 1 && !isSubSource)
                {
                    navigationStack.Clear();
                }
                navigationStack.Push(itemSources);
            }
            searchItemSources = navigationStack.Peek();

            return searchItemSources
                .AsParallel()
                .Select(source => source.GetItems())
                .Where(items => items != null)
                .SelectMany(items => items)
                .ToList();
        }

        private int maxFields = 0;

        public void QueueIndexUpdate(IEnumerable<ISearchItemSource<string>> itemSources = null, bool isSubSource = false)
        {
            if (!(openedSearchTokenSource?.IsCancellationRequested ?? true))
            {
                openedSearchTokenSource.Cancel();
            }
            openedSearchTokenSource = new CancellationTokenSource();
            var cancellationToken = openedSearchTokenSource.Token;
            Task updateTask = Task.Run(() =>
            {
#if DEBUG
                var sw = Stopwatch.StartNew();
#endif
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsLoadingResults = true;
                });

                var items = StartIndexUpdate(itemSources, isSubSource);

                if (!cancellationToken.IsCancellationRequested)
                {
                    // SearchItems = items;
                    using (var writer = new IndexWriter(indexDir, analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED))
                    {
                        writer.DeleteAll();
                        cachedItems.Clear();
                        maxFields = 0;
                        foreach (var item in items)
                        {

                            var doc = new Document();
                            doc.Add(new Field("itemId", cachedItems.Count.ToString(), Field.Store.YES, Field.Index.NO));
                            cachedItems.Add(item);
                            int i = 0;
                            foreach (var key in item.Keys)
                            {
                                if (!string.IsNullOrWhiteSpace(key.Key))
                                {
                                    Field field = new Field($"key{i++}", false, key.Key, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
                                    field.Boost = key.Weight;
                                    doc.Add(field);
                                }
                            }
                            maxFields = Math.Max(maxFields, i);
                            writer.AddDocument(doc);
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }
                        }
                        writer.Commit();
                    }
                }
#if DEBUG
                sw.Stop();
                //Debug.WriteLine($"Updated index in {sw.Elapsed.TotalSeconds}s");
                SearchPlugin.logger.Info($"Updated Index in {sw.ElapsedMilliseconds / 1000.0} seconds.");
#endif
            });
            var task = Task.WhenAny(
                updateTask,
                Task.Run(() =>
                {
                    SpinWait.SpinUntil(() => cancellationToken.IsCancellationRequested || updateTask.IsCompleted || updateTask.IsFaulted);
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        IsLoadingResults = false;
                    });
                })
            );
            backgroundTask = backgroundTask.ContinueWith(t =>
            {
                t.Dispose();
                task.Wait();
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

        public float ComputeScore(ISearchItem<string> item, string input)
        {
            var scores = item.Keys
                .Where(k => k.Weight > 0)
                .Select(k => new { Score = GetCombinedScore(input, k.Key), Key = k });
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

        public float ComputePreliminaryScore(ISearchItem<string> item, string input)
        {
            var scores = item.Keys.Where(k => k.Weight > 0).Select(k => new { Score = MatchingLetterPairs2(input, k.Key, ScoreNormalization.Str1), Key = k });
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

        internal void QueueSearch(string input, bool showAll = false)
        {
            textChangedTokenSource.Cancel();
            var oldSource = textChangedTokenSource;
            textChangedTokenSource = new CancellationTokenSource();
            var cancellationToken = textChangedTokenSource.Token;
            var sources = searchItemSources;
            backgroundTask = backgroundTask.ContinueWith(t =>
            {
                var searchSw = Stopwatch.StartNew();
                t.Dispose();
                Application.Current.Dispatcher.Invoke(() => { IsLoadingResults = true; });

                if (cancellationToken.IsCancellationRequested)
                {
                    searchSw.Stop();
                    return;
                }
                int maxResults = 0;
                int addedItems = 0;
                var passes = 0;

                var prefix = searchItemSources.FirstOrDefault() is ISearchSubItemSource<string> subSource ? subSource.Prefix : string.Empty;
                prefix = prefix.Trim();
                var query = input;
                if (query.StartsWith(prefix))
                {
                    query = query.Substring(prefix.Length);
                    query = query.Trim();
                }

                List<Models.Candidate> addedCandidates = new List<Models.Candidate>();
                List<ISearchItem<string>> queryDependantItems = new List<ISearchItem<string>>();
                if (!string.IsNullOrEmpty(input) || showAll)
                {
                    var canditates = new List<Models.Candidate>();

                    foreach (var source in sources)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            searchSw.Stop();
                            return;
                        }
                        if (source.GetItems(input) is IEnumerable<ISearchItem<string>> items)
                        {
                            foreach (var item in items)
                            {
                                queryDependantItems.Add(item);
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    searchSw.Stop();
                                    return;
                                }
                            }
                        }
                    }

                    if (showAll)
                    {
                        canditates = queryDependantItems
                        .Where(item => !cancellationToken.IsCancellationRequested)
                        .Select(item => (!cancellationToken.IsCancellationRequested) ? new Models.Candidate { Marked = false, Item = item, Score = ComputeScore(item, input), Query = query } : null)
                        .ToList();
                    }
                    else
                    {
                        canditates = queryDependantItems.AsParallel()
                        .Where(item => !cancellationToken.IsCancellationRequested && ComputePreliminaryScore(item, input) >= searchPlugin.Settings.Threshold)
                        .Select(item => (!cancellationToken.IsCancellationRequested) ? new Models.Candidate { Marked = false, Item = item, Score = ComputeScore(item, input), Query = query } : null)
                        .Where(candidate => (candidate?.Score ?? 0) >= searchPlugin.Settings.Threshold)
                        .ToList();
                    }

                    var maxNumFields = maxFields;

                    var readers = new List<IndexReader>();
                    var searchers = new List<Searcher>();
                    if (sources.OfType<GameSearchSource>().FirstOrDefault() is GameSearchSource gameSource)
                    {
                        var reader = IndexReader.Open(gameSource.LuceneDirectory, true);
                        readers.Add(reader);
                        var searcher = new IndexSearcher(reader);
                        searchers.Add(searcher);
                        maxNumFields = Math.Max(maxNumFields, gameSource.MaxFields);
                    }

                    var defaultReader = IndexReader.Open(indexDir, true);
                    readers.Add(defaultReader);
                    var defaultSearcher = new IndexSearcher(defaultReader);
                    searchers.Add(defaultSearcher);

                    MultiSearcher multiSearcher = new MultiSearcher(searchers.ToArray());

                    using (var disposer = new MultiDisposer(searchers, readers, new[] { multiSearcher }))
                    {
                        var words = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        List<Query> queries = new List<Query>();

                        var specChars = new[] { "+", "-", "&&", "||", "!", "(", ")", "{", "}", "[", "]", "^", "\"", "~*", "?", ":" };

                        var escapedInput = query;

                        foreach (var specChar in specChars)
                        {
                            escapedInput = escapedInput.Replace(specChar, "\\" + specChar);
                        }

                        var terms = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(escapedInput))
                        {
                            var fieldQuery = new List<Query>();
                            for (int i = 0; i < maxFields; i++)
                            {
                                var boolQuery = new BooleanQuery();
                                var pos = 0;
                                foreach (var term in terms)
                                {
                                    var queryTerm = new Term($"key{i}", term);
                                    var fuzzy = new FuzzyQuery(queryTerm, 0.4f, 0);
                                    fuzzy.Boost = 5;
                                    boolQuery.Add(fuzzy, Occur.SHOULD);
                                    boolQuery.Add(new SpanFirstQuery(new SpanTermQuery(queryTerm), ++pos), Occur.SHOULD);
                                }
                                fieldQuery.Add(boolQuery);
                            }

                            var disjunction = new DisjunctionMaxQuery(fieldQuery, 0.9f);

                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }

                            TopDocs topDocs = multiSearcher.Search(disjunction, 111);
#if DEBUG
                        Debug.WriteLine($"Query answered in {searchSw.ElapsedMilliseconds}ms.");
#endif
                            GameSearchSource gameSource1 = null;
                            if (sources.OfType<GameSearchSource>().FirstOrDefault() is GameSearchSource s)
                            {
                                gameSource1 = s;
                            }

                            for (int i = 0; i < topDocs.ScoreDocs.Length; i++)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    return;
                                }
#if DEBUG
                            if (i == 0)
                            {
                                Debug.WriteLine(multiSearcher.Explain(disjunction, topDocs.ScoreDocs[i].Doc));
                            }
#endif
                                ISearchItem<string> item = null;
                                var resultDoc = multiSearcher.Doc(topDocs.ScoreDocs[i].Doc);

                                if (resultDoc.Get("gameId") is string gameIdString
                                    && Guid.TryParse(gameIdString, out var gameId)
                                    && gameSource1 != null)
                                {
                                    item = gameSource1.GetGameSearchItem(gameId);
                                }
                                else
                                {
                                    var id = int.Parse(resultDoc.Get("itemId"));
                                    if (cachedItems.Count > id)
                                    {
                                        item = cachedItems[id];
                                    }
                                }
                                if (item != null)
                                {
                                    var score = ComputeScore(item, input);
                                    if (score >= searchPlugin.Settings.Threshold)
                                    {
                                        canditates.Add(new Models.Candidate { Item = item, Query = input, Score = score });
                                    }
                                }
                            }
                        }
                        else if (showAll)
                        {
                            canditates = canditates.Concat(cachedItems.Select(item => new Models.Candidate { Item = item, Query = input })).ToList();
                        }
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        searchSw.Stop();
                        return;
                    }

                    if (canditates.Any(candidate => candidate.Item is GameSearchItem))
                    {
                        foreach (var candidate in canditates)
                        {
                            if (candidate.Item is FilterItem)
                            {
                                candidate.Score = (float)Math.Pow(candidate.Score, 8);
                            }
                        }
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
                    

                    while (addedItems < maxResults && !done)
                    {
                        ++passes;
                        if (cancellationToken.IsCancellationRequested)
                        {
                            searchSw.Stop();
                            sw.Stop();
                            return;
                        }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            sw.Restart();
                            do
                            {
                                sw.Start();
                                var maxIdx = showAll ? addedItems : FindMax(canditates, cancellationToken);

                                if (maxIdx >= 0)
                                {
                                    canditates[maxIdx].Marked = true;

                                    if (canditates[maxIdx].Item is FilterItem filterItem && !filterItem.filter.IsEmpty)
                                    {
                                        var matches = SearchPlugin.Instance.PlayniteApi.Database.Games.AsParallel().Count(filterItem.filter.Eval);
                                        if (matches == 0)
                                        {
                                            continue;
                                        } else
                                        {
                                            filterItem.TopRight = matches.ToString();
                                        }
                                    }

                                    addedCandidates.Add(canditates[maxIdx]);

                                    if (SearchResults.Count > addedItems)
                                    {
                                        var prevItem = SearchResults[addedItems].Item;
                                        if (canditates[maxIdx].Item == prevItem)
                                        {
                                            SearchResults[addedItems].Item = canditates[maxIdx].Item;
                                            SearchResults[addedItems].Score = canditates[maxIdx].Score;
                                            SearchResults[addedItems].Query = canditates[maxIdx].Query;
                                            SearchResults[addedItems].Marked = canditates[maxIdx].Marked;
                                        }
                                        else
                                        {
                                            SearchResults[addedItems] = canditates[maxIdx];
                                        }

                                        if (addedItems == 0)
                                        {
                                            if (prevItem == canditates[maxIdx].Item)
                                            {
                                                // SelectedIndex = selectedAction;
                                            }
                                            else
                                            {
                                                SelectedItem = SearchResults[0];
                                                SelectedIndex = 0;
                                            }
                                            if (prevItem != canditates[maxIdx].Item)
                                            {
                                                SelectedIndex = addedItems;
                                                SelectedItem = SearchResults[addedItems];
                                            }
                                        }
                                    }
                                    else
                                    {
                                        SearchResults.Add(canditates[maxIdx]);
                                    }
                                    if (SearchResults.Count > 0 && addedItems == 0)
                                    {
                                        SelectedIndex = 0;
                                        SelectedItem = SearchResults[0];
                                    }
                                    ++addedItems;
                                }
                                else
                                {
                                    done = true;
                                    break;
                                }
                                sw.Stop();
                            } while (addedItems < maxResults && !cancellationToken.IsCancellationRequested && sw.ElapsedMilliseconds <= 8);
                            sw.Stop();
                        }, searchPlugin.Settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Normal);
                    }
                    sw.Reset();
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    searchSw.Stop();
                    return;
                }
#if DEBUG
                Debug.WriteLine("------------------");
                foreach (var c in addedCandidates)
                {
                    Debug.WriteLine($"{c.Item.TopLeft} : {c.Score}");
                }
                Debug.WriteLine("------------------");
#endif
                Application.Current.Dispatcher.Invoke(() =>
                {
                    for (int i = SearchResults.Count - 1; i >= addedItems; --i)
                    {
                        SearchResults.RemoveAt(i);
                    }
                    if (SearchResults.Count > 0)
                    {
                        SelectedIndex = 0;
                    }

                }, searchPlugin.Settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Normal);

                searchSw.Stop();
                var elapsedMs = (int)searchSw.ElapsedMilliseconds;

                var remainingWaitTime = Math.Max(SearchPlugin.Instance.Settings.AsyncItemsDelay - elapsedMs, 0);
                if (SpinWait.SpinUntil(() => cancellationToken.IsCancellationRequested, remainingWaitTime))
                {
                    return;
                }

                InsertDelayedDependentItems(input, addedCandidates, (float)SearchPlugin.Instance.Settings.Threshold, SearchPlugin.Instance.Settings.MaxNumberResults, cancellationToken, showAll);


            }, textChangedTokenSource.Token);
            backgroundTask = backgroundTask.ContinueWith(t =>
            {
                t.Dispose();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsLoadingResults = false;
                    oldSource.Dispose();
                });
            });
        }

        private void InsertDelayedDependentItems(string input, IList<Models.Candidate> addedCandidates, float threshold, int maxItems, CancellationToken cancellationToken, bool showAll = false)
        {
            var queue = new ConcurrentQueue<ISearchItem<string>>();

            TaskFactory factory = new TaskFactory();

            var sources = searchItemSources;

            var highestScore = 0.0f;
            if (addedCandidates.Count > 0)
            {
                highestScore = ComputePreliminaryScore(addedCandidates.First().Item, input);
            }

            List<SearchItems.Candidate> addedItems = addedCandidates.Select(c => new SearchItems.Candidate { Item = c.Item, Marked = c.Marked, Score = c.Score }).ToList();
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
                foreach (var t in completed)
                {
                    t.Dispose();
                }
            });

            for (int i = allTasksList.Count - 1; i >= 0; --i)
            {
                var task = allTasksList[i];
                if (task.IsCompleted || task.IsCanceled || task.IsFaulted)
                {
                    task.Dispose();
                }
                allTasksList.RemoveAt(i);
            }

            var prefix = searchItemSources.FirstOrDefault() is ISearchSubItemSource<string> subSource ? subSource.Prefix : string.Empty;
            prefix = prefix.Trim();
            var query = input;
            if (query.StartsWith(prefix))
            {
                query = query.Substring(prefix.Length);
                query = query.Trim();
            }
            allTasksList.Add(allTasks);

            while (!cancellationToken.IsCancellationRequested)
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
                        for (int i = 0; i < addedCandidates.Count; ++i)
                        {
                            if (score > addedCandidates[i].Score)
                            {
                                insertionIdx = i;
                                break;
                            }
                            else if (score == addedCandidates[i].Score)
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
                            var candidateItem = new Models.Candidate() { Item = item, Score = score, Query = query };
                            addedCandidates.Insert(insertionIdx, candidateItem);

                            if (addedCandidates.Count > maxItems && maxItems > 0)
                            {
                                addedCandidates.RemoveAt(maxItems);
                            }
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                SearchResults.Insert(insertionIdx, candidateItem);

                                if (SearchResults.Count > maxItems && maxItems > 0)
                                {
                                    SearchResults.RemoveAt(maxItems);
                                }

                                if (SelectedIndex == -1)
                                {
                                    SelectedIndex = 0;
                                    SelectedIndex = -1;
                                    SelectedIndex = 0;
                                }
                                else
                                {
                                    var selected = SelectedIndex;
                                    SelectedIndex = -1;
                                    SelectedIndex = selected;
                                }
                            }, searchPlugin.Settings.IncrementalUpdate ? DispatcherPriority.Background : DispatcherPriority.Normal);
                        }
                    }
                }
            }


        }

        private int FindMax(in IList<Models.Candidate> canditates, in CancellationToken cancellationToken)
        {
            return FindMax(canditates, 0, canditates.Count, cancellationToken);
        }

        private int FindMax(IList<Models.Candidate> canditates, int start, int end, CancellationToken cancellationToken)
        {
            var length = end - start;

            if (cancellationToken.IsCancellationRequested)
            {
                return -1;
            }

            if (length > 400)
            {
                var leftIdxTask = Task.Run(() => FindMax(canditates, start, start + length / 2, cancellationToken));
                var rightIdxTask = Task.Run(() => FindMax(canditates, start + length / 2 + 1, end, cancellationToken));
                var leftIdx = leftIdxTask.Result;
                var rightIdx = rightIdxTask.Result;
                leftIdxTask.Dispose();
                rightIdxTask.Dispose();
                if (leftIdx != -1 && rightIdx == -1)
                {
                    return leftIdx;
                }
                else if (leftIdx == -1 && rightIdx != -1)
                {
                    return rightIdx;
                }
                else if (leftIdx == -1 && rightIdx == -1)
                {
                    return -1;
                }
                else if (canditates[leftIdx].Score > canditates[rightIdx].Score)
                {
                    return leftIdx;
                }
                else if (canditates[leftIdx].Score < canditates[rightIdx].Score)
                {
                    return rightIdx;
                }
                else
                {
                    return CompareSearchItems(canditates[leftIdx].Item, canditates[rightIdx].Item) <= 0 ? leftIdx : rightIdx;
                }
            }

            float maxScore = float.NegativeInfinity;
            int maxIdx = -1;

            for (int i = start; i < end; ++i)
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
                    var name = RemoveDiacritics(gameItem.game.Name.ToLower()).CompareTo(RemoveDiacritics(maxGameItem.game.Name.ToLower()));
                    var lastPlayed = (gameItem.game.LastActivity ?? DateTime.MinValue).CompareTo(maxGameItem.game.LastActivity ?? DateTime.MinValue);
                    var installed = gameItem.game.IsInstalled.CompareTo(maxGameItem.game.IsInstalled);
                    var hidden = gameItem.game.Hidden.CompareTo(maxGameItem.game.Hidden);
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
                        else if (hidden != 0)
                        {
                            return hidden;
                        }
                        else if (lastPlayed != 0)
                        {
                            return -1 * lastPlayed;
                        }
                    }
                    else
                    {
                        if (name != 0)
                        {
                            return -1;
                        }
                        else if (hidden != 0)
                        {
                            return hidden;
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

            var topLeftComparison = a.TopLeft.CompareTo(b.TopLeft);
            if (topLeftComparison != 0)
            {
                return topLeftComparison;
            }

            var bottomLeftComparison = a.BottomLeft.CompareTo(b.BottomLeft);
            if (bottomLeftComparison != 0)
            {
                return bottomLeftComparison;
            }

            var aType = a.GetType().Name;
            var bType = b.GetType().Name;

            return aType.CompareTo(bType);
        }

        public void OnStartPageOpened()
        {
            OpenSearch();
        }

        internal void OpenSearch()
        {
            foreach (var assembly in QuickSearchSDK.registeredAssemblies)
            {
                if (!searchPlugin.Settings.EnabledAssemblies.ContainsKey(assembly))
                {
                    searchPlugin.Settings.EnabledAssemblies.Add(assembly, new AssemblyOptions());
                }
            }

            var sources = searchPlugin.searchItemSources.Values.AsEnumerable();

            if (searchPlugin.Settings.EnableExternalItems)
            {
                sources = sources.Concat(QuickSearchSDK.searchItemSources
                .Where(e => SearchPlugin.Instance.Settings.EnabledAssemblies[searchPlugin.GetAssemblyName(e.Key)].Items)
                .Select(e => e.Value));
            }

            if (navigationStack.Count <= 1)
            {
                QueueIndexUpdate(sources);
            }
            else
            {
                QueueIndexUpdate();
            }
        }

        public void OnStartPageClosed()
        {
            CloseSearch();
        }

        internal void CloseSearch()
        {
            QueueIndexClear();
        }

        public void OnDayChanged(DateTime newTime)
        {

        }
    }
}
