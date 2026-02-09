using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace MermaidEditor;

public enum AppTheme
{
    Dark,
    Light,
    Twilight
}

public static class ThemeManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MermaidEditor", "theme.json");
    
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
    
    public static event Action? ThemeChanged;
    
    public static void LoadTheme()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<ThemeSettings>(json);
                if (settings != null && Enum.TryParse<AppTheme>(settings.Theme, out var theme))
                {
                    ApplyTheme(theme, false);
                    return;
                }
            }
        }
        catch
        {
            // Use default theme if loading fails
        }
        
        ApplyTheme(AppTheme.Dark, false);
    }
    
    public static void SaveTheme()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var settings = new ThemeSettings { Theme = CurrentTheme.ToString() };
            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    public static void ApplyTheme(AppTheme theme, bool save = true)
    {
        CurrentTheme = theme;
        
        var colors = GetThemeColors(theme);
        var resources = System.Windows.Application.Current.Resources;
        
        // Update color resources
        resources["ThemeBackgroundColor"] = colors.Background;
        resources["ThemeMenuBackgroundColor"] = colors.MenuBackground;
        resources["ThemeToolbarBackgroundColor"] = colors.ToolbarBackground;
        resources["ThemeBorderColor"] = colors.Border;
        resources["ThemeForegroundColor"] = colors.Foreground;
        resources["ThemeAccentColor"] = colors.Accent;
        resources["ThemePurpleAccentColor"] = colors.PurpleAccent;
        resources["ThemeHoverColor"] = colors.Hover;
        resources["ThemeSelectedColor"] = colors.Selected;
        resources["ThemeDisabledForegroundColor"] = colors.DisabledForeground;
        resources["ThemeEditorBackgroundColor"] = colors.EditorBackground;
        resources["ThemeEditorForegroundColor"] = colors.EditorForeground;
        resources["ThemeLineNumberColor"] = colors.LineNumber;
        resources["ThemeSelectionColor"] = colors.Selection;
        resources["ThemeStatusBarColor"] = colors.StatusBar;
        resources["ThemeSliderTrackColor"] = colors.SliderTrack;
        resources["ThemeSliderThumbColor"] = colors.SliderThumb;
        resources["ThemeSliderThumbHoverColor"] = colors.SliderThumbHover;
        resources["ThemeTabSelectedBackgroundColor"] = colors.TabSelectedBackground;
        
        // Update brush resources directly (required for DynamicResource to work properly)
        resources["ThemeBackgroundBrush"] = new SolidColorBrush(colors.Background);
        resources["ThemeMenuBackgroundBrush"] = new SolidColorBrush(colors.MenuBackground);
        resources["ThemeToolbarBackgroundBrush"] = new SolidColorBrush(colors.ToolbarBackground);
        resources["ThemeBorderBrush"] = new SolidColorBrush(colors.Border);
        resources["ThemeForegroundBrush"] = new SolidColorBrush(colors.Foreground);
        resources["ThemeAccentBrush"] = new SolidColorBrush(colors.Accent);
        resources["ThemePurpleAccentBrush"] = new SolidColorBrush(colors.PurpleAccent);
        resources["ThemeHoverBrush"] = new SolidColorBrush(colors.Hover);
        resources["ThemeSelectedBrush"] = new SolidColorBrush(colors.Selected);
        resources["ThemeDisabledForegroundBrush"] = new SolidColorBrush(colors.DisabledForeground);
        resources["ThemeEditorBackgroundBrush"] = new SolidColorBrush(colors.EditorBackground);
        resources["ThemeEditorForegroundBrush"] = new SolidColorBrush(colors.EditorForeground);
        resources["ThemeLineNumberBrush"] = new SolidColorBrush(colors.LineNumber);
        resources["ThemeSelectionBrush"] = new SolidColorBrush(colors.Selection);
        resources["ThemeStatusBarBrush"] = new SolidColorBrush(colors.StatusBar);
        resources["ThemeSliderTrackBrush"] = new SolidColorBrush(colors.SliderTrack);
        resources["ThemeSliderThumbBrush"] = new SolidColorBrush(colors.SliderThumb);
        resources["ThemeSliderThumbHoverBrush"] = new SolidColorBrush(colors.SliderThumbHover);
        resources["ThemeTabSelectedBackgroundBrush"] = new SolidColorBrush(colors.TabSelectedBackground);
        
        if (save)
        {
            SaveTheme();
        }
        
        ThemeChanged?.Invoke();
    }
    
    public static ThemeColors GetThemeColors(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Light => new ThemeColors
            {
                Background = ColorFromHex("#F5F5F5"),
                MenuBackground = ColorFromHex("#F0F0F0"),
                ToolbarBackground = ColorFromHex("#E8E8E8"),
                Border = ColorFromHex("#CCCCCC"),
                Foreground = ColorFromHex("#1E1E1E"),
                Accent = ColorFromHex("#0078D4"),
                PurpleAccent = ColorFromHex("#7B68EE"),
                Hover = ColorFromHex("#E0E0E0"),
                Selected = ColorFromHex("#CCE8FF"),
                DisabledForeground = ColorFromHex("#A0A0A0"),
                EditorBackground = ColorFromHex("#FFFFFF"),
                EditorForeground = ColorFromHex("#1E1E1E"),
                LineNumber = ColorFromHex("#6E6E6E"),
                Selection = ColorFromHex("#ADD6FF"),
                StatusBar = ColorFromHex("#0078D4"),
                SliderTrack = ColorFromHex("#CCCCCC"),
                SliderThumb = ColorFromHex("#999999"),
                SliderThumbHover = ColorFromHex("#666666"),
                TabSelectedBackground = ColorFromHex("#FFFFFF")
            },
            AppTheme.Twilight => new ThemeColors
            {
                Background = ColorFromHex("#1A1A2E"),
                MenuBackground = ColorFromHex("#16213E"),
                ToolbarBackground = ColorFromHex("#1F2544"),
                Border = ColorFromHex("#3D4A6B"),
                Foreground = ColorFromHex("#E8E8E8"),
                Accent = ColorFromHex("#4A90D9"),
                PurpleAccent = ColorFromHex("#9D84E8"),
                Hover = ColorFromHex("#2D3A5C"),
                Selected = ColorFromHex("#3D5A80"),
                DisabledForeground = ColorFromHex("#6B7A99"),
                EditorBackground = ColorFromHex("#141428"),
                EditorForeground = ColorFromHex("#D4D4E8"),
                LineNumber = ColorFromHex("#6B7A99"),
                Selection = ColorFromHex("#3D5A80"),
                StatusBar = ColorFromHex("#4A90D9"),
                SliderTrack = ColorFromHex("#3D4A6B"),
                SliderThumb = ColorFromHex("#5A6A8A"),
                SliderThumbHover = ColorFromHex("#7A8AAA"),
                TabSelectedBackground = ColorFromHex("#1A1A2E")
            },
            _ => new ThemeColors // Dark theme (default)
            {
                Background = ColorFromHex("#1E1E1E"),
                MenuBackground = ColorFromHex("#1E1E1E"),
                ToolbarBackground = ColorFromHex("#2D2D30"),
                Border = ColorFromHex("#3E3E42"),
                Foreground = ColorFromHex("#F1F1F1"),
                Accent = ColorFromHex("#007ACC"),
                PurpleAccent = ColorFromHex("#9184EE"),
                Hover = ColorFromHex("#3E3E42"),
                Selected = ColorFromHex("#094771"),
                DisabledForeground = ColorFromHex("#656565"),
                EditorBackground = ColorFromHex("#1E1E1E"),
                EditorForeground = ColorFromHex("#D4D4D4"),
                LineNumber = ColorFromHex("#858585"),
                Selection = ColorFromHex("#264F78"),
                StatusBar = ColorFromHex("#007ACC"),
                SliderTrack = ColorFromHex("#4A4A4A"),
                SliderThumb = ColorFromHex("#6A6A6A"),
                SliderThumbHover = ColorFromHex("#8A8A8A"),
                TabSelectedBackground = ColorFromHex("#1E1E1E")
            }
        };
    }
    
    private static System.Windows.Media.Color ColorFromHex(string hex)
    {
        return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
    }
    
    public static bool IsDarkTheme => CurrentTheme == AppTheme.Dark || CurrentTheme == AppTheme.Twilight;
}

