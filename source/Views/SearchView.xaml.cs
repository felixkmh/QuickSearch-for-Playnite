using QuickSearch.Controls;
using QuickSearch.SearchItems;
using QuickSearch.ViewModels;
using System;
using System.Collections.Generic;
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
using System.Windows.Threading;

namespace QuickSearch.Views
{
    /// <summary>
    /// Interaktionslogik für SearchView.xaml
    /// </summary>
    public partial class SearchView : UserControl
    {
        public SearchView()
        {
            InitializeComponent();
        }

        private void SelectActionButton(int idx)
        {
            ActionsListBox.SelectedIndex = idx;
            //var containter = ActionsListBox.ItemContainerGenerator.ContainerFromIndex(idx);
            ActionsListBox.ScrollIntoView(ActionsListBox.SelectedItem);
            lastSelectedAction = idx;
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
                    //searchPlugin.popup.IsOpen = false;
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

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                int currentIdx = ActionsListBox.SelectedIndex;
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (currentIdx > -1)
                    {
                        SelectActionButton((currentIdx + ActionsListBox.Items.Count - 1) % ActionsListBox.Items.Count);
                    }
                }
                else
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
                if (e.IsRepeat)
                {
                    idx = Math.Min(count - 1, Math.Max(0, idx));
                }
                else
                {
                    idx = (idx + count) % count;
                }
                if (SearchResults.SelectedIndex != idx)
                {
                    //DetailsPopup.PopupAnimation = PopupAnimation.None;
                    //DetailsPopup.IsOpen = false;
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

        private void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                SelectionChanged(listBox, e.AddedItems, e.RemovedItems);
            }
        }

        private void SelectionChanged(ListBox sender, System.Collections.IList addedItems, System.Collections.IList removedItems)
        {

            if (addedItems.Count > 0 && addedItems[0] is Models.Candidate candidate1)
            {
                if (ActionsListBox.Items.Count > 0)
                {
                    ActionsListBox.Dispatcher.BeginInvoke((Action<int>)SelectActionButton, DispatcherPriority.Normal, 0);
                }
                SearchResults.ScrollIntoView(addedItems[0]);
                lastSelected = candidate1.Item;
            } else
            {
                lastSelected = null;
            }
            SearchBox.Focus();
        }

        ISearchItem<string> lastSelected = null;
        int lastSelectedAction = -1;

        private void ActionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActionsListBox.Items.Count > 0 && ActionsListBox.SelectedIndex == -1)
            {
                if (SearchResults.SelectedItem is Models.Candidate candidate2)
                {
                    if (candidate2.Item == lastSelected)
                    {
                        ActionsListBox.Dispatcher.BeginInvoke((Action<int>)SelectActionButton, DispatcherPriority.Normal, lastSelectedAction);
                    } else
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

        private void SearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is LuceneSearchViewModel viewModel)
            {
                viewModel.OpenSearch();
            }
        }

        private void SearchBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is LuceneSearchViewModel viewModel)
            {
                viewModel.CloseSearch();
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
