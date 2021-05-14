using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace QuickSearch
{
    /// <summary>
    /// Interaktionslogik für GameResult.xaml
    /// </summary>
    public partial class GameResult : ListBoxItem
    {
        public GameResult()
        {
            InitializeComponent();
            Selected += GameResult_Selected;
            Unselected += GameResult_Unselected;
        }

        public void SetGame(Playnite.SDK.Models.Game game)
        {
            DataContext = game;
            Playtime.Text = TimeSpan.FromSeconds(game.Playtime).ToString("%h'h'%m'm'");
            if (string.IsNullOrEmpty(game.GameImagePath))
            {
                ROM.Text = string.Empty;
            } else
            {
                ROM.Text = System.IO.Path.GetFileNameWithoutExtension(game.GameImagePath);
            }
        }

        private void GameResult_Unselected(object sender, RoutedEventArgs e)
        {
            SelectedView.Visibility = Visibility.Collapsed;
            DefaultView.Visibility = Visibility.Visible;
        }

        private void GameResult_Selected(object sender, RoutedEventArgs e)
        {
            SelectedView.Visibility = Visibility.Visible;
            DefaultView.Visibility = Visibility.Collapsed;
        }
    }
}
