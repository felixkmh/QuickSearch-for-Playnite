﻿using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using static QuickSearch.Matching;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Windows.Controls.Primitives;
using QuickSearch.Controls;
using QuickSearch.SearchItems;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Documents;
using QuickSearch.ViewModels;
using System.Collections.Specialized;
using PlayniteCommon;

[assembly: InternalsVisibleTo("QuickSearch")]
namespace QuickSearch
{
    /// <summary>
    /// Interaktionslogik für SearchWindow.xaml
    /// </summary>
    public partial class SearchWindow : UserControl
    {
        readonly SearchPlugin searchPlugin;
        internal LuceneSearchViewModel searchViewModel;

        internal double heightSelected = double.NaN;
        internal double heightNotSelected = double.NaN;
        internal int? maxItems = null;

        public void Reset()
        {
            heightSelected = double.NaN;
            heightNotSelected = double.NaN;
            SearchBox.Text = string.Empty;
        }

        public SearchWindow(SearchPlugin plugin)
        {
            InitializeComponent();
            searchPlugin = plugin;
        }

        public SearchWindow(SearchPlugin plugin, LuceneSearchViewModel searchViewModel) : this(plugin)
        {
            this.searchViewModel = searchViewModel;
            DataContext = searchViewModel;
            searchViewModel.SearchResults.CollectionChanged += ListDataContext_CollectionChanged;
        }

