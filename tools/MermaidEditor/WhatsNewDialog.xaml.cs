using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Markdig;

namespace MermaidEditor;

public partial class WhatsNewDialog : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    /// <summary>
    /// After the dialog closes, indicates whether the user checked "Don't show again until next update".
    /// </summary>
    public bool DontShowAgain => DontShowCheckBox.IsChecked == true;

    public WhatsNewDialog()
    {
        InitializeComponent();
        SourceInitialized += WhatsNewDialog_SourceInitialized;
        Loaded += WhatsNewDialog_Loaded;
    }

    private void WhatsNewDialog_SourceInitialized(object? sender, EventArgs e)
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
                int captionColor = (colors.Background.B << 16) | (colors.Background.G << 8) | colors.Background.R;
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
            }
        }
        catch
        {
            // Silently fail if DWM API is not available
        }
    }

    private async void WhatsNewDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Load the What's New markdown from embedded resource
            var markdown = LoadWhatsNewMarkdown();
            if (string.IsNullOrEmpty(markdown))
            {
                markdown = "# What's New\n\nNo release notes available.";
            }

            // Convert markdown to HTML using Markdig
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
            var htmlBody = Markdig.Markdown.ToHtml(markdown, pipeline);

            // Build full HTML page with theme-aware styling
            var html = BuildThemedHtml(htmlBody);

            // Initialize WebView2 and display
            await ContentWebView.EnsureCoreWebView2Async();
            ContentWebView.NavigateToString(html);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to load What's New content: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string LoadWhatsNewMarkdown()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Try to find the embedded resource
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith("WhatsNew.md", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
            }
        }

        return "";
    }

    private static string BuildThemedHtml(string htmlBody)
    {
        // Get theme colors
        var theme = ThemeManager.CurrentTheme;
        var colors = ThemeManager.GetThemeColors(theme);

        var bgColor = $"#{colors.Background.R:X2}{colors.Background.G:X2}{colors.Background.B:X2}";
        var fgColor = $"#{colors.Foreground.R:X2}{colors.Foreground.G:X2}{colors.Foreground.B:X2}";
        var accentColor = $"#{colors.Accent.R:X2}{colors.Accent.G:X2}{colors.Accent.B:X2}";
        var borderColor = $"#{colors.Border.R:X2}{colors.Border.G:X2}{colors.Border.B:X2}";
        var toolbarBg = $"#{colors.ToolbarBackground.R:X2}{colors.ToolbarBackground.G:X2}{colors.ToolbarBackground.B:X2}";

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
    body {{
        background-color: {bgColor};
        color: {fgColor};
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        font-size: 14px;
        line-height: 1.6;
        padding: 24px 32px;
        margin: 0;
    }}
    h1 {{
        color: {accentColor};
        font-size: 28px;
        font-weight: 600;
        margin-bottom: 8px;
        border-bottom: 2px solid {accentColor};
        padding-bottom: 8px;
    }}
    h2 {{
        color: {accentColor};
        font-size: 20px;
        font-weight: 600;
        margin-top: 32px;
        margin-bottom: 4px;
        border-bottom: 1px solid {borderColor};
        padding-bottom: 6px;
    }}
    h3 {{
        color: {fgColor};
        font-size: 16px;
        font-weight: 600;
        margin-top: 20px;
        margin-bottom: 8px;
    }}
    em {{
        color: {borderColor};
        font-style: italic;
    }}
    hr {{
        border: none;
        border-top: 1px solid {borderColor};
        margin: 24px 0;
    }}
    ul {{
        padding-left: 24px;
        margin: 8px 0;
    }}
    li {{
        margin: 4px 0;
        line-height: 1.5;
    }}
    strong {{
        color: {accentColor};
        font-weight: 600;
    }}
    code {{
        background-color: {toolbarBg};
        color: {accentColor};
        padding: 2px 6px;
        border-radius: 3px;
        font-family: 'Cascadia Code', 'Consolas', monospace;
        font-size: 13px;
    }}
    pre {{
        background-color: {toolbarBg};
        border: 1px solid {borderColor};
        border-radius: 4px;
        padding: 12px;
        overflow-x: auto;
    }}
    pre code {{
        padding: 0;
        background: none;
    }}
    ::-webkit-scrollbar {{
        width: 10px;
        height: 10px;
    }}
    ::-webkit-scrollbar-track {{
        background: {bgColor};
    }}
    ::-webkit-scrollbar-thumb {{
        background: {borderColor};
        border-radius: 5px;
    }}
    ::-webkit-scrollbar-thumb:hover {{
        background: {accentColor};
    }}
</style>
</head>
<body>
{htmlBody}
</body>
</html>";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
