﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        }

        private void GameResult_Selected(object sender, RoutedEventArgs e)
        {
            Expand();
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