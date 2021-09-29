using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    /// Interaktionslogik für AddonDetailsView.xaml
    /// </summary>
    public partial class AddonDetailsView : UserControl
    {
        public AddonDetailsView()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var proc = Process.Start(e.Uri.AbsoluteUri);
            proc?.Close();
            proc?.Dispose();
        }

        private void Popup_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Popup popup)
            {
                popup.IsOpen = false;
            }
        }
    }
}
