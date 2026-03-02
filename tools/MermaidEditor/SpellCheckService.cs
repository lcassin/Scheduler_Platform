using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WeCantSpell.Hunspell;

namespace MermaidEditor;

/// <summary>
/// Provides spell checking functionality using Hunspell dictionaries.
/// Thread-safe: WordList can be queried concurrently.
/// </summary>
public sealed class SpellCheckService : IDisposable
{
    private WordList? _dictionary;
    private readonly HashSet<string> _customWords = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _customDictionaryPath;
    private bool _isLoaded;

    // Common programming/markup terms to always ignore
    private static readonly HashSet<string> BuiltInIgnoreWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Mermaid keywords
        "flowchart", "sequenceDiagram", "classDiagram", "stateDiagram", "erDiagram",
        "gantt", "gitGraph", "mindmap", "timeline", "sankey", "quadrantChart",
        "subgraph", "participant", "actor", "rect", "rgba", "rgb",
        "classDef", "linkStyle", "nodeShape", "arrowhead",
        "dateFormat", "axisFormat", "tickInterval",
        "autonumber", "activate", "deactivate", "loop", "alt", "opt", "par",
        "endl", "endr", "endp",
        // Markdown keywords
        "href", "src", "img", "http", "https", "www", "html", "css", "svg",
        "yaml", "json", "xml", "api", "url", "uri",
        // Common code terms
        "param", "params", "args", "argv", "argc", "bool", "int", "uint",
        "enum", "async", "await", "const", "readonly", "namespace", "typeof",
        "foreach", "struct", "inline", "stdout", "stderr", "stdin",
        "localhost", "webpack", "npm", "git", "github", "vscode",
        "todo", "fixme", "hack", "xxx", "nbsp"
    };

    // Regex to detect words that should be skipped
    private static readonly Regex UrlPattern = new(
        @"https?://\S+|www\.\S+|[\w.+-]+@[\w-]+\.[\w.-]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CodeFencePattern = new(
        @"^```",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Match words (letters, apostrophes within words)
    private static readonly Regex WordPattern = new(
        @"\b[a-zA-Z][a-zA-Z']*[a-zA-Z]\b|[a-zA-Z]",
        RegexOptions.Compiled);

    public SpellCheckService()
    {
        _customDictionaryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MermaidEditor", "custom-dictionary.txt");
    }

    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// Loads the Hunspell dictionary from embedded resources.
    /// </summary>
    public async Task LoadAsync()
    {
        if (_isLoaded) return;

        await Task.Run(() =>
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var affResourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("en_US.aff", StringComparison.OrdinalIgnoreCase));
                var dicResourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("en_US.dic", StringComparison.OrdinalIgnoreCase));

                if (affResourceName == null || dicResourceName == null)
                {
                    System.Diagnostics.Debug.WriteLine("SpellCheck: Dictionary resources not found.");
                    return;
                }

                using var affStream = assembly.GetManifestResourceStream(affResourceName)!;
                using var dicStream = assembly.GetManifestResourceStream(dicResourceName)!;

                _dictionary = WordList.CreateFromStreams(dicStream, affStream);
                _isLoaded = true;

                // Load custom words
                LoadCustomDictionary();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SpellCheck: Failed to load dictionary: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Checks if a word is spelled correctly.
    /// </summary>
    public bool Check(string word)
    {
        if (!_isLoaded || _dictionary == null) return true; // Don't flag if not loaded
        if (string.IsNullOrWhiteSpace(word)) return true;
        if (word.Length <= 1) return true;

        // Skip numbers and words with digits
        if (word.Any(char.IsDigit)) return true;

        // Skip ALL CAPS words (acronyms like HTML, CSS, API)
        if (word.All(c => char.IsUpper(c) || c == '\'')) return true;

        // Skip camelCase / PascalCase identifiers
        if (IsCamelCase(word)) return true;

        // Skip built-in ignore words
        if (BuiltInIgnoreWords.Contains(word)) return true;

        // Skip custom dictionary words
        if (_customWords.Contains(word)) return true;

        // Check with Hunspell
        return _dictionary.Check(word);
    }

    /// <summary>
    /// Gets spelling suggestions for a misspelled word.
    /// </summary>
    public IReadOnlyList<string> Suggest(string word)
    {
        if (!_isLoaded || _dictionary == null) return Array.Empty<string>();

        var suggestions = _dictionary.Suggest(word);
        return suggestions.Take(7).ToList();
    }

    /// <summary>
    /// Adds a word to the custom dictionary (persisted across sessions).
    /// </summary>
    public void AddToCustomDictionary(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;

        _customWords.Add(word);
        SaveCustomDictionary();
    }

    /// <summary>
    /// Finds all misspelled words in a line of text, returning their offsets and lengths.
    /// Skips words inside code spans, URLs, and special markup.
    /// </summary>
    public List<MisspelledWord> CheckLine(string lineText, int lineStartOffset, bool isMermaid)
    {
        var results = new List<MisspelledWord>();
        if (!_isLoaded || _dictionary == null) return results;
        if (string.IsNullOrWhiteSpace(lineText)) return results;

        // Skip entire line if it looks like a code fence or mermaid directive
        var trimmed = lineText.TrimStart();
        if (trimmed.StartsWith("```")) return results;
        if (trimmed.StartsWith("---")) return results;

        // For Mermaid files, skip lines that are purely structural
        if (isMermaid)
        {
            // Skip lines that start with mermaid keywords/directives
            if (trimmed.StartsWith("%%")) return results; // Comments
            if (trimmed.StartsWith("style ") || trimmed.StartsWith("classDef ")) return results;
            if (trimmed.StartsWith("linkStyle ")) return results;
            if (trimmed.StartsWith("click ")) return results;
        }

        // Build a set of character ranges to skip (inline code, URLs, HTML tags)
        var skipRanges = new List<(int start, int end)>();

        // Skip inline code: `...`
        for (int i = 0; i < lineText.Length; i++)
        {
            if (lineText[i] == '`')
            {
                int end = lineText.IndexOf('`', i + 1);
                if (end > i)
                {
                    skipRanges.Add((i, end));
                    i = end;
                }
            }
        }

        // Skip URLs
        foreach (Match m in UrlPattern.Matches(lineText))
        {
            skipRanges.Add((m.Index, m.Index + m.Length - 1));
        }

        // Skip HTML tags: <...>
        for (int i = 0; i < lineText.Length; i++)
        {
            if (lineText[i] == '<')
            {
                int end = lineText.IndexOf('>', i + 1);
                if (end > i)
                {
                    skipRanges.Add((i, end));
                    i = end;
                }
            }
        }

        // Skip markdown link URLs: [text](url) - skip the url part
        var linkUrlPattern = new Regex(@"\]\(([^)]+)\)");
        foreach (Match m in linkUrlPattern.Matches(lineText))
        {
            skipRanges.Add((m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length - 1));
        }

        // Find all words and check them
        foreach (Match wordMatch in WordPattern.Matches(lineText))
        {
            int wordStart = wordMatch.Index;
            int wordEnd = wordStart + wordMatch.Length - 1;

            // Skip if word falls in a skip range
            bool shouldSkip = false;
            foreach (var (start, end) in skipRanges)
            {
                if (wordStart >= start && wordEnd <= end)
                {
                    shouldSkip = true;
                    break;
                }
            }

            if (shouldSkip) continue;

            var word = wordMatch.Value;

            // Strip leading/trailing apostrophes
            word = word.Trim('\'');
            if (word.Length <= 1) continue;

            if (!Check(word))
            {
                results.Add(new MisspelledWord
                {
                    Word = word,
                    StartOffset = lineStartOffset + wordMatch.Index,
                    Length = wordMatch.Length
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Determines which line ranges are inside code blocks (fenced with ```).
    /// Returns a set of 0-based line numbers that are inside code blocks.
    /// </summary>
    public static HashSet<int> GetCodeBlockLines(string fullText)
    {
        var result = new HashSet<int>();
        var lines = fullText.Split('\n');
        bool insideCodeBlock = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("```"))
            {
                result.Add(i); // The fence line itself is also skipped
                insideCodeBlock = !insideCodeBlock;
                continue;
            }

            if (insideCodeBlock)
            {
                result.Add(i);
            }
        }

        return result;
    }

    private static bool IsCamelCase(string word)
    {
        if (word.Length < 2) return false;

        // If it has mixed case beyond first letter, treat as camelCase/PascalCase
        bool hasLower = false;
        bool hasUpperAfterLower = false;

        for (int i = 0; i < word.Length; i++)
        {
            if (char.IsLower(word[i]))
                hasLower = true;
            else if (char.IsUpper(word[i]) && hasLower)
            {
                hasUpperAfterLower = true;
                break;
            }
        }

        return hasUpperAfterLower;
    }

    private void LoadCustomDictionary()
    {
        try
        {
            if (File.Exists(_customDictionaryPath))
            {
                var words = File.ReadAllLines(_customDictionaryPath);
                foreach (var word in words)
                {
                    if (!string.IsNullOrWhiteSpace(word))
                    {
                        _customWords.Add(word.Trim());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SpellCheck: Failed to load custom dictionary: {ex.Message}");
        }
    }

    private void SaveCustomDictionary()
    {
        try
        {
            var directory = Path.GetDirectoryName(_customDictionaryPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(_customDictionaryPath, _customWords.OrderBy(w => w));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SpellCheck: Failed to save custom dictionary: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // WordList doesn't implement IDisposable, but we clean up custom state
        _customWords.Clear();
        _dictionary = null;
        _isLoaded = false;
    }
}

/// <summary>
/// Represents a misspelled word with its position in the document.
/// </summary>
public class MisspelledWord
{
    public string Word { get; set; } = "";
    public int StartOffset { get; set; }
    public int Length { get; set; }
}
