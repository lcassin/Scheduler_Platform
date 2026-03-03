using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MermaidEditor;

public partial class SettingsDialog : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    /// <summary>
    /// True if user clicked OK, false if cancelled.
    /// </summary>
    public bool Saved { get; private set; }

    /// <summary>
    /// True if the theme was changed (so caller can re-apply).
    /// </summary>
    public bool ThemeChanged { get; private set; }

    private readonly string _originalTheme;

    public SettingsDialog()
    {
        InitializeComponent();
        _originalTheme = SettingsManager.Current.Theme;
        LoadCurrentSettings();
        Loaded += SettingsDialog_Loaded;
        SourceInitialized += SettingsDialog_SourceInitialized;
    }

    private void SettingsDialog_SourceInitialized(object? sender, EventArgs e)
    {
        UpdateTitleBarTheme();
    }

    private void UpdateTitleBarTheme()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int useDarkMode = ThemeManager.IsDarkTheme ? 1 : 0;
                DwmSetWindowAttribute(hwnd, 20, ref useDarkMode, sizeof(int));
            }
        }
        catch
        {
            // Ignore - title bar theming is optional
        }
    }

    private void SettingsDialog_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateFontPreview();
    }

    private void LoadCurrentSettings()
    {
        var settings = SettingsManager.Current;

        // Appearance
        SelectComboItemByContent(ThemeCombo, settings.Theme);
        SelectComboItemByContent(DefaultFileTypeCombo, settings.DefaultFileType);

        // Editor
        SelectComboItemByContent(FontFamilyCombo, settings.EditorFontFamily);
        FontSizeTextBox.Text = settings.EditorFontSize.ToString("F0");
        WordWrapDefaultCheck.IsChecked = settings.WordWrapDefault;
        ShowLineNumbersDefaultCheck.IsChecked = settings.ShowLineNumbersDefault;
        ShowMinimapDefaultCheck.IsChecked = settings.ShowMinimapDefault;
        BracketMatchingDefaultCheck.IsChecked = settings.BracketMatchingDefault;
        SpellCheckEnabledCheck.IsChecked = settings.SpellCheckEnabled;

        // Auto-save
        AutoSaveEnabledCheck.IsChecked = settings.AutoSaveEnabled;
        AutoSaveIntervalTextBox.Text = settings.AutoSaveIntervalSeconds.ToString();

        // AI
        SelectComboItemByContent(AiProviderCombo, settings.AiProvider);
        AiApiKeyBox.Password = settings.AiApiKey;
        UpdateAiModelList(settings.AiProvider);
        SelectComboItemByContent(AiModelCombo, settings.AiModel);
    }

    private void SaveCurrentSettings()
    {
        var settings = SettingsManager.Current;

        // Appearance
        settings.Theme = GetComboSelectedContent(ThemeCombo) ?? "Dark";
        settings.DefaultFileType = GetComboSelectedContent(DefaultFileTypeCombo) ?? "Mermaid";

        // Editor
        settings.EditorFontFamily = GetComboSelectedContent(FontFamilyCombo) ?? "Consolas";
        if (double.TryParse(FontSizeTextBox.Text, out double fontSize) && fontSize >= 6 && fontSize <= 72)
        {
            settings.EditorFontSize = fontSize;
        }
        settings.WordWrapDefault = WordWrapDefaultCheck.IsChecked == true;
        settings.ShowLineNumbersDefault = ShowLineNumbersDefaultCheck.IsChecked == true;
        settings.ShowMinimapDefault = ShowMinimapDefaultCheck.IsChecked == true;
        settings.BracketMatchingDefault = BracketMatchingDefaultCheck.IsChecked == true;
        settings.SpellCheckEnabled = SpellCheckEnabledCheck.IsChecked == true;

        // Auto-save
        settings.AutoSaveEnabled = AutoSaveEnabledCheck.IsChecked == true;
        if (int.TryParse(AutoSaveIntervalTextBox.Text, out int interval) && interval >= 5 && interval <= 600)
        {
            settings.AutoSaveIntervalSeconds = interval;
        }

        // AI
        settings.AiProvider = GetComboSelectedContent(AiProviderCombo) ?? "OpenAI";
        settings.AiApiKey = AiApiKeyBox.Password;
        settings.AiModel = GetComboText(AiModelCombo);

        // Check if theme changed
        ThemeChanged = settings.Theme != _originalTheme;

        SettingsManager.Save();
    }

    // ── Event Handlers ──

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Live preview of theme change
        var selectedTheme = GetComboSelectedContent(ThemeCombo);
        if (selectedTheme != null && Enum.TryParse<AppTheme>(selectedTheme, out var theme))
        {
            ThemeManager.ApplyTheme(theme, save: false);
            UpdateTitleBarTheme();
        }
    }

    private void AiProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var provider = GetComboSelectedContent(AiProviderCombo);
        if (provider != null)
        {
            var currentModel = GetComboText(AiModelCombo);
            UpdateAiModelList(provider);
            // Try to keep the user's custom model if they typed one in
            SelectComboItemByContent(AiModelCombo, currentModel);
        }
    }

    private void UpdateAiModelList(string provider)
    {
        if (AiModelCombo == null) return;
        
        AiModelCombo.Items.Clear();
        
        switch (provider)
        {
            case "Anthropic":
                AiModelCombo.Items.Add(new ComboBoxItem { Content = "claude-sonnet-4-20250514" });
                AiModelCombo.Items.Add(new ComboBoxItem { Content = "claude-3-5-sonnet-20241022" });
                AiModelCombo.Items.Add(new ComboBoxItem { Content = "claude-3-5-haiku-20241022" });
                AiModelCombo.Items.Add(new ComboBoxItem { Content = "claude-3-opus-20240229" });
                break;
            default: // OpenAI
                AiModelCombo.Items.Add(new ComboBoxItem { Content = "gpt-4o" });
                AiModelCombo.Items.Add(new ComboBoxItem { Content = "gpt-4o-mini" });
                AiModelCombo.Items.Add(new ComboBoxItem { Content = "gpt-4-turbo" });
                AiModelCombo.Items.Add(new ComboBoxItem { Content = "gpt-3.5-turbo" });
                break;
        }
        
        // Default to first item
        if (AiModelCombo.Items.Count > 0)
        {
            AiModelCombo.SelectedIndex = 0;
        }
    }

    private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateFontPreview();
    }

    private void FontSizeTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateFontPreview();
    }

    private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentSettings();
        Saved = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Revert theme if it was live-previewed
        var currentTheme = GetComboSelectedContent(ThemeCombo) ?? "Dark";
        if (currentTheme != _originalTheme && Enum.TryParse<AppTheme>(_originalTheme, out var revertTheme))
        {
            ThemeManager.ApplyTheme(revertTheme, save: false);
        }

        DialogResult = false;
        Close();
    }

    private void UpdateFontPreview()
    {
        if (FontPreviewText == null) return;
        
        var fontFamily = GetComboSelectedContent(FontFamilyCombo) ?? "Consolas";
        if (double.TryParse(FontSizeTextBox?.Text, out double fontSize))
        {
            fontSize = Math.Max(6, Math.Min(fontSize, 72));
        }
        else
        {
            fontSize = 14;
        }
        
        FontPreviewText.FontFamily = new System.Windows.Media.FontFamily(fontFamily);
        FontPreviewText.FontSize = fontSize;
    }

    // ── Helper Methods ──

    private static void SelectComboItemByContent(System.Windows.Controls.ComboBox combo, string content)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Content?.ToString() == content)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        // If not found in list, for editable combos set the text directly
        if (combo.IsEditable)
        {
            combo.Text = content;
        }
        else if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private static string? GetComboSelectedContent(System.Windows.Controls.ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item)
        {
            return item.Content?.ToString();
        }
        return null;
    }

    private static string GetComboText(System.Windows.Controls.ComboBox combo)
    {
        if (combo.IsEditable && !string.IsNullOrWhiteSpace(combo.Text))
        {
            return combo.Text;
        }
        return GetComboSelectedContent(combo) ?? "";
    }
}