public class ThemeColors
{
    public System.Windows.Media.Color Background { get; set; }
    public System.Windows.Media.Color MenuBackground { get; set; }
    public System.Windows.Media.Color ToolbarBackground { get; set; }
    public System.Windows.Media.Color Border { get; set; }
    public System.Windows.Media.Color Foreground { get; set; }
    public System.Windows.Media.Color Accent { get; set; }
    public System.Windows.Media.Color PurpleAccent { get; set; }
    public System.Windows.Media.Color Hover { get; set; }
    public System.Windows.Media.Color Selected { get; set; }
    public System.Windows.Media.Color DisabledForeground { get; set; }
    public System.Windows.Media.Color EditorBackground { get; set; }
    public System.Windows.Media.Color EditorForeground { get; set; }
    public System.Windows.Media.Color LineNumber { get; set; }
    public System.Windows.Media.Color Selection { get; set; }
    public System.Windows.Media.Color StatusBar { get; set; }
    public System.Windows.Media.Color SliderTrack { get; set; }
    public System.Windows.Media.Color SliderThumb { get; set; }
    public System.Windows.Media.Color SliderThumbHover { get; set; }
    public System.Windows.Media.Color TabSelectedBackground { get; set; }
}

public class ThemeSettings
{
    public string Theme { get; set; } = "Dark";
}
