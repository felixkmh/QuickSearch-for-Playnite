using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        public IList<GameAction> GameActions { get; set; } = new List<GameAction>();

        public bool DependsOnQuery => false;

        public IEnumerable<ISearchItem<string>> GetItems(string query)
        {
            GameActions.Clear();
            if (SearchPlugin.Instance.settings.EnableExternalGameActions)
            {
                foreach (var item in QuickSearchSDK.gameActions)
                {
                    GameActions.Add(new GameAction() { Name = item.Key, Action = item.Value });
                }
            }
            return SearchPlugin.Instance.PlayniteApi.Database.Games.Select(g =>
            {
                var item = new GameSearchItem(g);
                if (SearchPlugin.Instance.settings.EnableExternalGameActions)
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

    public class GameSearchItem : ISearchItem<string>
    {
        public GameSearchItem(Game game)
        {
            this.game = game;
            keys = new List<ISearchKey<string>>();
            if (!string.IsNullOrEmpty(game.Name))
                keys.Add(new NameKey { game = game });
            if (!string.IsNullOrEmpty(game.GameImagePath))
                keys.Add(new RomKey { game = game });
            //if (!string.IsNullOrEmpty(game.Source?.Name))
            //    keys.Add(new SourceKey { game = game });
        }

        public Game game;

        private readonly IList<ISearchKey<string>> keys;
        public IList<ISearchKey<string>> Keys => keys;

        private IList<ISearchAction<string>> actions;
        public IList<ISearchAction<string>> Actions
        {
            get
            {
                if (actions == null)
                {
                    var action = new ContextAction();
                    action.Name = game.IsInstalled ?
                        Application.Current.FindResource("LOCPlayGame") as string :
                        Application.Current.FindResource("LOCInstallGame") as string;
                    actions = new List<ISearchAction<string>> { action };
                }
                return actions;
            }
        }

        public Uri Icon 
        {
            get
            {
                if (!string.IsNullOrEmpty(game.Icon)) 
                {
                    var path = SearchPlugin.Instance.PlayniteApi.Paths.ConfigurationPath;
                    path = Path.Combine(path, "library", "files", game.Icon);
                    if (File.Exists(path))
                    {
                        return new Uri(path);
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

        public ScoreMode ScoreMode => ScoreMode.WeightedAverage;
    }
}
