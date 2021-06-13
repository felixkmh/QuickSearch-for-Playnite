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
        public string Key => game.GameImagePath;

        public float Weight => string.IsNullOrEmpty(game.GameImagePath)? 0f : 1f;
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

        public bool DependsOnQuery => false;

        public IEnumerable<ISearchItem<string>> GetItems(string query)
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
            return SearchPlugin.Instance.PlayniteApi.Database.Games.Select(g =>
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
                    new SubItemsAction() { CloseAfterExecute = false, Name = "Show", SubItemSource = new FavoritesSource()},
                    "Favorites") {IconChar = QuickSearch.IconChars.Star } });
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
    }

    public class FavoritesSource : ISearchSubItemSource<string>
    {
        private static Tuple<string, string> GetAssemblyName(string name)
        {
            var sep = name.IndexOf('_');
            return new Tuple<string, string>(name.Substring(0, sep), name.Substring(sep + 1));
        }

        public IList<GameAction> GameActions { get; set; } = new List<GameAction>();

        public bool DependsOnQuery => false;

        public string Prefix => Application.Current.FindResource("LOCQuickFilterFavorites") as string;

        public bool DisplayAllIfQueryIsEmpty => true;

        public IEnumerable<ISearchItem<string>> GetItems(string query)
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

        public Task<IEnumerable<ISearchItem<string>>> GetItemsTask(string query, IReadOnlyList<Candidate> addedItems)
        {
            return null;
        }
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
            if (!string.IsNullOrEmpty(game.GameImagePath))
                keys.Add(new RomKey { game = game });
        }

        public Game game;

        private readonly IList<ISearchKey<string>> keys;
        public IList<ISearchKey<string>> Keys => keys;

        public IList<ISearchAction<string>> Actions
        {
            get
            {

                var launchAciton = new ContextAction
                {
                    Name = game.IsInstalled ?
                    Application.Current.FindResource("LOCPlayGame") as string :
                    Application.Current.FindResource("LOCInstallGame") as string
                };

                var actions = new List<ISearchAction<string>> { launchAciton };

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
                if (!string.IsNullOrEmpty(game.Icon)) {
                    string path;
                    if (IsURL(game.Icon) || Path.IsPathRooted(game.Icon))
                    {
                        path = game.Icon;
                    } else
                    {
                        path = SearchPlugin.Instance.PlayniteApi.Database.GetFullFilePath(game.Icon);
                    }
                    if (File.Exists(path))
                    {
                        if (Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out var uri)) 
                        {
                            return uri;
                        }
                    }
                }
                return (Application.Current.FindResource("TrayIcon") as BitmapImage).UriSource;
            }
        }

        public string TopLeft => game.Name;

        public string TopRight => (game.Source != null ? (game.Source.Name + " - "):"") + (game.IsInstalled ? 
            Application.Current.FindResource("LOCGameIsInstalledTitle") as string  : 
            Application.Current.FindResource("LOCGameIsUnInstalledTitle") as string);

        public string BottomLeft => game.Platform?.ToString();

        public string BottomCenter => Path.GetFileNameWithoutExtension(
            SearchPlugin.Instance.PlayniteApi.ExpandGameVariables(game, game.GameImagePath??string.Empty));

        public string BottomRight
        {
            get
            {
                var time = TimeSpan.FromSeconds(game.Playtime);
                int hours = (int)Math.Truncate(time.TotalHours);
                int minutes = (int)((time.TotalHours - hours) * 60);
                return Application.Current.FindResource("LOCTimePlayed") as string + ": " + $"{hours}h{minutes}min";
            }
        }

        public ScoreMode ScoreMode => ScoreMode.WeightedMaxScore;

        public char? IconChar => null;
    }
}
