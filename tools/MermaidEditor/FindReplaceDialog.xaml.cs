using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ICSharpCode.AvalonEdit;
using Key = System.Windows.Input.Key;

namespace MermaidEditor;

public partial class FindReplaceDialog : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    private readonly TextEditor _editor;
    private int _lastSearchIndex = -1;
    private readonly bool _showReplace;
    
    public FindReplaceDialog(TextEditor editor, bool showReplace = true)
    {
        InitializeComponent();
        _editor = editor;
        _showReplace = showReplace;
        
        // Apply title bar theming
        SourceInitialized += FindReplaceDialog_SourceInitialized;
        
        // Configure dialog based on mode
        if (!showReplace)
        {
            Title = "Find";
            Height = 180;
            ReplaceLabelText.Visibility = Visibility.Collapsed;
            ReplaceTextBox.Visibility = Visibility.Collapsed;
            ReplaceButton.Visibility = Visibility.Collapsed;
            ReplaceAllButton.Visibility = Visibility.Collapsed;
        }
        
        // Pre-populate with selected text if any
        if (_editor.SelectionLength > 0 && _editor.SelectionLength < 100)
        {
            FindTextBox.Text = _editor.SelectedText;
        }
        
        FindTextBox.Focus();
        
        // Handle keyboard shortcuts
        KeyDown += FindReplaceDialog_KeyDown;
    }
    
    private void FindReplaceDialog_SourceInitialized(object? sender, EventArgs e)
    {
        UpdateTitleBarTheme();
    }

    private void UpdateTitleBarTheme()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                var isDark = ThemeManager.IsDarkTheme;
                int darkModeValue = isDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, sizeof(int));

                var colors = ThemeManager.GetThemeColors(ThemeManager.CurrentTheme);
                int captionColor = ColorToInt(colors.Background);
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
            }
        }
        catch
        {
            // Silently fail if DWM API is not available
        }
    }

    private static int ColorToInt(System.Windows.Media.Color color)
    {
        // Color format is 0x00BBGGRR (BGR, not RGB)
        return (color.B << 16) | (color.G << 8) | color.R;
    }
    
    private void FindReplaceDialog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            FindNext_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            FindNext_Click(sender, e);
            e.Handled = true;
        }
    }
    
    private void FindTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _lastSearchIndex = -1;
        StatusText.Text = "";
    }
    
    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        var searchText = FindTextBox.Text;
        if (string.IsNullOrEmpty(searchText))
        {
            StatusText.Text = "Enter text to find";
            return;
        }
        
        var text = _editor.Text;
        var comparison = MatchCaseCheckBox.IsChecked == true 
            ? StringComparison.Ordinal 
            : StringComparison.OrdinalIgnoreCase;
        
        // Start searching from current position or after last found position
        int startIndex = _lastSearchIndex >= 0 
            ? _lastSearchIndex + 1 
            : _editor.CaretOffset;
        
        if (startIndex >= text.Length)
            startIndex = 0;
        
        int foundIndex = -1;
        
        if (WholeWordCheckBox.IsChecked == true)
        {
            foundIndex = FindWholeWord(text, searchText, startIndex, comparison);
            if (foundIndex < 0 && startIndex > 0)
            {
                // Wrap around
                foundIndex = FindWholeWord(text, searchText, 0, comparison);
            }
        }
        else
        {
            foundIndex = text.IndexOf(searchText, startIndex, comparison);
            if (foundIndex < 0 && startIndex > 0)
            {
                // Wrap around
                foundIndex = text.IndexOf(searchText, 0, comparison);
            }
        }
        
        if (foundIndex >= 0)
        {
            _lastSearchIndex = foundIndex;
            _editor.Select(foundIndex, searchText.Length);
            _editor.ScrollTo(_editor.Document.GetLineByOffset(foundIndex).LineNumber, 0);
            _editor.Focus();
            StatusText.Text = "";
        }
        else
        {
            StatusText.Text = "No matches found";
            _lastSearchIndex = -1;
        }
    }
    
    private int FindWholeWord(string text, string searchText, int startIndex, StringComparison comparison)
    {
        var pattern = @"\b" + Regex.Escape(searchText) + @"\b";
        var options = comparison == StringComparison.OrdinalIgnoreCase 
            ? RegexOptions.IgnoreCase 
            : RegexOptions.None;
        
        var match = Regex.Match(text.Substring(startIndex), pattern, options);
        if (match.Success)
        {
            return startIndex + match.Index;
        }
        return -1;
    }
    
    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        var searchText = FindTextBox.Text;
        var replaceText = ReplaceTextBox.Text;
        
        if (string.IsNullOrEmpty(searchText))
        {
            StatusText.Text = "Enter text to find";
            return;
        }
        
        // If current selection matches search text, replace it
        if (_editor.SelectionLength > 0)
        {
            var comparison = MatchCaseCheckBox.IsChecked == true 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;
            
            if (string.Equals(_editor.SelectedText, searchText, comparison))
            {
                _editor.Document.Replace(_editor.SelectionStart, _editor.SelectionLength, replaceText);
                StatusText.Text = "Replaced";
            }
        }
        
        // Find next occurrence
        FindNext_Click(sender, e);
    }
    
    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        var searchText = FindTextBox.Text;
        var replaceText = ReplaceTextBox.Text;
        
        if (string.IsNullOrEmpty(searchText))
        {
            StatusText.Text = "Enter text to find";
            return;
        }
        
        var text = _editor.Text;
        int count = 0;
        
        if (WholeWordCheckBox.IsChecked == true)
        {
            var pattern = @"\b" + Regex.Escape(searchText) + @"\b";
            var options = MatchCaseCheckBox.IsChecked == true 
                ? RegexOptions.None 
                : RegexOptions.IgnoreCase;
            
            var newText = Regex.Replace(text, pattern, replaceText, options);
            count = Regex.Matches(text, pattern, options).Count;
            
            if (count > 0)
            {
                _editor.Document.Text = newText;
            }
        }
        else
        {
            var comparison = MatchCaseCheckBox.IsChecked == true 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;
            
            // Count occurrences
            int index = 0;
            while ((index = text.IndexOf(searchText, index, comparison)) >= 0)
            {
                count++;
                index += searchText.Length;
            }
            
            if (count > 0)
            {
                // Replace all
                if (MatchCaseCheckBox.IsChecked == true)
                {
                    _editor.Document.Text = text.Replace(searchText, replaceText);
                }
                else
                {
                    _editor.Document.Text = Regex.Replace(text, Regex.Escape(searchText), replaceText, RegexOptions.IgnoreCase);
                }
            }
        }
        
        StatusText.Text = count > 0 ? $"Replaced {count} occurrence(s)" : "No matches found";
        _lastSearchIndex = -1;
    }
    
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    /// <summary>
    /// Public method to trigger Find Next from external code (e.g., F3 key)
    /// </summary>
    public void TriggerFindNext()
    {
        FindNext_Click(this, new RoutedEventArgs());
    }
}
