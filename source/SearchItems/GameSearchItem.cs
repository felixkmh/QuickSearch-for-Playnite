using Playnite.SDK;
using Playnite.SDK.Models;
using QuickSearch.Controls;
using QuickSearch.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheArtOfDev.HtmlRenderer.WPF;

namespace QuickSearch.SearchItems
{
    using GameFilter = MultiFilter<Game>;

    struct NameKey : ISearchKey<string>
    {
        public NameKey(Game game)
        {
            this.game = game;
        }
        public Game game;
        public string Key => game.Name;

        public float Weight => 1f;
    }

    struct AcronymKey : ISearchKey<string>
    {
        public static readonly Regex romanNumerals = new Regex(@"^[MDCLXVI]+$");
        // https://mnaoumov.wordpress.com/2014/06/14/stripping-invalid-characters-from-utf-16-strings/
        public static readonly Regex invalidChar = new Regex(@"([\ud800-\udbff](?![\udc00-\udfff]))|((?<![\ud800-\udbff])[\udc00-\udfff])");

        public AcronymKey(Game game)
        {
            this.game = game;
            // var words = game.Name.Split(new[] { ' ', '-', ':', '"', '\'', '&', '.', '«', '»', '“', '”', '„', '‟' }, StringSplitOptions.RemoveEmptyEntries);
            var nameWithoutQuotes = game.Name.Replace("\'", "");
            var normalized = Matching.RemoveDiacritics(nameWithoutQuotes);
            normalized = CleanNameKey.spaceLike.Replace(normalized, " ");
            normalized = CleanNameKey.regex.Replace(normalized, string.Empty);
            var matches = CleanNameKey.words.Matches(normalized);
            List<string> words = new List<string>();
            foreach(Match match in matches)
            {
                words.Add(match.Value);
            }
            var sb = new StringBuilder();
            foreach (var word in words)
            {
                if (int.TryParse(word, out var _) || romanNumerals.IsMatch(word))
                {
                    sb.Append(word);
                } else if (!invalidChar.IsMatch(word[0].ToString()))
                {
                    sb.Append(word[0]);
                }
            }
            Key = sb.ToString().ToUpper();
        }
        public Game game;
        public string Key { get; internal set; }

        public float Weight => 0.9998f;
    }

    struct CleanNameKey : ISearchKey<string>
    {
        static public readonly Regex regex = new Regex(@"[\[\]\{}&.,:;^°_`´~!""§$%&/()=?<>#|'’]");
        static public readonly Regex spaceLike = new Regex(@"[-+]");
        static public readonly Regex words = new Regex(@"([A-Z][a-z]+)|([A-Z,a-z]+)|([A-Z,a-z,0-9]+)");

        public CleanNameKey(Game game)
        {
            this.game = game;
            Key = spaceLike.Replace(game.Name, " ");
            Key = regex.Replace(Key, string.Empty);
        }

        public Game game;
        public string Key { get; internal set; }

        public float Weight => 1f;
    }

    struct RomKey : ISearchKey<string>
    {
        public Game game;
        public string Key => game.Roms?.FirstOrDefault()?.Name;

        public float Weight => string.IsNullOrEmpty(game.Roms?.FirstOrDefault()?.Name) ? 0f : 1f;
    }

    struct SourceKey : ISearchKey<string>
    {
        public Game game;
        public string Key => game.Source?.Name;

        public float Weight => string.IsNullOrEmpty(game.Source?.Name) ? 0f : 0.01f;
    }

    class ContextAction : ISearchAction<string>
    {
        public string Name { get; set; }

        public bool CloseAfterExecute => true;
#pragma warning disable CS0067
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067
        public bool CanExecute(object searchItem)
        {
            if (searchItem is GameSearchItem game)
            {
                return SearchPlugin.Instance.PlayniteApi.Database.Games.Get(game.game.Id) is Game;
            }
            return false;
        }

