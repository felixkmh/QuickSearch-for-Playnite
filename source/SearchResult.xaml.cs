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
            AlwaysExpand = SearchPlugin.Instance.Settings.ExpandAllItems;
            Seperator.Height = SearchPlugin.Instance.Settings.ShowSeperator ? 5 : 0;
            TopLeftCollapsed.Inlines.Clear();
            TopLeftExpanded.Inlines.Clear();
            if (e.NewValue is SearchWindow.Candidate candidate)
            {
                foreach(var run in candidate.GetFormattedRuns(candidate.Query))
                {
                    TopLeftCollapsed.Inlines.Add(new Run(run.Text) { FontWeight = run.FontWeight, TextDecorations = run.TextDecorations });
                    TopLeftExpanded.Inlines.Add(run);
                }
                candidate.PropertyChanged += Candidate_PropertyChanged;
            }
        }

        private void Candidate_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchWindow.Candidate.Query))
            {
                if (sender is SearchWindow.Candidate candidate)
                {
                    TopLeftCollapsed.Inlines.Clear();
                    TopLeftExpanded.Inlines.Clear();
                    foreach (var run in candidate.GetFormattedRuns(candidate.Query))
                    {
                        TopLeftCollapsed.Inlines.Add(new Run(run.Text) { FontWeight = run.FontWeight, TextDecorations = run.TextDecorations });
                        TopLeftExpanded.Inlines.Add(run);
                    }
                }
            }
        }

        private void GameResult_Unselected(object sender, RoutedEventArgs e)
        {
            if (SearchPlugin.Instance.Settings.ExpandAllItems)
            {
                Expand();
            } else
            {
                Collapse();
            }

            BottomTextScroller.IsAnimating = false;
        }

        private void GameResult_Selected(object sender, RoutedEventArgs e)
        {
            Expand();
            BottomTextScroller.IsAnimating = true;
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
