using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace QuickSearch
{
    /// <summary>
    /// Interaktionslogik für GameResult.xaml
    /// </summary>
    public partial class SearchResult : UserControl
    {
        public Boolean IsSelected 
        {
            get => (Boolean)GetValue(IsSelectedProperty); 
            set => SetValue(IsSelectedProperty, value);

        }

        public static readonly DependencyProperty IsSelectedProperty = 
            DependencyProperty.Register(nameof(IsSelected), typeof(Boolean), typeof(SearchResult), new PropertyMetadata(false, new PropertyChangedCallback(IsSelectedChangedCallback)));

        private static void IsSelectedChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is SearchResult result)
            {
                if ((Boolean)e.NewValue)
                    result.GameResult_Selected(result, null);
                else
                    result.GameResult_Unselected(result, null);
            }
        }

        public bool AlwaysExpand { 
            get => alwaysExpand; 
            set {
                alwaysExpand = value;
                if (value) Expand();
                else if (!IsSelected) Collapse();
            } 
        }
        private bool alwaysExpand = false;

        public SearchResult()
        {
            InitializeComponent();
            //Selected += GameResult_Selected;
            //Unselected += GameResult_Unselected;
            DataContextChanged += SearchResult_DataContextChanged;
        }

        private void SearchResult_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AlwaysExpand = SearchPlugin.Instance.settings.ExpandAllItems;
            Seperator.Height = SearchPlugin.Instance.settings.ShowSeperator ? 5 : 0;
        }

        private void GameResult_Unselected(object sender, RoutedEventArgs e)
        {
            if (SearchPlugin.Instance.settings.ExpandAllItems)
            {
                Expand();
            } else
            {
                Collapse();
            }

            cancel = true;
        }

        Task animation = Task.CompletedTask;

        bool cancel = false;

        private void GameResult_Selected(object sender, RoutedEventArgs e)
        {
            Expand();
            cancel = true;
            animation = animation.ContinueWith(t => { t.Dispose();  cancel = false; })
            .ContinueWith((t) =>
            {
                t.Dispose();
                if (SpinWait.SpinUntil(() => cancel, 1500)) return;
                double offset = 0;
                Dispatcher.Invoke(() =>
                {
                    offset = ((TextBlock)BottomTextScroller.Content).DesiredSize.Width - BottomTextScroller.ActualWidth;
                }, System.Windows.Threading.DispatcherPriority.Background);

                if (offset > 0)
                {
                    var start = DateTime.Now;
                    var elapsed = (DateTime.Now - start).TotalSeconds;
                    var duration = offset / 20.0;
                    while (elapsed < duration)
                    {
                        if (cancel)
                        {
                            return;
                        }
                        if (SpinWait.SpinUntil(() => cancel, 17)) return;
                        Dispatcher.Invoke(() =>
                        {
                            BottomTextScroller.ScrollToHorizontalOffset((elapsed / duration) * offset);
                        }, System.Windows.Threading.DispatcherPriority.Background);

                        elapsed = (DateTime.Now - start).TotalSeconds;
                    }

                    SpinWait.SpinUntil(() => cancel, 1500);
                } else
                {
                    return;
                }

            }).ContinueWith(t => { 
                t.Dispose(); 
                cancel = false;
                Dispatcher.Invoke(() =>
                {
                    BottomTextScroller.ScrollToHorizontalOffset(0);
                }, System.Windows.Threading.DispatcherPriority.Background);
            });
        }

        private void Collapse()
        {
            SelectedView.Visibility = Visibility.Collapsed;
            DefaultView.Visibility = Visibility.Visible;
        }

        private void Expand()
        {
            SelectedView.Visibility = Visibility.Visible;
            DefaultView.Visibility = Visibility.Collapsed;
        }
    }
}
