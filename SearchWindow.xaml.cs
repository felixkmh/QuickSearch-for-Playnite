using DuoVia.FuzzyStrings;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading;
using System;

namespace Search
{
    /// <summary>
    /// Interaktionslogik für SearchWindow.xaml
    /// </summary>
    public partial class SearchWindow : UserControl
    {
        SearchPlugin searchPlugin;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        Task backgroundTask = Task.CompletedTask;

        public SearchWindow(SearchPlugin plugin)
        {
            InitializeComponent();
            searchPlugin = plugin;
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
                    var fuzzy = searchPlugin.PlayniteApi.Database.Games
                    .Where(g => Matching.GetScore(input, g.Name) > 0.5)
                    .OrderByDescending(g => Matching.GetScore(input, g.Name))
                    .ThenBy(g => g.Name)
                    .Take(20);
                    var results = searchPlugin.PlayniteApi.Database.Games
                    .Where(g => g.Name.ToLower().Contains(input) && input.Length > 0)
                    .OrderByDescending(g => input.LongestCommonSubsequence(g.Name.ToLower()))
                    .ThenBy(g => g.Name)
                    .Take(20)
                    .Union(fuzzy)
                    .Distinct()
                    .Take(20);
                    int count = 0;
                    foreach(var result in results)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            oldSource.Dispose();
                            return;
                        }

                        Dispatcher.Invoke(() => { 
                            if (count == 0)
                            {
                                SearchResults.Items.Clear();
                            }
                            ListBoxItem newItem = new ListBoxItem() { Content = new TextBlock() { Text = (result.IsInstalled ? "Launch " : "Install ") + result.Name + " on " + result.Source?.Name ?? "" }, Tag = result };
                            SearchResults.Items.Add(newItem);
                        });
                        count++;
                    }
                    Dispatcher.Invoke(() =>
                    {
                        if (count == 0)
                        {
                            SearchResults.Items.Clear();
                        }
                        if (SearchResults.Items.Count > 0)
                        {
                            SearchResults.SelectedIndex = 0;
                        }
                    });
                    oldSource.Dispose();
                }, tokenSource.Token);
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
                    idx = idx + count - 1;
                }
                idx = idx % count;
                SearchResults.SelectedIndex = idx;
                if (e.Key == Key.Enter || e.Key == Key.Return)
                {
                    if (SearchResults.SelectedIndex != -1)
                    {
                        var game = ((ListBoxItem)SearchResults.SelectedItem).Tag as Playnite.SDK.Models.Game;
                        searchPlugin.PlayniteApi.StartGame(game.Id);
                    }
                    searchPlugin.popup.IsOpen = false;
                }
            }
        }
    }
}
