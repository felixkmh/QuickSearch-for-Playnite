﻿using Playnite.SDK;
using Playnite.SDK.Models;
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
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace QuickSearch.SearchItems
{
    using GameFilter = MultiFilter<Game>;

    struct NameKey : ISearchKey<string>
    {
        public Game game;
        public string Key => game.Name;

        public float Weight => 1f;
    }

    struct CleanNameKey : ISearchKey<string>
    {
        static public readonly Regex regex = new Regex("[" + Regex.Escape("{}&.,:;^°_`´~+!\"§$%&/()=?<>#|'’") + "]");
        public Game game;
        public string Key => regex.Replace(game.Name, string.Empty);

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
                SearchPlugin.Instance.PlayniteApi.StartGame(game.game.Id);
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

            var items = SearchPlugin.Instance.PlayniteApi.Database.Games.Select(g =>
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
            var games = SearchPlugin.Instance.PlayniteApi.Database.Games;
            IEnumerable<ISearchItem<string>> items = SearchPlugin.Instance.PlayniteApi.Database.Sources
                .Where(s => SearchPlugin.Instance.PlayniteApi.Database.Games.Any(previousFilter.CopyAndAdd(g => g.Source == s, mode).Eval))
                .Select(s =>
                {
                    var source = s;
                    GameFilter filter = new GameFilter(g => g.Source == source, previousFilter, mode);
                    var item = new FilterItem(s.Name, prefix, ResourceProvider.GetString("LOC_QS_Library"), filter, seperator);
                    return item;
                });
            items = items.Concat(SearchPlugin.Instance.PlayniteApi.Database.Platforms
                .Where(p => SearchPlugin.Instance.PlayniteApi.Database.Games.Any(previousFilter.CopyAndAdd(g => g.Platforms?.FirstOrDefault() == p, mode).Eval))
                .Select(p =>
                {
                    var platform = p;
                    GameFilter filter = new GameFilter(g => g.Platforms?.FirstOrDefault() == platform, previousFilter, mode);
                    var item = new FilterItem(p.Name, prefix, ResourceProvider.GetString("LOC_QS_Platform"), filter, seperator);
                    return item;
                }));
            items = items.Concat(SearchPlugin.Instance.PlayniteApi.Database.Genres
                .Where(gr => SearchPlugin.Instance.PlayniteApi.Database.Games.Any(previousFilter.CopyAndAdd(g => g.Genres?.Contains(gr) ?? false, mode).Eval))
                .Select(gr =>
                {
                    GameFilter filter = new GameFilter(g => g.Genres?.Contains(gr) ?? false, previousFilter, mode);
                    var item = new FilterItem(gr.Name, prefix, ResourceProvider.GetString("LOC_QS_Genre"), filter, seperator);
                    return item;
                }));
            items = items.Concat(SearchPlugin.Instance.PlayniteApi.Database.Categories
                .Where(c => SearchPlugin.Instance.PlayniteApi.Database.Games.Any(previousFilter.CopyAndAdd(g => g.Categories?.Contains(c) ?? false, mode).Eval))
                .Select(c =>
                {
                    GameFilter filter = new GameFilter(g => g.Categories?.Contains(c) ?? false, previousFilter, mode);
                    var item = new FilterItem(c.Name, prefix, ResourceProvider.GetString("LOC_QS_Category"), filter, seperator);
                    return item;
                }));
            items = items.Concat(SearchPlugin.Instance.PlayniteApi.Database.Companies
                .Where(c => SearchPlugin.Instance.PlayniteApi.Database.Games.Any(previousFilter.CopyAndAdd(g => (g.PublisherIds?.Contains(c.Id) ?? false) || (g.DeveloperIds?.Contains(c.Id) ?? false), mode).Eval))
                .Select(c =>
                {
                    GameFilter filter = new GameFilter(g => (g.PublisherIds?.Contains(c.Id) ?? false) || (g.DeveloperIds?.Contains(c.Id) ?? false), previousFilter, mode);
                    var item = new FilterItem(c.Name, prefix, ResourceProvider.GetString("LOC_QS_Company"), filter, seperator);
                    return item;
                }));
            items = items.Concat((new[] { true, false })
                .Where(c => SearchPlugin.Instance.PlayniteApi.Database.Games.Any(previousFilter.CopyAndAdd(g => g.IsInstalled == c, mode).Eval))
                .Select(c =>
                {
                    var name = c ? ResourceProvider.GetString("LOC_QS_Installed") : ResourceProvider.GetString("LOC_QS_Uninstalled");
                    GameFilter filter = new GameFilter(g => g.IsInstalled == c, previousFilter, mode);
                    var item = new FilterItem(name, prefix, ResourceProvider.GetString("LOC_QS_InstallationStatus"), filter, seperator);
                    return item;
                }));
            return items;
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
        private GameFilter filter;

        public IList<ISearchKey<string>> Keys { 
            get
            {
                var keys = new List<ISearchKey<string>>();
                if (!string.IsNullOrEmpty(seperator))
                {
                    keys.Add(new CommandItemKey { Key = seperator + name });
                    keys.Add(new CommandItemKey { Key = $"{seperator} {name}" });
                    keys.Add(new CommandItemKey { Key = seperator });
                }
                keys.Add(new CommandItemKey { Key = kind });
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
    }

    public class GameSearchItem : ISearchItem<string>
    {
        public GameSearchItem(Game game)
        {
            this.game = game;
            keys = new List<ISearchKey<string>>();
            if (!string.IsNullOrEmpty(game.Name))
                keys.Add(new NameKey { game = game });
            if (!string.IsNullOrEmpty(game.Name) && CleanNameKey.regex.IsMatch(game.Name))
                keys.Add(new CleanNameKey { game = game });
            if (game.Roms?.Count > 0)
                keys.Add(new RomKey { game = game });
        }

        public Game game;

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
                    Action = () => SearchPlugin.Instance.PlayniteApi.MainView.SelectGame(game.Id)
                };

                var actions = new List<ISearchAction<string>> { launchAciton, showAction };

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

                if (ImageExtensions.Contains(Path.GetExtension(Path.GetFileName(fullPath)).ToLower()))
                {
                    return Uri.TryCreate(fullPath, UriKind.RelativeOrAbsolute, out uri);
                }
            }
            return false;
        }

        public string TopLeft => game.Name;

        public string TopRight => (game.Source != null ? (game.Source.Name + " - "):"") + (game.IsInstalled ?
            ResourceProvider.GetString("LOCGameIsInstalledTitle") :
            ResourceProvider.GetString("LOCGameIsUnInstalledTitle"));

        public string BottomLeft => game.Platforms?.FirstOrDefault()?.ToString();

        public string BottomCenter => Path.GetFileNameWithoutExtension(
            SearchPlugin.Instance.PlayniteApi.ExpandGameVariables(game, game.Roms?.FirstOrDefault()?.Name??string.Empty));

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
    }
}