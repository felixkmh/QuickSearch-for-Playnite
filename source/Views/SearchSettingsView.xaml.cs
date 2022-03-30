using Playnite.SDK;
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
            if (DataContext is SearchSettings settings)
            {
                e.Handled = true;
                SetHotkey(e, ShortcutText, SetHotkeyButton, settings.SearchShortcut);
            }
        }

        private void SetHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            ShortcutText.Focusable = true;
            ShortcutText.Focus();
            SetHotkeyButton.Content = ResourceProvider.GetString("LOC_QS_SetHotkeyPromtpButton");
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

        private void SetHotkeyButtonGlobal_Click(object sender, RoutedEventArgs e)
        {
            ShortcutTextGlobal.Focusable = true;
            ShortcutTextGlobal.Focus();
            SetHotkeyButtonGlobal.Content = ResourceProvider.GetString("LOC_QS_SetHotkeyPromtpButton");
        }

        private void ShortcutTextGlobal_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is SearchSettings settings)
            {
                e.Handled = true;
                SetHotkey(e, ShortcutTextGlobal, SetHotkeyButtonGlobal, settings.SearchShortcutGlobal);
            }
        }

        private void SetHotkey(KeyEventArgs e, TextBox shortcutText, Button setHotkeyButton, SearchSettings.Hotkey searchHotkey)
        {
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

            shortcutText.Text = $"{modifiers} + {key}";

            searchHotkey.Key = key;
            searchHotkey.Modifiers = modifiers;
            shortcutText.Focusable = false;
            setHotkeyButton.Focus();
            setHotkeyButton.Content = ResourceProvider.GetString("LOC_QS_SetHotkeyButton");
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/settings/tokens/new");
        }
    }
}