        public void Execute(object searchItem)
        {
            if (searchItem is GameSearchItem game)
            {
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background, 
                    (Action<Guid>)SearchPlugin.Instance.PlayniteApi.StartGame, 
                    game.game.Id);
            }
        }
    }

    public class GameAction : ISearchAction<string>
    {
        public Action<Game> Action { get; set; }

        public string Name { get; set; }

        public bool CloseAfterExecute => true;
#pragma warning disable CS0067
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067
        public bool CanExecute(object searchItem)
        {
            if (searchItem is GameSearchItem game)
            {
                return SearchPlugin.Instance.PlayniteApi.Database.Games.Get(game.game.Id) is Game;
            }
            return false;
        }

        public void Execute(object searchItem)
        {
            if (searchItem is GameSearchItem game)
            {
                Action(game.game);
            }
        }
    }

    class RemoveAction : ISearchAction<string>
    {
        public string Name { get; set; } = "Edit";

        public bool CloseAfterExecute => true;
#pragma warning disable CS0067
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067
        public bool CanExecute(object searchItem)
        {
            if (searchItem is GameSearchItem game)
            {
                return SearchPlugin.Instance.PlayniteApi.Database.Games.Get(game.game.Id) is Game;
            }
            return false;
        }

        public void Execute(object searchItem)
        {
            if (searchItem is GameSearchItem game)
            {
                SearchPlugin.Instance.PlayniteApi.Dialogs.ShowMessage($"Edit {game.game.Name}");
            }
        }
    }

    public class GameSearchSource : ISearchItemSource<string>
    {
        private static Tuple<string, string> GetAssemblyName(string name)
        {
            var sep = name.IndexOf('_');
            return new Tuple<string, string>(name.Substring(0, sep), name.Substring(sep + 1));
        }

        public IList<GameAction> GameActions { get; set; } = new List<GameAction>();

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            return null;
        }

        string quote = null;

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return Task.Run(() => 
            {
                List<ISearchItem<string>> items = new List<ISearchItem<string>>();
                if (query.Contains("quote"))
                {
                    if (quote == null)
                    {
                        using (var client = new System.Net.Http.HttpClient())
                        {
                            client.GetStringAsync("https://zenquotes.io/api/today").ContinueWith(t => { 
                                dynamic json = Newtonsoft.Json.Linq.JArray.Parse(t.Result);
                                quote = (string)json[0]["q"] + " - " + (string)json[0]["a"];
                                t.Dispose();
                            }, TaskContinuationOptions.OnlyOnRanToCompletion).Wait(10000);
                        }
                    }
                    items.Add(new CommandItem("Quote of the Day", () => Process.Start("https://zenquotes.io"), quote, "Go to Url") 
                    { TopRight = "Inspirational quotes provided by https://zenquotes.io/ ZenQuotes API", IconChar = ''});

                    string random = null;
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.GetStringAsync("https://zenquotes.io/api/random").ContinueWith(t => {
                            dynamic json = Newtonsoft.Json.Linq.JArray.Parse(t.Result);
                            random = (string)json[0]["q"] + " - " + (string)json[0]["a"];
                            t.Dispose();
                        }, TaskContinuationOptions.OnlyOnRanToCompletion).Wait(10000);
                    }
                    if (random is string)
                    {
                        items.Add(new CommandItem("Random Quote", () => Process.Start("https://zenquotes.io"), random, "Go to Url")
                        { TopRight = "Inspirational quotes provided by https://zenquotes.io/ ZenQuotes API", IconChar = '' });
                    }
                }
                return items.AsEnumerable();
            });
        }

        public IEnumerable<ISearchItem<string>> GetItems()
        {
            GameActions.Clear();
            if (SearchPlugin.Instance.Settings.EnableExternalGameActions)
            {
                foreach (var item in QuickSearchSDK.gameActions)
                {
                    var extracted = GetAssemblyName(item.Key);
                    var assembly = extracted.Item1;
                    var name = extracted.Item2;
                    if (SearchPlugin.Instance.Settings.EnabledAssemblies[assembly].Actions)
                    {
                        GameActions.Add(new GameAction() { Name = name, Action = item.Value });
                    }
                }
            }

            var items = SearchPlugin.Instance.PlayniteApi.Database.Games
                .Where(g => !g.Hidden || !SearchPlugin.Instance.Settings.IgnoreHiddenGames)
                .Where(g => (!g.TagIds?.Contains(SearchPlugin.Instance.Settings.IgnoreTagId)) ?? true)
                .Select(g =>
            {
                var item = new GameSearchItem(g);
                if (SearchPlugin.Instance.Settings.EnableExternalGameActions)
                {
                    foreach (var action in GameActions)
                    {
                        item.Actions.Add(action);
                    }
                }
                return item;
            }).Concat(new ISearchItem<string>[] {
                new CommandItem(Application.Current.FindResource("LOCQuickFilterFavorites") as string,
                    new SubItemsAction() { CloseAfterExecute = false, Name = ResourceProvider.GetString("LOC_QS_ShowAction"), SubItemSource = new FavoritesSource()},
                    "Favorites") {IconChar = QuickSearch.IconChars.Star },
                new CommandItem(Application.Current.FindResource("LOCQuickFilterRecentlyPlayed") as string,
                        new SubItemsAction() { CloseAfterExecute = false, Name = ResourceProvider.GetString("LOC_QS_ShowAction"), SubItemSource = new RecentlyPlayedSource()},
                        "Recently Played") {IconChar = '\uEEDC' }});
            if (SearchPlugin.Instance.Settings.EnableFilterSubSources)
            {
                items = items.Concat(GetFilterItems(new GameFilter()));
            }
            return items;
        }

        public static IEnumerable<ISearchItem<string>> GetFilterItems(GameFilter previousFilter, GameFilter.Mode mode = GameFilter.Mode.Or, string seperator = null, string previousName = null)
        {
            string prefix = string.Empty;
            if (!string.IsNullOrEmpty(previousName))
            {
                prefix = $"{previousName}{seperator}";
            }
            var games = SearchPlugin.Instance.PlayniteApi.Database.Games.AsParallel();
            IEnumerable<ISearchItem<string>> items = SearchPlugin.Instance.UsedSources
                //.Where(s => previousFilter.IsEmpty || games.AsParallel().Any(previousFilter.CopyAndAdd(g => g.Source == s, mode).Eval))
                .Select(s =>
                {
                    var source = s;
                    GameFilter filter = new GameFilter(g => g.Source == source, previousFilter, mode);
                    var item = new FilterItem(s.Name, prefix, ResourceProvider.GetString("LOC_QS_Library"), filter, seperator);
                    return item;
                });
            items = items.Concat(SearchPlugin.Instance.UsedPlatforms
                //.Where(p => previousFilter.IsEmpty || games.Any(previousFilter.CopyAndAdd(g => g.Platforms?.FirstOrDefault() == p, mode).Eval))
                .Select(p =>
                {
                    var platform = p;
                    GameFilter filter = new GameFilter(g => g.Platforms?.FirstOrDefault() == platform, previousFilter, mode);
                    var item = new FilterItem(p.Name, prefix, ResourceProvider.GetString("LOC_QS_Platform"), filter, seperator);
                    return item;
                }));
            items = items.Concat(SearchPlugin.Instance.UsedGenres
                //.Where(gr => previousFilter.IsEmpty || games.Any(previousFilter.CopyAndAdd(g => g.Genres?.Contains(gr) ?? false, mode).Eval))
                .Select(gr =>
                {
                    GameFilter filter = new GameFilter(g => g.Genres?.Contains(gr) ?? false, previousFilter, mode);
                    var item = new FilterItem(gr.Name, prefix, ResourceProvider.GetString("LOC_QS_Genre"), filter, seperator);
                    return item;
                }));
            items = items.Concat(SearchPlugin.Instance.UsedCategories
                //.Where(c => previousFilter.IsEmpty || games.Any(previousFilter.CopyAndAdd(g => g.Categories?.Contains(c) ?? false, mode).Eval))
                .Select(c =>
                {
                    GameFilter filter = new GameFilter(g => g.Categories?.Contains(c) ?? false, previousFilter, mode);
                    var item = new FilterItem(c.Name, prefix, ResourceProvider.GetString("LOC_QS_Category"), filter, seperator);
                    return item;
                }));
            items = items.Concat(SearchPlugin.Instance.UsedCompanies
                //.Where(c => previousFilter.IsEmpty || games.Any(previousFilter.CopyAndAdd(g => (g.PublisherIds?.Contains(c.Id) ?? false) || (g.DeveloperIds?.Contains(c.Id) ?? false), mode).Eval))
                .Select(c =>
                {
                    GameFilter filter = new GameFilter(g => (g.PublisherIds?.Contains(c.Id) ?? false) || (g.DeveloperIds?.Contains(c.Id) ?? false), previousFilter, mode);
                    var item = new FilterItem(c.Name, prefix, ResourceProvider.GetString("LOC_QS_Company"), filter, seperator);
                    return item;
                }));
            items = items.Concat((new[] { true, false })
                //.Where(c => previousFilter.IsEmpty || games.Any(previousFilter.CopyAndAdd(g => g.IsInstalled == c, mode).Eval))
                .Select(c =>
                {
                    var name = c ? ResourceProvider.GetString("LOC_QS_Installed") : ResourceProvider.GetString("LOC_QS_Uninstalled");
                    GameFilter filter = new GameFilter(g => g.IsInstalled == c, previousFilter, mode);
                    var item = new FilterItem(name, prefix, ResourceProvider.GetString("LOC_QS_InstallationStatus"), filter, seperator);
                    return item;
                }));
            items = items.Concat(SearchPlugin.Instance.UsedTags
                //.Where(c => previousFilter.IsEmpty || games.Any(previousFilter.CopyAndAdd(g => g.TagIds?.Contains(c.Id) ?? false, mode).Eval))
                .Select(c =>
                {
                    GameFilter filter = new GameFilter(g => g.TagIds?.Contains(c.Id) ?? false, previousFilter, mode);
                    var item = new FilterItem(c.Name, prefix, ResourceProvider.GetString("LOCTagLabel"), filter, seperator);
                    return item;
                }));
            items = items.Concat(SearchPlugin.Instance.UsedFeatures
                //.Where(c => previousFilter.IsEmpty || games.Any(previousFilter.CopyAndAdd(g => g.FeatureIds?.Contains(c.Id) ?? false, mode).Eval))
                .Select(c =>
                {
                    GameFilter filter = new GameFilter(g => g.FeatureIds?.Contains(c.Id) ?? false, previousFilter, mode);
                    var item = new FilterItem(c.Name, prefix, ResourceProvider.GetString("LOCFeatureLabel"), filter, seperator);
                    return item;
                }));
            return items.AsParallel();
        }
    }

    public class FavoritesSource : ISearchSubItemSource<string>
    {
        private static Tuple<string, string> GetAssemblyName(string name)
        {
            var sep = name.IndexOf('_');
            return new Tuple<string, string>(name.Substring(0, sep), name.Substring(sep + 1));
        }

        public IList<GameAction> GameActions { get; set; } = new List<GameAction>();

        public string Prefix => Application.Current.FindResource("LOCQuickFilterFavorites") as string;

        public bool DisplayAllIfQueryIsEmpty => true;

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            return null;
        }

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return null;
        }

        public IEnumerable<ISearchItem<string>> GetItems()
        {
            GameActions.Clear();
            if (SearchPlugin.Instance.Settings.EnableExternalGameActions)
            {
                foreach (var item in QuickSearchSDK.gameActions)
                {
                    var extracted = GetAssemblyName(item.Key);
                    var assembly = extracted.Item1;
                    var name = extracted.Item2;
                    if (SearchPlugin.Instance.Settings.EnabledAssemblies[assembly].Actions)
                    {
                        GameActions.Add(new GameAction() { Name = name, Action = item.Value });
                    }
                }
            }
            return SearchPlugin.Instance.PlayniteApi.Database.Games.Where(g => g.Favorite).Select(g =>
            {
                var item = new GameSearchItem(g);
                if (SearchPlugin.Instance.Settings.EnableExternalGameActions)
                {
                    foreach (var action in GameActions)
                    {
                        item.Actions.Add(action);
                    }
                }
                return item;
            });
        }
    }

    public class RecentlyPlayedSource : ISearchSubItemSource<string>
    {
        private static Tuple<string, string> GetAssemblyName(string name)
        {
            var sep = name.IndexOf('_');
            return new Tuple<string, string>(name.Substring(0, sep), name.Substring(sep + 1));
        }

        public IList<GameAction> GameActions { get; set; } = new List<GameAction>();

        public string Prefix => Application.Current.FindResource("LOCQuickFilterRecentlyPlayed") as string;

        public bool DisplayAllIfQueryIsEmpty => true;

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            return null;
        }

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return null;
        }

        public IEnumerable<ISearchItem<string>> GetItems()
        {
            GameActions.Clear();
            if (SearchPlugin.Instance.Settings.EnableExternalGameActions)
            {
                foreach (var item in QuickSearchSDK.gameActions)
                {
                    var extracted = GetAssemblyName(item.Key);
                    var assembly = extracted.Item1;
                    var name = extracted.Item2;
                    if (SearchPlugin.Instance.Settings.EnabledAssemblies[assembly].Actions)
                    {
                        GameActions.Add(new GameAction() { Name = name, Action = item.Value });
                    }
                }
            }
            return SearchPlugin.Instance.PlayniteApi.Database.Games
                .Where(g => (g.LastActivity ?? DateTime.MinValue).AddDays(7) >= DateTime.Now)
                .OrderByDescending(g => g.LastActivity ?? DateTime.MinValue)
                .Select(g =>
                {
                    var item = new GameSearchItem(g);
                    if (SearchPlugin.Instance.Settings.EnableExternalGameActions)
                    {
                        foreach (var action in GameActions)
                        {
                            item.Actions.Add(action);
                        }
                    }
                    return item;
                });
        }
    }

    public class FilteredGameSource : ISearchSubItemSource<string>
    {
        private static Tuple<string, string> GetAssemblyName(string name)
        {
            var sep = name.IndexOf('_');
            return new Tuple<string, string>(name.Substring(0, sep), name.Substring(sep + 1));
        }

        public FilteredGameSource(GameFilter filter, string filterName) 
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (filterName == null) throw new ArgumentNullException(nameof(filterName));
            this.filter = filter;
            this.Prefix = filterName;
        }

        private GameFilter filter;

        public IList<GameAction> GameActions { get; set; } = new List<GameAction>();

        public string Prefix { get; set; }

        public bool DisplayAllIfQueryIsEmpty => true;

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            GameFilter.Mode mode = GameFilter.Mode.None;
            string sep;
            if (query.Trim().StartsWith(","))
            {
                mode = GameFilter.Mode.Or;
                sep = ",";
            } else if (query.Trim().StartsWith("&"))
            {
                mode = GameFilter.Mode.And;
                sep = "&";
            } else
            {
                return null;
            }
            var items = GameSearchSource.GetFilterItems(filter, mode, sep, Prefix);
            return items;
        }

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return null;
        }

        public IEnumerable<ISearchItem<string>> GetItems()
        {
            GameActions.Clear();
            if (SearchPlugin.Instance.Settings.EnableExternalGameActions)
            {
                foreach (var item in QuickSearchSDK.gameActions)
                {
                    var extracted = GetAssemblyName(item.Key);
                    var assembly = extracted.Item1;
                    var name = extracted.Item2;
                    if (SearchPlugin.Instance.Settings.EnabledAssemblies[assembly].Actions)
                    {
                        GameActions.Add(new GameAction() { Name = name, Action = item.Value });
                    }
                }
            }
            return SearchPlugin.Instance.PlayniteApi.Database.Games
                .Where(g => filter.Eval(g))
                .OrderBy(g => g.Name)
                .Select(g =>
                {
                    var item = new GameSearchItem(g);
                    if (SearchPlugin.Instance.Settings.EnableExternalGameActions)
                    {
                        foreach (var action in GameActions)
                        {
                            item.Actions.Add(action);
                        }
                    }
                    return item;
                });
        }
    }

    public class FilterItem : ISearchItem<string>
    {
        public FilterItem(string name, string prefix, string kind, GameFilter filter, string seperator)
        {
            this.name = name;
            this.prefix = prefix;
            this.filter = filter;
            this.kind = kind;
            this.seperator = seperator;
        }

        private string name;
        private string prefix;
        private string kind;
        private string seperator;
        internal GameFilter filter;

        public IList<ISearchKey<string>> Keys
        {
            get
            {
                var keys = new List<ISearchKey<string>>();
                if (!string.IsNullOrEmpty(seperator))
                {
                    keys.Add(new CommandItemKey { Key = seperator + name });
                    //keys.Add(new CommandItemKey { Key = $"{seperator} {name}" });
                    //keys.Add(new CommandItemKey { Key = seperator });
                }
                keys.Add(new CommandItemKey { Key = name });
                return keys;
            } 
        }

        public IList<ISearchAction<string>> Actions 
            => new [] { new SubItemsAction() { CloseAfterExecute = false, Name = ResourceProvider.GetString("LOC_QS_ApplyAction"), SubItemSource = new FilteredGameSource(filter, $"{prefix}{name}") }};

        public ScoreMode ScoreMode => ScoreMode.WeightedMaxScore;

        public Uri Icon => null;

        public string TopLeft => name;

        public string TopRight => SearchPlugin.Instance.PlayniteApi.Database.Games.Count(filter.Eval).ToString();

        public string BottomLeft => string.Format(ResourceProvider.GetString("LOC_QS_XFilter"), kind);

        public string BottomCenter => null;

        public string BottomRight => kind;

        public char? IconChar => '\uEF29';

        public FrameworkElement DetailsView => null;
    }

    public class GameSearchItem : ISearchItem<string>
    {
        public GameSearchItem()
        {

        }

        public GameSearchItem(Game game)
        {
            this.game = game;
            keys = new List<ISearchKey<string>>();
            if (!string.IsNullOrEmpty(game.Name))
            {
                keys.Add(new NameKey(game));
                if (SearchPlugin.Instance.Settings.MinAcronmLength > 0)
                {
                    var ac = new AcronymKey(game);
                    if (ac.Key.Length >= SearchPlugin.Instance.Settings.MinAcronmLength)
                    {
                        keys.Add(ac);
                    }
                }
            }
            if (!string.IsNullOrEmpty(game.Name) && CleanNameKey.regex.IsMatch(game.Name))
                keys.Add(new CleanNameKey(game));
            if (game.Roms?.Count > 0)
                keys.Add(new RomKey { game = game });
        }

        public Game game;
        public Game Game => game;

        internal static string[] ImageExtensions = new string[] { ".png", ".jpg", ".jpeg", ".ico", ".bmp", ".tiff", ".gif" };

        private readonly IList<ISearchKey<string>> keys;
        public IList<ISearchKey<string>> Keys => keys;

        public IList<ISearchAction<string>> Actions
        {
            get
            {

                var launchAciton = new ContextAction
                {
                    Name = game.IsInstalled ?
                    ResourceProvider.GetString("LOC_QS_PlayAction") :
                    ResourceProvider.GetString("LOC_QS_InstallAction")
                };

                var showAction = new CommandAction
                {
                    Name = ResourceProvider.GetString("LOC_QS_ShowAction"),
                    Action = () => 
                    {
                        SearchPlugin.Instance.PlayniteApi.MainView.SwitchToLibraryView();
                        SearchPlugin.Instance.PlayniteApi.MainView.SelectGame(game.Id);
                    }
                };

                var actions = new List<ISearchAction<string>> { launchAciton, showAction };

                if (SearchPlugin.Instance.Settings.SwapGameActions)
                {
                    var tmp = actions[0];
                    actions[0] = actions[1];
                    actions[1] = tmp;
                }

                if (game.IsInstalled)
                {
                    //var uninstallAction = new GameAction() 
                    //{ 
                    //    Name = Application.Current.FindResource("LOCUninstallGame") as string,
                    //    Action = g => 
                    //}
                }
                return actions;
            }
        }

        internal bool IsURL(string path)
        {
            return path.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        }

        public Uri Icon
        {
            get
            {
                Uri icon = null;
                Uri cover = null;
                CheckImagePath(game.Icon, ref icon);
                CheckImagePath(game.CoverImage, ref cover);

                if (cover is Uri && (SearchPlugin.Instance.Settings.PreferCoverArt || icon == null))
                {
                    return cover;
                }

                if (icon is Uri)
                {
                    return icon;
                }

                return (Application.Current.FindResource("TrayIcon") as BitmapImage).UriSource;
            }
        }

        private bool CheckImagePath(string path, ref Uri uri)
        {
            string fullPath = path;
            if (!string.IsNullOrEmpty(fullPath))
            {
                if (!IsURL(fullPath) && !Path.IsPathRooted(fullPath))
                {
                    fullPath = SearchPlugin.Instance.PlayniteApi.Database.GetFullFilePath(path);
                }

                if (!Path.GetExtension(Path.GetFileName(fullPath)).ToLower().EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.TryCreate(fullPath, UriKind.RelativeOrAbsolute, out uri);
                }
            }
            return false;
        }
#if DEBUG
        public string TopLeft => game.Name + $" ({keys.OfType<AcronymKey>().FirstOrDefault().Key})";
#else
        public string TopLeft => game.Name;
#endif

        public string TopRight => (game.Source != null ? (game.Source.Name + " - ") : "") + (game.IsInstalled ?
            ResourceProvider.GetString("LOCGameIsInstalledTitle") :
            ResourceProvider.GetString("LOCGameIsUnInstalledTitle"));

        public string BottomLeft => game.Platforms?.FirstOrDefault()?.ToString();

        public string BottomCenter => Path.GetFileNameWithoutExtension(
            SearchPlugin.Instance.PlayniteApi.ExpandGameVariables(game, game.Roms?.FirstOrDefault()?.Name ?? string.Empty));

        public string BottomRight
        {
            get
            {
                var time = TimeSpan.FromSeconds(game.Playtime);
                int hours = (int)Math.Truncate(time.TotalHours);
                int minutes = (int)((time.TotalHours - hours) * 60);
                return ResourceProvider.GetString("LOCTimePlayed") + ": " + $"{hours}h{minutes}min";
            }
        }

        public ScoreMode ScoreMode => ScoreMode.WeightedMaxScore;

        public char? IconChar => null;

        private GameDetailsView details = null;

        public FrameworkElement DetailsView
        {
            get
            {
                if (details == null)
                {
                    details = new GameDetailsView() { DataContext = null };
                }
                details.DataContext = game;
                return details;
            } 
        }
    }
}