        private void ListDataContext_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is ObservableCollection<Models.Candidate> candidates)
            {
                if (candidates.Count > 0 && ActionsListBox.Visibility == Visibility.Hidden)
                {
                    ActionsListBox.Visibility = Visibility.Visible;
                    SearchResults.Visibility = Visibility.Visible;
                }
                if (candidates.Count == 0 && ActionsListBox.Visibility == Visibility.Visible)
                {
                    ActionsListBox.Visibility = Visibility.Hidden;
                    SearchResults.Visibility = Visibility.Collapsed;
                }
                UpdateListBox(candidates.Count);
            }
        }

        DispatcherTimer timer = null;

        ISearchItem<string> previouslySelected = null;

        private void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectionChanged(e.AddedItems, e.RemovedItems);
        }

        private void SelectionChanged(System.Collections.IList addedItems, System.Collections.IList removedItems)
        {
            if ((addedItems.Count > 0 && addedItems[0] is Models.Candidate candidate && candidate.Item != previouslySelected)
                || searchViewModel.SearchResults.Count == 0
                || (removedItems.Count > 0 && !searchViewModel.SearchResults.Contains(removedItems[0])))
            {
                if (timer == null)
                {
                    timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
                    timer.Interval = TimeSpan.FromSeconds(0.5);
                    timer.Tick += Timer_Tick;
                }
                timer.Stop();
                if (DetailsPopup.IsOpen)
                {
                    DetailsPopup.PopupAnimation = PopupAnimation.None;
                    DetailsPopup.IsOpen = false;
                }
                if (DetailsScrollViewer.Content != null)
                {
                    DetailsScrollViewer.Content = null;
                }
                DetailsBorder.Visibility = Visibility.Hidden;
            }
            if (addedItems.Count > 0 && addedItems[0] is Models.Candidate candidate1)
            {
                timer.Start();
                //if (ActionsListBox.Items.Count > 0)
                {
                    ActionsListBox.Dispatcher.BeginInvoke((Action<int>)SelectActionButton, DispatcherPriority.Normal, 0);
                }
                //scrollViewer?.ScrollToVerticalOffset(0);
                SearchResults.ScrollIntoView(addedItems[0]);
                previouslySelected = candidate1.Item;
            }
            if (searchViewModel.SearchResults.Count == 0)
            {
                previouslySelected = null;
            }
            SearchBox.Focus();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            timer.Stop();
            if (SearchPlugin.Instance.Settings.EnableDetailsView)
            {
                if (SearchResults.SelectedItem is Models.Candidate item)
                {
                    if (IsVisible && item.Item.DetailsView is FrameworkElement view)
                    {
                        DetailsScrollViewer.Content = view;
                        DetailsScrollViewer.ScrollToVerticalOffset(0);
                        DetailsBorder.Visibility = Visibility.Visible;
                        DetailsPopup.PopupAnimation = PopupAnimation.Fade;
                        DetailsPopup.IsOpen = true;
                    }
                }
            }
        }

        private string GetAssemblyName(string name)
        {
            var sep = name.IndexOf('_');
            return name.Substring(0, sep);
        }

        private ScrollViewer scrollViewer = null;
        private ScrollBar scrollBar = null;

        private Lazy<double> scrollbarWidth = new Lazy<double>(() => SystemParameters.VerticalScrollBarWidth);

        internal void UpdateListBox(int items, bool refresh = false)
        {
            if (refresh)
            {
                heightSelected = double.NaN;
                heightNotSelected = double.NaN;
                scrollViewer = null;
                maxItems = null;
            }
            if (scrollViewer != null && 
                !double.IsNaN(heightSelected) && 
                !double.IsNaN(heightNotSelected) && 
                maxItems != null)
            {
                var margin = SearchResults.Margin;
                if (maxItems + 1 >= items)
                {
                    if (scrollViewer.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled)
                    {
                        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    }
                }
                else if (scrollViewer.VerticalScrollBarVisibility != ScrollBarVisibility.Visible)
                {
                    scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                }
                switch (scrollViewer.VerticalScrollBarVisibility)
                {
                    case ScrollBarVisibility.Visible:
                        margin.Right = -(scrollBar?.Width ?? 0);
                        break;
                    case ScrollBarVisibility.Hidden:
                    case ScrollBarVisibility.Disabled:
                        margin.Right = 0;
                        break;
                }
                if (SearchResults.Margin.Right != margin.Right)
                {
                    SearchResults.Margin = margin;
                }
                
                return;
            }
            if (scrollViewer == null)
            {
                var childCount = VisualTreeHelper.GetChildrenCount(SearchResults);
                if (childCount > 0)
                {
                    Decorator border = VisualTreeHelper.GetChild(SearchResults, 0) as Decorator;
                    scrollViewer = border.Child as ScrollViewer;
                }
            }
            if (scrollViewer is null)
            {
                return;
            }
            scrollBar = scrollBar ?? PlayniteCommon.UI.UiHelper.FindVisualChildren<ScrollBar>(scrollViewer, "PART_VerticalScrollBar").FirstOrDefault();
            if (SearchResults.Items.Count > 1)
            {
                if (double.IsNaN(heightSelected) || double.IsNaN(heightNotSelected))
                {
                    if (SearchResults.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem first &&
                        SearchResults.ItemContainerGenerator.ContainerFromIndex(1) is ListBoxItem second)
                    {
                        if(first != null && second != null)
                        {
                            heightSelected = first.RenderSize.Height;
                            heightNotSelected = second.RenderSize.Height;
                        }
                    }

                }
                if (!double.IsNaN(heightSelected) && !double.IsNaN(heightNotSelected))
                {
                    var availableHeight = 400.0;
                    availableHeight -= heightSelected;
                    maxItems = (int)Math.Floor(availableHeight / heightNotSelected);
                    var maxHeight = heightSelected + (maxItems ?? 0) * heightNotSelected + SearchResults.Padding.Top + SearchResults.Padding.Bottom + 2;
                    if (maxItems + 1 >= items)
                    {
                        var margin = SearchResults.Margin;
                        margin.Right = 0;
                        SearchResults.Margin = margin;
                        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    }
                    else
                    {
                        var margin = SearchResults.Margin;
                        margin.Right = -(scrollBar?.Width ?? 0);
                        SearchResults.Margin = margin;
                        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                    }
                    if (SearchResults.MaxHeight != maxHeight)
                    {
                        SearchResults.MaxHeight = maxHeight;
                    }
                }
            }
            else
            {
                if (SearchResults.Items.Count > 0)
                {
                    var margin = SearchResults.Margin;
                    margin.Right = -(scrollBar?.Width ?? 0);
                    SearchResults.Margin = margin;
                    //scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                }
            }
        }

        private void ItemClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 1)
                {
                    if (sender is SearchResult searchResult)
                    {
                        SearchResults.SelectedItem = searchResult;
                    }
                }
                if (e.ClickCount == 2)
                {
                    if (sender is SearchResult searchResult)
                    {
                        SearchResults.SelectedItem = searchResult;
                        if (SearchResults.SelectedIndex != -1)
                        {
                            if (ActionsListBox.SelectedItem is ISearchAction<string> action)
                            {
                                ExecuteAction(action);
                            }
                        }
                    }
                }
            }
            SearchBox.Focus();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                int currentIdx = ActionsListBox.SelectedIndex;
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (currentIdx >= 0)
                    {
                        SelectActionButton((currentIdx + ActionsListBox.Items.Count - 1) % ActionsListBox.Items.Count);
                    }
                } else
                {
                    if (currentIdx >= 0)
                    {
                        SelectActionButton((currentIdx + 1) % ActionsListBox.Items.Count);
                    }
                }
                e.Handled = true;
            }
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
                    idx++;
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        idx = count - 1;
                    }
                    e.Handled = true;
                }
                if (e.Key == Key.Up)
                {
                    if (SearchResults.SelectedIndex == -1)
                        SearchResults.SelectedIndex = 0;
                    idx--;
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        idx = 0;
                    }
                    e.Handled = true;
                }
                if(e.IsRepeat)
                {
                    idx = Math.Min(count - 1, Math.Max(0, idx));
                } else
                {
                    idx = (idx + count) % count;
                }
                if (SearchResults.SelectedIndex != idx)
                {
                    DetailsPopup.PopupAnimation = PopupAnimation.None;
                    DetailsPopup.IsOpen = false;
                }
                SearchResults.SelectedIndex = idx;
                if (e.Key == Key.Enter || e.Key == Key.Return)
                {
                    if (SearchResults.SelectedIndex != -1)
                    {
                        if (ActionsListBox.SelectedItem is ISearchAction<string> action)
                        {
                            ExecuteAction(action);
                        }
                    }
                }
            }
        }

        private void ExecuteAction(ISearchAction<string> action)
        {
            if (DataContext is LuceneSearchViewModel model)
            {
                if (action is ISubItemsAction<string> subItemsAction)
                {
                    var source = subItemsAction.SubItemSource;
                    model.QueueIndexUpdate(new ISearchItemSource<string>[] { source }, true);
                    if (source != null)
                    {
                        string input = source.Prefix + " ";

                        SearchBox.Text = input;
                        SearchBox.CaretIndex = SearchBox.Text.Length;
                        //PlaceholderText.Text = SearchBox.Text;

                        bool displayAllIfQueryIsEmpty = source.DisplayAllIfQueryIsEmpty;
                        model.QueueSearch(input, displayAllIfQueryIsEmpty);
                    }
                }
                else if (action.CloseAfterExecute)
                {
                    DetailsPopup.IsOpen = false;
                    searchPlugin.popup.IsOpen = false;
                }
                if (SearchResults.SelectedItem is Models.Candidate item)
                {
                    if (action.CanExecute(item.Item))
                    {
                        action.Execute(item.Item);
                    }
                }
                SearchResults.Items.Refresh();
            }
        }

        private void SelectActionButton(int idx)
        {
            if (ActionsListBox.Items.Count > idx)
            {
                ActionsListBox.SelectedIndex = idx;
                ActionsListBox.ScrollIntoView(ActionsListBox.SelectedItem);
            }
        }

        private void ActionButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is ActionButton bt)
            {
                var lbi = ActionsListBox.ContainerFromElement(bt);
                if (lbi is ListBoxItem)
                {
                    SelectActionButton(ActionsListBox.ItemContainerGenerator.IndexFromContainer(lbi));
                }
            }
        }

        int lastSelectedAction = -1;

        private void ActionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActionsListBox.Items.Count > 0 && ActionsListBox.SelectedIndex == -1)
            {
                if (SearchResults.SelectedItem is Models.Candidate candidate2)
                {
                    if (candidate2.Item == previouslySelected)
                    {
                        ActionsListBox.Dispatcher.BeginInvoke((Action<int>)SelectActionButton, DispatcherPriority.Normal, lastSelectedAction);
                    }
                    else
                    {
                        ActionsListBox.Dispatcher.BeginInvoke((Action<int>)SelectActionButton, DispatcherPriority.Normal, 0);
                    }
                }
            }
            if (ActionsListBox.SelectedIndex > -1)
            {
                lastSelectedAction = ActionsListBox.SelectedIndex;
            }
        }

        private void ActionsListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Decorator border = VisualTreeHelper.GetChild(ActionsListBox, 0) as Decorator;
            ScrollViewer scrollViewer = border.Child as ScrollViewer;
            if (e.Delta > 0)
            {
                scrollViewer.LineLeft();
            }
            else
            {
                scrollViewer.LineRight();
            }
            e.Handled = true;
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ActionButton bt)
            {
                if (bt.DataContext is ISearchAction<string> action)
                {
                    ExecuteAction(action);
                    SearchBox.Focus();
                }
            }
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool visbible && visbible)
            {
                if (timer != null)
                {
                    timer.Stop();
                    timer.Start();
                }
            } 
        }
    }

    // Fix for CanExecute being called with null value
    // https://stackoverflow.com/a/24669638
    public static class ButtonHelper
    {
        public static DependencyProperty CommandParameterProperty = DependencyProperty.RegisterAttached(
            "CommandParameter",
            typeof(object),
            typeof(ButtonHelper),
            new PropertyMetadata(CommandParameter_Changed));

        private static void CommandParameter_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ButtonBase target)
            {
                target.CommandParameter = e.NewValue;
                var temp = target.Command;
                // Have to set it to null first or CanExecute won't be called.
                target.Command = null;
                target.Command = temp;
            }
        }

        public static object GetCommandParameter(ButtonBase target)
        {
            return target.GetValue(CommandParameterProperty);
        }

        public static void SetCommandParameter(ButtonBase target, object value)
        {
            target.SetValue(CommandParameterProperty, value);
        }
    }
}
