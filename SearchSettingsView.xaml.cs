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
    public partial class SearchSettingsView : UserControl
    {
        public SearchSettingsView()
        {
            InitializeComponent();
            ShortcutText.PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            // Get modifiers and key data
            var modifiers = Keyboard.Modifiers;
            var key = e.Key;

            // When Alt is pressed, SystemKey is used instead
            if (key == Key.System)
            {
                key = e.SystemKey;
            }

            // Pressing delete, backspace or escape without modifiers clears the current value
            if (modifiers == ModifierKeys.None &&
                (key == Key.Delete || key == Key.Back || key == Key.Escape))
            {
                // Hotkey = null;
                return;
            }

            // If no actual key was pressed - return
            if (key == Key.LeftCtrl ||
                key == Key.RightCtrl ||
                key == Key.LeftAlt ||
                key == Key.RightAlt ||
                key == Key.LeftShift ||
                key == Key.RightShift ||
                key == Key.LWin ||
                key == Key.RWin ||
                key == Key.Clear ||
                key == Key.OemClear ||
                key == Key.Apps)
            {
                return;
            }

            ShortcutText.Text =  $"{modifiers} + {key}";

            if (DataContext is SearchSettings settings)
            {
                settings.SearchShortcut = new SearchSettings.Hotkey(key, modifiers);
            }
            ShortcutText.Focusable = false;
            SetHotkeyButton.Focus();
            SetHotkeyButton.Content = "Set Hotkey";
        }

        private void SetHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            ShortcutText.Focusable = true;
            ShortcutText.Focus();
            SetHotkeyButton.Content = "Press Hotkey (+ modfiers) now";
        }

        private void MaxNumberResultsTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (int.TryParse(e.Text, out var val))
            {
                if (val >= 0)
                {
                    return;
                }
            }
            e.Handled = true;
        }

        private void MaxNumberResultsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(MaxNumberResultsTextBox.Text, out var val))
            {
                if (val >= 0)
                {
                    return;
                }
            }
            MaxNumberResultsTextBox.Text = "0";
            MaxNumberResultsTextBox.SelectAll();
        }
    }
}