using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
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

namespace QuickSearch.Views
{
    /// <summary>
    /// Interaktionslogik für GameDetailsView.xaml
    /// </summary>
    public partial class GameDetailsView : UserControl
    {
        public GameDetailsView()
        {
            InitializeComponent();

            DataContextChanged += GameDetailsView_DataContextChanged;
        }

        private void GameDetailsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is Game game)
            {
                var path = System.IO.Path.Combine(SearchPlugin.Instance.PlayniteApi.Paths.ConfigurationPath, "ExtraMetadata", "games", game.Id.ToString());
                if (System.IO.Directory.Exists(path))
                {
                    var files = System.IO.Directory.GetFiles(path);
                    if (files.FirstOrDefault(f => System.IO.Path.GetFileName(f).StartsWith("Logo")) is string logoPath)
                    {
                        if (Uri.TryCreate(logoPath, UriKind.RelativeOrAbsolute, out var uri))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                            bitmap.UriSource = uri;
                            bitmap.EndInit();
                            LogoImage.Source = bitmap;
                            LogoImage.Visibility = Visibility.Visible;
                            return;
                        }
                    }
                }
            }
            LogoImage.Visibility = Visibility.Collapsed;
            LogoImage.Source = null;
        }
    }
}
