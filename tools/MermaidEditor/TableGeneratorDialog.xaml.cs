using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace MermaidEditor;

public partial class TableGeneratorDialog : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    public string GeneratedMarkdown { get; private set; } = string.Empty;

    private int _columns = 3;
    private int _rows = 3;
    private readonly List<WpfTextBox> _headerTextBoxes = new();
    private readonly List<WpfComboBox> _alignmentCombos = new();

    public TableGeneratorDialog()
    {
        InitializeComponent();
        SourceInitialized += TableGeneratorDialog_SourceInitialized;
        Loaded += TableGeneratorDialog_Loaded;
    }

    private void TableGeneratorDialog_SourceInitialized(object? sender, EventArgs e)
    {
        UpdateTitleBarTheme();
    }

    private void TableGeneratorDialog_Loaded(object sender, RoutedEventArgs e)
    {
        RebuildColumnConfig();
        UpdatePreview();
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
        return (color.B << 16) | (color.G << 8) | color.R;
    }

    private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private void Dimensions_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;

        if (int.TryParse(ColumnsTextBox.Text, out int cols) && cols >= 1 && cols <= 20)
        {
            _columns = cols;
        }
        else
        {
            return;
        }

        if (int.TryParse(RowsTextBox.Text, out int rows) && rows >= 1 && rows <= 100)
        {
            _rows = rows;
        }
        else
        {
            return;
        }

        RebuildColumnConfig();
        UpdatePreview();
    }

    private void RebuildColumnConfig()
    {
        if (ColumnConfigPanel == null) return;

        // Preserve existing header text and alignment
        var existingHeaders = new List<string>();
        var existingAlignments = new List<int>();
        for (int i = 0; i < _headerTextBoxes.Count; i++)
        {
            existingHeaders.Add(_headerTextBoxes[i].Text);
            existingAlignments.Add(_alignmentCombos[i].SelectedIndex);
        }

        ColumnConfigPanel.Children.Clear();
        _headerTextBoxes.Clear();
        _alignmentCombos.Clear();

        for (int i = 0; i < _columns; i++)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 0), Width = 120 };

            var headerLabel = new TextBlock
            {
                Text = $"Column {i + 1}",
                Foreground = (System.Windows.Media.Brush)FindResource("ThemeForegroundBrush"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var headerTextBox = new WpfTextBox
            {
                Text = i < existingHeaders.Count ? existingHeaders[i] : $"Header {i + 1}",
                Width = 120
            };
            headerTextBox.TextChanged += HeaderOrAlignment_Changed;

            var alignCombo = new WpfComboBox
            {
                Width = 120,
                Margin = new Thickness(0, 4, 0, 0),
                SelectedIndex = i < existingAlignments.Count ? existingAlignments[i] : 0
            };
            alignCombo.Items.Add(new WpfComboBoxItem { Content = "Left" });
            alignCombo.Items.Add(new WpfComboBoxItem { Content = "Center" });
            alignCombo.Items.Add(new WpfComboBoxItem { Content = "Right" });
            alignCombo.SelectionChanged += HeaderOrAlignment_Changed;

            panel.Children.Add(headerLabel);
            panel.Children.Add(headerTextBox);
            panel.Children.Add(alignCombo);

            _headerTextBoxes.Add(headerTextBox);
            _alignmentCombos.Add(alignCombo);

            ColumnConfigPanel.Children.Add(panel);
        }
    }

    private void HeaderOrAlignment_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void HeaderOrAlignment_Changed(object sender, SelectionChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (PreviewText == null || _headerTextBoxes.Count == 0) return;

        GeneratedMarkdown = BuildMarkdownTable();
        PreviewText.Text = GeneratedMarkdown;
    }

    private string BuildMarkdownTable()
    {
        var sb = new StringBuilder();

        // Header row
        sb.Append('|');
        for (int col = 0; col < _columns; col++)
        {
            var header = col < _headerTextBoxes.Count ? _headerTextBoxes[col].Text : $"Header {col + 1}";
            if (string.IsNullOrWhiteSpace(header)) header = " ";
            sb.Append($" {header} |");
        }
        sb.AppendLine();

        // Separator row with alignment
        sb.Append('|');
        for (int col = 0; col < _columns; col++)
        {
            var alignIndex = col < _alignmentCombos.Count ? _alignmentCombos[col].SelectedIndex : 0;
            var separator = alignIndex switch
            {
                1 => " :---: |",  // Center
                2 => " ---: |",   // Right
                _ => " --- |"     // Left (default)
            };
            sb.Append(separator);
        }
        sb.AppendLine();

        // Data rows
        for (int row = 0; row < _rows; row++)
        {
            sb.Append('|');
            for (int col = 0; col < _columns; col++)
            {
                sb.Append("  |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        GeneratedMarkdown = BuildMarkdownTable();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
