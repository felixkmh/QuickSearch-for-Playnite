using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

[assembly: InternalsVisibleTo("QuickSearch")]

namespace QuickSearch.SearchItems
{
    public class SimpleCommandItem : ISearchItem<string>
    {
        public SimpleCommandItem(string name, Action action, string descripton = null, string absoluteIconPath = null)
        {
            TopLeft = name;
            BottomLeft = descripton;

            Keys.Add(new SimpleCommandItemKey { Key = name, Weight = 0.8f });
            Keys.Add(new SimpleCommandItemKey { Key = descripton, Weight = 0.2f });

            Actions.Add(new SimpleCommandItemAction { Action = action, Name = "Execute" });

            if (!string.IsNullOrEmpty(absoluteIconPath))
            {
                if (Uri.TryCreate(absoluteIconPath, UriKind.RelativeOrAbsolute, out var uri))
                {
                    try
                    {
                        var image = new BitmapImage(uri);
                    }
                    catch (Exception e)
                    {
                        SearchPlugin.logger.Debug(e, $"Could not create command icon for {name}");
                    }
                }
            }
        }

        public IList<ISearchKey<string>> Keys { get; private set; } = new List<ISearchKey<string>>();

        public float TotalKeyWeight { get; private set; } = 0.8f;

        public IList<ISearchAction<string>> Actions { get; private set; } = new List<ISearchAction<string>>();

        public Uri Icon { get; private set; } = null;

        public string TopLeft { get; private set; } = null;

        public string TopRight { get; private set; } = null;

        public string BottomLeft { get; private set; } = null;

        public string BottomCenter { get; private set; } = null;

        public string BottomRight { get; private set; } = null;
    }

    public class SimpleCommandItemSource : ISearchItemSource<string>
    {
        public List<SimpleCommandItem> Items { get; private set; } = new List<SimpleCommandItem>();

        public IEnumerable<ISearchItem<string>> GetItems()
        {
            return Items;
        }
    }

    public class SimpleCommandItemKey : ISearchKey<string>
    {
        public string Key { get; set; }

        public float Weight { get; set; }
    }

    public class SimpleCommandItemAction : ISearchAction<string>
    {
        public string Name { get; set; }

        public Action Action { get; set; }

#pragma warning disable CS0067
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object parameter)
        {
            return Action is Action;
        }

        public void Execute(object parameter)
        {
            Action?.Invoke();
        }
    }
}
