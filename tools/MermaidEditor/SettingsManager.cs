using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MermaidEditor;

/// <summary>
/// Manages persistent application settings stored in %AppData%/MermaidEditor/settings.json
/// </summary>
public static class SettingsManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MermaidEditor", "settings.json");

    private static AppSettings _current = new();

    public static AppSettings Current => _current;

    /// <summary>
    /// Loads settings from disk. Falls back to defaults if file doesn't exist or is corrupt.
    /// Also migrates legacy theme.json if present.
    /// </summary>
    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    _current = loaded;
                    return;
                }
            }
        }
        catch
        {
            // Use defaults if loading fails
        }

        // Check for legacy theme.json and migrate
        MigrateLegacyTheme();
    }

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    public static void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_current, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    /// <summary>
    /// Migrates the old theme.json into the new unified settings file.
    /// </summary>
    private static void MigrateLegacyTheme()
    {
        try
        {
            var legacyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MermaidEditor", "theme.json");

            if (File.Exists(legacyPath))
            {
                var json = File.ReadAllText(legacyPath);
                var legacy = JsonSerializer.Deserialize<ThemeSettings>(json);
                if (legacy != null && Enum.TryParse<AppTheme>(legacy.Theme, out var theme))
                {
                    _current.Theme = theme.ToString();
                }

                // Don't delete legacy file in case user downgrades
            }
        }
        catch
        {
            // Ignore migration errors
        }
    }
}

/// <summary>
/// All application settings. Add new settings here as properties.
/// </summary>
public class AppSettings
{
    // ── Appearance ──
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Dark";

    // ── Editor ──
    [JsonPropertyName("editorFontFamily")]
    public string EditorFontFamily { get; set; } = "Consolas";

    [JsonPropertyName("editorFontSize")]
    public double EditorFontSize { get; set; } = 14;

    [JsonPropertyName("wordWrapDefault")]
    public bool WordWrapDefault { get; set; } = false;

    [JsonPropertyName("showLineNumbersDefault")]
    public bool ShowLineNumbersDefault { get; set; } = true;

    [JsonPropertyName("showMinimapDefault")]
    public bool ShowMinimapDefault { get; set; } = false;

    [JsonPropertyName("bracketMatchingDefault")]
    public bool BracketMatchingDefault { get; set; } = true;

    [JsonPropertyName("spellCheckEnabled")]
    public bool SpellCheckEnabled { get; set; } = true;

    // ── Auto-save ──
    [JsonPropertyName("autoSaveIntervalSeconds")]
    public int AutoSaveIntervalSeconds { get; set; } = 30;

    [JsonPropertyName("autoSaveEnabled")]
    public bool AutoSaveEnabled { get; set; } = true;

    // ── Default File Type ──
    [JsonPropertyName("defaultFileType")]
    public string DefaultFileType { get; set; } = "Mermaid"; // "Mermaid" or "Markdown"

    // ── AI Integration ──
    [JsonPropertyName("aiProvider")]
    public string AiProvider { get; set; } = "OpenAI"; // "OpenAI", "Anthropic"

    [JsonPropertyName("aiApiKey")]
    public string AiApiKey { get; set; } = "";

    [JsonPropertyName("aiModel")]
    public string AiModel { get; set; } = "gpt-4o"; // default model

    [JsonPropertyName("aiEndpoint")]
    public string AiEndpoint { get; set; } = ""; // Optional custom endpoint (e.g. Azure OpenAI)

    // ── What's New ──
    [JsonPropertyName("lastSeenWhatsNewVersion")]
    public string LastSeenWhatsNewVersion { get; set; } = "";

    [JsonPropertyName("showWhatsNewOnStartup")]
    public bool ShowWhatsNewOnStartup { get; set; } = true;
}
