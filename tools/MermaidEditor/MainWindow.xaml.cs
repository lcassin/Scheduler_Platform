using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System.Xml;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;
using Bold = DocumentFormat.OpenXml.Wordprocessing.Bold;
using Italic = DocumentFormat.OpenXml.Wordprocessing.Italic;
using Drawing = DocumentFormat.OpenXml.Wordprocessing.Drawing;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MermaidEditor;

public enum RenderMode
{
    Mermaid,
    Markdown
}

public enum VisualEditorMode
{
    Text,
    Visual,
    Split
}

public partial class MainWindow : Window
{
    // P/Invoke for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    
    // Multi-document support
    private List<DocumentModel> _openDocuments = new();
    private DocumentModel? _activeDocument;
    private bool _isSwitchingDocuments;
    
    // These fields are updated when switching documents for backward compatibility
    private string? _currentFilePath;
    private bool _isDirty;
    private double _currentZoom = 1.0;
    private readonly DispatcherTimer _renderTimer;
    private bool _webViewInitialized;
    private CompletionWindow? _completionWindow;
    private RenderMode _currentRenderMode = RenderMode.Mermaid;
    private TaskCompletionSource<string>? _pngExportTcs;
    private string _currentBrowserPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string? _lastExportDirectory;
    private string? _currentVirtualHostFolder;
    private const string VirtualHostName = "localfiles.mermaideditor";
    private List<string> _recentFiles = new();
    private const int MaxRecentFiles = 10;
    private static readonly string RecentFilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MermaidEditor", "recent.json");
    private bool _isRenderingContent; // Flag set when we call NavigateToString, cleared after navigation completes
    private bool _isGoingBack; // Flag set when user clicks back button, cleared after navigation completes
    private bool _hasNavigatedAway; // Track if user has navigated away from rendered content
    private bool _markdownPageLoaded; // Track if Markdown page structure is already loaded (for incremental updates)
    private bool _mermaidPageLoaded; // Track if Mermaid page structure is already loaded (for incremental updates)
    
    // File change detection
    private FileSystemWatcher? _fileWatcher;
    private bool _isReloadingFile; // Prevent recursive change notifications during reload
    private bool _isSavingFile; // Prevent change notification when we save the file ourselves
    
    // Tab drag-and-drop
    private System.Windows.Point _tabDragStartPoint;
    private bool _isTabDragging;
    private System.Windows.Controls.Border? _draggedTab;
    
    // Auto-save and session restore
    private readonly DispatcherTimer _autoSaveTimer;
    private bool _isAutoSaving; // Prevent re-entrancy during auto-save
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MermaidEditor");
    private static readonly string AutoSaveFolder = Path.Combine(AppDataFolder, "AutoSave");
    private static readonly string SessionFilePath = Path.Combine(AppDataFolder, "session.json");
    // Default auto-save interval; overridden by SettingsManager on load
    private int _autoSaveIntervalSeconds = 30;
    private bool _lastCaretWasInComment = false;

    // Spell check
    private SpellCheckService? _spellCheckService;
    private SpellCheckBackgroundRenderer? _spellCheckRenderer;
    private bool _isSpellCheckEnabled;

    // Visual editor
    private VisualEditorMode _visualEditorMode = VisualEditorMode.Text;
    private VisualEditorBridge? _visualEditorBridge;
    private bool _visualEditorInitialized;
    private FlowchartModel? _currentFlowchartModel;
    private bool _isVisualEditorUpdating; // Prevent re-entrant updates between text <-> visual

    private const string DefaultMermaidCode= @"flowchart TD
    A[Start] --> B[End]";

    private const string DefaultMarkdownCode = @"# Welcome to Markdown Editor

This is a **live preview** markdown editor.

## Features

- Headers and text formatting
- Code blocks with syntax highlighting
- Tables and lists
- And more!

```csharp
Console.WriteLine(""Hello, World!"");
```

| Column 1 | Column 2 |
|----------|----------|
| Value 1  | Value 2  |
";

    public ICommand NewCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand ExportPngCommand { get; }
    public ICommand ExportSvgCommand { get; }
    public ICommand ExportEmfCommand { get; }
    public ICommand ExportWordCommand { get; }
    public ICommand FormatBoldCommand { get; }
    public ICommand FormatItalicCommand { get; }
    public ICommand FormatLinkCommand { get; }
    public ICommand FindReplaceCommand { get; }
    public ICommand FindNextCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand AskAiCommand { get; }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        NewCommand = new RelayCommand(_ => New_Click(this, new RoutedEventArgs()));
        OpenCommand = new RelayCommand(_ => Open_Click(this, new RoutedEventArgs()));
        SaveCommand = new RelayCommand(_ => Save_Click(this, new RoutedEventArgs()));
        SaveAsCommand = new RelayCommand(_ => SaveAs_Click(this, new RoutedEventArgs()));
        ExportPngCommand = new RelayCommand(_ => ExportPng_Click(this, new RoutedEventArgs()));
        ExportSvgCommand = new RelayCommand(_ => ExportSvg_Click(this, new RoutedEventArgs()));
        ExportEmfCommand = new RelayCommand(_ => ExportEmf_Click(this, new RoutedEventArgs()));
        ExportWordCommand = new RelayCommand(_ => ExportWord_Click(this, new RoutedEventArgs()));
        FormatBoldCommand = new RelayCommand(_ => FormatBold_Click(this, new RoutedEventArgs()));
        FormatItalicCommand = new RelayCommand(_ => FormatItalic_Click(this, new RoutedEventArgs()));
        FormatLinkCommand = new RelayCommand(_ => FormatLink_Click(this, new RoutedEventArgs()));
        FindReplaceCommand = new RelayCommand(_ => FindReplace_Click(this, new RoutedEventArgs()));
        FindNextCommand = new RelayCommand(_ => FindNext_Click(this, new RoutedEventArgs()));
        SettingsCommand = new RelayCommand(_ => Settings_Click(this, new RoutedEventArgs()));
        AskAiCommand = new RelayCommand(_ => AskAi_Click(this, new RoutedEventArgs()));

        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _renderTimer.Tick += RenderTimer_Tick;
        
        // Auto-save timer (interval configured via SettingsManager)
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_autoSaveIntervalSeconds)
        };
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SourceInitialized += MainWindow_SourceInitialized;

        SetupCodeEditor();
        
        // Initialize multi-document tab system (includes session restore)
        InitializeDocumentTabs();
    }

    private void LoadFile(string filePath)
    {
        try
        {
            CodeEditor.Text = File.ReadAllText(filePath);
            _currentFilePath = filePath;
            SetRenderModeFromFile(filePath);
            _isDirty = false;
            UpdateTitle();
            UpdateNavigationDropdown();
            
            // Update current browser path for Open/Save dialogs
            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder))
            {
                _currentBrowserPath = folder;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            CodeEditor.Text = DefaultMermaidCode;
            _isDirty = false;
        }
        
        AddToRecentFiles(filePath);
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Set title bar color based on current theme (loaded from settings)
        UpdateTitleBarTheme();
    }

    private void LoadRecentFiles()
    {
        try
        {
            if (File.Exists(RecentFilesPath))
            {
                var json = File.ReadAllText(RecentFilesPath);
                _recentFiles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
        }
        catch
        {
            _recentFiles = new List<string>();
        }
        UpdateRecentFilesMenu();
    }

    private void SaveRecentFiles()
    {
        try
        {
            var directory = Path.GetDirectoryName(RecentFilesPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var json = System.Text.Json.JsonSerializer.Serialize(_recentFiles);
            File.WriteAllText(RecentFilesPath, json);
        }
        catch
        {
            // Silently fail if we can't save recent files
        }
    }

    private void AddToRecentFiles(string filePath)
    {
        // Remove if already exists (to move to top)
        _recentFiles.Remove(filePath);
        
        // Add to beginning
        _recentFiles.Insert(0, filePath);
        
        // Keep only MaxRecentFiles
        if (_recentFiles.Count > MaxRecentFiles)
        {
            _recentFiles = _recentFiles.Take(MaxRecentFiles).ToList();
        }
        
        SaveRecentFiles();
        UpdateRecentFilesMenu();
    }

    private void UpdateRecentFilesMenu()
    {
        RecentFilesMenuItem.Items.Clear();
        
        if (_recentFiles.Count == 0)
        {
            var emptyItem = new System.Windows.Controls.MenuItem { Header = "(No recent files)", IsEnabled = false };
            RecentFilesMenuItem.Items.Add(emptyItem);
            return;
        }
        
        foreach (var filePath in _recentFiles)
        {
            var menuItem = new System.Windows.Controls.MenuItem
            {
                Header = Path.GetFileName(filePath),
                ToolTip = filePath
            };
            menuItem.Click += (s, e) => OpenRecentFile(filePath);
            RecentFilesMenuItem.Items.Add(menuItem);
        }
        
        RecentFilesMenuItem.Items.Add(new Separator());
        
        var clearItem = new System.Windows.Controls.MenuItem { Header = "Clear Recent Files" };
        clearItem.Click += (s, e) =>
        {
            _recentFiles.Clear();
            SaveRecentFiles();
            UpdateRecentFilesMenu();
        };
        RecentFilesMenuItem.Items.Add(clearItem);
    }

    private void OpenRecentFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            MessageBox.Show($"File not found: {filePath}\n\nIt will be removed from the recent files list.", 
                "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            _recentFiles.Remove(filePath);
            SaveRecentFiles();
            UpdateRecentFilesMenu();
            return;
        }
        
        // Open file in a new tab
        OpenFileInTab(filePath);
    }

    private void SetupCodeEditor()
    {
        CodeEditor.TextArea.TextEntering += TextArea_TextEntering;
        CodeEditor.TextArea.TextEntered += TextArea_TextEntered;
        CodeEditor.TextChanged += CodeEditor_TextChanged;
        
        CodeEditor.TextArea.Caret.PositionChanged += (s, e) =>
        {
            StatusText.Text = $"Line {CodeEditor.TextArea.Caret.Line}, Col {CodeEditor.TextArea.Caret.Column}";
            UpdateToggleCommentIconColor();
            
            // Ensure the editor scrolls to keep the caret visible when navigating
            // with arrow keys, Tab, Home/End, etc. on long lines without word wrap.
            // The root fix is CanContentScroll in the ScrollViewer template (MainWindow.xaml)
            // which restores AvalonEdit's native IScrollInfo chain. This deferred call
            // is a lightweight safety net that uses AvalonEdit's built-in scroll-to-caret.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                try
                {
                    CodeEditor.TextArea.Caret.BringCaretToView();
                }
                catch
                {
                    // Ignore errors during document switching
                }
            }));
        };
        
        // Intercept Ctrl+F and Ctrl+H before AvalonEdit handles them
        CodeEditor.PreviewKeyDown += CodeEditor_PreviewKeyDown;

        RegisterMermaidSyntaxHighlighting();
        
        // Enable bracket highlighting by default
        EnableBracketHighlighting();
    }
    
    private void CodeEditor_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            // Ctrl+F - Open Find dialog
            OpenFindDialog(showReplace: false);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.H && Keyboard.Modifiers == ModifierKeys.Control)
        {
            // Ctrl+H - Open Find and Replace dialog
            OpenFindDialog(showReplace: true);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.F3 && Keyboard.Modifiers == ModifierKeys.None)
        {
            // F3 - Find Next
            FindNext_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void RegisterMermaidSyntaxHighlighting()
    {
        var xshd = "<?xml version=\"1.0\"?>" +
            "<SyntaxDefinition name=\"Mermaid\" xmlns=\"http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008\">" +
            "<Color name=\"Comment\" foreground=\"#6A9955\" />" +
            "<Color name=\"Keyword\" foreground=\"#569CD6\" fontWeight=\"bold\" />" +
            "<Color name=\"DiagramType\" foreground=\"#C586C0\" fontWeight=\"bold\" />" +
            "<RuleSet>" +
            "<Span color=\"Comment\" begin=\"%%\" />" +
            "<Keywords color=\"DiagramType\">" +
            "<Word>flowchart</Word><Word>graph</Word><Word>sequenceDiagram</Word>" +
            "<Word>classDiagram</Word><Word>stateDiagram</Word><Word>erDiagram</Word>" +
            "<Word>journey</Word><Word>gantt</Word><Word>pie</Word><Word>mindmap</Word>" +
            "<Word>timeline</Word><Word>gitGraph</Word><Word>quadrantChart</Word>" +
            "<Word>requirementDiagram</Word><Word>C4Context</Word><Word>C4Container</Word><Word>C4Component</Word><Word>C4Dynamic</Word><Word>C4Deployment</Word>" +
            "</Keywords>" +
            "<Keywords color=\"Keyword\">" +
            "<Word>subgraph</Word><Word>end</Word><Word>direction</Word>" +
            "<Word>participant</Word><Word>actor</Word><Word>activate</Word><Word>deactivate</Word>" +
            "<Word>Note</Word><Word>note</Word><Word>loop</Word><Word>alt</Word><Word>else</Word>" +
            "<Word>opt</Word><Word>par</Word><Word>critical</Word><Word>break</Word><Word>rect</Word>" +
            "<Word>class</Word><Word>state</Word><Word>section</Word><Word>title</Word>" +
            "<Word>TB</Word><Word>TD</Word><Word>BT</Word><Word>RL</Word><Word>LR</Word>" +
            "<Word>requirement</Word><Word>functionalRequirement</Word><Word>performanceRequirement</Word>" +
            "<Word>interfaceRequirement</Word><Word>physicalRequirement</Word><Word>designConstraint</Word>" +
            "<Word>element</Word><Word>satisfies</Word><Word>traces</Word><Word>contains</Word>" +
            "<Word>derives</Word><Word>refines</Word><Word>verifies</Word><Word>copies</Word>" +
            "</Keywords>" +
            "</RuleSet>" +
            "</SyntaxDefinition>";

        try
        {
            using var reader = new XmlTextReader(new StringReader(xshd));
            var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            HighlightingManager.Instance.RegisterHighlighting("Mermaid", new[] { ".mmd", ".mermaid" }, definition);
            CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Mermaid");
        }
        catch
        {
            // If syntax highlighting fails, continue without it
        }
    }

    private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > 0 && _completionWindow != null)
        {
            if (!char.IsLetterOrDigit(e.Text[0]))
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length == 1 && (char.IsLetter(e.Text[0]) || e.Text[0] == '-'))
        {
            ShowCompletionWindow();
        }
    }

    private void ShowCompletionWindow()
    {
        // Only show IntelliSense for Mermaid files, not Markdown
        if (_currentRenderMode != RenderMode.Mermaid)
            return;
            
        var wordStart = GetWordStart();
        var currentWord = GetCurrentWord(wordStart);
        
        if (string.IsNullOrEmpty(currentWord) || currentWord.Length < 1)
            return;

        var completionData = GetCompletionData(currentWord);
        if (completionData.Count == 0)
        {
            // Close any existing completion window if no matches
            _completionWindow?.Close();
            return;
        }

        _completionWindow = new CompletionWindow(CodeEditor.TextArea);
        _completionWindow.CloseAutomatically = true;
        _completionWindow.CloseWhenCaretAtBeginning = true;
        var data = _completionWindow.CompletionList.CompletionData;
        
        foreach (var item in completionData)
        {
            data.Add(item);
        }

        _completionWindow.StartOffset = wordStart;
        _completionWindow.Show();
        _completionWindow.Closed += (s, e) => _completionWindow = null;
    }

    private int GetWordStart()
    {
        var offset = CodeEditor.CaretOffset;
        var text = CodeEditor.Text;
        
        while (offset > 0 && (char.IsLetterOrDigit(text[offset - 1]) || text[offset - 1] == '-'))
        {
            offset--;
        }
        
        return offset;
    }

    private string GetCurrentWord(int wordStart)
    {
        var offset = CodeEditor.CaretOffset;
        return CodeEditor.Text.Substring(wordStart, offset - wordStart);
    }

    private List<MermaidCompletionData> GetCompletionData(string prefix)
    {
        var allKeywords = new List<(string keyword, string description)>
        {
            ("flowchart", "Flowchart diagram"),
            ("graph", "Graph diagram (alias for flowchart)"),
            ("sequenceDiagram", "Sequence diagram"),
            ("classDiagram", "Class diagram"),
            ("stateDiagram", "State diagram"),
            ("stateDiagram-v2", "State diagram v2"),
            ("erDiagram", "Entity Relationship diagram"),
            ("journey", "User journey diagram"),
            ("gantt", "Gantt chart"),
            ("pie", "Pie chart"),
            ("quadrantChart", "Quadrant chart"),
            ("requirementDiagram", "Requirement diagram"),
            ("gitGraph", "Git graph"),
            ("mindmap", "Mind map"),
            ("timeline", "Timeline diagram"),
            ("zenuml", "ZenUML sequence diagram"),
            ("sankey-beta", "Sankey diagram (beta)"),
            ("xychart-beta", "XY chart (beta)"),
            ("block-beta", "Block diagram (beta)"),
            
            ("subgraph", "Define a subgraph"),
            ("end", "End subgraph/block"),
            ("direction", "Set direction (TB, TD, BT, RL, LR)"),
            ("participant", "Define a participant"),
            ("actor", "Define an actor"),
            ("activate", "Activate a participant"),
            ("deactivate", "Deactivate a participant"),
            ("Note", "Add a note"),
            ("note", "Add a note"),
            ("loop", "Loop block"),
            ("alt", "Alternative block"),
            ("else", "Else branch"),
            ("opt", "Optional block"),
            ("par", "Parallel block"),
            ("critical", "Critical section"),
            ("break", "Break block"),
            ("rect", "Rectangle highlight"),
            ("class", "Define a class"),
            ("state", "Define a state"),
            ("section", "Define a section"),
            ("title", "Set diagram title"),
            ("dateFormat", "Set date format (Gantt)"),
            ("axisFormat", "Set axis format (Gantt)"),
            ("excludes", "Exclude dates (Gantt)"),
            ("todayMarker", "Today marker (Gantt)"),
            
            ("TB", "Top to Bottom direction"),
            ("TD", "Top Down direction"),
            ("BT", "Bottom to Top direction"),
            ("RL", "Right to Left direction"),
            ("LR", "Left to Right direction"),
            
            // Frontmatter config options
            ("---", "Start/end frontmatter config block"),
            ("config:", "Configuration section in frontmatter"),
            ("look:", "Diagram look style (classic, handDrawn)"),
            ("theme:", "Diagram theme (default, forest, dark, neutral, base)"),
            ("layout:", "Layout algorithm (dagre, elk)"),
            ("classic", "Classic look style"),
            ("handDrawn", "Hand-drawn sketch style"),
            ("default", "Default theme"),
            ("forest", "Forest green theme"),
            ("dark", "Dark theme"),
            ("neutral", "Neutral gray theme"),
            ("base", "Base theme for customization"),
            ("dagre", "Dagre layout algorithm"),
            ("elk", "ELK layout algorithm"),
            
            // Additional config options
            ("flowchart:", "Flowchart-specific config"),
            ("sequence:", "Sequence diagram config"),
            ("gantt:", "Gantt chart config"),
            ("themeVariables:", "Custom theme variables"),
            ("htmlLabels:", "Enable HTML labels (true/false)"),
            ("curve:", "Edge curve style (basis, linear, cardinal)"),
            ("padding:", "Diagram padding"),
            ("nodeSpacing:", "Space between nodes"),
            ("rankSpacing:", "Space between ranks"),
            ("diagramPadding:", "Padding around diagram"),
            ("useMaxWidth:", "Use maximum width (true/false)"),
            ("wrap:", "Enable text wrapping (true/false)"),
            
            // More config options from schema
            ("handDrawnSeed:", "Seed for handDrawn look (0 = random)"),
            ("darkMode:", "Enable dark mode (true/false)"),
            ("fontFamily:", "Font family for diagram text"),
            ("fontSize:", "Font size for diagram text"),
            ("maxTextSize:", "Maximum text size (default 50000)"),
            ("maxEdges:", "Maximum number of edges (default 500)"),
            ("securityLevel:", "Security level (strict, loose, antiscript, sandbox)"),
            ("themeCSS:", "Custom CSS for theme"),
            
            // Diagram-specific config sections
            ("journey:", "User journey diagram config"),
            ("timeline:", "Timeline diagram config"),
            ("class:", "Class diagram config"),
            ("state:", "State diagram config"),
            ("er:", "ER diagram config"),
            ("pie:", "Pie chart config"),
            ("quadrantChart:", "Quadrant chart config"),
            ("xyChart:", "XY chart config"),
            ("mindmap:", "Mindmap config"),
            ("gitGraph:", "Git graph config"),
            ("sankey:", "Sankey diagram config"),
            ("packet:", "Packet diagram config"),
            ("block:", "Block diagram config"),
            ("radar:", "Radar diagram config"),
            ("kanban:", "Kanban diagram config"),
            ("architecture:", "Architecture diagram config"),
            ("c4:", "C4 diagram config"),
            ("requirement:", "Requirement diagram config"),
        };

        return allKeywords
            .Where(k => k.keyword.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => new MermaidCompletionData(k.keyword, k.description))
            .ToList();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
                // Load unified settings (also loads theme via SettingsManager)
                ThemeManager.LoadTheme();
                ApplySettingsToEditor();
                UpdateEditorTheme();
                UpdateTitleBarTheme();
                UpdateTabStyles(); // Refresh tab styles after theme is loaded
            
            await PreviewWebView.EnsureCoreWebView2Async();
            _webViewInitialized = true;
            
            // Set up navigation completed handler to update back button state
            PreviewWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            
            RenderPreview();
            
            // Style the toolbar overflow button programmatically
            StyleToolbarOverflowButtons();
            
            // Initialize SVG icons for toolbar buttons and menu items
            InitializeIcons();
            // Force-refresh comment icon color after InitializeIcons resets all icons to default.
            // This is critical for session restore: the constructor already ran SwitchToDocument()
            // which set _lastCaretWasInComment, but InitializeIcons() just overwrote the green tint.
            UpdateToggleCommentIconColor(force: true);
            
            // Load recent files
            LoadRecentFiles();
            
            // Enable bracket highlighting by default (since toggle is checked by default)
            if (_isBracketMatchingEnabled)
            {
                EnableBracketHighlighting();
            }
            
            // Show New Document dialog if no file was opened via command line
            // (skip if we restored a session)
            if (_showNewDocumentDialogOnLoad)
            {
                _showNewDocumentDialogOnLoad = false;
                ShowStartupNewDocumentDialog();
            }
            
            // Start auto-save timer
            _autoSaveTimer.Start();
            
            // Initialize spell check
            await InitializeSpellCheckAsync();
            
            // Initialize Visual Editor WebView2
            await InitializeVisualEditorAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2: {ex.Message}\n\nMake sure WebView2 Runtime is installed.",
                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    #region SVG Icon Management

    private const double IconSize = 18; // Slightly larger than the old 14px font icons
    private const double MenuIconSize = 16; // Menu item icons stay at 16px

    /// <summary>
    /// Initializes all toolbar and menu SVG icons at startup.
    /// Called from MainWindow_Loaded after the window is fully initialized.
    /// </summary>
    private void InitializeIcons()
    {
        try
        {
            // === File toolbar buttons ===
            SetButtonIcon(NewToolbarButton, "new-file.svg", IconSize);
            SetButtonIcon(OpenToolbarButton, "open-file.svg", IconSize);

            // === Edit toolbar buttons ===
            SetButtonIcon(ToggleCommentToolbarButton, "toggle-comment.svg", IconSize);

            // === Export toolbar buttons ===
            SetButtonIcon(ExportPngToolbarButton, "export-to-png.svg", IconSize);
            SetButtonIcon(ExportSvgToolbarButton, "export-to-svg.svg", IconSize);
            SetButtonIcon(ExportEmfToolbarButton, "export-to-emf.svg", IconSize);
            SetButtonIcon(ExportWordToolbarButton, "microsoft-word.svg", IconSize);

            // === Toggle toolbar buttons (set initial icon based on current state) ===
            UpdateWordWrapIcons();
            UpdateSplitViewIcons();
            UpdateLineNumbersIcons();
            UpdateBracketMatchingIcons();
            UpdateMinimapIcons();

            // === File menu items ===
            SetMenuItemIcon(NewMenuItem, "new-file.svg");
            SetMenuItemIcon(OpenMenuItem, "open-file.svg");

            // === Edit menu items ===
            SetMenuItemIcon(ToggleCommentMenuItem, "toggle-comment.svg");

            // === Export menu items ===
            SetMenuItemIcon(ExportPngMenuItem, "export-to-png.svg");
            SetMenuItemIcon(ExportSvgMenuItem, "export-to-svg.svg");
            SetMenuItemIcon(ExportEmfMenuItem, "export-to-emf.svg");
            SetMenuItemIcon(ExportWordMenuItem, "microsoft-word.svg");

            // === Help menu items ===
            SetMenuItemIcon(MermaidHelpMenuItem, "help-mermaid-icon.svg");
            SetMenuItemIcon(MarkdownHelpMenuItem, "markdown-help-icon.svg");
            SetMenuItemIcon(AboutMenuItem, "about-icon.svg");
        }
        catch (Exception)
        {
            // Silently fail - icons are cosmetic, app still works with fallback text icons
        }
    }

    /// <summary>
    /// Sets the Content of a Button to an SVG icon Image.
    /// </summary>
    private static void SetButtonIcon(System.Windows.Controls.Primitives.ButtonBase? button, string svgFileName, double size)
    {
        if (button == null) return;
        var icon = SvgIconHelper.CreateIcon(svgFileName, size);
        if (icon != null)
        {
            button.Content = icon;
        }
    }

    /// <summary>
    /// Sets the Icon property of a MenuItem to an SVG icon Image.
    /// </summary>
    private static void SetMenuItemIcon(System.Windows.Controls.MenuItem? menuItem, string svgFileName, double size = MenuIconSize)
    {
        if (menuItem == null) return;
        var icon = SvgIconHelper.CreateIcon(svgFileName, size);
        if (icon != null)
        {
            menuItem.Icon = icon;
        }
    }

    /// <summary>
    /// Updates the Word Wrap toggle button and menu item icons based on current state.
    /// Word wrap uses the same icon (word-wrap.svg) - toggle state shown by border.
    /// </summary>
    private void UpdateWordWrapIcons()
    {
        SetButtonIcon(WordWrapToggle, "word-wrap.svg", IconSize);
        SetMenuItemIcon(WordWrapMenuItem, "word-wrap.svg");
    }

    /// <summary>
    /// Updates the Split View (Preview Panel) toggle icons based on current state.
    /// Uses preview-panel-open.svg when visible, preview-panel-closed.svg when hidden.
    /// </summary>
    private void UpdateSplitViewIcons()
    {
        var svgName = _isPreviewVisible ? "preview-panel-open.svg" : "preview-panel-closed.svg";
        SetButtonIcon(SplitViewToggle, svgName, IconSize);
        SetMenuItemIcon(SplitViewMenuItem, svgName);
    }

    /// <summary>
    /// Updates the Line Numbers toggle icons based on current state.
    /// Uses line-numbers-toggle-on.svg when visible, line-numbers-toggle-off.svg when hidden.
    /// </summary>
    private void UpdateLineNumbersIcons()
    {
        var svgName = CodeEditor.ShowLineNumbers ? "line-numbers-toggle-on.svg" : "line-numbers-toggle-off.svg";
        SetButtonIcon(LineNumbersToggle, svgName, IconSize);
        SetMenuItemIcon(LineNumbersMenuItem, svgName);
    }

    /// <summary>
    /// Updates the Bracket Matching toggle icons based on current state.
    /// Uses bracket-matching-toggle-on.svg when enabled, bracket-matching-toggle-off.svg when disabled.
    /// </summary>
    private void UpdateBracketMatchingIcons()
    {
        var svgName = _isBracketMatchingEnabled ? "bracket-matching-toggle-on.svg" : "bracket-matching-toggle-off.svg";
        SetButtonIcon(BracketMatchingToggle, svgName, IconSize);
        SetMenuItemIcon(BracketMatchingMenuItem, svgName);
    }

    /// <summary>
    /// Updates the Minimap toggle icons based on current state.
    /// Uses minimap-toggle-on.svg when visible, minimap-toggle-off.svg when hidden.
    /// </summary>
    private void UpdateMinimapIcons()
    {
        var svgName = _isMinimapVisible ? "minimap-toggle-on.svg" : "minimap-toggle-off.svg";
        SetButtonIcon(MinimapToggle, svgName, IconSize);
        SetMenuItemIcon(MinimapMenuItem, svgName);
    }

    /// <summary>
    /// Checks if the current caret line is inside a comment and tints the toggle-comment
    /// toolbar button and menu item icon green when it is.
    /// </summary>
    private void UpdateToggleCommentIconColor(bool force = false)
    {
        try
        {
            var doc = CodeEditor.Document;
            if (doc == null) return;

            var line = doc.GetLineByOffset(CodeEditor.CaretOffset);
            var lineText = doc.GetText(line.Offset, line.Length).TrimStart();

            bool isComment = false;
            if (_currentRenderMode == RenderMode.Mermaid)
            {
                isComment = lineText.StartsWith("%%");
            }
            else
            {
                // Markdown: check for <!-- --> single-line comments or lines starting with <!--
                isComment = lineText.StartsWith("<!--");
            }

            if (!force && isComment == _lastCaretWasInComment) return;
            _lastCaretWasInComment = isComment;

            if (isComment)
            {
                var greenBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4E, 0xC9, 0x4E)); // Bright green
                SetButtonIconWithBrush(ToggleCommentToolbarButton, "toggle-comment.svg", IconSize, greenBrush);
                SetMenuItemIconWithBrush(ToggleCommentMenuItem, "toggle-comment.svg", MenuIconSize, greenBrush);
                if (ToggleCommentMenuItem != null) ToggleCommentMenuItem.IsChecked = true;
            }
            else
            {
                // Revert to default theme color
                SetButtonIcon(ToggleCommentToolbarButton, "toggle-comment.svg", IconSize);
                SetMenuItemIcon(ToggleCommentMenuItem, "toggle-comment.svg");
                if (ToggleCommentMenuItem != null) ToggleCommentMenuItem.IsChecked = false;
            }
        }
        catch
        {
            // Silently ignore - cosmetic feature
        }
    }

    private static void SetButtonIconWithBrush(System.Windows.Controls.Primitives.ButtonBase? button, string svgFileName, double size, System.Windows.Media.Brush fillBrush)
    {
        if (button == null) return;
        var icon = SvgIconHelper.CreateIcon(svgFileName, size, fillBrush);
        if (icon != null)
        {
            button.Content = icon;
        }
    }

    private static void SetMenuItemIconWithBrush(System.Windows.Controls.MenuItem? menuItem, string svgFileName, double size, System.Windows.Media.Brush fillBrush)
    {
        if (menuItem == null) return;
        var icon = SvgIconHelper.CreateIcon(svgFileName, size, fillBrush);
        if (icon != null)
        {
            menuItem.Icon = icon;
        }
    }

    #endregion

    private void CoreWebView2_NavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_isRenderingContent)
        {
            // This navigation is from our NavigateToString call - this is the base render
            _isRenderingContent = false;
            _hasNavigatedAway = false;
            PreviewBackButton.IsEnabled = false;
            
            // Mark page as loaded so subsequent updates use JavaScript instead of page reload
            if (_currentRenderMode == RenderMode.Markdown)
            {
                _markdownPageLoaded = true;
            }
            else if (_currentRenderMode == RenderMode.Mermaid)
            {
                _mermaidPageLoaded = true;
            }
        }
        else if (_isGoingBack)
        {
            // This navigation is from clicking the back button - stay disabled
            _isGoingBack = false;
            _hasNavigatedAway = false;
            PreviewBackButton.IsEnabled = false;
            // Reset page loaded flags since we navigated away
            _markdownPageLoaded = false;
            _mermaidPageLoaded = false;
        }
        else
        {
            // User has navigated away from rendered content (clicked a link)
            _hasNavigatedAway = true;
            PreviewBackButton.IsEnabled = true;
            // Reset page loaded flags since we navigated away
            _markdownPageLoaded = false;
            _mermaidPageLoaded = false;
        }
    }
    
    private void PreviewBack_Click(object sender, RoutedEventArgs e)
    {
        if (_hasNavigatedAway && PreviewWebView.CoreWebView2?.CanGoBack == true)
        {
            _isGoingBack = true;
            PreviewWebView.CoreWebView2.GoBack();
        }
    }
    
    private void StyleToolbarOverflowButtons()
    {
        // Use Dispatcher to ensure visual tree is fully built
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            var darkBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D2D30"));
            var foregroundBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F1F1F1"));
            
            // Find all ToolBars in the visual tree
            var toolBars = FindVisualChildren<System.Windows.Controls.ToolBar>(this);
            foreach (var toolBar in toolBars)
            {
                // Find the overflow button by looking for ToggleButton in the toolbar
                // BUT skip our named toggle buttons (SplitViewToggle, LineNumbersToggle, etc.)
                var toggleButtons = FindVisualChildren<System.Windows.Controls.Primitives.ToggleButton>(toolBar);
                foreach (var toggleButton in toggleButtons)
                {
                    // Skip our custom toggle buttons - they have names
                    if (!string.IsNullOrEmpty(toggleButton.Name))
                    {
                        continue;
                    }
                    
                    // Style the overflow toggle button itself
                    toggleButton.Background = darkBrush;
                    toggleButton.Foreground = foregroundBrush;
                    toggleButton.BorderThickness = new Thickness(0);
                    toggleButton.BorderBrush = darkBrush;
                    
                    // Style all Border elements inside the toggle button
                    var borders = FindVisualChildren<System.Windows.Controls.Border>(toggleButton);
                    foreach (var border in borders)
                    {
                        border.Background = darkBrush;
                        border.BorderBrush = darkBrush;
                    }
                    
                    // Style all Path elements (arrows) inside
                    var paths = FindVisualChildren<System.Windows.Shapes.Path>(toggleButton);
                    foreach (var path in paths)
                    {
                        path.Fill = foregroundBrush;
                    }
                }
                
                // Also find and style any Grid or Panel backgrounds in the toolbar's overflow area
                var grids = FindVisualChildren<System.Windows.Controls.Grid>(toolBar);
                foreach (var grid in grids)
                {
                    // Only style grids that are small (likely the overflow button container)
                    if (grid.ActualWidth < 30 && grid.ActualWidth > 0)
                    {
                        grid.Background = darkBrush;
                    }
                }
            }
        }));
    }
    
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;
        
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }
            
            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
    
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;
        
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }
            
            var result = FindVisualChild<T>(child);
            if (result != null)
            {
                return result;
            }
        }
        
        return null;
    }

    private void UpdateVirtualHostMapping(string? folderPath)
    {
        if (!_webViewInitialized || PreviewWebView.CoreWebView2 == null) return;
        if (string.IsNullOrEmpty(folderPath)) return;
        
        // Only update if the folder has changed
        if (_currentVirtualHostFolder == folderPath) return;
        
        // Clear previous mapping if exists
        if (!string.IsNullOrEmpty(_currentVirtualHostFolder))
        {
            PreviewWebView.CoreWebView2.ClearVirtualHostNameToFolderMapping(VirtualHostName);
        }
        
        // Set new mapping
        PreviewWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHostName,
            folderPath,
            CoreWebView2HostResourceAccessKind.Allow);
        
        _currentVirtualHostFolder = folderPath;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Stop auto-save timer
        _autoSaveTimer.Stop();
        
        // Save session state and auto-save content before prompting user
        // This ensures we have a backup even if the user cancels or the app crashes later
        SaveActiveDocumentState();
        AutoSaveAllDocuments();
        SaveSessionState();
        
        // Check all open documents for unsaved changes
        var unsavedDocs = _openDocuments.Where(d => d.IsDirty).ToList();
        
        if (unsavedDocs.Count > 0)
        {
            var message = unsavedDocs.Count == 1
                ? $"'{unsavedDocs[0].DisplayName}' has unsaved changes. Do you want to save before closing?"
                : $"{unsavedDocs.Count} documents have unsaved changes. Do you want to save before closing?";
            
            var result = MessageBox.Show(message,
                "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    foreach (var doc in unsavedDocs)
                    {
                        if (!SaveDocument(doc))
                        {
                            e.Cancel = true;
                            _autoSaveTimer.Start(); // Restart timer if user cancels
                            return;
                        }
                    }
                    // User saved all docs - update session to reflect saved state
                    SaveSessionState();
                    break;
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    _autoSaveTimer.Start(); // Restart timer if user cancels
                    return;
                case MessageBoxResult.No:
                    // User chose not to save - session already saved above with dirty state
                    break;
            }
        }
        
        // Clean up file watcher
        _fileWatcher?.Dispose();
        _fileWatcher = null;
    }

    private void CodeEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_isSwitchingDocuments) return; // Don't mark dirty when switching documents
        
        _isDirty = true;
        if (_activeDocument != null)
        {
            _activeDocument.IsDirty = true;
        }
        UpdateTitle();
        UpdateUndoRedoState();
        _renderTimer.Stop();
        _renderTimer.Start();
        
        // Reset auto-save timer on text change so it fires 30s after last edit
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }
    
    private void UpdateUndoRedoState()
    {
        var canUndo = CodeEditor.CanUndo;
        var canRedo = CodeEditor.CanRedo;
        
        UndoButton.IsEnabled = canUndo;
        RedoButton.IsEnabled = canRedo;
        UndoMenuItem.IsEnabled = canUndo;
        RedoMenuItem.IsEnabled = canRedo;
    }

    private void RenderTimer_Tick(object? sender, EventArgs e)
    {
        _renderTimer.Stop();
        RenderPreview();
        UpdateNavigationDropdown();
    }

    private void RenderPreview()
    {
        if (_currentRenderMode == RenderMode.Markdown)
        {
            RenderMarkdown();
        }
        else
        {
            RenderMermaid();
        }
    }

    private async void RenderMermaid()
    {
        if (!_webViewInitialized) return;
        
        // Reset markdown page loaded flag since we're switching to Mermaid mode
        _markdownPageLoaded = false;

        var mermaidCode = CodeEditor.Text;
        var escapedCode = System.Text.Json.JsonSerializer.Serialize(mermaidCode);
        
        // If page is already loaded, just update the diagram via JavaScript (preserves pan/zoom position)
        if (_mermaidPageLoaded && !_hasNavigatedAway)
        {
            try
            {
                // When switching documents, we need to apply the document's zoom level, scroll position, and pan position
                // Otherwise, preserve the current pan/zoom position for normal edits
                if (_isSwitchingDocuments && _activeDocument != null)
                {
                    // Update diagram and pass the document's zoom level, scroll position, and pan position
                    // This ensures the correct state is applied after the async render completes
                    var scrollLeft = _activeDocument.PreviewScrollLeft.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var scrollTop = _activeDocument.PreviewScrollTop.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var panX = _activeDocument.PreviewPanX.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var panY = _activeDocument.PreviewPanY.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    await PreviewWebView.CoreWebView2.ExecuteScriptAsync($@"
                        (function() {{
                            if (typeof updateDiagram === 'function') {{
                                updateDiagram({escapedCode}, {_currentZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {scrollLeft}, {scrollTop}, {panX}, {panY});
                            }}
                        }})();
                    ");
                }
                else
                {
                    // Update diagram without reloading the page - pan/zoom position is preserved
                    await PreviewWebView.CoreWebView2.ExecuteScriptAsync($@"
                        (function() {{
                            if (typeof updateDiagram === 'function') {{
                                updateDiagram({escapedCode});
                            }}
                        }})();
                    ");
                }
                StatusText.Text = "Mermaid rendered";
                UpdateZoomUI();
                return;
            }
            catch
            {
                // If JavaScript update fails, fall back to full page reload
                _mermaidPageLoaded = false;
            }
        }

        // Get target positions for restoration when switching documents (full page reload path)
        var targetScrollLeft = (_isSwitchingDocuments && _activeDocument != null) 
            ? _activeDocument.PreviewScrollLeft.ToString(System.Globalization.CultureInfo.InvariantCulture) 
            : "0";
        var targetScrollTop = (_isSwitchingDocuments && _activeDocument != null) 
            ? _activeDocument.PreviewScrollTop.ToString(System.Globalization.CultureInfo.InvariantCulture) 
            : "0";
        var targetPanX = (_isSwitchingDocuments && _activeDocument != null) 
            ? _activeDocument.PreviewPanX.ToString(System.Globalization.CultureInfo.InvariantCulture) 
            : "0";
        var targetPanY = (_isSwitchingDocuments && _activeDocument != null) 
            ? _activeDocument.PreviewPanY.ToString(System.Globalization.CultureInfo.InvariantCulture) 
            : "0";
        var targetZoom = (_isSwitchingDocuments && _activeDocument != null) 
            ? _activeDocument.PreviewZoom.ToString(System.Globalization.CultureInfo.InvariantCulture) 
            : "1";

        var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <script src=""https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/panzoom@9.4.3/dist/panzoom.min.js""></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        html, body {{ 
            width: 100%; 
            height: 100%; 
            overflow: hidden;
            background: #f5f5f5;
        }}
        #container {{
            width: 100%;
            height: 100%;
            display: flex;
            align-items: flex-start;
            justify-content: flex-start;
            padding: 20px;
            overflow: auto;
        }}
        #diagram {{
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            display: inline-block;
        }}
        #diagram.has-error {{
            max-width: calc(100vw - 60px);
        }}
        #diagram svg {{
            display: block;
        }}
        /* Override Mermaid's huge inline width styles ONLY for gantt charts */
        #diagram svg[aria-roledescription=""gantt""] {{
            width: auto !important;
            min-width: auto !important;
            max-width: none !important;
        }}
        .error {{
            color: #d32f2f;
            padding: 20px;
            font-family: Consolas, monospace;
            white-space: pre-wrap;
            word-wrap: break-word;
            word-break: break-word;
            overflow-wrap: break-word;
            background: #ffebee;
            border-radius: 8px;
            max-width: 100%;
            overflow-x: auto;
        }}
    </style>
</head>
<body>
    <div id=""container"">
        <div id=""diagram"">
            <pre class=""mermaid"">{System.Web.HttpUtility.HtmlEncode(mermaidCode)}</pre>
        </div>
    </div>
    <script>
        // Use window-level variables so they can be accessed from C# via ExecuteScriptAsync
        window.panzoomInstance = null;
        window.currentZoom = {_currentZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)};
        
        // Target positions for restoration when switching documents
        var targetScrollLeft = {targetScrollLeft};
        var targetScrollTop = {targetScrollTop};
        var targetPanX = {targetPanX};
        var targetPanY = {targetPanY};
        var targetZoom = {targetZoom};
        
        // Don't set theme here - let frontmatter config take precedence
        // Mermaid will parse ---config:--- frontmatter automatically
        mermaid.initialize({{ 
            startOnLoad: true,
            securityLevel: 'loose'
        }});
        
        mermaid.run().then(() => {{
            const container = document.getElementById('container');
            const diagram = document.getElementById('diagram');
            const svg = document.querySelector('#diagram svg');
            
            // Auto-size the container to fit the actual SVG content
            if (svg) {{
                try {{
                    // Actually remove Mermaid's inline width/min-width styles using removeProperty
                    // Setting to '' doesn't work - must use removeProperty to truly remove inline styles
                    svg.style.removeProperty('width');
                    svg.style.removeProperty('min-width');
                    svg.style.removeProperty('max-width');
                    svg.style.removeProperty('height');
                    svg.style.removeProperty('min-height');
                    
                    // Get dimensions from viewBox if available (more reliable for gantt charts)
                    const viewBox = svg.getAttribute('viewBox');
                    if (viewBox) {{
                        const parts = viewBox.split(' ').map(Number);
                        if (parts.length === 4 && parts[2] > 0 && parts[3] > 0) {{
                            const padding = 20;
                            svg.setAttribute('width', parts[2] + padding);
                            svg.setAttribute('height', parts[3] + padding);
                        }}
                    }} else {{
                        // Fall back to getBBox for diagrams without viewBox
                        const bbox = svg.getBBox();
                        if (bbox && bbox.width > 0 && bbox.height > 0) {{
                            const padding = 20;
                            svg.setAttribute('width', bbox.width + padding);
                            svg.setAttribute('height', bbox.height + padding);
                            svg.setAttribute('viewBox', `${{bbox.x - padding/2}} ${{bbox.y - padding/2}} ${{bbox.width + padding}} ${{bbox.height + padding}}`);
                        }}
                    }}
                }} catch (e) {{
                    // getBBox may fail in some cases, just continue
                }}
            }}
            
            window.panzoomInstance = panzoom(diagram, {{
                maxZoom: 10,
                minZoom: 0.1,
                initialZoom: 1,
                bounds: false,
                boundsPadding: 0.1
            }});
            
            // Restore zoom and pan position (use target values if switching documents, otherwise reset to default)
            var restoreZoom = (targetPanX !== 0 || targetPanY !== 0 || targetZoom !== 1) ? targetZoom : 1;
            var restorePanX = targetPanX;
            var restorePanY = targetPanY;
            
            window.panzoomInstance.zoomAbs(0, 0, restoreZoom);
            window.currentZoom = restoreZoom;
            
            // Use setTimeout to ensure panzoom is fully initialized before setting pan position
            setTimeout(function() {{
                window.panzoomInstance.moveTo(restorePanX, restorePanY);
                
                // Restore scroll position
                if (targetScrollLeft !== 0 || targetScrollTop !== 0) {{
                    container.scrollLeft = targetScrollLeft;
                    container.scrollTop = targetScrollTop;
                }}
                
                // Notify C# that diagram is ready
                window.chrome.webview.postMessage({{ 
                    type: 'diagramReady', 
                    targetScrollLeft: targetScrollLeft, 
                    targetScrollTop: targetScrollTop,
                    targetPanX: restorePanX,
                    targetPanY: restorePanY
                }});
            }}, 50);
            
            window.panzoomInstance.on('zoom', function(e) {{
                window.currentZoom = e.getTransform().scale;
                window.chrome.webview.postMessage({{ type: 'zoom', level: window.currentZoom }});
            }});
            
            // Add click handlers to diagram nodes for click-to-highlight feature
            setupNodeClickHandlers(svg);
        }}).catch(err => {{
            const diagram = document.getElementById('diagram');
            diagram.classList.add('has-error');
            diagram.innerHTML = '<div class=""error"">Error: ' + err.message + '</div>';
        }});
        
        function setupNodeClickHandlers(svg) {{
            // Find all clickable elements in the SVG (nodes, edges, labels, subgraphs)
            const clickableElements = svg.querySelectorAll('[id*=""flowchart-""], [id*=""stateDiagram-""], [id*=""classDiagram-""], [id*=""sequenceDiagram-""], .node, .cluster, .actor, .messageText, .labelText, .edgeLabel, .nodeLabel, g[class*=""node""], .cluster-label, [id*=""subGraph""]');
            
            clickableElements.forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
                    e.stopPropagation();
                    
                    // Try to extract the node ID from the element
                    let nodeId = '';
                    let textContent = '';
                    
                    // Get the element's ID
                    const elId = el.id || el.getAttribute('id') || '';
                    
                    // Check if this is a subgraph/cluster
                    const isSubgraph = el.classList.contains('cluster') || el.classList.contains('cluster-label') || elId.includes('subGraph');
                    
                    // Try to extract node ID from Mermaid's ID format (e.g., 'flowchart-A-0')
                    if (elId) {{
                        const parts = elId.split('-');
                        if (parts.length >= 2) {{
                            nodeId = parts[1]; // Get the node name (e.g., 'A' from 'flowchart-A-0')
                        }}
                    }}
                    
                    // For subgraphs, look for the label text in cluster-label or foreignObject
                    if (isSubgraph || el.classList.contains('cluster')) {{
                        const clusterLabel = el.querySelector('.cluster-label, foreignObject, text') || el.closest('.cluster')?.querySelector('.cluster-label, foreignObject, text');
                        if (clusterLabel) {{
                            textContent = (clusterLabel.textContent || clusterLabel.innerText || '').trim();
                        }}
                    }}
                    
                    // Also get text content from the element or its children
                    if (!textContent) {{
                        const textEl = el.querySelector('text, .nodeLabel, span, foreignObject div, foreignObject') || el;
                        textContent = (textEl.textContent || textEl.innerText || '').trim();
                    }}
                    
                    // If no nodeId found, try to use the class name
                    if (!nodeId && el.classList) {{
                        el.classList.forEach(cls => {{
                            if (cls.startsWith('node-')) {{
                                nodeId = cls.replace('node-', '');
                            }}
                        }});
                    }}
                    
                    if (nodeId || textContent) {{
                        window.chrome.webview.postMessage({{ 
                            type: 'nodeClick', 
                            nodeId: nodeId,
                            text: textContent
                        }});
                    }}
                }});
            }});
            
            // Also handle clicks on text elements directly (including subgraph labels)
            const textElements = svg.querySelectorAll('text, .nodeLabel, .cluster-label foreignObject, .cluster-label text');
            textElements.forEach(el => {{
                if (!el.closest('[id*=""flowchart-""]') || el.closest('.cluster-label')) {{ // Don't double-handle unless it's a cluster label
                    el.style.cursor = 'pointer';
                    el.addEventListener('click', function(e) {{
                        e.stopPropagation();
                        const textContent = (el.textContent || el.innerText || '').trim();
                        if (textContent) {{
                            window.chrome.webview.postMessage({{ 
                                type: 'nodeClick', 
                                nodeId: '',
                                text: textContent
                            }});
                        }}
                    }});
                }}
            }});
        }}
        
        window.setZoom = function(level) {{
            if (panzoomInstance) {{
                panzoomInstance.zoomAbs(window.innerWidth / 2, window.innerHeight / 2, level);
                currentZoom = level;
            }}
        }};
        
        window.resetView = function() {{
            if (panzoomInstance) {{
                panzoomInstance.moveTo(0, 0);
                panzoomInstance.zoomAbs(window.innerWidth / 2, window.innerHeight / 2, 1);
                currentZoom = 1;
            }}
        }};
        
        window.fitToWindow = function() {{
            if (panzoomInstance) {{
                const diagram = document.getElementById('diagram');
                const container = document.getElementById('container');
                
                // First reset to get accurate measurements
                panzoomInstance.moveTo(0, 0);
                panzoomInstance.zoomAbs(0, 0, 1);
                
                // Use setTimeout to ensure DOM has updated
                setTimeout(() => {{
                    const diagramRect = diagram.getBoundingClientRect();
                    const containerRect = container.getBoundingClientRect();
                    
                    const scaleX = (containerRect.width - 40) / diagramRect.width;
                    const scaleY = (containerRect.height - 40) / diagramRect.height;
                    const scale = Math.min(scaleX, scaleY, 1) * 0.95;
                    
                    panzoomInstance.zoomAbs(0, 0, scale);
                    currentZoom = scale;
                    window.chrome.webview.postMessage({{ type: 'zoom', level: currentZoom }});
                }}, 10);
            }}
        }};
        
        window.getSvgContent = function() {{
            const svg = document.querySelector('#diagram svg');
            return svg ? svg.outerHTML : null;
        }};
        
        window.exportPngHighRes = function(scale) {{
            const svg = document.querySelector('#diagram svg');
            if (!svg) {{
                window.chrome.webview.postMessage({{ type: 'pngExportError', error: 'No SVG found' }});
                return;
            }}
            
            try {{
                // Clone the SVG to avoid modifying the original
                const svgClone = svg.cloneNode(true);
                
                // Get the SVG dimensions
                const bbox = svg.getBBox();
                const svgWidth = svg.width.baseVal.value || bbox.width + 40;
                const svgHeight = svg.height.baseVal.value || bbox.height + 40;
                
                // Check for canvas size limits (browsers typically limit to ~16384 pixels or ~268M total pixels)
                const canvasWidth = svgWidth * scale;
                const canvasHeight = svgHeight * scale;
                const totalPixels = canvasWidth * canvasHeight;
                const maxPixels = 268000000; // ~268 million pixels (browser limit)
                const maxDimension = 16384; // Max single dimension
                
                if (canvasWidth > maxDimension || canvasHeight > maxDimension || totalPixels > maxPixels) {{
                    // Calculate the maximum safe scale
                    const maxScaleByDimension = Math.min(maxDimension / svgWidth, maxDimension / svgHeight);
                    const maxScaleByPixels = Math.sqrt(maxPixels / (svgWidth * svgHeight));
                    const maxSafeScale = Math.floor(Math.min(maxScaleByDimension, maxScaleByPixels));
                    
                    window.chrome.webview.postMessage({{ 
                        type: 'pngExportError', 
                        error: 'Image too large for ' + scale + 'x export (' + Math.round(canvasWidth) + 'x' + Math.round(canvasHeight) + ' pixels). Try ' + maxSafeScale + 'x or lower resolution, or export as SVG instead.' 
                    }});
                    return;
                }}
                
                // Set explicit dimensions on the clone
                svgClone.setAttribute('width', svgWidth);
                svgClone.setAttribute('height', svgHeight);
                
                // Create a canvas with scaled dimensions
                const canvas = document.createElement('canvas');
                canvas.width = canvasWidth;
                canvas.height = canvasHeight;
                const ctx = canvas.getContext('2d');
                
                if (!ctx) {{
                    window.chrome.webview.postMessage({{ type: 'pngExportError', error: 'Failed to create canvas context. Image may be too large.' }});
                    return;
                }}
                
                // Fill with white background
                ctx.fillStyle = 'white';
                ctx.fillRect(0, 0, canvas.width, canvas.height);
                
                // Serialize SVG to string and create a data URL (more reliable than blob URL)
                const svgData = new XMLSerializer().serializeToString(svgClone);
                const svgBase64 = btoa(unescape(encodeURIComponent(svgData)));
                const dataUrl = 'data:image/svg+xml;base64,' + svgBase64;
                
                const img = new Image();
                img.onload = function() {{
                    try {{
                        ctx.scale(scale, scale);
                        ctx.drawImage(img, 0, 0);
                        
                        // Convert to base64 PNG and send via postMessage
                        const pngData = canvas.toDataURL('image/png');
                        if (!pngData || pngData === 'data:,') {{
                            window.chrome.webview.postMessage({{ type: 'pngExportError', error: 'Canvas export failed. Image may be too large for this resolution. Try a lower scale or export as SVG.' }});
                            return;
                        }}
                        window.chrome.webview.postMessage({{ type: 'pngExport', data: pngData }});
                    }} catch (drawError) {{
                        window.chrome.webview.postMessage({{ type: 'pngExportError', error: 'Failed to draw image: ' + drawError.message }});
                    }}
                }};
                img.onerror = function(e) {{
                    window.chrome.webview.postMessage({{ type: 'pngExportError', error: 'Failed to load SVG as image: ' + (e.message || 'unknown error') }});
                }};
                img.src = dataUrl;
            }} catch (e) {{
                window.chrome.webview.postMessage({{ type: 'pngExportError', error: e.message }});
            }}
        }};
        
        // Update diagram content without reloading the page (preserves pan/zoom position)
        // Optional targetZoom parameter allows overriding the zoom level (used when switching documents)
        // Optional targetScrollLeft/targetScrollTop parameters allow overriding scroll position (used when switching documents)
        // Optional targetPanX/targetPanY parameters allow overriding pan position (used when switching documents)
        window.updateDiagram = function(newCode, targetZoom, targetScrollLeft, targetScrollTop, targetPanX, targetPanY) {{
            // Save current panzoom transform (only zoom level, not position - position causes issues when diagram size changes)
            // If targetZoom is provided, use that instead of the current zoom (for document switching)
            let savedZoom = (typeof targetZoom === 'number') ? targetZoom : window.currentZoom;
            let savedTransform = null;
            if (window.panzoomInstance) {{
                savedTransform = window.panzoomInstance.getTransform();
            }}
            
            // Save pan position (or use provided target pan positions for document switching)
            let savedPanX = (typeof targetPanX === 'number') ? targetPanX : (savedTransform ? savedTransform.x : 0);
            let savedPanY = (typeof targetPanY === 'number') ? targetPanY : (savedTransform ? savedTransform.y : 0);
            
            const diagram = document.getElementById('diagram');
            const container = document.getElementById('container');
            
            // Save scroll position of container (or use provided target scroll positions for document switching)
            const savedScrollLeft = (typeof targetScrollLeft === 'number') ? targetScrollLeft : container.scrollLeft;
            const savedScrollTop = (typeof targetScrollTop === 'number') ? targetScrollTop : container.scrollTop;
            
            // Clear existing content and add new mermaid code
            diagram.innerHTML = '<pre class=""mermaid"">' + newCode.replace(/</g, '&lt;').replace(/>/g, '&gt;') + '</pre>';
            diagram.classList.remove('has-error');
            diagram.style.minWidth = '2000px';
            diagram.style.width = '';
            
            // Reset any transform on diagram before re-rendering
            diagram.style.transform = '';
            
            // Destroy old panzoom instance
            if (window.panzoomInstance) {{
                window.panzoomInstance.dispose();
                window.panzoomInstance = null;
            }}
            
            // Re-render mermaid
            mermaid.run().then(() => {{
                // Use requestAnimationFrame to ensure SVG is fully rendered
                requestAnimationFrame(() => {{
                    const svg = document.querySelector('#diagram svg');
                    
                    // Fix SVG and container dimensions after render
                    if (svg) {{
                        // Wait another frame to ensure text is fully rendered
                        requestAnimationFrame(() => {{
                            let svgWidth = 0;
                            let svgHeight = 0;
                            
                            // Use getBBox for accurate dimensions (includes all rendered content)
                            try {{
                                const bbox = svg.getBBox();
                                svgWidth = bbox.width + 40;
                                svgHeight = bbox.height + 40;
                            }} catch (e) {{
                                // Fall back to viewBox
                                const viewBox = svg.getAttribute('viewBox');
                                if (viewBox) {{
                                    const parts = viewBox.split(' ');
                                    if (parts.length === 4) {{
                                        svgWidth = parseFloat(parts[2]);
                                        svgHeight = parseFloat(parts[3]);
                                    }}
                                }}
                            }}
                            
                            if (svgWidth > 0 && svgHeight > 0) {{
                                svg.style.width = svgWidth + 'px';
                                svg.style.height = svgHeight + 'px';
                                svg.style.minWidth = svgWidth + 'px';
                                svg.style.minHeight = svgHeight + 'px';
                                svg.removeAttribute('max-width');
                                
                                diagram.style.minWidth = 'auto';
                                diagram.style.width = 'auto';
                            }}
                            
                            // Set up click handlers
                            setupNodeClickHandlers(svg);
                            
                            // Re-create panzoom
                            window.panzoomInstance = panzoom(diagram, {{
                                maxZoom: 10,
                                minZoom: 0.1,
                                initialZoom: 1,
                                bounds: false,
                                boundsPadding: 0.1
                            }});
                            
                            // Restore zoom level
                            window.panzoomInstance.zoomAbs(0, 0, savedZoom);
                            window.currentZoom = savedZoom;
                            
                            // Restore pan position (the drag/translate position)
                            // Use setTimeout to ensure panzoom is fully initialized
                            setTimeout(function() {{
                                window.panzoomInstance.moveTo(savedPanX, savedPanY);
                                
                                // Notify C# that diagram is ready, passing the target scroll and pan positions
                                // C# will restore the scroll position to ensure proper timing
                                window.chrome.webview.postMessage({{ 
                                    type: 'diagramReady', 
                                    targetScrollLeft: savedScrollLeft, 
                                    targetScrollTop: savedScrollTop,
                                    targetPanX: savedPanX,
                                    targetPanY: savedPanY
                                }});
                            }}, 50);
                            
                            window.panzoomInstance.on('zoom', function(e) {{
                                window.currentZoom = e.getTransform().scale;
                                window.chrome.webview.postMessage({{ type: 'zoom', level: window.currentZoom }});
                            }});
                        }});
                    }}
                }});
            }}).catch(err => {{
                diagram.classList.add('has-error');
                diagram.innerHTML = '<div class=""error"">Error: ' + err.message + '</div>';
            }});
        }};
    </script>
</body>
</html>";

        _isRenderingContent = true;
        PreviewWebView.NavigateToString(html);
        PreviewWebView.WebMessageReceived -= PreviewWebView_WebMessageReceived;
        PreviewWebView.WebMessageReceived += PreviewWebView_WebMessageReceived;
        StatusText.Text = "Mermaid rendered";
    }

    private async void RenderMarkdown()
    {
        if (!_webViewInitialized) return;
        
        // Reset mermaid page loaded flag since we're switching to Markdown mode
        _mermaidPageLoaded = false;

        var markdownCode = CodeEditor.Text;
        var escapedCode = System.Text.Json.JsonSerializer.Serialize(markdownCode);
        
        // If page is already loaded, just update the content via JavaScript (preserves scroll position)
        if (_markdownPageLoaded && !_hasNavigatedAway)
        {
            try
            {
                // When switching documents, restore the saved scroll position
                // Otherwise, preserve the current scroll position for normal edits
                if (_isSwitchingDocuments && _activeDocument != null)
                {
                    var scrollTop = _activeDocument.PreviewScrollTop.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var scrollLeft = _activeDocument.PreviewScrollLeft.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    await PreviewWebView.CoreWebView2.ExecuteScriptAsync($@"
                        (async function() {{
                            const markdownContent = {escapedCode};
                            document.getElementById('content').innerHTML = marked.parse(markdownContent);
                            await renderMermaidBlocks();
                            setupClickHandlers();
                            // Restore scroll position after content is updated
                            // Try all methods since html/body can both have overflow:auto
                            setTimeout(function() {{
                                var x = {scrollLeft};
                                var y = {scrollTop};
                                window.scrollTo(x, y);
                                document.documentElement.scrollLeft = x;
                                document.documentElement.scrollTop = y;
                                document.body.scrollLeft = x;
                                document.body.scrollTop = y;
                            }}, 50);
                        }})();
                    ");
                }
                else
                {
                    // Update content without reloading the page - scroll position is naturally preserved
                    await PreviewWebView.CoreWebView2.ExecuteScriptAsync($@"
                        (async function() {{
                            const markdownContent = {escapedCode};
                            document.getElementById('content').innerHTML = marked.parse(markdownContent);
                            await renderMermaidBlocks();
                            setupClickHandlers();
                        }})();
                    ");
                }
                StatusText.Text = "Markdown rendered";
                return;
            }
            catch
            {
                // If JavaScript update fails, fall back to full page reload
                _markdownPageLoaded = false;
            }
        }
        
        // Set up virtual host mapping for resolving relative image paths
        var baseTag = "";
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            var directory = Path.GetDirectoryName(_currentFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                UpdateVirtualHostMapping(directory);
                baseTag = $@"<base href=""https://{VirtualHostName}/"">";
            }
        }
        
        // Get target scroll position for restoration when switching documents
        var targetScrollLeft = (_isSwitchingDocuments && _activeDocument != null) 
            ? _activeDocument.PreviewScrollLeft.ToString(System.Globalization.CultureInfo.InvariantCulture) 
            : "0";
        var targetScrollTop = (_isSwitchingDocuments && _activeDocument != null) 
            ? _activeDocument.PreviewScrollTop.ToString(System.Globalization.CultureInfo.InvariantCulture) 
            : "0";

        var html = $@"<!DOCTYPE html
<html>
<head>
    <meta charset=""UTF-8"">
    {baseTag}
    <script src=""https://cdn.jsdelivr.net/npm/marked/marked.min.js""></script>
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/github-markdown-css@5/github-markdown-light.min.css"">
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/highlight.js@11/styles/github.min.css"">
    <script src=""https://cdn.jsdelivr.net/npm/highlight.js@11/lib/core.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/highlight.js@11/lib/languages/javascript.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/highlight.js@11/lib/languages/csharp.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/highlight.js@11/lib/languages/python.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/highlight.js@11/lib/languages/bash.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/highlight.js@11/lib/languages/xml.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/highlight.js@11/lib/languages/json.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/highlight.js@11/lib/languages/sql.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.min.js""></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        html, body {{ 
            width: 100%; 
            height: 100%; 
            overflow: auto;
            background: #ffffff;
        }}
        .markdown-body {{
            padding: 20px 32px;
            max-width: 980px;
            margin: 0 auto;
        }}
        .markdown-body pre {{
            background-color: #f6f8fa;
            border-radius: 6px;
            padding: 16px;
            overflow: auto;
        }}
        .markdown-body code {{
            background-color: #f6f8fa;
            border-radius: 3px;
            padding: 0.2em 0.4em;
            font-size: 85%;
        }}
        .markdown-body pre code {{
            background-color: transparent;
            padding: 0;
        }}
        .markdown-body table {{
            border-collapse: collapse;
            width: 100%;
            margin: 16px 0;
        }}
        .markdown-body table th,
        .markdown-body table td {{
            border: 1px solid #d0d7de;
            padding: 6px 13px;
        }}
        .markdown-body table tr:nth-child(2n) {{
            background-color: #f6f8fa;
        }}
        /* Mermaid diagram containers in markdown */
        .mermaid-container {{
            background: #ffffff;
            border: 1px solid #d0d7de;
            border-radius: 6px;
            padding: 16px;
            margin: 16px 0;
            overflow: auto;
            text-align: center;
        }}
        .mermaid-container svg {{
            max-width: 100%;
            height: auto;
        }}
        .mermaid-error {{
            color: #cf222e;
            background: #fff5f5;
            border: 1px solid #cf222e;
            border-radius: 6px;
            padding: 16px;
            margin: 16px 0;
            font-family: monospace;
            font-size: 13px;
            white-space: pre-wrap;
        }}
    </style>
</head>
<body>
    <article class=""markdown-body"" id=""content""></article>
    <script>
        // Initialize mermaid for rendering embedded diagrams in markdown
        mermaid.initialize({{ 
            startOnLoad: false,
            theme: 'default',
            securityLevel: 'loose',
            fontFamily: 'Segoe UI, Helvetica, Arial, sans-serif'
        }});
        
        // Counter for unique mermaid diagram IDs
        var mermaidCounter = 0;
        
        // Find all mermaid code blocks and render them as diagrams
        async function renderMermaidBlocks() {{
            const content = document.getElementById('content');
            const codeBlocks = content.querySelectorAll('pre code.language-mermaid');
            
            for (const codeBlock of codeBlocks) {{
                const pre = codeBlock.parentElement;
                const mermaidCode = codeBlock.textContent;
                const container = document.createElement('div');
                container.className = 'mermaid-container';
                
                try {{
                    const id = 'mermaid-diagram-' + (mermaidCounter++);
                    const {{ svg }} = await mermaid.render(id, mermaidCode);
                    container.innerHTML = svg;
                }} catch (err) {{
                    container.className = 'mermaid-error';
                    container.textContent = 'Mermaid Error: ' + err.message;
                }}
                
                pre.replaceWith(container);
            }}
        }}
        
        const markdownContent = {escapedCode};
        
        marked.setOptions({{
            highlight: function(code, lang) {{
                if (lang && lang !== 'mermaid' && hljs.getLanguage(lang)) {{
                    try {{
                        return hljs.highlight(code, {{ language: lang }}).value;
                    }} catch (e) {{}}
                }}
                return code;
            }},
            breaks: true,
            gfm: true
        }});
        
        function setupClickHandlers() {{
            const content = document.getElementById('content');
            
            // Add click handlers to headings
            content.querySelectorAll('h1, h2, h3, h4, h5, h6').forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
                    e.stopPropagation();
                    const text = el.textContent.trim();
                    if (text) {{
                        window.chrome.webview.postMessage({{ 
                            type: 'elementClick', 
                            text: text,
                            elementType: 'heading'
                        }});
                    }}
                }});
            }});
            
            // Add click handlers to code blocks
            content.querySelectorAll('pre code').forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
                    e.stopPropagation();
                    // Get first line of code for matching
                    const text = el.textContent.split('\\n')[0].trim();
                    if (text) {{
                        window.chrome.webview.postMessage({{ 
                            type: 'elementClick', 
                            text: text,
                            elementType: 'code'
                        }});
                    }}
                }});
            }});
            
            // Add click handlers to inline code
            content.querySelectorAll('code:not(pre code)').forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
                    e.stopPropagation();
                    const text = el.textContent.trim();
                    if (text) {{
                        window.chrome.webview.postMessage({{ 
                            type: 'elementClick', 
                            text: text,
                            elementType: 'code'
                        }});
                    }}
                }});
            }});
            
            // Add click handlers to list items - handle clicks anywhere in the list item
            content.querySelectorAll('li').forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
                    // Don't navigate if clicking on a link
                    if (e.target.tagName === 'A' || e.target.closest('a')) {{
                        return;
                    }}
                    e.stopPropagation();
                    const text = el.textContent.trim();
                    if (text) {{
                        window.chrome.webview.postMessage({{ 
                            type: 'elementClick', 
                            text: text,
                            elementType: 'listitem'
                        }});
                    }}
                }});
            }});
            
            // Add click handlers to table cells
            content.querySelectorAll('td, th').forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
                    e.stopPropagation();
                    const text = el.textContent.trim();
                    if (text) {{
                        window.chrome.webview.postMessage({{ 
                            type: 'elementClick', 
                            text: text,
                            elementType: 'table'
                        }});
                    }}
                }});
            }});
            
            // Add click handlers to bold text (strong)
            // If inside a list item, use the list item's text for better context
            content.querySelectorAll('strong').forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
                    e.stopPropagation();
                    const listItem = el.closest('li');
                    if (listItem) {{
                        // Use list item text for better matching
                        const text = listItem.textContent.trim();
                        if (text) {{
                            window.chrome.webview.postMessage({{ 
                                type: 'elementClick', 
                                text: text,
                                elementType: 'listitem'
                            }});
                        }}
                    }} else {{
                        const text = el.textContent.trim();
                        if (text) {{
                            window.chrome.webview.postMessage({{ 
                                type: 'elementClick', 
                                text: text,
                                elementType: 'bold'
                            }});
                        }}
                    }}
                }});
            }});
            
            // Add click handlers to italic text (em)
            // If inside a list item, use the list item's text for better context
            content.querySelectorAll('em').forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
                    e.stopPropagation();
                    const listItem = el.closest('li');
                    if (listItem) {{
                        // Use list item text for better matching
                        const text = listItem.textContent.trim();
                        if (text) {{
                            window.chrome.webview.postMessage({{ 
                                type: 'elementClick', 
                                text: text,
                                elementType: 'listitem'
                            }});
                        }}
                    }} else {{
                        const text = el.textContent.trim();
                        if (text) {{
                            window.chrome.webview.postMessage({{ 
                                type: 'elementClick', 
                                text: text,
                                elementType: 'italic'
                            }});
                        }}
                    }}
                }});
            }});
            
            // Add click handlers to paragraphs - handle clicks anywhere in the paragraph
            content.querySelectorAll('p').forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
                    // Don't navigate if clicking on a link (let the link work normally)
                    if (e.target.tagName === 'A' || e.target.closest('a')) {{
                        return;
                    }}
                    e.stopPropagation();
                    const text = el.textContent.trim().substring(0, 50); // First 50 chars
                    if (text) {{
                        window.chrome.webview.postMessage({{ 
                            type: 'elementClick', 
                            text: text,
                            elementType: 'paragraph'
                        }});
                    }}
                }});
            }});
        }}
        
        // Run initial render in async IIFE to properly await mermaid rendering
        (async function() {{
            document.getElementById('content').innerHTML = marked.parse(markdownContent);
            
            // Render any embedded mermaid diagrams
            await renderMermaidBlocks();
            
            // Add click handlers for click-to-highlight feature
            setupClickHandlers();
            
            // Notify C# that markdown is ready and pass target scroll position for restoration
            var targetScrollLeft = {targetScrollLeft};
            var targetScrollTop = {targetScrollTop};
            setTimeout(function() {{
                window.chrome.webview.postMessage({{ 
                    type: 'markdownReady', 
                    targetScrollLeft: targetScrollLeft, 
                    targetScrollTop: targetScrollTop 
                }});
            }}, 50);
        }})();
    </script>
</body>
</html>";

        _isRenderingContent = true;
        PreviewWebView.NavigateToString(html);
        PreviewWebView.WebMessageReceived -= PreviewWebView_WebMessageReceived;
        PreviewWebView.WebMessageReceived += PreviewWebView_WebMessageReceived;
        StatusText.Text = "Markdown rendered";
    }

    private void PreviewWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var message = System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson);
            if (message.RootElement.TryGetProperty("type", out var typeElement))
            {
                var messageType = typeElement.GetString();
                
                if (messageType == "zoom" && message.RootElement.TryGetProperty("level", out var levelElement))
                {
                    _currentZoom = levelElement.GetDouble();
                    // Also update the active document's zoom so it's preserved when switching tabs
                    if (_activeDocument != null)
                    {
                        _activeDocument.PreviewZoom = _currentZoom;
                    }
                    UpdateZoomUI();
                }
                else if (messageType == "pngExport" && message.RootElement.TryGetProperty("data", out var dataElement))
                {
                    var data = dataElement.GetString();
                    _pngExportTcs?.TrySetResult(data ?? "");
                }
                else if (messageType == "pngExportError" && message.RootElement.TryGetProperty("error", out var errorElement))
                {
                    var error = errorElement.GetString();
                    _pngExportTcs?.TrySetException(new Exception(error ?? "Unknown error"));
                }
                else if (messageType == "nodeClick")
                {
                    var nodeId = message.RootElement.TryGetProperty("nodeId", out var nodeIdElement) ? nodeIdElement.GetString() : "";
                    var text = message.RootElement.TryGetProperty("text", out var textElement) ? textElement.GetString() : "";
                    FindAndHighlightInEditor(nodeId, text);
                }
                else if (messageType == "elementClick")
                {
                    var text = message.RootElement.TryGetProperty("text", out var textElement) ? textElement.GetString() : "";
                    var elementType = message.RootElement.TryGetProperty("elementType", out var typeEl) ? typeEl.GetString() : "";
                    FindAndHighlightInEditor("", text, elementType);
                }
                else if (messageType == "diagramReady")
                {
                    // Diagram has finished rendering - restore scroll position from C#
                    var scrollLeft = message.RootElement.TryGetProperty("targetScrollLeft", out var scrollLeftEl) ? scrollLeftEl.GetDouble() : 0;
                    var scrollTop = message.RootElement.TryGetProperty("targetScrollTop", out var scrollTopEl) ? scrollTopEl.GetDouble() : 0;
                    
                    // Only restore if we have non-zero scroll values (indicating we're switching documents)
                    if (scrollLeft > 0 || scrollTop > 0)
                    {
                        _ = RestorePreviewScrollPositionAsync(scrollLeft, scrollTop);
                    }
                }
                else if (messageType == "markdownReady")
                {
                    // Markdown has finished rendering - restore scroll position from C#
                    var scrollLeft = message.RootElement.TryGetProperty("targetScrollLeft", out var scrollLeftEl) ? scrollLeftEl.GetDouble() : 0;
                    var scrollTop = message.RootElement.TryGetProperty("targetScrollTop", out var scrollTopEl) ? scrollTopEl.GetDouble() : 0;
                    
                    // Only restore if we have non-zero scroll values (indicating we're switching documents)
                    if (scrollLeft > 0 || scrollTop > 0)
                    {
                        _ = RestoreMarkdownScrollPositionAsync(scrollLeft, scrollTop);
                    }
                }
            }
        }
        catch
        {
        }
    }
    
    private async Task RestorePreviewScrollPositionAsync(double scrollLeft, double scrollTop)
    {
        if (!_webViewInitialized) return;
        
        try
        {
            // Use ExecuteScriptAsync to set the scroll position from C#
            // This ensures proper timing after the diagram is fully rendered
            await PreviewWebView.CoreWebView2.ExecuteScriptAsync($@"
                (function() {{
                    var container = document.getElementById('container');
                    if (container) {{
                        container.scrollLeft = {scrollLeft.ToString(System.Globalization.CultureInfo.InvariantCulture)};
                        container.scrollTop = {scrollTop.ToString(System.Globalization.CultureInfo.InvariantCulture)};
                    }}
                }})();
            ");
        }
        catch { }
    }
    
    private async Task RestoreMarkdownScrollPositionAsync(double scrollLeft, double scrollTop)
    {
        if (!_webViewInitialized) return;
        
        try
        {
            // Use ExecuteScriptAsync to set the scroll position for markdown
            // Try multiple methods since html/body can both have overflow:auto
            await PreviewWebView.CoreWebView2.ExecuteScriptAsync($@"
                (function() {{
                    var x = {scrollLeft.ToString(System.Globalization.CultureInfo.InvariantCulture)};
                    var y = {scrollTop.ToString(System.Globalization.CultureInfo.InvariantCulture)};
                    // Try all methods to ensure scroll is set
                    window.scrollTo(x, y);
                    document.documentElement.scrollLeft = x;
                    document.documentElement.scrollTop = y;
                    document.body.scrollLeft = x;
                    document.body.scrollTop = y;
                }})();
            ");
        }
        catch { }
    }

    private void FindAndHighlightInEditor(string? nodeId, string? text, string? elementType = null)
    {
        if (string.IsNullOrEmpty(nodeId) && string.IsNullOrEmpty(text)) return;
        
        var sourceCode = CodeEditor.Text;
        var lines = sourceCode.Split('\n');
        int bestLineIndex = -1;
        int bestMatchStart = -1;
        int bestMatchLength = 0;
        
        // Normalize text for searching - handle HTML line breaks and whitespace
        var normalizedText = text?.Replace("\n", " ").Replace("\r", "").Replace("<br>", " ").Replace("<br/>", " ").Trim() ?? "";
        
        // PRIORITY 1: Find by node ID (most reliable for flowchart/state diagrams)
        // This ensures we find the right node even when multiple nodes have the same text (e.g., "Yes", "No")
        if (!string.IsNullOrEmpty(nodeId))
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                
                // Look for node definition: nodeId followed by bracket (e.g., B{, A[, C()
                var nodeIdx = line.IndexOf(nodeId, StringComparison.Ordinal);
                while (nodeIdx >= 0)
                {
                    // Check if this is a word boundary (not part of a longer identifier)
                    bool isWordStart = nodeIdx == 0 || !char.IsLetterOrDigit(line[nodeIdx - 1]);
                    
                    if (isWordStart)
                    {
                        // Look for bracket after the nodeId (possibly with whitespace)
                        var afterNode = nodeIdx + nodeId.Length;
                        while (afterNode < line.Length && char.IsWhiteSpace(line[afterNode]))
                            afterNode++;
                        
                        if (afterNode < line.Length)
                        {
                            var nextChar = line[afterNode];
                            // Check for node definition brackets: [, (, {, <, or : (for state diagrams)
                            if (nextChar == '[' || nextChar == '(' || nextChar == '{' || nextChar == '<' || nextChar == ':')
                            {
                                bestLineIndex = i;
                                bestMatchStart = nodeIdx;
                                
                                // Find the closing bracket to highlight the full node definition
                                var closingBracket = nextChar switch
                                {
                                    '[' => ']',
                                    '(' => ')',
                                    '{' => '}',
                                    '<' => '>',
                                    _ => '\0'
                                };
                                
                                if (closingBracket != '\0')
                                {
                                    var closeIdx = line.IndexOf(closingBracket, afterNode + 1);
                                    if (closeIdx > afterNode)
                                    {
                                        bestMatchLength = closeIdx - nodeIdx + 1; // Include closing bracket
                                    }
                                    else
                                    {
                                        bestMatchLength = line.Length - nodeIdx; // To end of line
                                    }
                                }
                                else
                                {
                                    bestMatchLength = nodeId.Length;
                                }
                                break;
                            }
                        }
                    }
                    
                    nodeIdx = line.IndexOf(nodeId, nodeIdx + 1, StringComparison.Ordinal);
                }
                
                if (bestLineIndex >= 0) break;
            }
            
            // If no definition found, look for references (arrows)
            if (bestLineIndex < 0)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var nodeIdx = line.IndexOf(nodeId, StringComparison.Ordinal);
                    while (nodeIdx >= 0)
                    {
                        bool isWordStart = nodeIdx == 0 || !char.IsLetterOrDigit(line[nodeIdx - 1]);
                        bool isWordEnd = (nodeIdx + nodeId.Length >= line.Length) || !char.IsLetterOrDigit(line[nodeIdx + nodeId.Length]);
                        
                        if (isWordStart && isWordEnd)
                        {
                            // Check if it's near an arrow (flowchart or sequence diagram)
                            var before = nodeIdx > 4 ? line.Substring(Math.Max(0, nodeIdx - 5), Math.Min(5, nodeIdx)) : "";
                            var afterEnd = nodeIdx + nodeId.Length;
                            var after = afterEnd < line.Length ? line.Substring(afterEnd, Math.Min(5, line.Length - afterEnd)) : "";
                            
                            if (before.Contains("-->") || before.Contains("---") || before.Contains("-.-") ||
                                before.Contains("->>") || before.Contains("-->>") || before.Contains("-x") ||
                                after.StartsWith("-->") || after.StartsWith("---") || after.StartsWith("-.-") ||
                                after.StartsWith("->>") || after.StartsWith("-->>") || after.StartsWith("-x") ||
                                after.StartsWith(" -->") || after.StartsWith(" ---") || after.StartsWith(" ->>"))
                            {
                                bestLineIndex = i;
                                bestMatchStart = nodeIdx;
                                bestMatchLength = nodeId.Length;
                                break;
                            }
                        }
                        nodeIdx = line.IndexOf(nodeId, nodeIdx + 1, StringComparison.Ordinal);
                    }
                    if (bestLineIndex >= 0) break;
                }
            }
        }
        
        // PRIORITY 2: Find by text content
        if (bestLineIndex < 0 && !string.IsNullOrEmpty(normalizedText))
        {
            // For Markdown with element type hints
            if (!string.IsNullOrEmpty(elementType))
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    
                    if (elementType == "heading" && line.TrimStart().StartsWith("#") && line.Contains(normalizedText, StringComparison.OrdinalIgnoreCase))
                    {
                        bestLineIndex = i;
                        var idx = line.IndexOf(normalizedText, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0) { bestMatchStart = idx; bestMatchLength = normalizedText.Length; }
                        break;
                    }
                    else if (elementType == "code" && line.Contains(normalizedText))
                    {
                        bestLineIndex = i;
                        var idx = line.IndexOf(normalizedText);
                        if (idx >= 0) { bestMatchStart = idx; bestMatchLength = normalizedText.Length; }
                        break;
                    }
                    else if ((elementType == "bold" || elementType == "italic") && line.Contains(normalizedText, StringComparison.OrdinalIgnoreCase))
                    {
                        bestLineIndex = i;
                        var idx = line.IndexOf(normalizedText, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0) { bestMatchStart = idx; bestMatchLength = normalizedText.Length; }
                        break;
                    }
                    else if (elementType == "listitem")
                    {
                        // For list items, look for lines starting with - or * and containing the text
                        var trimmedLine = line.TrimStart();
                        if ((trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* ") || trimmedLine.StartsWith("+ ")) &&
                            LineContainsListItemText(line, normalizedText))
                        {
                            bestLineIndex = i;
                            bestMatchStart = 0;
                            bestMatchLength = line.Length;
                            break;
                        }
                    }
                    else if (elementType == "paragraph")
                    {
                        // For paragraphs, search for any part of the text (handles Markdown syntax differences)
                        // Try to find a line that contains a significant portion of the text
                        var searchText = normalizedText.Length > 20 ? normalizedText.Substring(0, 20) : normalizedText;
                        if (line.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            bestLineIndex = i;
                            var idx = line.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
                            if (idx >= 0) { bestMatchStart = idx; bestMatchLength = searchText.Length; }
                            break;
                        }
                    }
                    else if (string.IsNullOrEmpty(elementType) || elementType == "text")
                    {
                        if (line.Contains(normalizedText, StringComparison.OrdinalIgnoreCase))
                        {
                            bestLineIndex = i;
                            var idx = line.IndexOf(normalizedText, StringComparison.OrdinalIgnoreCase);
                            if (idx >= 0) { bestMatchStart = idx; bestMatchLength = normalizedText.Length; }
                            break;
                        }
                    }
                }
            }
            else
            {
                // For Mermaid diagrams - comprehensive search with priority ordering
                // Priority: exact matches first, then partial matches
                
                // PASS 1: Look for participant/actor definitions (exact match on name or alias)
                // This must come first so clicking on actor boxes finds the definition, not text containing the name
                string[] actorKeywords = { "participant ", "actor " };
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var trimmedLine = line.TrimStart();
                    
                    foreach (var keyword in actorKeywords)
                    {
                        if (trimmedLine.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                        {
                            var afterKeyword = trimmedLine.Substring(keyword.Length).Trim();
                            // Handle "participant A as Alice" format
                            var asIdx = afterKeyword.IndexOf(" as ", StringComparison.OrdinalIgnoreCase);
                            string actorName = asIdx > 0 ? afterKeyword.Substring(0, asIdx) : afterKeyword;
                            string actorAlias = asIdx > 0 ? afterKeyword.Substring(asIdx + 4).Trim() : "";
                            
                            if (actorName.Equals(normalizedText, StringComparison.OrdinalIgnoreCase) ||
                                actorAlias.Equals(normalizedText, StringComparison.OrdinalIgnoreCase))
                            {
                                bestLineIndex = i;
                                var keywordIdx = line.IndexOf(keyword.TrimEnd(), StringComparison.OrdinalIgnoreCase);
                                if (keywordIdx >= 0)
                                {
                                    bestMatchStart = keywordIdx;
                                    bestMatchLength = line.Length - keywordIdx;
                                }
                                break;
                            }
                        }
                    }
                    if (bestLineIndex >= 0) break;
                }
                
                // PASS 2: Look for sequence diagram keywords (alt, else, loop, opt, par, critical, break, Note)
                // Requires exact match or text starts with the search text
                if (bestLineIndex < 0)
                {
                    string[] sequenceKeywords = { "alt ", "else ", "loop ", "opt ", "par ", "critical ", "break ", "Note over ", "Note left of ", "Note right of " };
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        var trimmedLine = line.TrimStart();
                        
                        foreach (var keyword in sequenceKeywords)
                        {
                            if (trimmedLine.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                            {
                                // Check if the text after the keyword matches exactly
                                var afterKeyword = trimmedLine.Substring(keyword.Length).Trim();
                                // For Note, handle the colon separator
                                var colonIdx = afterKeyword.IndexOf(':');
                                var noteText = colonIdx > 0 ? afterKeyword.Substring(colonIdx + 1).Trim() : afterKeyword;
                                
                                if (afterKeyword.Equals(normalizedText, StringComparison.OrdinalIgnoreCase) ||
                                    noteText.Equals(normalizedText, StringComparison.OrdinalIgnoreCase))
                                {
                                    bestLineIndex = i;
                                    var keywordIdx = line.IndexOf(keyword.TrimEnd(), StringComparison.OrdinalIgnoreCase);
                                    if (keywordIdx >= 0)
                                    {
                                        bestMatchStart = keywordIdx;
                                        bestMatchLength = line.Length - keywordIdx;
                                    }
                                    break;
                                }
                            }
                        }
                        if (bestLineIndex >= 0) break;
                    }
                }
                
                // PASS 3: Look for text in brackets [text], (text), {text}, "text" - exact match
                if (bestLineIndex < 0)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        var textIdx = line.IndexOf(normalizedText, StringComparison.OrdinalIgnoreCase);
                        
                        if (textIdx >= 0)
                        {
                            // Check if it's inside brackets
                            var beforeText = textIdx > 0 ? line[textIdx - 1] : ' ';
                            var afterIdx = textIdx + normalizedText.Length;
                            var afterText = afterIdx < line.Length ? line[afterIdx] : ' ';
                            
                            // Check for common bracket pairs
                            bool inBrackets = (beforeText == '[' && afterText == ']') ||
                                             (beforeText == '(' && afterText == ')') ||
                                             (beforeText == '{' && afterText == '}') ||
                                             (beforeText == '"' && afterText == '"') ||
                                             (beforeText == '\'' && afterText == '\'');
                            
                            if (inBrackets)
                            {
                                bestLineIndex = i;
                                bestMatchStart = textIdx;
                                bestMatchLength = normalizedText.Length;
                                break;
                            }
                        }
                    }
                }
                
                // PASS 4: Look for sequence diagram messages (text after colon) - exact match only
                // This comes after brackets and actors to avoid matching partial text
                if (bestLineIndex < 0)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        var colonIdx = line.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            var afterColon = line.Substring(colonIdx + 1).Trim();
                            // Only match if the text after colon equals the search text exactly
                            if (afterColon.Equals(normalizedText, StringComparison.OrdinalIgnoreCase))
                            {
                                bestLineIndex = i;
                                var textIdx = line.IndexOf(normalizedText, StringComparison.OrdinalIgnoreCase);
                                if (textIdx >= 0)
                                {
                                    bestMatchStart = textIdx;
                                    bestMatchLength = normalizedText.Length;
                                }
                                else
                                {
                                    bestMatchStart = colonIdx + 1;
                                    bestMatchLength = line.Length - colonIdx - 1;
                                }
                                break;
                            }
                        }
                    }
                }
                
                // PASS 5: Look for messages containing the text (partial match, but text must be > 3 chars)
                // This allows finding longer messages but avoids matching short words like "User" in "User interaction"
                if (bestLineIndex < 0 && normalizedText.Length > 10)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        var colonIdx = line.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            var afterColon = line.Substring(colonIdx + 1).Trim();
                            if (afterColon.Contains(normalizedText, StringComparison.OrdinalIgnoreCase))
                            {
                                bestLineIndex = i;
                                var textIdx = line.IndexOf(normalizedText, StringComparison.OrdinalIgnoreCase);
                                if (textIdx >= 0)
                                {
                                    bestMatchStart = textIdx;
                                    bestMatchLength = normalizedText.Length;
                                }
                                break;
                            }
                        }
                    }
                }
                
                // PASS 6: Plain text search as final fallback
                if (bestLineIndex < 0)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (line.Contains(normalizedText, StringComparison.OrdinalIgnoreCase))
                        {
                            bestLineIndex = i;
                            var idx = line.IndexOf(normalizedText, StringComparison.OrdinalIgnoreCase);
                            if (idx >= 0) { bestMatchStart = idx; bestMatchLength = normalizedText.Length; }
                            break;
                        }
                    }
                }
            }
        }
        
        // If we found a match, scroll to it and highlight
        if (bestLineIndex >= 0)
        {
            // Calculate the offset in the document
            int offset = 0;
            for (int i = 0; i < bestLineIndex; i++)
            {
                offset += lines[i].Length + 1; // +1 for newline
            }
            
            if (bestMatchStart >= 0)
            {
                offset += bestMatchStart;
            }
            
            // Scroll to the line and select the text
            CodeEditor.ScrollToLine(bestLineIndex + 1); // Lines are 1-indexed
            
            if (bestMatchLength > 0 && bestMatchStart >= 0)
            {
                CodeEditor.Select(offset, bestMatchLength);
            }
            else
            {
                // Just move the caret to the line
                CodeEditor.TextArea.Caret.Offset = offset;
            }
            
            // Focus the editor
            CodeEditor.Focus();
            
            StatusText.Text = $"Found at line {bestLineIndex + 1}";
        }
        else
        {
            StatusText.Text = "Element not found in source";
        }
    }

    /// <summary>
    /// Helper method to check if a Markdown list item line contains the rendered text.
    /// Handles Markdown syntax like **bold**, *italic*, [links](url), etc.
    /// </summary>
    private bool LineContainsListItemText(string line, string renderedText)
    {
        // The rendered text is what appears in the browser (without Markdown syntax)
        // We need to check if the line contains the key words from the rendered text
        
        // Split the rendered text into words and check if the line contains them
        var words = renderedText.Split(new[] { ' ', '-', ':', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Check if the line contains at least the first few significant words
        int matchCount = 0;
        foreach (var word in words.Take(5))
        {
            if (word.Length > 2 && line.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
            }
        }
        
        // If we match at least 2 words (or all words if less than 2), consider it a match
        return matchCount >= Math.Min(2, words.Length);
    }

    private void UpdateTitle()
    {
        var fileName = string.IsNullOrEmpty(_currentFilePath) ? "Untitled" : System.IO.Path.GetFileName(_currentFilePath);
        var modeIndicator = _currentRenderMode == RenderMode.Markdown ? " [Markdown]" : " [Mermaid]";
        Title = $"{fileName}{(_isDirty ? "*" : "")}{modeIndicator} - Mermaid Editor";
        FilePathText.Text = _currentFilePath ?? "Untitled";
    }

    private void SetRenderModeFromFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        _currentRenderMode = ext == ".md" ? RenderMode.Markdown : RenderMode.Mermaid;
        
        // Update syntax highlighting based on mode
        if (_currentRenderMode == RenderMode.Markdown)
        {
            RegisterMarkdownSyntaxHighlighting();
        }
        else
        {
            CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Mermaid");
        }
        
        // Update export menu visibility based on file type
        UpdateExportMenuVisibility();
        UpdateMarkdownFormattingVisibility();
        UpdateZoomControlsVisibility();

        // Update spell check mermaid flag when switching file types
        if (_spellCheckRenderer != null)
        {
            _spellCheckRenderer.IsMermaid = _currentRenderMode == RenderMode.Mermaid;
            if (_isSpellCheckEnabled)
            {
                _spellCheckRenderer.InvalidateSpelling();
            }
        }

        // Update visual editor mode toolbar visibility
        UpdateVisualEditorModeToolbarVisibility();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        // Show template selection dialog
        var dialog = new NewDocumentDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            if (dialog.SelectedRecentFilePath != null)
            {
                // User selected a recent file
                OpenFileInTab(dialog.SelectedRecentFilePath);
                StatusText.Text = "File opened";
            }
            else if (dialog.OpenExistingFile)
            {
                // User wants to browse for a file
                Open_Click(sender, e);
            }
            else if (dialog.SelectedTemplate != null)
            {
                // Create a new document with the selected template
                var doc = CreateNewDocument(null, dialog.SelectedTemplate);
                doc.RenderMode = dialog.IsMermaid ? RenderMode.Mermaid : RenderMode.Markdown;
                SwitchToDocument(doc);
                
                // Update syntax highlighting
                if (dialog.IsMermaid)
                {
                    CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Mermaid");
                }
                else
                {
                    RegisterMarkdownSyntaxHighlighting();
                }
                
                UpdateExportMenuVisibility();
                
                // Auto fit to window after template loads
                _ = Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(500); // Wait for render to complete
                    if (_webViewInitialized)
                    {
                        await PreviewWebView.CoreWebView2.ExecuteScriptAsync("window.fitToWindow()");
                    }
                });
            }
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Mermaid & Markdown (*.mmd;*.mermaid;*.md)|*.mmd;*.mermaid;*.md|Mermaid Files (*.mmd;*.mermaid)|*.mmd;*.mermaid|Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
            Title = "Open File"
        };

        if (dialog.ShowDialog() == true)
        {
            OpenFileInTab(dialog.FileName);
            StatusText.Text = "File opened";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDocument == null) return;
        
        if (SaveDocument(_activeDocument))
        {
            _currentFilePath = _activeDocument.FilePath;
            _isDirty = _activeDocument.IsDirty;
            UpdateTitle();
            StatusText.Text = "File saved";
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDocument == null) return;
        
        var dialog = new SaveFileDialog
        {
            Filter = _activeDocument.RenderMode == RenderMode.Markdown
                ? "Markdown Files (*.md)|*.md|All Files (*.*)|*.*"
                : "Mermaid Files (*.mmd)|*.mmd|All Files (*.*)|*.*",
            Title = "Save File",
            DefaultExt = _activeDocument.RenderMode == RenderMode.Markdown ? ".md" : ".mmd"
        };

        if (dialog.ShowDialog() == true)
        {
            _activeDocument.FilePath = dialog.FileName;
            _currentFilePath = dialog.FileName;
            // Update export directory to match where the file was saved
            UpdateLastExportDirectory(dialog.FileName);
            Save_Click(sender, e);
        }
    }

    private string GetExportDefaultPath(string extension)
    {
        // Use last export directory if set, otherwise use current file's directory
        var directory = _lastExportDirectory;
        
        if (string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(_currentFilePath))
        {
            directory = Path.GetDirectoryName(_currentFilePath);
        }
        
        if (string.IsNullOrEmpty(directory))
        {
            directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
        
        // Use current file's name with new extension, or default name
        var filename = "diagram" + extension;
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            var baseName = Path.GetFileNameWithoutExtension(_currentFilePath);
            if (!string.IsNullOrEmpty(baseName))
            {
                filename = baseName + extension;
            }
        }
        
        // Return full path - this works more reliably with SaveFileDialog than setting
        // InitialDirectory and FileName separately
        return Path.Combine(directory!, filename);
    }

    private void UpdateLastExportDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _lastExportDirectory = directory;
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (CodeEditor.CanUndo)
        {
            CodeEditor.Undo();
            UpdateUndoRedoState();
        }
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (CodeEditor.CanRedo)
        {
            CodeEditor.Redo();
            UpdateUndoRedoState();
        }
    }

    #region Find and Replace
    
    private FindReplaceDialog? _findReplaceDialog;
    
    private void Find_Click(object sender, RoutedEventArgs e)
    {
        // Open Find-only dialog (no Replace section)
        OpenFindDialog(showReplace: false);
    }
    
    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        // If dialog is open, trigger find next; otherwise open Find dialog
        if (_findReplaceDialog != null && _findReplaceDialog.IsLoaded)
        {
            _findReplaceDialog.TriggerFindNext();
        }
        else
        {
            OpenFindDialog(showReplace: false);
        }
    }
    
    private void FindReplace_Click(object sender, RoutedEventArgs e)
    {
        // Open full Find and Replace dialog
        OpenFindDialog(showReplace: true);
    }
    
    private void OpenFindDialog(bool showReplace)
    {
        // Close existing dialog if switching modes
        if (_findReplaceDialog != null && _findReplaceDialog.IsLoaded)
        {
            _findReplaceDialog.Activate();
            return;
        }
        
        _findReplaceDialog = new FindReplaceDialog(CodeEditor, showReplace) { Owner = this };
        _findReplaceDialog.Show();
    }
    
    #endregion

    #region Markdown Formatting
    
    private void UpdateMarkdownFormattingVisibility()
    {
        var isMarkdown = _currentRenderMode == RenderMode.Markdown;
        FormatMenu.Visibility = isMarkdown ? Visibility.Visible : Visibility.Collapsed;
        MarkdownFormattingToolbar.Visibility = isMarkdown ? Visibility.Visible : Visibility.Collapsed;
    }
    
    private void UpdateZoomControlsVisibility()
    {
        var isMermaid = _currentRenderMode == RenderMode.Mermaid;
        ZoomControlsPanel.Visibility = isMermaid ? Visibility.Visible : Visibility.Collapsed;
    }
    
    private void FormatBold_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        WrapSelectedText("**", "**");
    }
    
    private void FormatItalic_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        WrapSelectedText("*", "*");
    }
    
    private void FormatStrikethrough_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        WrapSelectedText("~~", "~~");
    }
    
    private void FormatH1_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        ApplyLinePrefix("# ");
    }
    
    private void FormatH2_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        ApplyLinePrefix("## ");
    }
    
    private void FormatH3_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        ApplyLinePrefix("### ");
    }
    
    private void FormatH4_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        ApplyLinePrefix("#### ");
    }
    
    private void FormatInlineCode_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        WrapSelectedText("`", "`");
    }
    
    private void FormatCodeBlock_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        WrapSelectedTextMultiline("```\n", "\n```");
    }
    
    private void FormatBulletList_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        ApplyLinePrefix("- ");
    }
    
    private void FormatNumberedList_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        ApplyLinePrefix("1. ");
    }
    
    private void FormatQuote_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        ApplyLinePrefix("> ");
    }
    
    private void FormatLink_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        
        var selectedText = CodeEditor.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
        {
            InsertText("[link text](url)");
        }
        else
        {
            WrapSelectedText("[", "](url)");
        }
    }
    
    private void FormatImage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        
        var selectedText = CodeEditor.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
        {
            InsertText("![alt text](image-url)");
        }
        else
        {
            WrapSelectedText("![", "](image-url)");
        }
    }
    
    private void FormatHorizontalRule_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        
        var doc = CodeEditor.Document;
        var caretOffset = CodeEditor.CaretOffset;
        var line = doc.GetLineByOffset(caretOffset);
        
        // Insert horizontal rule on a new line
        var insertText = "\n\n---\n\n";
        if (line.Offset == caretOffset && caretOffset > 0)
        {
            // Already at start of line
            insertText = "\n---\n\n";
        }
        
        doc.Insert(caretOffset, insertText);
        CodeEditor.CaretOffset = caretOffset + insertText.Length;
    }
    
    private void InsertTable_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRenderMode != RenderMode.Markdown) return;
        
        var dialog = new TableGeneratorDialog { Owner = this };
        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.GeneratedMarkdown))
        {
            var doc = CodeEditor.Document;
            var caretOffset = CodeEditor.CaretOffset;
            var line = doc.GetLineByOffset(caretOffset);
            
            // Ensure table starts on a new line with blank line before it
            var prefix = "";
            if (caretOffset > 0)
            {
                var lineText = doc.GetText(line.Offset, line.Length);
                if (!string.IsNullOrWhiteSpace(lineText))
                {
                    prefix = "\n\n";
                }
                else if (line.Offset > 0)
                {
                    prefix = "\n";
                }
            }
            
            var tableText = prefix + dialog.GeneratedMarkdown;
            doc.Insert(caretOffset, tableText);
            CodeEditor.CaretOffset = caretOffset + tableText.Length;
        }
    }
    
    private void WrapSelectedText(string prefix, string suffix)
    {
        var doc = CodeEditor.Document;
        var selectedText = CodeEditor.SelectedText;
        var selectionStart = CodeEditor.SelectionStart;
        var selectionLength = CodeEditor.SelectionLength;
        
        if (selectionLength > 0)
        {
            // Wrap selected text
            var newText = prefix + selectedText + suffix;
            doc.Replace(selectionStart, selectionLength, newText);
            // Select the wrapped text (excluding markers)
            CodeEditor.Select(selectionStart + prefix.Length, selectedText.Length);
        }
        else
        {
            // No selection - insert markers and place cursor between them
            var caretOffset = CodeEditor.CaretOffset;
            doc.Insert(caretOffset, prefix + suffix);
            CodeEditor.CaretOffset = caretOffset + prefix.Length;
        }
    }
    
    private void WrapSelectedTextMultiline(string prefix, string suffix)
    {
        var doc = CodeEditor.Document;
        var selectedText = CodeEditor.SelectedText;
        var selectionStart = CodeEditor.SelectionStart;
        var selectionLength = CodeEditor.SelectionLength;
        
        if (selectionLength > 0)
        {
            // Wrap selected text
            var newText = prefix + selectedText + suffix;
            doc.Replace(selectionStart, selectionLength, newText);
            // Place cursor after the block
            CodeEditor.CaretOffset = selectionStart + newText.Length;
        }
        else
        {
            // No selection - insert block and place cursor inside
            var caretOffset = CodeEditor.CaretOffset;
            doc.Insert(caretOffset, prefix + suffix);
            CodeEditor.CaretOffset = caretOffset + prefix.Length;
        }
    }
    
    private void ApplyLinePrefix(string prefix)
    {
        var doc = CodeEditor.Document;
        var selectionStart = CodeEditor.SelectionStart;
        var selectionLength = CodeEditor.SelectionLength;
        
        if (selectionLength > 0)
        {
            // Apply prefix to each selected line
            var startLine = doc.GetLineByOffset(selectionStart);
            var endLine = doc.GetLineByOffset(selectionStart + selectionLength);
            
            // Process lines from bottom to top to preserve offsets
            for (int lineNum = endLine.LineNumber; lineNum >= startLine.LineNumber; lineNum--)
            {
                var line = doc.GetLineByNumber(lineNum);
                var lineText = doc.GetText(line.Offset, line.Length);
                
                // Remove existing heading/list prefixes before applying new one
                var trimmedText = RemoveExistingPrefix(lineText);
                var newLineText = prefix + trimmedText;
                
                doc.Replace(line.Offset, line.Length, newLineText);
            }
        }
        else
        {
            // Apply to current line
            var line = doc.GetLineByOffset(CodeEditor.CaretOffset);
            var lineText = doc.GetText(line.Offset, line.Length);
            
            // Remove existing heading/list prefixes before applying new one
            var trimmedText = RemoveExistingPrefix(lineText);
            var newLineText = prefix + trimmedText;
            
            doc.Replace(line.Offset, line.Length, newLineText);
            CodeEditor.CaretOffset = line.Offset + newLineText.Length;
        }
    }
    
    private string RemoveExistingPrefix(string text)
    {
        // Remove existing markdown prefixes (headings, lists, quotes)
        var patterns = new[]
        {
            @"^#{1,6}\s+",      // Headings
            @"^[-*+]\s+",       // Unordered lists
            @"^\d+\.\s+",       // Ordered lists
            @"^>\s*"            // Blockquotes
        };
        
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success)
            {
                return text.Substring(match.Length);
            }
        }
        
        return text;
    }
    
    private void InsertText(string text)
    {
        var doc = CodeEditor.Document;
        var caretOffset = CodeEditor.CaretOffset;
        doc.Insert(caretOffset, text);
        CodeEditor.CaretOffset = caretOffset + text.Length;
    }
    
    #endregion

    #region Edit Toolbar (Indent, Comment, Move Lines, Word Wrap)

    private void IndentLines_Click(object sender, RoutedEventArgs e)
    {
        var doc = CodeEditor.Document;
        var selection = CodeEditor.TextArea.Selection;
        
        if (selection.IsEmpty)
        {
            // Indent current line
            var line = doc.GetLineByOffset(CodeEditor.CaretOffset);
            doc.Insert(line.Offset, "    ");
        }
        else
        {
            // Indent all selected lines
            var startLine = doc.GetLineByOffset(selection.SurroundingSegment.Offset);
            var endLine = doc.GetLineByOffset(selection.SurroundingSegment.EndOffset);
            
            using (doc.RunUpdate())
            {
                for (int lineNum = startLine.LineNumber; lineNum <= endLine.LineNumber; lineNum++)
                {
                    var line = doc.GetLineByNumber(lineNum);
                    doc.Insert(line.Offset, "    ");
                }
            }
        }
    }

    private void OutdentLines_Click(object sender, RoutedEventArgs e)
    {
        var doc = CodeEditor.Document;
        var selection = CodeEditor.TextArea.Selection;
        
        if (selection.IsEmpty)
        {
            // Outdent current line
            var line = doc.GetLineByOffset(CodeEditor.CaretOffset);
            RemoveLeadingIndent(doc, line);
        }
        else
        {
            // Outdent all selected lines
            var startLine = doc.GetLineByOffset(selection.SurroundingSegment.Offset);
            var endLine = doc.GetLineByOffset(selection.SurroundingSegment.EndOffset);
            
            using (doc.RunUpdate())
            {
                for (int lineNum = startLine.LineNumber; lineNum <= endLine.LineNumber; lineNum++)
                {
                    var line = doc.GetLineByNumber(lineNum);
                    RemoveLeadingIndent(doc, line);
                }
            }
        }
    }

    private void RemoveLeadingIndent(ICSharpCode.AvalonEdit.Document.TextDocument doc, ICSharpCode.AvalonEdit.Document.DocumentLine line)
    {
        var lineText = doc.GetText(line.Offset, line.Length);
        
        // Remove up to 4 spaces or 1 tab from the beginning
        if (lineText.StartsWith("    "))
        {
            doc.Remove(line.Offset, 4);
        }
        else if (lineText.StartsWith("\t"))
        {
            doc.Remove(line.Offset, 1);
        }
        else if (lineText.StartsWith("   "))
        {
            doc.Remove(line.Offset, 3);
        }
        else if (lineText.StartsWith("  "))
        {
            doc.Remove(line.Offset, 2);
        }
        else if (lineText.StartsWith(" "))
        {
            doc.Remove(line.Offset, 1);
        }
    }

    private void ToggleComment_Click(object sender, RoutedEventArgs e)
    {
        var doc = CodeEditor.Document;
        var selection = CodeEditor.TextArea.Selection;
        
        if (_currentRenderMode == RenderMode.Mermaid)
        {
            // Mermaid uses line-based comments (%%)
            ToggleMermaidComment(doc, selection);
        }
        else
        {
            // Markdown uses block comments (<!-- -->)
            ToggleMarkdownComment(doc, selection);
        }
        
        // Force-refresh the comment icon color since the text changed but
        // the caret position may not have moved (so PositionChanged won't fire)
        UpdateToggleCommentIconColor(force: true);
    }

    private void ToggleMermaidComment(ICSharpCode.AvalonEdit.Document.TextDocument doc, ICSharpCode.AvalonEdit.Editing.Selection selection)
    {
        string commentPrefix = "%% ";
        
        if (selection.IsEmpty)
        {
            // Toggle comment on current line
            var line = doc.GetLineByOffset(CodeEditor.CaretOffset);
            ToggleMermaidLineComment(doc, line, commentPrefix);
        }
        else
        {
            // Toggle comment on all selected lines
            var startLine = doc.GetLineByOffset(selection.SurroundingSegment.Offset);
            var endLine = doc.GetLineByOffset(selection.SurroundingSegment.EndOffset);
            
            // Check if all lines are already commented
            bool allCommented = true;
            for (int lineNum = startLine.LineNumber; lineNum <= endLine.LineNumber; lineNum++)
            {
                var line = doc.GetLineByNumber(lineNum);
                var lineText = doc.GetText(line.Offset, line.Length).TrimStart();
                if (!lineText.StartsWith("%%"))
                {
                    allCommented = false;
                    break;
                }
            }
            
            using (doc.RunUpdate())
            {
                for (int lineNum = endLine.LineNumber; lineNum >= startLine.LineNumber; lineNum--)
                {
                    var line = doc.GetLineByNumber(lineNum);
                    if (allCommented)
                    {
                        UncommentMermaidLine(doc, line);
                    }
                    else
                    {
                        CommentMermaidLine(doc, line, commentPrefix);
                    }
                }
            }
        }
    }

    private void ToggleMermaidLineComment(ICSharpCode.AvalonEdit.Document.TextDocument doc, ICSharpCode.AvalonEdit.Document.DocumentLine line, string prefix)
    {
        var lineText = doc.GetText(line.Offset, line.Length);
        var trimmedText = lineText.TrimStart();
        
        if (trimmedText.StartsWith("%%"))
        {
            UncommentMermaidLine(doc, line);
        }
        else
        {
            CommentMermaidLine(doc, line, prefix);
        }
    }

    private void CommentMermaidLine(ICSharpCode.AvalonEdit.Document.TextDocument doc, ICSharpCode.AvalonEdit.Document.DocumentLine line, string prefix)
    {
        var lineText = doc.GetText(line.Offset, line.Length);
        var trimmedText = lineText.TrimStart();
        var leadingWhitespace = lineText.Substring(0, lineText.Length - trimmedText.Length);
        
        string newText = leadingWhitespace + prefix + trimmedText;
        doc.Replace(line.Offset, line.Length, newText);
    }

    private void UncommentMermaidLine(ICSharpCode.AvalonEdit.Document.TextDocument doc, ICSharpCode.AvalonEdit.Document.DocumentLine line)
    {
        var lineText = doc.GetText(line.Offset, line.Length);
        var trimmedText = lineText.TrimStart();
        var leadingWhitespace = lineText.Substring(0, lineText.Length - trimmedText.Length);
        
        string newText;
        if (trimmedText.StartsWith("%% "))
        {
            newText = leadingWhitespace + trimmedText.Substring(3);
        }
        else if (trimmedText.StartsWith("%%"))
        {
            newText = leadingWhitespace + trimmedText.Substring(2);
        }
        else
        {
            newText = lineText;
        }
        
        doc.Replace(line.Offset, line.Length, newText);
    }

    private void ToggleMarkdownComment(ICSharpCode.AvalonEdit.Document.TextDocument doc, ICSharpCode.AvalonEdit.Editing.Selection selection)
    {
        if (selection.IsEmpty)
        {
            // No selection - try to find surrounding comment block or comment current line
            int caretOffset = CodeEditor.CaretOffset;
            string fullText = doc.Text;
            
            // Look for <!-- before cursor and --> after cursor
            int commentStart = fullText.LastIndexOf("<!--", caretOffset);
            int commentEnd = fullText.IndexOf("-->", caretOffset);
            
            // Also check if cursor is right after <!-- or right before -->
            if (commentStart == -1 && caretOffset >= 4)
            {
                // Check if we're inside the <!-- marker itself
                int checkStart = Math.Max(0, caretOffset - 4);
                string nearText = fullText.Substring(checkStart, Math.Min(8, fullText.Length - checkStart));
                int markerPos = nearText.IndexOf("<!--");
                if (markerPos >= 0)
                {
                    commentStart = checkStart + markerPos;
                }
            }
            
            if (commentStart >= 0 && commentEnd >= 0 && commentEnd > commentStart)
            {
                // Check if there's no --> between commentStart and cursor (meaning we're inside a comment)
                string textBetween = fullText.Substring(commentStart + 4, caretOffset - commentStart - 4);
                if (!textBetween.Contains("-->"))
                {
                    // We're inside a comment block - uncomment it
                    using (doc.RunUpdate())
                    {
                        // Remove --> first (to preserve offsets)
                        doc.Remove(commentEnd, 3);
                        // Remove <!-- (and optional space after)
                        int removeLength = 4;
                        if (commentStart + 4 < doc.TextLength && doc.GetCharAt(commentStart + 4) == ' ')
                        {
                            removeLength = 5;
                        }
                        doc.Remove(commentStart, removeLength);
                    }
                    return;
                }
            }
            
            // Not inside a comment - comment the current line
            var line = doc.GetLineByOffset(caretOffset);
            var lineText = doc.GetText(line.Offset, line.Length);
            var trimmedText = lineText.TrimStart();
            var leadingWhitespace = lineText.Substring(0, lineText.Length - trimmedText.Length);
            
            // Check if line is already a single-line comment
            if (trimmedText.StartsWith("<!--") && trimmedText.TrimEnd().EndsWith("-->"))
            {
                // Uncomment single line
                string content = trimmedText;
                if (content.StartsWith("<!-- "))
                    content = content.Substring(5);
                else if (content.StartsWith("<!--"))
                    content = content.Substring(4);
                    
                if (content.TrimEnd().EndsWith(" -->"))
                    content = content.Substring(0, content.TrimEnd().Length - 4);
                else if (content.TrimEnd().EndsWith("-->"))
                    content = content.Substring(0, content.TrimEnd().Length - 3);
                
                doc.Replace(line.Offset, line.Length, leadingWhitespace + content);
            }
            else
            {
                // Comment single line
                string newText = leadingWhitespace + "<!-- " + trimmedText + " -->";
                doc.Replace(line.Offset, line.Length, newText);
            }
        }
        else
        {
            // Has selection - use block comment for the entire selection
            int startOffset = selection.SurroundingSegment.Offset;
            int endOffset = selection.SurroundingSegment.EndOffset;
            string selectedText = doc.GetText(startOffset, endOffset - startOffset);
            
            // Check if selection is already wrapped in a comment
            string trimmedSelection = selectedText.Trim();
            if (trimmedSelection.StartsWith("<!--") && trimmedSelection.EndsWith("-->"))
            {
                // Uncomment - remove the outer <!-- and -->
                int commentStartInSelection = selectedText.IndexOf("<!--");
                int commentEndInSelection = selectedText.LastIndexOf("-->");
                
                if (commentStartInSelection >= 0 && commentEndInSelection >= 0)
                {
                    using (doc.RunUpdate())
                    {
                        // Calculate actual positions
                        int actualCommentStart = startOffset + commentStartInSelection;
                        int actualCommentEnd = startOffset + commentEndInSelection;
                        
                        // Remove --> first
                        doc.Remove(actualCommentEnd, 3);
                        
                        // Remove <!-- (and optional space)
                        int removeLength = 4;
                        if (actualCommentStart + 4 < doc.TextLength && doc.GetCharAt(actualCommentStart + 4) == ' ')
                        {
                            removeLength = 5;
                        }
                        doc.Remove(actualCommentStart, removeLength);
                    }
                }
            }
            else
            {
                // Comment - wrap entire selection with single <!-- -->
                using (doc.RunUpdate())
                {
                    doc.Insert(endOffset, " -->");
                    doc.Insert(startOffset, "<!-- ");
                }
            }
        }
    }

    private bool IsLineCommented(string lineText, string prefix, string suffix)
    {
        if (_currentRenderMode == RenderMode.Mermaid)
        {
            return lineText.StartsWith("%%");
        }
        else
        {
            return lineText.StartsWith("<!--") && lineText.TrimEnd().EndsWith("-->");
        }
    }

    private void ToggleLineComment(ICSharpCode.AvalonEdit.Document.TextDocument doc, ICSharpCode.AvalonEdit.Document.DocumentLine line, string prefix, string suffix)
    {
        var lineText = doc.GetText(line.Offset, line.Length);
        var trimmedText = lineText.TrimStart();
        var leadingWhitespace = lineText.Substring(0, lineText.Length - trimmedText.Length);
        
        if (IsLineCommented(trimmedText, prefix, suffix))
        {
            UncommentLine(doc, line, prefix, suffix);
        }
        else
        {
            CommentLine(doc, line, prefix, suffix);
        }
    }

    private void CommentLine(ICSharpCode.AvalonEdit.Document.TextDocument doc, ICSharpCode.AvalonEdit.Document.DocumentLine line, string prefix, string suffix)
    {
        var lineText = doc.GetText(line.Offset, line.Length);
        var trimmedText = lineText.TrimStart();
        var leadingWhitespace = lineText.Substring(0, lineText.Length - trimmedText.Length);
        
        string newText;
        if (_currentRenderMode == RenderMode.Mermaid)
        {
            newText = leadingWhitespace + prefix + trimmedText;
        }
        else
        {
            newText = leadingWhitespace + prefix + trimmedText + suffix;
        }
        
        doc.Replace(line.Offset, line.Length, newText);
    }

    private void UncommentLine(ICSharpCode.AvalonEdit.Document.TextDocument doc, ICSharpCode.AvalonEdit.Document.DocumentLine line, string prefix, string suffix)
    {
        var lineText = doc.GetText(line.Offset, line.Length);
        var trimmedText = lineText.TrimStart();
        var leadingWhitespace = lineText.Substring(0, lineText.Length - trimmedText.Length);
        
        string newText;
        if (_currentRenderMode == RenderMode.Mermaid)
        {
            // Remove %% prefix (with optional space)
            if (trimmedText.StartsWith("%% "))
            {
                newText = leadingWhitespace + trimmedText.Substring(3);
            }
            else if (trimmedText.StartsWith("%%"))
            {
                newText = leadingWhitespace + trimmedText.Substring(2);
            }
            else
            {
                newText = lineText;
            }
        }
        else
        {
            // Remove <!-- prefix and --> suffix
            if (trimmedText.StartsWith("<!-- ") && trimmedText.TrimEnd().EndsWith(" -->"))
            {
                var content = trimmedText.Substring(5);
                content = content.Substring(0, content.TrimEnd().Length - 4);
                newText = leadingWhitespace + content;
            }
            else if (trimmedText.StartsWith("<!--") && trimmedText.TrimEnd().EndsWith("-->"))
            {
                var content = trimmedText.Substring(4);
                content = content.Substring(0, content.TrimEnd().Length - 3);
                newText = leadingWhitespace + content;
            }
            else
            {
                newText = lineText;
            }
        }
        
        doc.Replace(line.Offset, line.Length, newText);
    }

    private void MoveLineUp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var doc = CodeEditor.Document;
            if (doc.TextLength == 0) return;
            
            var caretLine = doc.GetLineByOffset(CodeEditor.CaretOffset);
            int currentLineNumber = caretLine.LineNumber;
            
            if (currentLineNumber <= 1)
                return; // Can't move first line up
            
            var prevLine = doc.GetLineByNumber(currentLineNumber - 1);
            var currentLineText = doc.GetText(caretLine.Offset, caretLine.Length);
            var prevLineText = doc.GetText(prevLine.Offset, prevLine.Length);
            
            // Calculate new caret position
            int caretColumn = CodeEditor.CaretOffset - caretLine.Offset;
            int prevLineOffset = prevLine.Offset;
            // Use DelimiterLength to handle both \n and \r\n correctly
            int delimiterLength = prevLine.DelimiterLength;
            int totalLength = prevLine.Length + delimiterLength + caretLine.Length;
            
            // Get the actual delimiter text to preserve it
            string delimiter = delimiterLength > 0 
                ? doc.GetText(prevLine.Offset + prevLine.Length, delimiterLength) 
                : Environment.NewLine;
            
            using (doc.RunUpdate())
            {
                // Replace both lines, preserving the original delimiter
                doc.Replace(prevLineOffset, totalLength, 
                    currentLineText + delimiter + prevLineText);
            }
            
            // Restore caret position on the moved line (now at previous line number)
            var newLine = doc.GetLineByNumber(currentLineNumber - 1);
            CodeEditor.CaretOffset = newLine.Offset + Math.Min(caretColumn, newLine.Length);
        }
        catch (Exception)
        {
            // Silently ignore edge cases
        }
    }

    private void MoveLineDown_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var doc = CodeEditor.Document;
            if (doc.TextLength == 0) return;
            
            var caretLine = doc.GetLineByOffset(CodeEditor.CaretOffset);
            int currentLineNumber = caretLine.LineNumber;
            int lineCount = doc.LineCount;
            
            if (currentLineNumber >= lineCount)
                return; // Can't move last line down
            
            var nextLine = doc.GetLineByNumber(currentLineNumber + 1);
            var currentLineText = doc.GetText(caretLine.Offset, caretLine.Length);
            var nextLineText = doc.GetText(nextLine.Offset, nextLine.Length);
            
            // Calculate new caret position
            int caretColumn = CodeEditor.CaretOffset - caretLine.Offset;
            int currentLineOffset = caretLine.Offset;
            // Use DelimiterLength to handle both \n and \r\n correctly
            int delimiterLength = caretLine.DelimiterLength;
            int totalLength = caretLine.Length + delimiterLength + nextLine.Length;
            
            // Get the actual delimiter text to preserve it
            string delimiter = delimiterLength > 0 
                ? doc.GetText(caretLine.Offset + caretLine.Length, delimiterLength) 
                : Environment.NewLine;
            
            // Check if we have enough content to swap
            if (currentLineOffset + totalLength > doc.TextLength)
            {
                // Adjust for edge case where calculation exceeds document length
                totalLength = doc.TextLength - currentLineOffset;
            }
            
            using (doc.RunUpdate())
            {
                // Replace both lines, preserving the original delimiter
                doc.Replace(currentLineOffset, totalLength, 
                    nextLineText + delimiter + currentLineText);
            }
            
            // Restore caret position on the moved line (now at next line number)
            var newLine = doc.GetLineByNumber(currentLineNumber + 1);
            CodeEditor.CaretOffset = newLine.Offset + Math.Min(caretColumn, newLine.Length);
        }
        catch (Exception)
        {
            // Silently ignore edge cases
        }
    }

    private void WordWrap_Click(object sender, RoutedEventArgs e)
    {
        CodeEditor.WordWrap = !CodeEditor.WordWrap;
        if (WordWrapToggle != null)
        {
            WordWrapToggle.IsChecked = CodeEditor.WordWrap;
        }
        if (WordWrapMenuItem != null)
        {
            WordWrapMenuItem.IsChecked = CodeEditor.WordWrap;
        }
        
        // Update toggle icons
        UpdateWordWrapIcons();
        
        // Sync minimap word wrap with code editor
        if (_isMinimapVisible && MinimapEditor != null)
        {
            MinimapEditor.WordWrap = CodeEditor.WordWrap;
            // Deferred viewport update after layout recalculates with new wrapping
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                UpdateMinimapViewport();
            }));
        }
    }

    #endregion

    #region View Toolbar (Split View, Line Numbers, Bracket Matching)

    private GridLength _savedPreviewColumnWidth = new GridLength(1, GridUnitType.Star);
    private bool _isPreviewVisible = true;
    private bool _isBracketMatchingEnabled = true;
    private ICSharpCode.AvalonEdit.Rendering.IBackgroundRenderer? _bracketHighlighter;

    private void SplitView_Click(object sender, RoutedEventArgs e)
    {
        _isPreviewVisible = !_isPreviewVisible;
        
        if (_isPreviewVisible)
        {
            // Show preview panel
            PreviewColumn.Width = _savedPreviewColumnWidth;
            PreviewColumn.MinWidth = 200;
            SplitterColumn.Width = new GridLength(5);
        }
        else
        {
            // Hide preview panel - save current width first
            _savedPreviewColumnWidth = PreviewColumn.Width;
            PreviewColumn.Width = new GridLength(0);
            PreviewColumn.MinWidth = 0;
            SplitterColumn.Width = new GridLength(0);
        }
        
        if (SplitViewToggle != null)
        {
            SplitViewToggle.IsChecked = _isPreviewVisible;
        }
        if (SplitViewMenuItem != null)
        {
            SplitViewMenuItem.IsChecked = _isPreviewVisible;
        }
        
        // Update toggle icons
        UpdateSplitViewIcons();
    }

    private void LineNumbers_Click(object sender, RoutedEventArgs e)
    {
        CodeEditor.ShowLineNumbers = !CodeEditor.ShowLineNumbers;
        if (LineNumbersToggle != null)
        {
            LineNumbersToggle.IsChecked = CodeEditor.ShowLineNumbers;
        }
        if (LineNumbersMenuItem != null)
        {
            LineNumbersMenuItem.IsChecked = CodeEditor.ShowLineNumbers;
        }
        
        // Update toggle icons
        UpdateLineNumbersIcons();
    }

    private void BracketMatching_Click(object sender, RoutedEventArgs e)
    {
        _isBracketMatchingEnabled = !_isBracketMatchingEnabled;
        
        if (_isBracketMatchingEnabled)
        {
            EnableBracketHighlighting();
        }
        else
        {
            DisableBracketHighlighting();
        }
        
        if (BracketMatchingToggle != null)
        {
            BracketMatchingToggle.IsChecked = _isBracketMatchingEnabled;
        }
        if (BracketMatchingMenuItem != null)
        {
            BracketMatchingMenuItem.IsChecked = _isBracketMatchingEnabled;
        }
        
        // Update toggle icons
        UpdateBracketMatchingIcons();
    }

    private void EnableBracketHighlighting()
    {
        if (_bracketHighlighter == null)
        {
            _bracketHighlighter = new BracketHighlightRenderer(CodeEditor.TextArea);
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_bracketHighlighter);
        }
        CodeEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged_BracketHighlight;
    }

    private void DisableBracketHighlighting()
    {
        CodeEditor.TextArea.Caret.PositionChanged -= Caret_PositionChanged_BracketHighlight;
        if (_bracketHighlighter != null)
        {
            CodeEditor.TextArea.TextView.BackgroundRenderers.Remove(_bracketHighlighter);
            _bracketHighlighter = null;
        }
    }

    private void Caret_PositionChanged_BracketHighlight(object? sender, EventArgs e)
    {
        if (_bracketHighlighter is BracketHighlightRenderer renderer)
        {
            renderer.UpdateBrackets(CodeEditor.Document, CodeEditor.CaretOffset);
        }
    }

    #endregion

    #region Minimap

    private bool _isMinimapVisible = false;
    private bool _isMinimapDragging = false;
    private const double MinimapWidth = 120;

    private void Minimap_Click(object sender, RoutedEventArgs e)
    {
        _isMinimapVisible = !_isMinimapVisible;
        
        if (_isMinimapVisible)
        {
            ShowMinimap();
        }
        else
        {
            HideMinimap();
        }
        
        if (MinimapToggle != null)
        {
            MinimapToggle.IsChecked = _isMinimapVisible;
        }
        if (MinimapMenuItem != null)
        {
            MinimapMenuItem.IsChecked = _isMinimapVisible;
        }
        
        // Update toggle icons
        UpdateMinimapIcons();
    }

    private void ShowMinimap()
    {
        MinimapColumn.Width = new GridLength(MinimapWidth);
        MinimapBorder.Visibility = Visibility.Visible;
        
        // Copy the document to the minimap editor and sync settings
        SyncMinimapContent();
        
        // Sync word wrap setting with code editor
        MinimapEditor.WordWrap = CodeEditor.WordWrap;
        
        // Set up event handlers for syncing
        CodeEditor.TextChanged += CodeEditor_TextChanged_Minimap;
        CodeEditor.TextArea.TextView.ScrollOffsetChanged += TextView_ScrollOffsetChanged_Minimap;
        CodeEditor.TextArea.TextView.VisualLinesChanged += TextView_VisualLinesChanged_Minimap;
        
        // Also listen to the ScrollViewer's ScrollChanged routed event as a belt-and-suspenders approach
        CodeEditor.AddHandler(System.Windows.Controls.ScrollViewer.ScrollChangedEvent, 
            new System.Windows.Controls.ScrollChangedEventHandler(CodeEditor_ScrollChanged_Minimap));
        
        // Initial viewport update - immediate
        UpdateMinimapViewport();
        
        // Deferred update after layout is complete (DocumentHeight needs a render pass)
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            UpdateMinimapViewport();
        }));
    }

    private void HideMinimap()
    {
        MinimapColumn.Width = new GridLength(0);
        MinimapBorder.Visibility = Visibility.Collapsed;
        
        // Remove event handlers
        CodeEditor.TextChanged -= CodeEditor_TextChanged_Minimap;
        CodeEditor.TextArea.TextView.ScrollOffsetChanged -= TextView_ScrollOffsetChanged_Minimap;
        CodeEditor.TextArea.TextView.VisualLinesChanged -= TextView_VisualLinesChanged_Minimap;
        CodeEditor.RemoveHandler(System.Windows.Controls.ScrollViewer.ScrollChangedEvent, 
            new System.Windows.Controls.ScrollChangedEventHandler(CodeEditor_ScrollChanged_Minimap));
    }

    private void SyncMinimapContent()
    {
        if (MinimapEditor != null && CodeEditor != null)
        {
            MinimapEditor.Text = CodeEditor.Text;
            
            // Apply the same syntax highlighting
            MinimapEditor.SyntaxHighlighting = CodeEditor.SyntaxHighlighting;
        }
    }

    private void CodeEditor_TextChanged_Minimap(object? sender, EventArgs e)
    {
        SyncMinimapContent();
        UpdateMinimapViewport();
        
        // Deferred update - DocumentHeight changes after layout pass
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            UpdateMinimapViewport();
        }));
    }

    private void TextView_ScrollOffsetChanged_Minimap(object? sender, EventArgs e)
    {
        UpdateMinimapViewport();
    }

    private void TextView_VisualLinesChanged_Minimap(object? sender, EventArgs e)
    {
        UpdateMinimapViewport();
    }

    private void CodeEditor_ScrollChanged_Minimap(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        UpdateMinimapViewport();
    }

    private void MinimapViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMinimapViewport();
    }

    private void UpdateMinimapViewport()
    {
        if (MinimapEditor == null || CodeEditor == null || MinimapViewportCanvas == null || MinimapViewportIndicator == null || MinimapOverlayGrid == null)
            return;

        try
        {
            var panelHeight = MinimapOverlayGrid.ActualHeight;
            if (panelHeight <= 0) return;
            
            // Get the ScrollViewer from the code editor for ExtentHeight/ViewportHeight
            var editorScrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(CodeEditor);
            
            double editorExtentH, editorViewportH;
            
            if (editorScrollViewer != null)
            {
                editorExtentH = editorScrollViewer.ExtentHeight;
                editorViewportH = editorScrollViewer.ViewportHeight;
            }
            else
            {
                // Fallback to TextView properties
                var tv = CodeEditor.TextArea.TextView;
                editorExtentH = tv.DocumentHeight;
                editorViewportH = tv.ActualHeight;
            }
            
            // Read scroll offset from multiple sources and use the best one.
            // AvalonEdit's ScrollViewer.VerticalOffset stays at 0 (IScrollInfo bypass),
            // so we try CodeEditor.VerticalOffset first, then fall back to TextView.ScrollOffset.Y.
            var editorOffsetY = CodeEditor.VerticalOffset;
            if (editorOffsetY == 0)
            {
                // CodeEditor.VerticalOffset may not be updated yet during scroll events.
                // Read directly from the TextView's ScrollOffset which is the source of truth.
                editorOffsetY = CodeEditor.TextArea.TextView.ScrollOffset.Y;
            }
            
            if (editorExtentH <= 0) editorExtentH = 1;
            
            // What fraction of the document is visible (0 to 1)
            var viewportRatio = Math.Min(1.0, Math.Max(0.01, editorViewportH / editorExtentH));
            
            // How far we've scrolled (0 = top, 1 = bottom)
            var maxScroll = Math.Max(0, editorExtentH - editorViewportH);
            var scrollFraction = maxScroll > 0 ? Math.Max(0, Math.Min(1.0, editorOffsetY / maxScroll)) : 0;
            
            // Get minimap content height from its ScrollViewer too
            var minimapScrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(MinimapEditor);
            double minimapContentH;
            if (minimapScrollViewer != null)
            {
                minimapContentH = minimapScrollViewer.ExtentHeight;
            }
            else
            {
                minimapContentH = MinimapEditor.TextArea.TextView.DocumentHeight;
            }
            if (minimapContentH <= 0) minimapContentH = 1;
            
            // Map viewport indicator to the effective display area
            // Use the smaller of minimap content height and panel height
            var effectiveHeight = Math.Min(minimapContentH, panelHeight);
            
            var indicatorHeight = viewportRatio * effectiveHeight;
            indicatorHeight = Math.Max(10, indicatorHeight);
            
            var indicatorTop = scrollFraction * (effectiveHeight - indicatorHeight);
            indicatorTop = Math.Max(0, Math.Min(indicatorTop, effectiveHeight - indicatorHeight));
            
            // Sync minimap scrolling
            if (minimapContentH > panelHeight)
            {
                var maxMinimapScroll = minimapContentH - panelHeight;
                MinimapEditor.ScrollToVerticalOffset(scrollFraction * maxMinimapScroll);
            }
            else
            {
                MinimapEditor.ScrollToVerticalOffset(0);
            }
            
            Canvas.SetTop(MinimapViewportIndicator, indicatorTop);
            Canvas.SetLeft(MinimapViewportIndicator, 2);
            MinimapViewportIndicator.Width = MinimapWidth - 6;
            MinimapViewportIndicator.Height = indicatorHeight;
            
        }
        catch
        {
            // Ignore errors during viewport calculation
        }
    }

    private void MinimapViewport_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isMinimapDragging = true;
        MinimapViewportCanvas.CaptureMouse();
        NavigateToMinimapPosition(e.GetPosition(MinimapViewportCanvas).Y);
    }

    private void MinimapViewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isMinimapDragging)
        {
            NavigateToMinimapPosition(e.GetPosition(MinimapViewportCanvas).Y);
        }
    }

    private void MinimapViewport_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isMinimapDragging = false;
        MinimapViewportCanvas.ReleaseMouseCapture();
    }

    private void MinimapOverlay_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (CodeEditor == null) return;
        
        // Forward mouse wheel events to the code editor for scrolling
        var textView = CodeEditor.TextArea.TextView;
        var linesToScroll = 3.0; // Standard scroll amount
        var scrollAmount = linesToScroll * textView.DefaultLineHeight;
        
        if (e.Delta > 0)
        {
            // Scroll up
            CodeEditor.ScrollToVerticalOffset(CodeEditor.VerticalOffset - scrollAmount);
        }
        else
        {
            // Scroll down
            CodeEditor.ScrollToVerticalOffset(CodeEditor.VerticalOffset + scrollAmount);
        }
        
        e.Handled = true;
    }

    private void NavigateToMinimapPosition(double mouseY)
    {
        if (MinimapEditor == null || CodeEditor == null || MinimapBorder == null)
            return;

        try
        {
            var panelHeight = MinimapBorder.ActualHeight;
            if (panelHeight <= 0) return;
            
            // Get scroll metrics from ScrollViewer for accuracy
            var editorScrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(CodeEditor);
            double editorExtentH, editorViewportH;
            if (editorScrollViewer != null)
            {
                editorExtentH = editorScrollViewer.ExtentHeight;
                editorViewportH = editorScrollViewer.ViewportHeight;
            }
            else
            {
                var tv = CodeEditor.TextArea.TextView;
                editorExtentH = tv.DocumentHeight;
                editorViewportH = tv.ActualHeight;
            }
            if (editorExtentH <= 0) return;
            
            var minimapScrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(MinimapEditor);
            double minimapContentH = minimapScrollViewer != null 
                ? minimapScrollViewer.ExtentHeight 
                : MinimapEditor.TextArea.TextView.DocumentHeight;
            if (minimapContentH <= 0) return;
            
            var effectiveHeight = Math.Min(minimapContentH, panelHeight);
            mouseY = Math.Max(0, Math.Min(mouseY, effectiveHeight));
            
            // Convert click to a scroll fraction (0 to 1)
            var indicatorHeight = Math.Min(1.0, editorViewportH / editorExtentH) * effectiveHeight;
            indicatorHeight = Math.Max(10, indicatorHeight);
            var clickFraction = mouseY / effectiveHeight;
            
            // Map to editor scroll position, centering on click
            var maxEditorScroll = Math.Max(0, editorExtentH - editorViewportH);
            var targetScroll = clickFraction * maxEditorScroll;
            targetScroll = Math.Max(0, Math.Min(targetScroll, maxEditorScroll));
            
            CodeEditor.ScrollToVerticalOffset(targetScroll);
        }
        catch
        {
            // Ignore navigation errors
        }
    }

    #endregion

    #region Spell Check

    /// <summary>
    /// Initializes the spell check service and renderer asynchronously.
    /// Called from MainWindow_Loaded.
    /// </summary>
    private async Task InitializeSpellCheckAsync()
    {
        try
        {
            _spellCheckService = new SpellCheckService();
            await _spellCheckService.LoadAsync();

            if (_spellCheckService.IsLoaded)
            {
                _spellCheckRenderer = new SpellCheckBackgroundRenderer(CodeEditor, _spellCheckService);

                // Apply spell check default from settings
                _isSpellCheckEnabled = SettingsManager.Current.SpellCheckEnabled;
                if (_isSpellCheckEnabled)
                {
                    EnableSpellCheck();
                }

                // Sync toggle UI
                if (SpellCheckToggle != null) SpellCheckToggle.IsChecked = _isSpellCheckEnabled;
                if (SpellCheckMenuItem != null) SpellCheckMenuItem.IsChecked = _isSpellCheckEnabled;
                UpdateSpellCheckIcons();

                // Set up right-click context menu for spelling suggestions
                CodeEditor.TextArea.MouseRightButtonDown += TextArea_MouseRightButtonDown_SpellCheck;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SpellCheck init failed: {ex.Message}");
        }
    }

    private void SpellCheck_Click(object sender, RoutedEventArgs e)
    {
        _isSpellCheckEnabled = !_isSpellCheckEnabled;

        if (_isSpellCheckEnabled)
        {
            EnableSpellCheck();
        }
        else
        {
            DisableSpellCheck();
        }

        if (SpellCheckToggle != null) SpellCheckToggle.IsChecked = _isSpellCheckEnabled;
        if (SpellCheckMenuItem != null) SpellCheckMenuItem.IsChecked = _isSpellCheckEnabled;

        UpdateSpellCheckIcons();

        // Save preference
        SettingsManager.Current.SpellCheckEnabled = _isSpellCheckEnabled;
        SettingsManager.Save();
    }

    private void EnableSpellCheck()
    {
        if (_spellCheckRenderer == null) return;

        // Add renderer if not already added
        if (!CodeEditor.TextArea.TextView.BackgroundRenderers.Contains(_spellCheckRenderer))
        {
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_spellCheckRenderer);
        }

        // Update the mermaid flag
        _spellCheckRenderer.IsMermaid = _currentRenderMode == RenderMode.Mermaid;

        // Wire up text changed to trigger spell check
        CodeEditor.TextChanged += CodeEditor_TextChanged_SpellCheck;

        // Trigger initial spell check
        _spellCheckRenderer.InvalidateSpelling();
    }

    private void DisableSpellCheck()
    {
        if (_spellCheckRenderer == null) return;

        // Remove renderer
        CodeEditor.TextArea.TextView.BackgroundRenderers.Remove(_spellCheckRenderer);

        // Unhook text changed
        CodeEditor.TextChanged -= CodeEditor_TextChanged_SpellCheck;

        // Clear underlines
        _spellCheckRenderer.Clear();
    }

    private void CodeEditor_TextChanged_SpellCheck(object? sender, EventArgs e)
    {
        _spellCheckRenderer?.InvalidateSpelling();
    }

    private void UpdateSpellCheckIcons()
    {
        // Use a simple "ABC" text icon with checkmark for spell check since we don't have a custom SVG
        // The toggle state is shown by the ToggleButton border + IsChecked state
        if (SpellCheckToggle != null)
        {
            var tb = new System.Windows.Controls.TextBlock
            {
                Text = "ABC",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = _isSpellCheckEnabled
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4E, 0xC9, 0xB0)) // green when on
                    : (SolidColorBrush)System.Windows.Application.Current.Resources["ThemeForegroundBrush"]
            };
            // Add a squiggly underline effect when enabled
            if (_isSpellCheckEnabled)
            {
                tb.TextDecorations = new TextDecorationCollection
                {
                    new TextDecoration
                    {
                        Location = TextDecorationLocation.Underline,
                        Pen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x40, 0x40)), 1.2)
                        {
                            DashStyle = new System.Windows.Media.DashStyle(new[] { 1.0, 2.0 }, 0)
                        }
                    }
                };
            }
            SpellCheckToggle.Content = tb;
        }
    }

    /// <summary>
    /// Handles right-click on the text area to show spelling suggestions in the context menu.
    /// </summary>
    private void TextArea_MouseRightButtonDown_SpellCheck(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isSpellCheckEnabled || _spellCheckRenderer == null || _spellCheckService == null) return;

        // Get the position under the mouse
        var textView = CodeEditor.TextArea.TextView;
        var pos = textView.GetPosition(e.GetPosition(textView) + textView.ScrollOffset);
        if (pos == null) return;

        var offset = CodeEditor.Document.GetOffset(pos.Value.Location);
        var misspelled = _spellCheckRenderer.GetMisspelledWordAtOffset(offset);

        if (misspelled == null) return;

        // Build a context menu with suggestions
        var contextMenu = new System.Windows.Controls.ContextMenu();

        var suggestions = _spellCheckService.Suggest(misspelled.Word);
        if (suggestions.Count > 0)
        {
            foreach (var suggestion in suggestions)
            {
                var menuItem = new System.Windows.Controls.MenuItem
                {
                    Header = suggestion,
                    FontWeight = FontWeights.Bold
                };
                var capturedSuggestion = suggestion;
                var capturedWord = misspelled;
                menuItem.Click += (s, args) =>
                {
                    // Replace the misspelled word with the suggestion
                    CodeEditor.Document.Replace(capturedWord.StartOffset, capturedWord.Length, capturedSuggestion);
                };
                contextMenu.Items.Add(menuItem);
            }
        }
        else
        {
            var noSuggestions = new System.Windows.Controls.MenuItem
            {
                Header = "(No suggestions)",
                IsEnabled = false
            };
            contextMenu.Items.Add(noSuggestions);
        }

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        // "Add to Dictionary" option
        var addItem = new System.Windows.Controls.MenuItem
        {
            Header = $"Add \"{misspelled.Word}\" to Dictionary"
        };
        var capturedMisspelled = misspelled;
        addItem.Click += (s, args) =>
        {
            _spellCheckService.AddToCustomDictionary(capturedMisspelled.Word);
            _spellCheckRenderer?.InvalidateSpelling();
        };
        contextMenu.Items.Add(addItem);

        // "Ignore" option (just closes menu)
        var ignoreItem = new System.Windows.Controls.MenuItem
        {
            Header = "Ignore"
        };
        contextMenu.Items.Add(ignoreItem);

        // Show the context menu
        CodeEditor.TextArea.ContextMenu = contextMenu;
        contextMenu.IsOpen = true;

        // Restore default context menu after this one closes
        contextMenu.Closed += (s, args) =>
        {
            CodeEditor.TextArea.ContextMenu = null;
        };

        e.Handled = true;
    }

    #endregion

    #region Visual Editor Integration

    /// <summary>
    /// Initializes the Visual Editor WebView2 control and loads the embedded VisualEditor.html.
    /// </summary>
    private async Task InitializeVisualEditorAsync()
    {
        try
        {
            // Temporarily make the panel visible so WebView2 can initialize
            // (some systems require the control to be visible for EnsureCoreWebView2Async)
            var wasCollapsed = VisualEditorPanel.Visibility == Visibility.Collapsed;
            if (wasCollapsed)
            {
                VisualEditorPanel.Visibility = Visibility.Visible;
                VisualEditorPanel.Width = 0;
                VisualEditorPanel.Height = 0;
            }

            await VisualEditorWebView.EnsureCoreWebView2Async();
            _visualEditorInitialized = true;

            // Restore collapsed state after init
            if (wasCollapsed)
            {
                VisualEditorPanel.Visibility = Visibility.Collapsed;
                VisualEditorPanel.ClearValue(FrameworkElement.WidthProperty);
                VisualEditorPanel.ClearValue(FrameworkElement.HeightProperty);
            }

            // Load the embedded VisualEditor.html resource
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("MermaidEditor.Resources.VisualEditor.html");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var html = await reader.ReadToEndAsync();
                VisualEditorWebView.NavigateToString(html);
            }

            // Initialize the bridge with a default model
            _currentFlowchartModel = new FlowchartModel();
            _visualEditorBridge = new VisualEditorBridge(VisualEditorWebView, _currentFlowchartModel);

            // Wire up events
            _visualEditorBridge.ModelChanged += VisualEditorBridge_ModelChanged;
            _visualEditorBridge.EditorReady += VisualEditorBridge_EditorReady;

            // Apply current theme to visual editor
            var isDark = ThemeManager.IsDarkTheme;
            await _visualEditorBridge.SetThemeAsync(isDark ? "dark" : "light");

            // Now that visual editor is initialized, refresh toolbar visibility
            // (SetRenderModeFromFile may have already run before init completed)
            UpdateVisualEditorModeToolbarVisibility();
        }
        catch (Exception)
        {
            // Visual editor is optional - silently fail if WebView2 can't init for it
            _visualEditorInitialized = false;
        }
    }

    /// <summary>
    /// Called when the visual editor JS signals it is ready to receive diagram data.
    /// </summary>
    private async void VisualEditorBridge_EditorReady(object? sender, EventArgs e)
    {
        // If we're already in Visual or Split mode and have a model, send it
        if (_visualEditorMode != VisualEditorMode.Text && _currentFlowchartModel != null && _visualEditorBridge != null)
        {
            await _visualEditorBridge.SendDiagramToEditorAsync();
        }
    }

    /// <summary>
    /// Called when the visual editor modifies the FlowchartModel (node moved, created, deleted, etc.).
    /// Serializes the model back to text and updates the code editor + preview.
    /// </summary>
    private void VisualEditorBridge_ModelChanged(object? sender, ModelChangedEventArgs e)
    {
        if (_isVisualEditorUpdating) return;

        _isVisualEditorUpdating = true;
        try
        {
            // Serialize the model back to Mermaid text
            var text = MermaidSerializer.Serialize(e.Model);

            // Update the code editor text without triggering a re-parse loop
            if (_visualEditorMode == VisualEditorMode.Visual || _visualEditorMode == VisualEditorMode.Split)
            {
                _isSwitchingDocuments = true; // Suppress dirty flag from programmatic text change
                CodeEditor.Text = text;
                _isSwitchingDocuments = false;

                // Mark document as dirty
                _isDirty = true;
                if (_activeDocument != null)
                {
                    _activeDocument.IsDirty = true;
                }
                UpdateTitle();

                // Re-render preview
                RenderPreview();
            }
        }
        finally
        {
            _isVisualEditorUpdating = false;
        }
    }

    /// <summary>
    /// Updates the visibility of the visual editor mode toolbar based on the current render mode.
    /// Only visible for Mermaid files.
    /// </summary>
    private void UpdateVisualEditorModeToolbarVisibility()
    {
        if (VisualEditorModeToolbar != null)
        {
            VisualEditorModeToolbar.Visibility = _currentRenderMode == RenderMode.Mermaid && _visualEditorInitialized
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // If switching to a non-Mermaid file while in Visual/Split mode, revert to Text mode
        if (_currentRenderMode != RenderMode.Mermaid && _visualEditorMode != VisualEditorMode.Text)
        {
            SwitchToTextMode();
        }
    }

    private void TextMode_Click(object sender, RoutedEventArgs e)
    {
        SwitchToTextMode();
    }

    private void VisualMode_Click(object sender, RoutedEventArgs e)
    {
        SwitchToVisualMode();
    }

    private void SplitMode_Click(object sender, RoutedEventArgs e)
    {
        SwitchToSplitMode();
    }

    /// <summary>
    /// Switches to Text Mode: shows CodeEditor + Preview, hides Visual Editor.
    /// Serializes the current visual model back to text if coming from Visual/Split mode.
    /// </summary>
    private void SwitchToTextMode()
    {
        if (_visualEditorMode != VisualEditorMode.Text && _currentFlowchartModel != null && _visualEditorBridge != null)
        {
            // Serialize model back to text
            _isVisualEditorUpdating = true;
            try
            {
                var text = MermaidSerializer.Serialize(_currentFlowchartModel);
                _isSwitchingDocuments = true;
                CodeEditor.Text = text;
                _isSwitchingDocuments = false;
            }
            finally
            {
                _isVisualEditorUpdating = false;
            }
        }

        _visualEditorMode = VisualEditorMode.Text;
        UpdateModeToggleButtons();
        ApplyVisualEditorLayout();
    }

    /// <summary>
    /// Switches to Visual Mode: shows VisualEditor + Preview, hides CodeEditor.
    /// Parses current text into the FlowchartModel and sends to visual editor.
    /// </summary>
    private async void SwitchToVisualMode()
    {
        _visualEditorMode = VisualEditorMode.Visual;
        UpdateModeToggleButtons();
        ApplyVisualEditorLayout();

        // Parse current text into model and send to visual editor
        await ParseAndSendToVisualEditor();
    }

    /// <summary>
    /// Switches to Split Mode: shows CodeEditor + VisualEditor + Preview (all three).
    /// Parses current text and sends to visual editor.
    /// </summary>
    private async void SwitchToSplitMode()
    {
        _visualEditorMode = VisualEditorMode.Split;
        UpdateModeToggleButtons();
        ApplyVisualEditorLayout();

        // Parse current text into model and send to visual editor
        await ParseAndSendToVisualEditor();
    }

    /// <summary>
    /// Parses the current CodeEditor text via MermaidParser and sends the model to the visual editor.
    /// </summary>
    private async Task ParseAndSendToVisualEditor()
    {
        if (_visualEditorBridge == null || !_visualEditorInitialized) return;

        try
        {
            _isVisualEditorUpdating = true;
            var parsed = MermaidParser.ParseFlowchart(CodeEditor.Text);
            if (parsed != null)
            {
                _currentFlowchartModel = parsed;
                await _visualEditorBridge.UpdateModelAsync(_currentFlowchartModel);
            }
        }
        catch (Exception)
        {
            // If parsing fails, keep the existing model
        }
        finally
        {
            _isVisualEditorUpdating = false;
        }
    }

    /// <summary>
    /// Updates the toggle button checked states to reflect the current mode.
    /// </summary>
    private void UpdateModeToggleButtons()
    {
        TextModeToggle.IsChecked = _visualEditorMode == VisualEditorMode.Text;
        VisualModeToggle.IsChecked = _visualEditorMode == VisualEditorMode.Visual;
        SplitModeToggle.IsChecked = _visualEditorMode == VisualEditorMode.Split;
    }

    /// <summary>
    /// Applies the grid layout based on the current VisualEditorMode.
    /// Controls column widths and panel visibility.
    /// </summary>
    private void ApplyVisualEditorLayout()
    {
        switch (_visualEditorMode)
        {
            case VisualEditorMode.Text:
                // Show CodeEditor + Preview, hide Visual Editor
                CodeEditorColumn.Width = new GridLength(1, GridUnitType.Star);
                CodeEditorColumn.MinWidth = 200;
                VisualSplitterColumn.Width = new GridLength(0);
                VisualEditorColumn.Width = new GridLength(0);
                VisualEditorColumn.MinWidth = 0;
                VisualEditorPanel.Visibility = Visibility.Collapsed;
                VisualEditorSplitter.Visibility = Visibility.Collapsed;
                break;

            case VisualEditorMode.Visual:
                // Show Visual Editor + Preview, hide CodeEditor
                CodeEditorColumn.Width = new GridLength(0);
                CodeEditorColumn.MinWidth = 0;
                VisualSplitterColumn.Width = new GridLength(0);
                VisualEditorColumn.Width = new GridLength(1, GridUnitType.Star);
                VisualEditorColumn.MinWidth = 200;
                VisualEditorPanel.Visibility = Visibility.Visible;
                VisualEditorSplitter.Visibility = Visibility.Collapsed;
                break;

            case VisualEditorMode.Split:
                // Show all three: CodeEditor + Visual Editor + Preview
                CodeEditorColumn.Width = new GridLength(1, GridUnitType.Star);
                CodeEditorColumn.MinWidth = 200;
                VisualSplitterColumn.Width = new GridLength(5);
                VisualEditorColumn.Width = new GridLength(1, GridUnitType.Star);
                VisualEditorColumn.MinWidth = 200;
                VisualEditorPanel.Visibility = Visibility.Visible;
                VisualEditorSplitter.Visibility = Visibility.Visible;
                break;
        }

        // Ensure preview column stays visible in all modes
        if (_isPreviewVisible)
        {
            SplitterColumn.Width = new GridLength(5);
            PreviewColumn.Width = _savedPreviewColumnWidth.Value > 0
                ? _savedPreviewColumnWidth
                : new GridLength(1, GridUnitType.Star);
            PreviewColumn.MinWidth = 200;
        }
    }

    #endregion

    private async void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewInitialized) return;

        // For Mermaid diagrams, use high-resolution SVG-to-canvas export
        // For Markdown, fall back to viewport capture
        if (_currentRenderMode == RenderMode.Mermaid)
        {
            await ExportMermaidPng();
        }
        else
        {
            await ExportViewportPng();
        }
    }

    private async Task ExportMermaidPng()
    {
        // Ask user for scale factor
        var scaleResult = MessageBox.Show(
            "Choose export resolution:\n\n" +
            "Click YES for 4x (recommended for most diagrams)\n" +
            "Click NO for 6x (for very detailed/wide diagrams)\n" +
            "Click CANCEL to abort",
            "Export Resolution",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (scaleResult == MessageBoxResult.Cancel)
            return;

        var scale = scaleResult == MessageBoxResult.Yes ? 4 : 6;

        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image (*.png)|*.png",
            Title = $"Export as PNG ({scale}x Resolution)",
            DefaultExt = ".png",
            FileName = GetExportDefaultPath(".png")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                UpdateLastExportDirectory(dialog.FileName);
                StatusText.Text = $"Exporting {scale}x resolution PNG...";
                
                // Create a TaskCompletionSource to await the callback
                _pngExportTcs = new TaskCompletionSource<string>();
                
                // Trigger the export (result comes via postMessage callback)
                await PreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.exportPngHighRes({scale})");
                
                // Wait for the callback with a timeout
                var timeoutTask = Task.Delay(30000); // 30 second timeout
                var completedTask = await Task.WhenAny(_pngExportTcs.Task, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    throw new Exception("Export timed out");
                }
                
                var dataUrl = await _pngExportTcs.Task;
                
                if (!string.IsNullOrEmpty(dataUrl) && dataUrl.StartsWith("data:image/png;base64,"))
                {
                    var base64Data = dataUrl.Substring("data:image/png;base64,".Length);
                    var imageBytes = Convert.FromBase64String(base64Data);
                    await File.WriteAllBytesAsync(dialog.FileName, imageBytes);
                    StatusText.Text = $"Exported as PNG ({scale}x resolution)";
                    MessageBox.Show($"PNG exported successfully at {scale}x resolution!\n\nThe full diagram has been exported at high resolution for crisp viewing.", 
                        "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No diagram to export. Make sure the diagram is rendered correctly.", 
                        "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export PNG: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Export failed";
            }
            finally
            {
                _pngExportTcs = null;
            }
        }
    }

    private async Task ExportViewportPng()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image (*.png)|*.png",
            Title = "Export as PNG",
            DefaultExt = ".png",
            FileName = GetExportDefaultPath(".png")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                UpdateLastExportDirectory(dialog.FileName);
                using var stream = new MemoryStream();
                await PreviewWebView.CoreWebView2.CapturePreviewAsync(
                    CoreWebView2CapturePreviewImageFormat.Png, stream);
                
                await File.WriteAllBytesAsync(dialog.FileName, stream.ToArray());
                StatusText.Text = "Exported as PNG";
                MessageBox.Show("PNG exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export PNG: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void ExportSvg_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewInitialized) return;

        var dialog = new SaveFileDialog
        {
            Filter = "SVG Image (*.svg)|*.svg",
            Title = "Export as SVG",
            DefaultExt = ".svg",
            FileName = GetExportDefaultPath(".svg")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                UpdateLastExportDirectory(dialog.FileName);
                var svgContent = await PreviewWebView.CoreWebView2.ExecuteScriptAsync("window.getSvgContent()");
                
                if (svgContent != "null" && !string.IsNullOrEmpty(svgContent))
                {
                    var svg = System.Text.Json.JsonSerializer.Deserialize<string>(svgContent);
                    if (svg != null)
                    {
                        await File.WriteAllTextAsync(dialog.FileName, svg);
                        StatusText.Text = "Exported as SVG";
                        MessageBox.Show("SVG exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("No diagram to export. Make sure the diagram is rendered correctly.", 
                        "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export SVG: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void ExportEmf_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewInitialized) return;

        var dialog = new SaveFileDialog
        {
            Filter = "Enhanced Metafile (*.emf)|*.emf",
            Title = "Export as EMF",
            DefaultExt = ".emf",
            FileName = GetExportDefaultPath(".emf")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                UpdateLastExportDirectory(dialog.FileName);
                StatusText.Text = "Exporting EMF...";
                
                var svgContent = await PreviewWebView.CoreWebView2.ExecuteScriptAsync("window.getSvgContent()");
                
                if (svgContent != "null" && !string.IsNullOrEmpty(svgContent))
                {
                    var svg = System.Text.Json.JsonSerializer.Deserialize<string>(svgContent);
                    if (svg != null)
                    {
                        // Sanitize SVG for XML parsing - Mermaid generates HTML elements that aren't valid XML
                        svg = SanitizeSvgForXml(svg);
                        
                        // Parse the SVG using Svg.NET
                        var svgDocument = Svg.SvgDocument.FromSvg<Svg.SvgDocument>(svg);
                        
                        // Get the SVG dimensions
                        var width = (int)Math.Ceiling(svgDocument.Width.Value);
                        var height = (int)Math.Ceiling(svgDocument.Height.Value);
                        
                        // If dimensions are 0 or invalid, try to get from viewBox
                        if (width <= 0 || height <= 0)
                        {
                            if (svgDocument.ViewBox.Width > 0 && svgDocument.ViewBox.Height > 0)
                            {
                                width = (int)Math.Ceiling(svgDocument.ViewBox.Width);
                                height = (int)Math.Ceiling(svgDocument.ViewBox.Height);
                            }
                            else
                            {
                                // Default fallback dimensions
                                width = 800;
                                height = 600;
                            }
                        }
                        
                        // Create the EMF file
                        using var tempBitmap = new System.Drawing.Bitmap(width, height);
                        using var tempGraphics = System.Drawing.Graphics.FromImage(tempBitmap);
                        var hdc = tempGraphics.GetHdc();
                        
                        try
                        {
                            using var stream = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write);
                            using var metafile = new System.Drawing.Imaging.Metafile(
                                stream, 
                                hdc, 
                                new System.Drawing.RectangleF(0, 0, width, height),
                                System.Drawing.Imaging.MetafileFrameUnit.Pixel,
                                System.Drawing.Imaging.EmfType.EmfPlusDual);
                            
                            using var graphics = System.Drawing.Graphics.FromImage(metafile);
                            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                            
                            // Render the SVG to the metafile graphics
                            svgDocument.Draw(graphics);
                        }
                        finally
                        {
                            tempGraphics.ReleaseHdc(hdc);
                        }
                        
                        StatusText.Text = "Exported as EMF";
                        MessageBox.Show("EMF exported successfully!\n\nThe diagram has been exported as a vector EMF file that can be imported into Word, PowerPoint, and other applications.", 
                            "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("No diagram to export. Make sure the diagram is rendered correctly.", 
                        "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export EMF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Export failed";
            }
        }
    }

    private async void ExportWord_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Word Document (*.docx)|*.docx",
            Title = "Export as Word Document",
            DefaultExt = ".docx",
            FileName = GetExportDefaultPath(".docx")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                UpdateLastExportDirectory(dialog.FileName);
                StatusText.Text = "Exporting Word document...";
                
                if (_currentRenderMode == RenderMode.Mermaid)
                {
                    // For Mermaid diagrams, export as PNG embedded in Word
                    await ExportMermaidToWord(dialog.FileName);
                }
                else
                {
                    // For Markdown, convert to formatted Word document
                    var markdown = CodeEditor.Text;
                    ConvertMarkdownToWord(markdown, dialog.FileName);
                    StatusText.Text = "Exported as Word document";
                    MessageBox.Show("Word document exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export Word document: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Export failed";
            }
        }
    }
    
    private async Task ExportMermaidToWord(string outputPath)
    {
        if (!_webViewInitialized)
        {
            MessageBox.Show("WebView not initialized. Please wait for the preview to load.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // Get PNG data from the diagram at 4x resolution
        _pngExportTcs = new TaskCompletionSource<string>();
        
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync("window.exportPngHighRes(4)");
        
        // Wait for the callback with a timeout
        var timeoutTask = Task.Delay(30000);
        var completedTask = await Task.WhenAny(_pngExportTcs.Task, timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            _pngExportTcs = null;
            throw new Exception("Export timed out");
        }
        
        var dataUrl = await _pngExportTcs.Task;
        _pngExportTcs = null;
        
        if (string.IsNullOrEmpty(dataUrl) || !dataUrl.StartsWith("data:image/png;base64,"))
        {
            MessageBox.Show("No diagram to export. Make sure the diagram is rendered correctly.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var base64Data = dataUrl.Substring("data:image/png;base64,".Length);
        var imageBytes = Convert.FromBase64String(base64Data);
        
        // Create Word document with embedded image
        using var document = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());
        
        // Add the image to the document
        var imagePart = mainPart.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(imageBytes))
        {
            imagePart.FeedData(stream);
        }
        
        // Get image dimensions for proper sizing
        int widthEmu, heightEmu;
        using (var stream = new MemoryStream(imageBytes))
        {
            using var bitmap = new System.Drawing.Bitmap(stream);
            // Convert pixels to EMUs (English Metric Units) - 914400 EMUs per inch, assuming 96 DPI
            // Scale down by 4 since we exported at 4x resolution
            var scaleFactor = 4.0;
            widthEmu = (int)(bitmap.Width / scaleFactor * 914400 / 96);
            heightEmu = (int)(bitmap.Height / scaleFactor * 914400 / 96);
            
            // Limit max width to 6 inches (page width minus margins)
            var maxWidthEmu = 6 * 914400;
            if (widthEmu > maxWidthEmu)
            {
                var ratio = (double)maxWidthEmu / widthEmu;
                widthEmu = maxWidthEmu;
                heightEmu = (int)(heightEmu * ratio);
            }
        }
        
        var relationshipId = mainPart.GetIdOfPart(imagePart);
        
        // Create the image element
        var element = CreateImageElement(relationshipId, widthEmu, heightEmu);
        
        // Add a paragraph with the image
        var para = new Paragraph(new Run(element));
        body.AppendChild(para);
        
        StatusText.Text = "Exported diagram to Word document";
        MessageBox.Show("Mermaid diagram exported to Word successfully!\n\nThe diagram has been embedded as a high-resolution image.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    private static uint _imageIdCounter = 1;
    
    private static Drawing CreateImageElement(string relationshipId, int widthEmu, int heightEmu, string imageName = "Image")
    {
        var imageId = _imageIdCounter++;
        var element = new Drawing(
            new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent { Cx = widthEmu, Cy = heightEmu },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties { Id = imageId, Name = imageName },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.NonVisualGraphicFrameDrawingProperties(
                    new DocumentFormat.OpenXml.Drawing.GraphicFrameLocks { NoChangeAspect = true }),
                new DocumentFormat.OpenXml.Drawing.Graphic(
                    new DocumentFormat.OpenXml.Drawing.GraphicData(
                        new DocumentFormat.OpenXml.Drawing.Pictures.Picture(
                            new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties(
                                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties { Id = imageId, Name = $"{imageName}.png" },
                                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties()),
                            new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill(
                                new DocumentFormat.OpenXml.Drawing.Blip { Embed = relationshipId },
                                new DocumentFormat.OpenXml.Drawing.Stretch(new DocumentFormat.OpenXml.Drawing.FillRectangle())),
                            new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties(
                                new DocumentFormat.OpenXml.Drawing.Transform2D(
                                    new DocumentFormat.OpenXml.Drawing.Offset { X = 0, Y = 0 },
                                    new DocumentFormat.OpenXml.Drawing.Extents { Cx = widthEmu, Cy = heightEmu }),
                                new DocumentFormat.OpenXml.Drawing.PresetGeometry(
                                    new DocumentFormat.OpenXml.Drawing.AdjustValueList()) { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle }))
                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0,
                DistanceFromBottom = 0,
                DistanceFromLeft = 0,
                DistanceFromRight = 0
            });
        
        return element;
    }

    private void ConvertMarkdownToWord(string markdown, string outputPath)
    {
        // Reset image ID counter for each new document
        _imageIdCounter = 1;
        
        // Create the document
        using (var document = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            // Use Markdig to parse the markdown
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
            var markdownDoc = Markdown.Parse(markdown, pipeline);

            // Get the base directory for resolving relative image paths
            var baseDir = !string.IsNullOrEmpty(_currentFilePath) 
                ? Path.GetDirectoryName(_currentFilePath) 
                : Environment.CurrentDirectory;

            // Process each block in the markdown document
            var body = mainPart.Document.Body!;
            foreach (var block in markdownDoc)
            {
                ProcessMarkdownBlock(block, body, mainPart, baseDir ?? Environment.CurrentDirectory);
            }
            
            // Explicitly save the document
            mainPart.Document.Save();
        }
        
        // Fix the Content_Types.xml which OpenXML SDK 3.x generates incorrectly
        FixWordDocumentContentTypes(outputPath);
    }
    
    private static void FixWordDocumentContentTypes(string docxPath)
    {
        // Open the docx as a ZIP archive and fix the [Content_Types].xml
        using var archive = ZipFile.Open(docxPath, ZipArchiveMode.Update);
        
        // Get the content types entry
        var contentTypesEntry = archive.GetEntry("[Content_Types].xml");
        if (contentTypesEntry == null) return;
        
        // Read the current content
        string content;
        using (var stream = contentTypesEntry.Open())
        using (var reader = new StreamReader(stream))
        {
            content = reader.ReadToEnd();
        }
        
        // Fix the incorrect Default entry for XML files
        // Change: <Default Extension="xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml" />
        // To: <Default Extension="xml" ContentType="application/xml" /> + <Override PartName="/word/document.xml" ContentType="..." />
        if (content.Contains("Extension=\"xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\""))
        {
            content = content.Replace(
                "Extension=\"xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"",
                "Extension=\"xml\" ContentType=\"application/xml\"");
            
            // Add the Override element before the closing </Types> tag
            content = content.Replace(
                "</Types>",
                "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\" /></Types>");
            
            // Delete the old entry and create a new one with the fixed content
            contentTypesEntry.Delete();
            var newEntry = archive.CreateEntry("[Content_Types].xml");
            using var writeStream = newEntry.Open();
            using var writer = new StreamWriter(writeStream);
            writer.Write(content);
        }
    }

    private void ProcessMarkdownBlock(Block block, Body body, MainDocumentPart mainPart, string baseDir)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var fontSize = heading.Level switch
                {
                    1 => "32",
                    2 => "28",
                    3 => "26",
                    4 => "24",
                    5 => "22",
                    _ => "20"
                };
                var headingPara = new Paragraph();
                ProcessInlines(heading.Inline, headingPara, mainPart, baseDir, true, fontSize);
                body.AppendChild(headingPara);
                break;

            case ParagraphBlock paragraph:
                var para = new Paragraph();
                ProcessInlines(paragraph.Inline, para, mainPart, baseDir, false, null);
                body.AppendChild(para);
                break;

            case FencedCodeBlock codeBlock:
                var codeLines = codeBlock.Lines.ToString().Split('\n');
                foreach (var codeLine in codeLines)
                {
                    var codePara = new Paragraph(
                        new ParagraphProperties(
                            new Shading { Val = ShadingPatternValues.Clear, Fill = "E8E8E8" }
                        ),
                        new Run(
                            new RunProperties(
                                new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
                                new FontSize { Val = "20" }
                            ),
                            new Text(codeLine.TrimEnd('\r')) { Space = SpaceProcessingModeValues.Preserve }
                        )
                    );
                    body.AppendChild(codePara);
                }
                break;

            case CodeBlock simpleCodeBlock:
                var simpleCodeLines = simpleCodeBlock.Lines.ToString().Split('\n');
                foreach (var codeLine in simpleCodeLines)
                {
                    var codePara = new Paragraph(
                        new ParagraphProperties(
                            new Shading { Val = ShadingPatternValues.Clear, Fill = "E8E8E8" }
                        ),
                        new Run(
                            new RunProperties(
                                new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
                                new FontSize { Val = "20" }
                            ),
                            new Text(codeLine.TrimEnd('\r')) { Space = SpaceProcessingModeValues.Preserve }
                        )
                    );
                    body.AppendChild(codePara);
                }
                break;

            case ListBlock listBlock:
                ProcessListBlock(listBlock, body, mainPart, baseDir, 0);
                break;

            case QuoteBlock quoteBlock:
                foreach (var quoteChild in quoteBlock)
                {
                    if (quoteChild is ParagraphBlock quotePara)
                    {
                        var blockquotePara = new Paragraph(
                            new ParagraphProperties(
                                new Indentation { Left = "720" },
                                new ParagraphBorders(
                                    new LeftBorder { Val = BorderValues.Single, Size = 24, Color = "CCCCCC" }
                                )
                            )
                        );
                        ProcessInlines(quotePara.Inline, blockquotePara, mainPart, baseDir, false, null);
                        body.AppendChild(blockquotePara);
                    }
                }
                break;

            case ThematicBreakBlock:
                var hrPara = new Paragraph(
                    new ParagraphProperties(
                        new ParagraphBorders(
                            new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "CCCCCC" }
                        )
                    )
                );
                body.AppendChild(hrPara);
                break;

            default:
                // For any unhandled block types, add an empty paragraph
                body.AppendChild(new Paragraph());
                break;
        }
    }

    private void ProcessListBlock(ListBlock listBlock, Body body, MainDocumentPart mainPart, string baseDir, int indentLevel)
    {
        var isOrdered = listBlock.IsOrdered;
        var itemNumber = 1;

        foreach (var item in listBlock)
        {
            if (item is ListItemBlock listItem)
            {
                foreach (var itemContent in listItem)
                {
                    if (itemContent is ParagraphBlock itemPara)
                    {
                        var bullet = isOrdered ? $"{itemNumber}. " : "- ";
                        var listPara = new Paragraph(
                            new ParagraphProperties(
                                new Indentation { Left = ((indentLevel + 1) * 360).ToString() }
                            )
                        );
                        
                        // Add bullet/number
                        listPara.AppendChild(new Run(new Text(bullet) { Space = SpaceProcessingModeValues.Preserve }));
                        
                        // Add content
                        ProcessInlines(itemPara.Inline, listPara, mainPart, baseDir, false, null);
                        body.AppendChild(listPara);
                    }
                    else if (itemContent is ListBlock nestedList)
                    {
                        ProcessListBlock(nestedList, body, mainPart, baseDir, indentLevel + 1);
                    }
                }
                itemNumber++;
            }
        }
    }

    private void ProcessInlines(ContainerInline? inlines, Paragraph para, MainDocumentPart mainPart, string baseDir, bool isBold, string? fontSize)
    {
        if (inlines == null) return;

        foreach (var inline in inlines)
        {
            ProcessInline(inline, para, mainPart, baseDir, isBold, false, fontSize);
        }
    }

    private void ProcessInline(Inline inline, Paragraph para, MainDocumentPart mainPart, string baseDir, bool isBold, bool isItalic, string? fontSize)
    {
        switch (inline)
        {
            case LiteralInline literal:
                var run = new Run();
                var runProps = new RunProperties();
                
                if (isBold) runProps.AppendChild(new Bold());
                if (isItalic) runProps.AppendChild(new Italic());
                if (fontSize != null) runProps.AppendChild(new FontSize { Val = fontSize });
                
                if (runProps.HasChildren) run.AppendChild(runProps);
                run.AppendChild(new Text(literal.Content.ToString()) { Space = SpaceProcessingModeValues.Preserve });
                para.AppendChild(run);
                break;

            case EmphasisInline emphasis:
                var newBold = isBold || emphasis.DelimiterCount >= 2;
                var newItalic = isItalic || emphasis.DelimiterCount == 1 || emphasis.DelimiterCount == 3;
                foreach (var child in emphasis)
                {
                    ProcessInline(child, para, mainPart, baseDir, newBold, newItalic, fontSize);
                }
                break;

            case CodeInline code:
                var codeRun = new Run();
                var codeRunProps = new RunProperties();
                codeRunProps.AppendChild(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" });
                codeRunProps.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "E8E8E8" });
                if (fontSize != null) codeRunProps.AppendChild(new FontSize { Val = fontSize });
                codeRun.AppendChild(codeRunProps);
                codeRun.AppendChild(new Text(code.Content) { Space = SpaceProcessingModeValues.Preserve });
                para.AppendChild(codeRun);
                break;

            case LinkInline link:
                if (link.IsImage)
                {
                    // Handle image
                    var imagePath = link.Url;
                    if (imagePath != null && !imagePath.StartsWith("http"))
                    {
                        // Resolve relative path
                        var fullPath = Path.Combine(baseDir, imagePath);
                        if (File.Exists(fullPath))
                        {
                            try
                            {
                                var imageBytes = File.ReadAllBytes(fullPath);
                                var imageElement = EmbedImageInWord(mainPart, imageBytes, fullPath);
                                if (imageElement != null)
                                {
                                    para.AppendChild(new Run(imageElement));
                                }
                            }
                            catch
                            {
                                // If image loading fails, add placeholder text
                                para.AppendChild(new Run(new Text($"[Image: {link.Url}]")));
                            }
                        }
                        else
                        {
                            para.AppendChild(new Run(new Text($"[Image not found: {link.Url}]")));
                        }
                    }
                    else
                    {
                        // External URL - add as text
                        para.AppendChild(new Run(new Text($"[Image: {link.Url}]")));
                    }
                }
                else
                {
                    // Handle regular link - just show the text
                    foreach (var child in link)
                    {
                        ProcessInline(child, para, mainPart, baseDir, isBold, isItalic, fontSize);
                    }
                    if (link.Url != null)
                    {
                        var urlRun = new Run();
                        var urlProps = new RunProperties();
                        urlProps.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "0066CC" });
                        urlRun.AppendChild(urlProps);
                        urlRun.AppendChild(new Text($" ({link.Url})") { Space = SpaceProcessingModeValues.Preserve });
                        para.AppendChild(urlRun);
                    }
                }
                break;

            case LineBreakInline:
                para.AppendChild(new Run(new Break()));
                break;

            case ContainerInline container:
                foreach (var child in container)
                {
                    ProcessInline(child, para, mainPart, baseDir, isBold, isItalic, fontSize);
                }
                break;
        }
    }

    private Drawing? EmbedImageInWord(MainDocumentPart mainPart, byte[] imageBytes, string imagePath)
    {
        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        var imagePartType = extension switch
        {
            ".png" => ImagePartType.Png,
            ".jpg" or ".jpeg" => ImagePartType.Jpeg,
            ".gif" => ImagePartType.Gif,
            ".bmp" => ImagePartType.Bmp,
            _ => ImagePartType.Png
        };

        var imagePart = mainPart.AddImagePart(imagePartType);
        using (var stream = new MemoryStream(imageBytes))
        {
            imagePart.FeedData(stream);
        }

        // Get image dimensions
        long widthEmu, heightEmu;
        using (var stream = new MemoryStream(imageBytes))
        {
            using var bitmap = new System.Drawing.Bitmap(stream);
            // Convert pixels to EMUs (914400 EMUs per inch, assuming 96 DPI)
            // Use long to avoid integer overflow with large images
            widthEmu = (long)bitmap.Width * 914400L / 96L;
            heightEmu = (long)bitmap.Height * 914400L / 96L;

            // Limit max width to 6 inches (5486400 EMUs)
            const long maxWidthEmu = 6L * 914400L;
            if (widthEmu > maxWidthEmu)
            {
                var ratio = (double)maxWidthEmu / widthEmu;
                widthEmu = maxWidthEmu;
                heightEmu = (long)(heightEmu * ratio);
            }
            
            // Limit max height to 9 inches (8229600 EMUs) to fit on page
            const long maxHeightEmu = 9L * 914400L;
            if (heightEmu > maxHeightEmu)
            {
                var ratio = (double)maxHeightEmu / heightEmu;
                heightEmu = maxHeightEmu;
                widthEmu = (long)(widthEmu * ratio);
            }
        }

        var relationshipId = mainPart.GetIdOfPart(imagePart);
        var imageName = Path.GetFileNameWithoutExtension(imagePath);
        return CreateImageElement(relationshipId, (int)widthEmu, (int)heightEmu, imageName);
    }

    private void UpdateExportMenuVisibility()
    {
        // Show diagram exports only for Mermaid files
        // Show Word/PDF export for both (works for both Mermaid and Markdown)
        var isMermaid = _currentRenderMode == RenderMode.Mermaid;
        
        // Menu items
        ExportPngMenuItem.Visibility = isMermaid ? Visibility.Visible : Visibility.Collapsed;
        ExportSvgMenuItem.Visibility = isMermaid ? Visibility.Visible : Visibility.Collapsed;
        ExportEmfMenuItem.Visibility = isMermaid ? Visibility.Visible : Visibility.Collapsed;
        ExportWordMenuItem.Visibility = Visibility.Visible; // Always visible - works for both
        
        // Toolbar buttons - PNG/SVG/EMF only make sense for Mermaid diagrams
        ExportPngToolbarButton.Visibility = isMermaid ? Visibility.Visible : Visibility.Collapsed;
        ExportSvgToolbarButton.Visibility = isMermaid ? Visibility.Visible : Visibility.Collapsed;
        ExportEmfToolbarButton.Visibility = isMermaid ? Visibility.Visible : Visibility.Collapsed;
        ExportWordToolbarButton.Visibility = Visibility.Visible; // Always visible - works for both
    }

    private string SanitizeSvgForXml(string svg)
    {
        // Mermaid generates SVG with HTML elements that aren't valid XML
        // Fix common issues:
        
        // 1. Convert self-closing HTML tags to XML-compliant format
        svg = Regex.Replace(svg, @"<br\s*>", "<br/>", RegexOptions.IgnoreCase);
        svg = Regex.Replace(svg, @"<hr\s*>", "<hr/>", RegexOptions.IgnoreCase);
        
        // 2. Remove foreignObject elements which contain HTML that Svg.NET can't parse
        svg = Regex.Replace(svg, @"<foreignObject[^>]*>.*?</foreignObject>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        // 3. Remove any remaining HTML tags that might cause issues
        svg = Regex.Replace(svg, @"<span[^>]*>", "", RegexOptions.IgnoreCase);
        svg = Regex.Replace(svg, @"</span>", "", RegexOptions.IgnoreCase);
        svg = Regex.Replace(svg, @"<div[^>]*>", "", RegexOptions.IgnoreCase);
        svg = Regex.Replace(svg, @"</div>", "", RegexOptions.IgnoreCase);
        
        return svg;
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void PrintPreview_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewInitialized) return;

        try
        {
            // Capture the current diagram/markdown as an image
            var pngBytes = await CaptureDiagramAsPngBytes();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                MessageBox.Show("Unable to capture the preview for printing.", "Print Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Convert bytes to BitmapSource
            using var stream = new MemoryStream(pngBytes);
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            // Get document title for print job
            var documentTitle = _activeDocument?.DisplayName ?? "Untitled";
            if (documentTitle.EndsWith(".mmd") || documentTitle.EndsWith(".md"))
                documentTitle = Path.GetFileNameWithoutExtension(documentTitle);

            // Show print preview dialog (pass isMarkdown flag for default scaling)
            var isMarkdown = _currentRenderMode == RenderMode.Markdown;
            var printDialog = new PrintPreviewDialog(bitmap, documentTitle, isMarkdown);
            printDialog.Owner = this;
            printDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error preparing print preview: {ex.Message}", "Print Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<byte[]?> CaptureDiagramAsPngBytes()
    {
        if (!_webViewInitialized) return null;

        try
        {
            // For Mermaid diagrams, use high-res SVG export to capture full diagram
            if (_currentRenderMode == RenderMode.Mermaid)
            {
                // Use scale 2 for print preview (good balance of quality and performance)
                _pngExportTcs = new TaskCompletionSource<string>();
                
                await PreviewWebView.CoreWebView2.ExecuteScriptAsync("window.exportPngHighRes(2)");
                
                // Wait for the callback with a timeout
                var timeoutTask = Task.Delay(15000); // 15 second timeout
                var completedTask = await Task.WhenAny(_pngExportTcs.Task, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    _pngExportTcs = null;
                    // Fall back to viewport capture on timeout
                    return await CaptureViewportAsPngBytes();
                }
                
                var dataUrl = await _pngExportTcs.Task;
                _pngExportTcs = null;
                
                if (!string.IsNullOrEmpty(dataUrl) && dataUrl.StartsWith("data:image/png;base64,"))
                {
                    var base64Data = dataUrl.Substring("data:image/png;base64,".Length);
                    return Convert.FromBase64String(base64Data);
                }
                
                // Fall back to viewport capture if high-res export fails
                return await CaptureViewportAsPngBytes();
            }
            else
            {
                // For Markdown, capture the full scrollable content
                return await CaptureFullMarkdownAsPngBytes();
            }
        }
        catch
        {
            // Fall back to viewport capture on any error
            try
            {
                return await CaptureViewportAsPngBytes();
            }
            catch
            {
                return null;
            }
        }
    }
    
    private async Task<byte[]?> CaptureFullMarkdownAsPngBytes()
    {
        try
        {
            // Use callback pattern with postMessage (same as mermaid export)
            _pngExportTcs = new TaskCompletionSource<string>();
            
            // Inject html2canvas and capture full content, sending result via postMessage
            var script = @"
                (async function() {
                    try {
                        // Check if html2canvas is available, if not load it
                        if (typeof html2canvas === 'undefined') {
                            await new Promise((resolve, reject) => {
                                const script = document.createElement('script');
                                script.src = 'https://cdnjs.cloudflare.com/ajax/libs/html2canvas/1.4.1/html2canvas.min.js';
                                script.onload = resolve;
                                script.onerror = reject;
                                document.head.appendChild(script);
                            });
                        }
                        
                        // Get the content element
                        const content = document.getElementById('content') || document.body;
                        
                        // Capture the full content
                        const canvas = await html2canvas(content, {
                            scale: 2,
                            useCORS: true,
                            allowTaint: true,
                            backgroundColor: '#ffffff',
                            width: content.scrollWidth,
                            height: content.scrollHeight,
                            windowWidth: content.scrollWidth,
                            windowHeight: content.scrollHeight
                        });
                        
                        const dataUrl = canvas.toDataURL('image/png');
                        window.chrome.webview.postMessage({ type: 'pngExport', data: dataUrl });
                    } catch (err) {
                        window.chrome.webview.postMessage({ type: 'pngExportError', error: err.message });
                    }
                })();
            ";
            
            await PreviewWebView.CoreWebView2.ExecuteScriptAsync(script);
            
            // Wait for the callback with a timeout
            var timeoutTask = Task.Delay(15000); // 15 second timeout
            var completedTask = await Task.WhenAny(_pngExportTcs.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                _pngExportTcs = null;
                // Fall back to viewport capture on timeout
                return await CaptureViewportAsPngBytes();
            }
            
            var dataUrl = await _pngExportTcs.Task;
            _pngExportTcs = null;
            
            if (!string.IsNullOrEmpty(dataUrl) && dataUrl.StartsWith("data:image/png;base64,"))
            {
                var base64Data = dataUrl.Substring("data:image/png;base64,".Length);
                return Convert.FromBase64String(base64Data);
            }
            
            // Fall back to viewport capture
            return await CaptureViewportAsPngBytes();
        }
        catch
        {
            _pngExportTcs = null;
            // Fall back to viewport capture on error
            return await CaptureViewportAsPngBytes();
        }
    }

    private async Task<byte[]?> CaptureViewportAsPngBytes()
    {
        using var memoryStream = new MemoryStream();
        await PreviewWebView.CoreWebView2.CapturePreviewAsync(
            CoreWebView2CapturePreviewImageFormat.Png, memoryStream);
        memoryStream.Position = 0;
        return memoryStream.ToArray();
    }

    private void PrintCode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get the code from the editor
            var code = CodeEditor.Text;
            var documentTitle = _activeDocument?.DisplayName ?? "Untitled";

            // Show print code preview dialog
            var printCodeDialog = new PrintCodePreviewDialog(code, documentTitle);
            printCodeDialog.Owner = this;
            printCodeDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error printing code: {ex.Message}", "Print Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool _isUpdatingZoomSlider = false;
    
    private async void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewInitialized) return;
        _currentZoom = Math.Min(_currentZoom * 1.25, 5);
        await ApplyZoom();
    }

    private async void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewInitialized) return;
        _currentZoom = Math.Max(_currentZoom / 1.25, 0.1);
        await ApplyZoom();
    }

    private async void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewInitialized) return;
        _currentZoom = 1.0;
        // Sync zoom to active document so it's preserved when switching tabs
        if (_activeDocument != null)
        {
            _activeDocument.PreviewZoom = _currentZoom;
        }
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync("window.resetView()");
        UpdateZoomUI();
    }

    private async void FitToWindow_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewInitialized) return;
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync("window.fitToWindow()");
    }
    
    private async void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingZoomSlider || !_webViewInitialized) return;
        _currentZoom = e.NewValue / 100.0;
        // Sync zoom to active document so it's preserved when switching tabs
        if (_activeDocument != null)
        {
            _activeDocument.PreviewZoom = _currentZoom;
        }
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.setZoom({_currentZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        ZoomLevelText.Text = $"{e.NewValue:F0}%";
    }
    
    private async Task ApplyZoom()
    {
        // Sync zoom to active document so it's preserved when switching tabs
        if (_activeDocument != null)
        {
            _activeDocument.PreviewZoom = _currentZoom;
        }
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.setZoom({_currentZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        UpdateZoomUI();
    }
    
    private void UpdateZoomUI()
    {
        _isUpdatingZoomSlider = true;
        ZoomSlider.Value = _currentZoom * 100;
        ZoomLevelText.Text = $"{_currentZoom * 100:F0}%";
        _isUpdatingZoomSlider = false;
    }
    
    /// <summary>
    /// Saves the current preview scroll position and panzoom pan position to the document model
    /// </summary>
    private async Task SavePreviewScrollPositionAsync(DocumentModel doc)
    {
        if (!_webViewInitialized || doc == null) return;
        
        try
        {
            // Get both scroll position and panzoom transform
            // For Mermaid: use container element scroll and panzoom transform
            // For Markdown: use window/document scroll (no panzoom)
            var result = await PreviewWebView.CoreWebView2.ExecuteScriptAsync(@"
                (function() {
                    var container = document.getElementById('container');
                    var transform = window.panzoomInstance ? window.panzoomInstance.getTransform() : { x: 0, y: 0, scale: 1 };
                    var scrollLeft, scrollTop;
                    if (container) {
                        // Mermaid diagram - use container scroll
                        scrollLeft = container.scrollLeft;
                        scrollTop = container.scrollTop;
                    } else {
                        // Markdown - try multiple scroll sources (html/body can both have overflow:auto)
                        scrollLeft = window.pageXOffset || document.documentElement.scrollLeft || document.body.scrollLeft || 0;
                        scrollTop = window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop || 0;
                    }
                    return JSON.stringify({ 
                        scrollLeft: scrollLeft || 0, 
                        scrollTop: scrollTop || 0,
                        panX: transform.x || 0,
                        panY: transform.y || 0
                    });
                })()
            ");
            
            if (!string.IsNullOrEmpty(result) && result != "null")
            {
                var json = System.Text.Json.JsonDocument.Parse(result.Trim('"').Replace("\\\"", "\""));
                doc.PreviewScrollLeft = json.RootElement.GetProperty("scrollLeft").GetDouble();
                doc.PreviewScrollTop = json.RootElement.GetProperty("scrollTop").GetDouble();
                doc.PreviewPanX = json.RootElement.GetProperty("panX").GetDouble();
                doc.PreviewPanY = json.RootElement.GetProperty("panY").GetDouble();
            }
        }
        catch { }
    }

    private void SyntaxHelp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://mermaid.js.org/intro/syntax-reference.html",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MarkdownHelp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.markdownguide.org/basic-syntax/",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        // Create custom About dialog with Mermaid icon
        var aboutWindow = new Window
        {
            Title = "About Mermaid Editor",
            Width = 500,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = (SolidColorBrush)System.Windows.Application.Current.Resources["ThemeBackgroundBrush"]
        };
        
        var mainGrid = new Grid { Margin = new Thickness(20) };
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        
        // Header with icon and title
        var headerPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        
        // Load the icon from embedded resource
        var iconImage = new System.Windows.Controls.Image
        {
            Width = 64,
            Height = 64,
            Margin = new Thickness(0, 0, 16, 0)
        };
        try
        {
            var iconUri = new Uri("pack://application:,,,/app.ico", UriKind.Absolute);
            var decoder = new System.Windows.Media.Imaging.IconBitmapDecoder(
                iconUri,
                System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count > 0)
            {
                // Find the largest frame for best quality
                var largestFrame = decoder.Frames[0];
                foreach (var frame in decoder.Frames)
                {
                    if (frame.PixelWidth > largestFrame.PixelWidth)
                    {
                        largestFrame = frame;
                    }
                }
                iconImage.Source = largestFrame;
            }
        }
        catch
        {
            // If icon fails to load, continue without it
        }
        
        var titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titlePanel.Children.Add(new TextBlock
        {
            Text = "Mermaid Editor",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["ThemeForegroundBrush"]
        });
        titlePanel.Children.Add(new TextBlock
        {
            Text = "Version 3.0.0",
            FontSize = 14,
            Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["ThemeDisabledForegroundBrush"],
            Margin = new Thickness(0, 4, 0, 0)
        });
        
        headerPanel.Children.Add(iconImage);
        headerPanel.Children.Add(titlePanel);
        Grid.SetRow(headerPanel, 0);
        
        // Content with description
        var contentScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var contentText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["ThemeForegroundBrush"],
            Text = "A visual IDE for editing Mermaid diagrams and Markdown files.\n\n" +
                   "Features:\n" +
                   "- Live preview as you type\n" +
                   "- Mermaid diagram rendering with pan/zoom\n" +
                   "- Markdown rendering with GitHub styling\n" +
                   "- Syntax highlighting and IntelliSense\n" +
                   "- Click-to-navigate between preview and code\n" +
                   "- Navigation dropdown for quick section jumping\n" +
                   "- Export to PNG, SVG, EMF, and Word\n" +
                   "- Save to PDF via Print Preview\n" +
                   "- Auto-save with session restore\n" +
                   "- Spell check with suggestions (markdown)\n" +
                   "- Table generator dialog (markdown)\n" +
                   "- Ask AI chat with file attachments\n" +
                   "- Settings/Configuration with theme support\n" +
                   "- Bracket matching and minimap\n" +
                   "- Custom SVG toolbar icons\n" +
                   "- New document templates for all diagram types\n" +
                   "- File browser with preview on selection\n" +
                   "- Drag and drop file support\n" +
                   "- Undo/Redo support\n\n" +
                   "Supported file types:\n" +
                   "- .mmd, .mermaid - Mermaid diagrams\n" +
                   "- .md - Markdown files\n\n" +
                   "\u00A9 2026 Lee Cassin\n\n" +
                   "Licensed under the GNU General Public License v3.0\n" +
                   "This is free software; you are free to change and redistribute it.\n" +
                   "See LICENSE file for details."
        };
        contentScroll.Content = contentText;
        Grid.SetRow(contentScroll, 1);
        
        // OK button
        var okButton = new System.Windows.Controls.Button
        {
            Content = "OK",
            Width = 80,
            Padding = new Thickness(8, 6, 8, 6),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Background = (SolidColorBrush)System.Windows.Application.Current.Resources["ThemeToolbarBackgroundBrush"],
            Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["ThemeForegroundBrush"],
            BorderBrush = (SolidColorBrush)System.Windows.Application.Current.Resources["ThemeBorderBrush"]
        };
        okButton.Click += (s, args) => aboutWindow.Close();
        Grid.SetRow(okButton, 2);
        
        mainGrid.Children.Add(headerPanel);
        mainGrid.Children.Add(contentScroll);
        mainGrid.Children.Add(okButton);
        
        aboutWindow.Content = mainGrid;
        aboutWindow.ShowDialog();
    }

    private void ApplyTheme(AppTheme theme)
    {
        ThemeManager.ApplyTheme(theme);
        UpdateEditorTheme();
        UpdateTitleBarTheme();
        UpdateTabStyles(); // Update tab colors for new theme
        SvgIconHelper.ClearCache();
        InitializeIcons();
        // Force-refresh comment icon color since InitializeIcons reset all icons to default
        UpdateToggleCommentIconColor(force: true);
        RenderPreview(); // Re-render preview with new theme
    }

    private void UpdateEditorTheme()
    {
        var colors = ThemeManager.GetThemeColors(ThemeManager.CurrentTheme);
        
        // Update AvalonEdit colors
        CodeEditor.Background = new SolidColorBrush(colors.EditorBackground);
        CodeEditor.Foreground = new SolidColorBrush(colors.EditorForeground);
        CodeEditor.LineNumbersForeground = new SolidColorBrush(colors.LineNumber);
        
        // Update syntax highlighting based on theme
        RegisterThemeSyntaxHighlighting();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Saved)
        {
            // Apply theme change if needed
            if (dialog.ThemeChanged)
            {
                UpdateEditorTheme();
                UpdateTitleBarTheme();
                UpdateTabStyles();
                SvgIconHelper.ClearCache();
                InitializeIcons();
                // Force-refresh comment icon color since InitializeIcons reset all icons to default
                UpdateToggleCommentIconColor(force: true);
                RenderPreview();
            }

            // Apply editor settings
            ApplySettingsToEditor();
        }
    }

    private void AskAi_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AskAiDialog
        {
            Owner = this,
            EditorContent = CodeEditor.Text,
            FileType = _currentRenderMode == RenderMode.Mermaid ? "Mermaid" : "Markdown"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.TextToInsert))
        {
            // Insert the AI-generated text at the current cursor position
            var offset = CodeEditor.CaretOffset;
            CodeEditor.Document.Insert(offset, dialog.TextToInsert);
        }
    }

    /// <summary>
    /// Applies all settings from SettingsManager to the editor and auto-save timer.
    /// Called on startup and after the Settings dialog is closed with OK.
    /// </summary>
    private void ApplySettingsToEditor()
    {
        var settings = SettingsManager.Current;

        // Editor font
        CodeEditor.FontFamily = new System.Windows.Media.FontFamily(settings.EditorFontFamily);
        CodeEditor.FontSize = settings.EditorFontSize;

        // Word wrap
        CodeEditor.WordWrap = settings.WordWrapDefault;
        if (WordWrapToggle != null) WordWrapToggle.IsChecked = settings.WordWrapDefault;
        if (WordWrapMenuItem != null) WordWrapMenuItem.IsChecked = settings.WordWrapDefault;
        // Sync minimap word wrap
        if (_isMinimapVisible && MinimapEditor != null)
        {
            MinimapEditor.WordWrap = settings.WordWrapDefault;
        }
        UpdateWordWrapIcons();

        // Line numbers
        CodeEditor.ShowLineNumbers = settings.ShowLineNumbersDefault;
        if (LineNumbersToggle != null) LineNumbersToggle.IsChecked = settings.ShowLineNumbersDefault;
        if (LineNumbersMenuItem != null) LineNumbersMenuItem.IsChecked = settings.ShowLineNumbersDefault;
        UpdateLineNumbersIcons();

        // Bracket matching
        _isBracketMatchingEnabled = settings.BracketMatchingDefault;
        if (settings.BracketMatchingDefault)
        {
            EnableBracketHighlighting();
        }
        else
        {
            DisableBracketHighlighting();
        }
        if (BracketMatchingToggle != null) BracketMatchingToggle.IsChecked = settings.BracketMatchingDefault;
        if (BracketMatchingMenuItem != null) BracketMatchingMenuItem.IsChecked = settings.BracketMatchingDefault;
        UpdateBracketMatchingIcons();

        // Minimap
        if (settings.ShowMinimapDefault != _isMinimapVisible)
        {
            _isMinimapVisible = settings.ShowMinimapDefault;
            if (_isMinimapVisible)
            {
                ShowMinimap();
            }
            else
            {
                HideMinimap();
            }
        }
        if (MinimapToggle != null) MinimapToggle.IsChecked = settings.ShowMinimapDefault;
        if (MinimapMenuItem != null) MinimapMenuItem.IsChecked = settings.ShowMinimapDefault;
        UpdateMinimapIcons();

        // Spell check
        _isSpellCheckEnabled = settings.SpellCheckEnabled;
        if (_isSpellCheckEnabled)
        {
            EnableSpellCheck();
        }
        else
        {
            DisableSpellCheck();
        }
        if (SpellCheckToggle != null) SpellCheckToggle.IsChecked = _isSpellCheckEnabled;
        if (SpellCheckMenuItem != null) SpellCheckMenuItem.IsChecked = _isSpellCheckEnabled;
        UpdateSpellCheckIcons();

        // Auto-save
        _autoSaveIntervalSeconds = settings.AutoSaveIntervalSeconds;
        _autoSaveTimer.Interval = TimeSpan.FromSeconds(_autoSaveIntervalSeconds);
        if (settings.AutoSaveEnabled)
        {
            _autoSaveTimer.Start();
        }
        else
        {
            _autoSaveTimer.Stop();
        }
    }

    private void RegisterThemeSyntaxHighlighting()
    {
        var isDark = ThemeManager.IsDarkTheme;
        
        // Color values based on theme
        var commentColor = isDark ? "#6A9955" : "#008000";
        var keywordColor = isDark ? "#569CD6" : "#0000FF";
        var diagramTypeColor = isDark ? "#C586C0" : "#AF00DB";
        
        // Register Mermaid syntax highlighting
        var mermaidXshd = "<?xml version=\"1.0\"?>" +
            "<SyntaxDefinition name=\"Mermaid\" xmlns=\"http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008\">" +
            "<Color name=\"Comment\" foreground=\"" + commentColor + "\" />" +
            "<Color name=\"Keyword\" foreground=\"" + keywordColor + "\" fontWeight=\"bold\" />" +
            "<Color name=\"DiagramType\" foreground=\"" + diagramTypeColor + "\" fontWeight=\"bold\" />" +
            "<RuleSet>" +
            "<Span color=\"Comment\" begin=\"%%\" />" +
            "<Keywords color=\"DiagramType\">" +
            "<Word>flowchart</Word><Word>graph</Word><Word>sequenceDiagram</Word>" +
            "<Word>classDiagram</Word><Word>stateDiagram</Word><Word>erDiagram</Word>" +
            "<Word>journey</Word><Word>gantt</Word><Word>pie</Word><Word>mindmap</Word>" +
            "<Word>timeline</Word><Word>gitGraph</Word><Word>quadrantChart</Word>" +
            "<Word>requirementDiagram</Word><Word>C4Context</Word><Word>C4Container</Word><Word>C4Component</Word><Word>C4Dynamic</Word><Word>C4Deployment</Word>" +
            "</Keywords>" +
            "<Keywords color=\"Keyword\">" +
            "<Word>subgraph</Word><Word>end</Word><Word>direction</Word>" +
            "<Word>participant</Word><Word>actor</Word><Word>activate</Word><Word>deactivate</Word>" +
            "<Word>Note</Word><Word>note</Word><Word>loop</Word><Word>alt</Word><Word>else</Word>" +
            "<Word>opt</Word><Word>par</Word><Word>critical</Word><Word>break</Word><Word>rect</Word>" +
            "<Word>class</Word><Word>state</Word><Word>section</Word><Word>title</Word>" +
            "<Word>TB</Word><Word>TD</Word><Word>BT</Word><Word>RL</Word><Word>LR</Word>" +
            "<Word>requirement</Word><Word>functionalRequirement</Word><Word>performanceRequirement</Word>" +
            "<Word>interfaceRequirement</Word><Word>physicalRequirement</Word><Word>designConstraint</Word>" +
            "<Word>element</Word><Word>satisfies</Word><Word>traces</Word><Word>contains</Word>" +
            "<Word>derives</Word><Word>refines</Word><Word>verifies</Word><Word>copies</Word>" +
            "</Keywords>" +
            "</RuleSet>" +
            "</SyntaxDefinition>";

        try
        {
            using var mermaidReader = new XmlTextReader(new StringReader(mermaidXshd));
            var mermaidDef = HighlightingLoader.Load(mermaidReader, HighlightingManager.Instance);
            HighlightingManager.Instance.RegisterHighlighting("Mermaid", new[] { ".mmd", ".mermaid" }, mermaidDef);
        }
        catch
        {
            // If syntax highlighting fails, continue without it
        }

        // Register Markdown syntax highlighting (with theme-aware comment color)
        var markdownXshd = "<?xml version=\"1.0\"?>" +
            "<SyntaxDefinition name=\"Markdown\" xmlns=\"http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008\">" +
            "<Color name=\"Comment\" foreground=\"" + commentColor + "\" />" +
            "<RuleSet>" +
            "<Span color=\"Comment\" multiline=\"true\">" +
            "<Begin>&lt;!--</Begin>" +
            "<End>--&gt;</End>" +
            "</Span>" +
            "</RuleSet>" +
            "</SyntaxDefinition>";

        try
        {
            using var markdownReader = new XmlTextReader(new StringReader(markdownXshd));
            var markdownDef = HighlightingLoader.Load(markdownReader, HighlightingManager.Instance);
            HighlightingManager.Instance.RegisterHighlighting("Markdown", new[] { ".md", ".markdown" }, markdownDef);
        }
        catch
        {
            // If syntax highlighting fails, continue without it
        }

        // Apply the correct highlighting based on the active document type
        if (_currentRenderMode == RenderMode.Markdown)
        {
            CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Markdown");
        }
        else
        {
            CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Mermaid");
        }
    }

    private void RegisterMarkdownSyntaxHighlighting()
    {
        var isDark = ThemeManager.IsDarkTheme;
        
        // Color values based on theme - green for comments like Mermaid
        var commentColor = isDark ? "#6A9955" : "#008000";
        
        // Simplified XSHD - only HTML comments to avoid regex issues
        var xshd = "<?xml version=\"1.0\"?>" +
            "<SyntaxDefinition name=\"Markdown\" xmlns=\"http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008\">" +
            "<Color name=\"Comment\" foreground=\"" + commentColor + "\" />" +
            "<RuleSet>" +
            "<Span color=\"Comment\" multiline=\"true\">" +
            "<Begin>&lt;!--</Begin>" +
            "<End>--&gt;</End>" +
            "</Span>" +
            "</RuleSet>" +
            "</SyntaxDefinition>";

        try
        {
            using var reader = new XmlTextReader(new StringReader(xshd));
            var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            HighlightingManager.Instance.RegisterHighlighting("Markdown", new[] { ".md", ".markdown" }, definition);
            CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Markdown");
        }
        catch
        {
            // If syntax highlighting fails, continue without it
        }
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
                
                // Set caption color based on theme
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

    private void RenderMermaidPreview(string code)
    {
        var html = $@"<!DOCTYPE html>
<html>
<head>
    <script src=""https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/panzoom@9.4.3/dist/panzoom.min.js""></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        html, body {{ 
            width: 100%; 
            height: 100%; 
            overflow: hidden;
            background: #f5f5f5;
        }}
        #container {{
            width: 100%;
            height: 100%;
            display: flex;
            align-items: flex-start;
            justify-content: flex-start;
            padding: 20px;
            overflow: auto;
        }}
        #diagram {{
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            display: inline-block;
        }}
        #diagram.has-error {{
            max-width: calc(100vw - 60px);
        }}
        #diagram svg {{
            display: block;
        }}
        /* Override Mermaid's huge inline width styles ONLY for gantt charts */
        #diagram svg[aria-roledescription=""gantt""] {{
            width: auto !important;
            min-width: auto !important;
            max-width: none !important;
        }}
    </style>
</head>
<body>
    <div id=""container"">
        <div id=""diagram"">
            <pre class=""mermaid"">{System.Web.HttpUtility.HtmlEncode(code)}</pre>
        </div>
    </div>
    <script>
        mermaid.initialize({{ 
            startOnLoad: true, 
            theme: 'default', 
            securityLevel: 'loose'
        }});
        mermaid.run().then(() => {{
            const diagram = document.getElementById('diagram');
            const svg = document.querySelector('#diagram svg');
            
            // Auto-size the container to fit the actual SVG content
            if (svg) {{
                try {{
                    // Actually remove Mermaid's inline width/min-width styles using removeProperty
                    // Setting to '' doesn't work - must use removeProperty to truly remove inline styles
                    svg.style.removeProperty('width');
                    svg.style.removeProperty('min-width');
                    svg.style.removeProperty('max-width');
                    svg.style.removeProperty('height');
                    svg.style.removeProperty('min-height');
                    
                    // Get dimensions from viewBox if available (more reliable for gantt charts)
                    const viewBox = svg.getAttribute('viewBox');
                    if (viewBox) {{
                        const parts = viewBox.split(' ').map(Number);
                        if (parts.length === 4 && parts[2] > 0 && parts[3] > 0) {{
                            const padding = 20;
                            svg.setAttribute('width', parts[2] + padding);
                            svg.setAttribute('height', parts[3] + padding);
                        }}
                    }} else {{
                        // Fall back to getBBox for diagrams without viewBox
                        const bbox = svg.getBBox();
                        if (bbox && bbox.width > 0 && bbox.height > 0) {{
                            const padding = 20;
                            svg.setAttribute('width', bbox.width + padding);
                            svg.setAttribute('height', bbox.height + padding);
                            svg.setAttribute('viewBox', `${{bbox.x - padding/2}} ${{bbox.y - padding/2}} ${{bbox.width + padding}} ${{bbox.height + padding}}`);
                        }}
                    }}
                }} catch (e) {{
                    // getBBox may fail in some cases, just continue
                }}
            }}
            
            if (diagram) {{
                window.panzoomInstance = panzoom(diagram, {{
                    maxZoom: 10,
                    minZoom: 0.1,
                    bounds: false,
                    boundsPadding: 0.1
                }});
                
                // Reset position to top-left after initialization
                window.panzoomInstance.moveTo(0, 0);
                window.panzoomInstance.zoomAbs(0, 0, 1);
            }}
        }});
    </script>
</body>
</html>";
        _isRenderingContent = true;
        PreviewWebView.NavigateToString(html);
    }

    private void RenderMarkdownPreview(string code, string? filePath = null)
    {
        // Set up virtual host mapping for resolving relative image paths
        var baseTag = "";
        if (!string.IsNullOrEmpty(filePath))
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                UpdateVirtualHostMapping(directory);
                baseTag = $@"<base href=""https://{VirtualHostName}/"">";
            }
        }
        
        var html = $@"<!DOCTYPE html>
<html>
<head>
    {baseTag}
    <script src=""https://cdn.jsdelivr.net/npm/marked/marked.min.js""></script>
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/github-markdown-css@5/github-markdown-light.min.css"">
    <style>
        body {{ margin: 0; padding: 20px; background: #ffffff; }}
        .markdown-body {{ box-sizing: border-box; min-width: 200px; max-width: 980px; margin: 0 auto; }}
    </style>
</head>
<body>
    <div id=""content"" class=""markdown-body""></div>
    <script>
        document.getElementById('content').innerHTML = marked.parse({System.Text.Json.JsonSerializer.Serialize(code)});
    </script>
</body>
</html>";
        _isRenderingContent = true;
        PreviewWebView.NavigateToString(html);
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var ext = Path.GetExtension(files[0]).ToLowerInvariant();
                if (ext == ".mmd" || ext == ".mermaid" || ext == ".md")
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }
            }
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                // Open each dropped file in a new tab
                foreach (var filePath in files)
                {
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    
                    if (ext == ".mmd" || ext == ".mermaid" || ext == ".md")
                    {
                        OpenFileInTab(filePath);
                    }
                }
                
                StatusText.Text = files.Length == 1 
                    ? "File opened via drag and drop" 
                    : $"{files.Length} files opened via drag and drop";
            }
        }
    }

    private void UpdateNavigationDropdown()
    {
        var items = _currentRenderMode == RenderMode.Markdown 
            ? ExtractMarkdownHeadings() 
            : ExtractMermaidSections();
        
        // Use _isNavigating flag to prevent selection change from triggering navigation
        _isNavigating = true;
        try
        {
            NavigationDropdown.ItemsSource = items;
            if (items.Count > 0)
            {
                NavigationDropdown.SelectedIndex = 0;
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private List<NavigationItem> ExtractMarkdownHeadings()
    {
        var items = new List<NavigationItem>();
        var lines = CodeEditor.Text.Split('\n');
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimStart();
            if (line.StartsWith("#"))
            {
                int level = 0;
                while (level < line.Length && line[level] == '#') level++;
                
                if (level <= 6 && level < line.Length && line[level] == ' ')
                {
                    var headingText = line.Substring(level).Trim();
                    var headingId = headingText.ToLowerInvariant()
                        .Replace(" ", "-")
                        .Replace(".", "")
                        .Replace(",", "")
                        .Replace(":", "")
                        .Replace("'", "")
                        .Replace("\"", "");
                    
                    items.Add(new NavigationItem
                    {
                        DisplayText = headingText,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = level,
                        HeadingId = headingId
                    });
                }
            }
        }
        
        return items;
    }

    private List<NavigationItem> ExtractMermaidSections()
    {
        var items = new List<NavigationItem>();
        var lines = CodeEditor.Text.Split('\n');
        
        // Diagram types to look for
        var diagramTypes = new[] { "flowchart", "graph", "sequenceDiagram", "classDiagram", 
            "stateDiagram", "stateDiagram-v2", "erDiagram", "journey", "gantt", "pie", 
            "quadrantChart", "requirementDiagram", "gitGraph", "mindmap", "timeline",
            "zenuml", "sankey-beta", "xychart-beta", "block-beta", "C4Context", "C4Container", 
            "C4Component", "C4Dynamic", "C4Deployment" };
        
        bool foundDiagramType = false;
        string currentDiagramType = "";
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            // Skip frontmatter config lines
            if (line == "---" || line.StartsWith("config:") || line.StartsWith("look:") || 
                line.StartsWith("theme:") || line.StartsWith("layout:"))
                continue;
            
            // Check for diagram type declaration
            if (!foundDiagramType)
            {
                foreach (var diagramType in diagramTypes)
                {
                    if (line.StartsWith(diagramType, StringComparison.OrdinalIgnoreCase))
                    {
                        var displayName = diagramType;
                        currentDiagramType = diagramType.ToLower();
                        // Extract direction if present (e.g., "flowchart TD" -> "Flowchart (TD)")
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                        {
                            displayName = $"{diagramType} ({parts[1]})";
                        }
                        
                        items.Add(new NavigationItem
                        {
                            DisplayText = displayName,
                            RawText = line,
                            LineNumber = i + 1,
                            Level = 1,
                            HeadingId = ""
                        });
                        foundDiagramType = true;
                        break;
                    }
                }
            }
            
            // Subgraphs (flowchart/graph)
            if (line.StartsWith("subgraph ", StringComparison.OrdinalIgnoreCase))
            {
                var name = line.Substring(9).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Subgraph: " + name,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // State definitions (stateDiagram)
            else if (line.StartsWith("state ", StringComparison.OrdinalIgnoreCase) && line.Contains("{"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"state\s+""?([^""{\s]+)""?\s*\{?");
                if (match.Success)
                {
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  State: " + match.Groups[1].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            // Participants (sequenceDiagram)
            else if (line.StartsWith("participant ", StringComparison.OrdinalIgnoreCase))
            {
                var participantText = line.Substring(12).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Participant: " + participantText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // Actors (sequenceDiagram)
            else if (line.StartsWith("actor ", StringComparison.OrdinalIgnoreCase))
            {
                var actorText = line.Substring(6).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Actor: " + actorText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // Notes (sequenceDiagram)
            else if (line.StartsWith("Note ", StringComparison.OrdinalIgnoreCase))
            {
                var noteMatch = System.Text.RegularExpressions.Regex.Match(line, @"Note\s+(?:over|left of|right of)\s+([^:]+):\s*(.*)");
                if (noteMatch.Success)
                {
                    var noteText = noteMatch.Groups[2].Value.Trim();
                    if (noteText.Length > 30) noteText = noteText.Substring(0, 30) + "...";
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  Note: " + noteText,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            // Loop blocks (sequenceDiagram)
            else if (line.StartsWith("loop ", StringComparison.OrdinalIgnoreCase))
            {
                var loopText = line.Substring(5).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Loop: " + loopText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // Alt blocks (sequenceDiagram)
            else if (line.StartsWith("alt ", StringComparison.OrdinalIgnoreCase))
            {
                var altText = line.Substring(4).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Alt: " + altText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // Opt blocks (sequenceDiagram)
            else if (line.StartsWith("opt ", StringComparison.OrdinalIgnoreCase))
            {
                var optText = line.Substring(4).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Opt: " + optText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // Par blocks (sequenceDiagram)
            else if (line.StartsWith("par ", StringComparison.OrdinalIgnoreCase))
            {
                var parText = line.Substring(4).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Par: " + parText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // Critical blocks (sequenceDiagram)
            else if (line.StartsWith("critical ", StringComparison.OrdinalIgnoreCase))
            {
                var criticalText = line.Substring(9).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Critical: " + criticalText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // Break blocks (sequenceDiagram)
            else if (line.StartsWith("break ", StringComparison.OrdinalIgnoreCase))
            {
                var breakText = line.Substring(6).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Break: " + breakText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // Rect blocks (sequenceDiagram - highlight regions)
            else if (line.StartsWith("rect ", StringComparison.OrdinalIgnoreCase))
            {
                var rectText = line.Substring(5).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Rect: " + rectText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // Class definitions (classDiagram)
            else if (line.StartsWith("class ", StringComparison.OrdinalIgnoreCase) && currentDiagramType == "classdiagram")
            {
                var classMatch = System.Text.RegularExpressions.Regex.Match(line, @"class\s+(\w+)");
                if (classMatch.Success)
                {
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  Class: " + classMatch.Groups[1].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            // Sections (gantt, journey, timeline)
            else if (line.StartsWith("section ", StringComparison.OrdinalIgnoreCase))
            {
                var sectionText = line.Substring(8).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Section: " + sectionText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // Title (various diagrams)
            else if (line.StartsWith("title ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("title:"))
            {
                var titleText = line.Contains(":") ? line.Substring(line.IndexOf(':') + 1).Trim() : line.Substring(6).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Title: " + titleText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // gitGraph - branch, checkout, merge
            else if (line.StartsWith("branch ", StringComparison.OrdinalIgnoreCase))
            {
                var branchText = line.Substring(7).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Branch: " + branchText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            else if (line.StartsWith("checkout ", StringComparison.OrdinalIgnoreCase))
            {
                var checkoutText = line.Substring(9).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Checkout: " + checkoutText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            else if (line.StartsWith("merge ", StringComparison.OrdinalIgnoreCase))
            {
                var mergeText = line.Substring(6).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Merge: " + mergeText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            // requirementDiagram - requirement, element
            else if (line.StartsWith("requirement ", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("functionalRequirement ", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("performanceRequirement ", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("interfaceRequirement ", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("physicalRequirement ", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("designConstraint ", StringComparison.OrdinalIgnoreCase))
            {
                var reqMatch = System.Text.RegularExpressions.Regex.Match(line, @"^\w+\s+(\w+)\s*\{?");
                if (reqMatch.Success)
                {
                    var reqType = line.Split(' ')[0];
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  Req: " + reqMatch.Groups[1].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            else if (line.StartsWith("element ", StringComparison.OrdinalIgnoreCase))
            {
                var elemMatch = System.Text.RegularExpressions.Regex.Match(line, @"element\s+(\w+)\s*\{?");
                if (elemMatch.Success)
                {
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  Element: " + elemMatch.Groups[1].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            // C4 diagrams - Person, System, Container, Component, Boundary
            else if (line.StartsWith("Person(", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("Person_Ext(", StringComparison.OrdinalIgnoreCase))
            {
                var c4Match = System.Text.RegularExpressions.Regex.Match(line, @"Person(?:_Ext)?\((\w+),\s*""([^""]+)""");
                if (c4Match.Success)
                {
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  Person: " + c4Match.Groups[2].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            else if (line.StartsWith("System(", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("System_Ext(", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("SystemDb(", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("SystemDb_Ext(", StringComparison.OrdinalIgnoreCase))
            {
                var c4Match = System.Text.RegularExpressions.Regex.Match(line, @"System(?:Db)?(?:_Ext)?\((\w+),\s*""([^""]+)""");
                if (c4Match.Success)
                {
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  System: " + c4Match.Groups[2].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            else if (line.StartsWith("Container(", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("ContainerDb(", StringComparison.OrdinalIgnoreCase))
            {
                var c4Match = System.Text.RegularExpressions.Regex.Match(line, @"Container(?:Db)?\((\w+),\s*""([^""]+)""");
                if (c4Match.Success)
                {
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  Container: " + c4Match.Groups[2].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            else if (line.StartsWith("Component(", StringComparison.OrdinalIgnoreCase))
            {
                var c4Match = System.Text.RegularExpressions.Regex.Match(line, @"Component\((\w+),\s*""([^""]+)""");
                if (c4Match.Success)
                {
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  Component: " + c4Match.Groups[2].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            else if (line.StartsWith("Boundary(", StringComparison.OrdinalIgnoreCase))
            {
                var c4Match = System.Text.RegularExpressions.Regex.Match(line, @"Boundary\((\w+),\s*""([^""]+)""");
                if (c4Match.Success)
                {
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  Boundary: " + c4Match.Groups[2].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            // quadrantChart - x-axis, y-axis, quadrant labels
            else if (line.StartsWith("x-axis ", StringComparison.OrdinalIgnoreCase))
            {
                var axisText = line.Substring(7).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  X-Axis: " + axisText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            else if (line.StartsWith("y-axis ", StringComparison.OrdinalIgnoreCase))
            {
                var axisText = line.Substring(7).Trim();
                items.Add(new NavigationItem
                {
                    DisplayText = "  Y-Axis: " + axisText,
                    RawText = line,
                    LineNumber = i + 1,
                    Level = 2,
                    HeadingId = ""
                });
            }
            else if (line.StartsWith("quadrant-", StringComparison.OrdinalIgnoreCase))
            {
                var quadrantMatch = System.Text.RegularExpressions.Regex.Match(line, @"quadrant-(\d)\s+(.+)");
                if (quadrantMatch.Success)
                {
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  Quadrant " + quadrantMatch.Groups[1].Value + ": " + quadrantMatch.Groups[2].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            // mindmap - root node (first non-empty line after mindmap declaration)
            else if (line.StartsWith("root(", StringComparison.OrdinalIgnoreCase))
            {
                var rootMatch = System.Text.RegularExpressions.Regex.Match(line, @"root\(\(([^)]+)\)\)");
                if (rootMatch.Success)
                {
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  Root: " + rootMatch.Groups[1].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            // erDiagram - entity definitions (lines with { that define entities)
            else if (currentDiagramType == "erdiagram" && line.Contains("{") && !line.StartsWith("%%"))
            {
                var entityMatch = System.Text.RegularExpressions.Regex.Match(line, @"^\s*(\w+)\s*\{");
                if (entityMatch.Success)
                {
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  Entity: " + entityMatch.Groups[1].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
            // pie chart - data entries
            else if (currentDiagramType == "pie" && line.Contains(":") && line.Contains("\""))
            {
                var pieMatch = System.Text.RegularExpressions.Regex.Match(line, @"""([^""]+)""\s*:\s*(\d+)");
                if (pieMatch.Success)
                {
                    items.Add(new NavigationItem
                    {
                        DisplayText = "  " + pieMatch.Groups[1].Value + ": " + pieMatch.Groups[2].Value,
                        RawText = line,
                        LineNumber = i + 1,
                        Level = 2,
                        HeadingId = ""
                    });
                }
            }
        }
        
        return items;
    }

    private bool _isNavigating = false;

    private void NavigationDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isNavigating) return;
        
        if (NavigationDropdown.SelectedItem is NavigationItem item)
        {
            _isNavigating = true;
            try
            {
                var line = CodeEditor.Document.GetLineByNumber(item.LineNumber);
                CodeEditor.ScrollToLine(item.LineNumber);
                CodeEditor.TextArea.Caret.Offset = line.Offset;
                CodeEditor.TextArea.Caret.BringCaretToView();
                CodeEditor.Select(line.Offset, line.Length);
                
                if (_currentRenderMode == RenderMode.Markdown && !string.IsNullOrEmpty(item.HeadingId))
                {
                    ScrollPreviewToHeading(item.HeadingId);
                }
            }
            finally
            {
                _isNavigating = false;
            }
        }
    }

    private async void ScrollPreviewToHeading(string headingId)
    {
        if (!_webViewInitialized) return;
        
        try
        {
            var script = $@"
                (function() {{
                    var element = document.getElementById('{headingId}');
                    if (element) {{
                        element.scrollIntoView({{ behavior: 'smooth', block: 'start' }});
                        return true;
                    }}
                    var headings = document.querySelectorAll('h1, h2, h3, h4, h5, h6');
                    for (var i = 0; i < headings.length; i++) {{
                        var h = headings[i];
                        var text = h.textContent.toLowerCase().replace(/\s+/g, '-').replace(/[.,:'""]/g, '');
                        if (text === '{headingId}' || h.id === '{headingId}') {{
                            h.scrollIntoView({{ behavior: 'smooth', block: 'start' }});
                            return true;
                        }}
                    }}
                    return false;
                }})();
            ";
            await PreviewWebView.ExecuteScriptAsync(script);
        }
        catch
        {
        }
    }
    
    #region Multi-Document Tab Support
    
    /// <summary>
    /// Creates a new document and adds it to the tab strip
    /// </summary>
    private DocumentModel CreateNewDocument(string? filePath = null, string? content = null)
    {
        var doc = new DocumentModel
        {
            FilePath = filePath,
            RenderMode = RenderMode.Mermaid
        };
        
        if (!string.IsNullOrEmpty(filePath))
        {
            SetDocumentRenderModeFromFile(doc, filePath);
        }
        else
        {
            // Apply default file type from settings for untitled documents
            var defaultType = SettingsManager.Current.DefaultFileType;
            doc.RenderMode = string.Equals(defaultType, "Markdown", StringComparison.OrdinalIgnoreCase)
                ? RenderMode.Markdown
                : RenderMode.Mermaid;
        }
        
        doc.TextDocument.Text = content ?? (doc.RenderMode == RenderMode.Markdown ? DefaultMarkdownCode : DefaultMermaidCode);
        doc.TextDocument.UndoStack.ClearAll(); // Clear undo history so users can't undo past the initial content
        doc.IsDirty = false;
        
        _openDocuments.Add(doc);
        CreateTabButtonForDocument(doc);
        
        return doc;
    }
    
    /// <summary>
    /// Sets the render mode for a document based on its file extension
    /// </summary>
    private void SetDocumentRenderModeFromFile(DocumentModel doc, string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        doc.RenderMode = ext == ".md" ? RenderMode.Markdown : RenderMode.Mermaid;
    }
    
    /// <summary>
    /// Creates a tab button for a document and adds it to the tab strip
    /// </summary>
    private void CreateTabButtonForDocument(DocumentModel doc)
    {
        // Create a Border to wrap the button for rounded corners and purple accent styling
        var tabBorder = new System.Windows.Controls.Border
        {
            Tag = doc,
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            Margin = new Thickness(0, 0, 1, 1), // Bottom margin of 1 to sit on the purple line
            Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D2D30")),
            BorderThickness = new Thickness(1, 1, 1, 0),
            BorderBrush = System.Windows.Media.Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        
        // Create content with text and close button
        var stackPanel = new StackPanel 
        { 
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(12, 6, 8, 6)
        };
        
        var textBlock = new TextBlock
        {
            Text = doc.TabHeader,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F1F1F1")),
        };
        
        var closeButton = new System.Windows.Controls.Button
        {
            Content = "x",
            FontSize = 10,
            Width = 16,
            Height = 16,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#888888")),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = doc
        };
        closeButton.Click += CloseDocumentTab_Click;
        
        stackPanel.Children.Add(textBlock);
        stackPanel.Children.Add(closeButton);
        tabBorder.Child = stackPanel;
        
        // Handle click and drag on the border
        tabBorder.MouseLeftButtonDown += (s, e) =>
        {
            if (s is System.Windows.Controls.Border border && border.Tag is DocumentModel clickedDoc)
            {
                _tabDragStartPoint = e.GetPosition(DocumentTabsPanel);
                _draggedTab = border;
                _isTabDragging = false;
                SwitchToDocument(clickedDoc);
            }
        };
        
        tabBorder.MouseMove += (s, e) =>
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && _draggedTab != null)
            {
                var currentPos = e.GetPosition(DocumentTabsPanel);
                var diff = _tabDragStartPoint - currentPos;
                
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isTabDragging = true;
                    tabBorder.Cursor = System.Windows.Input.Cursors.Hand;
                    
                    var data = new System.Windows.DataObject(typeof(DocumentModel), _draggedTab.Tag);
                    System.Windows.DragDrop.DoDragDrop(_draggedTab, data, System.Windows.DragDropEffects.Move);
                    
                    _draggedTab = null;
                    _isTabDragging = false;
                }
            }
        };
        
        tabBorder.MouseLeftButtonUp += (s, e) =>
        {
            _draggedTab = null;
            _isTabDragging = false;
        };
        
        tabBorder.AllowDrop = true;
        tabBorder.DragOver += TabBorder_DragOver;
        tabBorder.Drop += TabBorder_Drop;
        
        // Add context menu for tab operations - uses XAML styles from Window.Resources
        var contextMenu = new System.Windows.Controls.ContextMenu();
        
        // Get the filename for the Close menu item
        var fileName = string.IsNullOrEmpty(doc.FilePath) ? "Untitled" : System.IO.Path.GetFileName(doc.FilePath);
        var closeItem = new System.Windows.Controls.MenuItem { Header = $"Close \"{fileName}\"", Tag = doc };
        closeItem.Click += (s, e) => CloseDocument(doc);
        
        var closeAllItem = new System.Windows.Controls.MenuItem { Header = "Close All" };
        closeAllItem.Click += (s, e) => CloseAllDocuments();
        
        var closeAllButThisItem = new System.Windows.Controls.MenuItem { Header = "Close All But This", Tag = doc };
        closeAllButThisItem.Click += (s, e) => CloseAllDocumentsExcept(doc);
        
        contextMenu.Items.Add(closeItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(closeAllItem);
        contextMenu.Items.Add(closeAllButThisItem);
        
        // Add file-related context menu items (only for saved documents)
        if (!string.IsNullOrEmpty(doc.FilePath))
        {
            contextMenu.Items.Add(new Separator());
            
            var copyPathItem = new System.Windows.Controls.MenuItem { Header = "Copy File Path" };
            copyPathItem.Click += (s, e) =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(doc.FilePath);
                    StatusText.Text = "File path copied to clipboard";
                }
                catch { }
            };
            
            var openLocationItem = new System.Windows.Controls.MenuItem { Header = "Open File Location" };
            openLocationItem.Click += (s, e) =>
            {
                try
                {
                    var directory = System.IO.Path.GetDirectoryName(doc.FilePath);
                    if (!string.IsNullOrEmpty(directory) && System.IO.Directory.Exists(directory))
                    {
                        // Open Explorer with the file selected
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{doc.FilePath}\"");
                    }
                }
                catch { }
            };
            
            contextMenu.Items.Add(copyPathItem);
            contextMenu.Items.Add(openLocationItem);
        }
        
        tabBorder.ContextMenu = contextMenu;
        
        // Subscribe to property changes to update tab header
        doc.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DocumentModel.TabHeader))
            {
                textBlock.Text = doc.TabHeader;
            }
        };
        
        // Store the border as the tab element (we'll cast it appropriately in UpdateTabStyles)
        doc.TabButton = null; // Clear the old button reference
        doc.TabBorder = tabBorder;
        DocumentTabsPanel.Children.Add(tabBorder);
        
        UpdateTabStyles();
    }
    
    private void TabBorder_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(DocumentModel)))
        {
            e.Effects = System.Windows.DragDropEffects.Move;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }
    
    private void TabBorder_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is System.Windows.Controls.Border targetBorder && 
            targetBorder.Tag is DocumentModel targetDoc &&
            e.Data.GetDataPresent(typeof(DocumentModel)))
        {
            var draggedDoc = e.Data.GetData(typeof(DocumentModel)) as DocumentModel;
            if (draggedDoc == null || draggedDoc == targetDoc) return;
            
            var draggedIndex = _openDocuments.IndexOf(draggedDoc);
            var targetIndex = _openDocuments.IndexOf(targetDoc);
            
            if (draggedIndex < 0 || targetIndex < 0) return;
            
            // Reorder in the documents list
            _openDocuments.RemoveAt(draggedIndex);
            _openDocuments.Insert(targetIndex, draggedDoc);
            
            // Reorder in the UI panel
            if (draggedDoc.TabBorder != null)
            {
                DocumentTabsPanel.Children.Remove(draggedDoc.TabBorder);
                DocumentTabsPanel.Children.Insert(targetIndex, draggedDoc.TabBorder);
            }
        }
        e.Handled = true;
    }
    
    /// <summary>
    /// Updates the visual styles of all document tabs
    /// </summary>
    private void UpdateTabStyles()
    {
        // Get theme-aware colors from resources
        var selectedBg = System.Windows.Application.Current.Resources["ThemeTabSelectedBackgroundBrush"] as SolidColorBrush 
            ?? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E"));
        var unselectedBg = System.Windows.Application.Current.Resources["ThemeToolbarBackgroundBrush"] as SolidColorBrush 
            ?? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D2D30"));
        var selectedFg = System.Windows.Application.Current.Resources["ThemeForegroundBrush"] as SolidColorBrush 
            ?? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F1F1F1"));
        var unselectedFg = System.Windows.Application.Current.Resources["ThemeDisabledForegroundBrush"] as SolidColorBrush 
            ?? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9D9D9D"));
        var purpleAccent = System.Windows.Application.Current.Resources["ThemePurpleAccentBrush"] as SolidColorBrush 
            ?? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9184EE"));
        
        foreach (var doc in _openDocuments)
        {
            var isSelected = doc == _activeDocument;
            
            // Update Border-based tabs (new style)
            if (doc.TabBorder != null)
            {
                doc.TabBorder.Background = isSelected ? selectedBg : unselectedBg;
                // Purple border on top and sides for selected tab, transparent for unselected
                doc.TabBorder.BorderBrush = isSelected ? purpleAccent : System.Windows.Media.Brushes.Transparent;
                // Selected tab extends down to cover the purple line (margin bottom = -1)
                doc.TabBorder.Margin = isSelected ? new Thickness(0, 0, 1, -1) : new Thickness(0, 0, 1, 1);
                // Set z-index so selected tab appears on top
                System.Windows.Controls.Panel.SetZIndex(doc.TabBorder, isSelected ? 1 : 0);
                
                // Update text color
                if (doc.TabBorder.Child is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is TextBlock tb)
                {
                    tb.Foreground = isSelected ? selectedFg : unselectedFg;
                }
            }
            // Fallback for Button-based tabs (legacy)
            else if (doc.TabButton != null)
            {
                doc.TabButton.Background = isSelected ? selectedBg : unselectedBg;
                doc.TabButton.Foreground = isSelected ? selectedFg : unselectedFg;
                doc.TabButton.BorderThickness = isSelected ? new Thickness(0, 0, 0, 2) : new Thickness(0);
                doc.TabButton.BorderBrush = isSelected ? purpleAccent : null;
            }
        }
    }
    
    /// <summary>
    /// Handles clicking on a document tab to switch to that document
    /// </summary>
    private void DocumentTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is DocumentModel doc)
        {
            SwitchToDocument(doc);
        }
    }
    
    /// <summary>
    /// Handles clicking the close button on a document tab
    /// </summary>
    private void CloseDocumentTab_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent the tab click from firing
        
        if (sender is System.Windows.Controls.Button button && button.Tag is DocumentModel doc)
        {
            CloseDocument(doc);
        }
    }
    
    /// <summary>
    /// Closes a document, prompting to save if dirty
    /// </summary>
    private void CloseDocument(DocumentModel doc)
    {
        if (doc.IsDirty)
        {
            var result = MessageBox.Show(
                $"Do you want to save changes to {doc.DisplayName}?",
                "Save Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Cancel)
                return;
            
            if (result == MessageBoxResult.Yes)
            {
                // Save the document
                if (!SaveDocument(doc))
                    return; // Save was cancelled
            }
        }
        
        // Clean up temp file for this document
        DeleteTempFile(doc);
        
        // Remove the tab element (Border or Button)
        if (doc.TabBorder != null)
        {
            DocumentTabsPanel.Children.Remove(doc.TabBorder);
        }
        else if (doc.TabButton != null)
        {
            DocumentTabsPanel.Children.Remove(doc.TabButton);
        }
        
        // Remove from list
        var index = _openDocuments.IndexOf(doc);
        _openDocuments.Remove(doc);
        
        // Update session state after closing a document
        SaveSessionState();
        
        // If this was the active document, switch to another
        if (doc == _activeDocument)
        {
            if (_openDocuments.Count > 0)
            {
                // Switch to the next document, or the previous if we closed the last one
                var newIndex = Math.Min(index, _openDocuments.Count - 1);
                SwitchToDocument(_openDocuments[newIndex]);
            }
            else
            {
                // No more documents, create a new untitled one
                var newDoc = CreateNewDocument();
                SwitchToDocument(newDoc);
            }
        }
    }
    
    /// <summary>
    /// Closes all open documents
    /// </summary>
    private void CloseAllDocuments()
    {
        // Make a copy of the list since we'll be modifying it
        var docsToClose = _openDocuments.ToList();
        foreach (var doc in docsToClose)
        {
            CloseDocument(doc);
            // If user cancelled closing a dirty document, stop
            if (_openDocuments.Contains(doc))
                break;
        }
    }
    
    /// <summary>
    /// Closes all documents except the specified one
    /// </summary>
    private void CloseAllDocumentsExcept(DocumentModel keepDoc)
    {
        // Make a copy of the list since we'll be modifying it
        var docsToClose = _openDocuments.Where(d => d != keepDoc).ToList();
        foreach (var doc in docsToClose)
        {
            CloseDocument(doc);
            // If user cancelled closing a dirty document, stop
            if (_openDocuments.Contains(doc))
                break;
        }
        
        // Make sure the kept document is active
        if (_openDocuments.Contains(keepDoc))
        {
            SwitchToDocument(keepDoc);
        }
    }
    
    /// <summary>
    /// Saves a document, returning true if successful
    /// </summary>
    private bool SaveDocument(DocumentModel doc)
    {
        if (string.IsNullOrEmpty(doc.FilePath))
        {
            // Need to do Save As
            var dialog = new SaveFileDialog
            {
                Filter = doc.RenderMode == RenderMode.Markdown
                    ? "Markdown Files (*.md)|*.md|All Files (*.*)|*.*"
                    : "Mermaid Files (*.mmd)|*.mmd|All Files (*.*)|*.*",
                DefaultExt = doc.RenderMode == RenderMode.Markdown ? ".md" : ".mmd"
            };
            
            if (dialog.ShowDialog() == true)
            {
                doc.FilePath = dialog.FileName;
            }
            else
            {
                return false;
            }
        }
        
        try
        {
            _isSavingFile = true;
            File.WriteAllText(doc.FilePath, doc.TextDocument.Text);
            doc.LastKnownWriteTime = File.GetLastWriteTimeUtc(doc.FilePath);
            _isSavingFile = false;
            doc.IsDirty = false;
            AddToRecentFiles(doc.FilePath);
            
            // Set up file watcher for the saved file
            SetupFileWatcher(doc.FilePath);
            
            // Clean up temp file now that content is saved to a real path
            DeleteTempFile(doc);
            
            return true;
        }
        catch (Exception ex)
        {
            _isSavingFile = false;
            MessageBox.Show($"Failed to save file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }
    
    /// <summary>
    /// Switches to a different document
    /// </summary>
    private async void SwitchToDocument(DocumentModel doc)
    {
        if (_activeDocument == doc) return;
        
        _isSwitchingDocuments = true;
        
        // Save current document state
        if (_activeDocument != null)
        {
            _activeDocument.CaretOffset = CodeEditor.CaretOffset;
            _activeDocument.VerticalScrollOffset = CodeEditor.VerticalOffset;
            _activeDocument.HorizontalScrollOffset = CodeEditor.HorizontalOffset;
            _activeDocument.SelectionStart = CodeEditor.SelectionStart;
            _activeDocument.SelectionLength = CodeEditor.SelectionLength;
            _activeDocument.PreviewZoom = _currentZoom;
            _activeDocument.HasNavigatedAway = _hasNavigatedAway;
            _activeDocument.IsSelected = false;
            
            // Save preview scroll position - must await to ensure it's saved before switching
            await SavePreviewScrollPositionAsync(_activeDocument);
        }
        
        // Switch to new document
        _activeDocument = doc;
        doc.IsSelected = true;
        
        // Update backward compatibility fields
        _currentFilePath = doc.FilePath;
        _isDirty = doc.IsDirty;
        _currentRenderMode = doc.RenderMode;
        _currentZoom = doc.PreviewZoom;
        
        // Reset navigation state for the new document
        // New documents start with HasNavigatedAway = false
        // Existing documents restore their saved state
        _hasNavigatedAway = doc.HasNavigatedAway;
        
        // Immediately disable back button - it will be re-enabled by CoreWebView2_NavigationCompleted
        // if the document has navigated away, but for new documents it should stay disabled
        PreviewBackButton.IsEnabled = false;
        
        // Swap the text document
        CodeEditor.Document = doc.TextDocument;
        
        // Restore editor state
        try
        {
            CodeEditor.CaretOffset = Math.Min(doc.CaretOffset, doc.TextDocument.TextLength);
            CodeEditor.ScrollToVerticalOffset(doc.VerticalScrollOffset);
            CodeEditor.ScrollToHorizontalOffset(doc.HorizontalScrollOffset);
            
            // Restore selection if there was one
            if (doc.SelectionLength > 0)
            {
                var selStart = Math.Min(doc.SelectionStart, doc.TextDocument.TextLength);
                var selLength = Math.Min(doc.SelectionLength, doc.TextDocument.TextLength - selStart);
                CodeEditor.Select(selStart, selLength);
            }
        }
        catch { }
        
        // Update syntax highlighting based on document type
        if (doc.RenderMode == RenderMode.Markdown)
        {
            RegisterMarkdownSyntaxHighlighting();
        }
        else
        {
            CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Mermaid");
        }
        
        // Update UI
        UpdateTabStyles();
        UpdateTitle();
        UpdateNavigationDropdown();
        UpdateExportMenuVisibility();
        UpdateMarkdownFormattingVisibility();
        UpdateZoomControlsVisibility();
        UpdateUndoRedoState();
        
        // Force-refresh comment icon color for the new document's caret position
        _lastCaretWasInComment = false;
        UpdateToggleCommentIconColor(force: true);
        
        // Re-render preview for the new document
        // This will trigger NavigateToString which resets _hasNavigatedAway to false
        // and keeps the back button disabled for fresh renders
        // Note: _isSwitchingDocuments stays true during RenderPreview() so zoom can be restored
        RenderPreview();
        
        _isSwitchingDocuments = false;
    }
    
    /// <summary>
    /// Opens a file in a new tab or switches to existing tab if already open
    /// </summary>
    private void OpenFileInTab(string filePath)
    {
        // Check if file is already open
        var existingDoc = _openDocuments.FirstOrDefault(d => 
            string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        
        if (existingDoc != null)
        {
            SwitchToDocument(existingDoc);
            return;
        }
        
        try
        {
            var content = File.ReadAllText(filePath);
            var doc = CreateNewDocument(filePath, content);
            doc.LastKnownWriteTime = File.GetLastWriteTimeUtc(filePath);
            SwitchToDocument(doc);
            
            // Set up file watcher for external change detection
            SetupFileWatcher(filePath);
            
            // Update current browser path for Open/Save dialogs
            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder))
            {
                _currentBrowserPath = folder;
            }
            
            AddToRecentFiles(filePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    /// <summary>
    /// Opens a file from an external source (e.g., another instance via named pipe)
    /// This is a public method called by App.xaml.cs for single-instance support
    /// </summary>
    public void OpenFileFromExternalSource(string filePath)
    {
        OpenFileInTab(filePath);
    }
    
    /// <summary>
    /// Initializes the document tab system on startup
    /// </summary>
    private void InitializeDocumentTabs()
    {
        // Check for command-line file argument first (e.g., double-click to open a file)
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1]))
        {
            // Open file from command line - this takes priority over session restore
            var content = File.ReadAllText(args[1]);
            var doc = CreateNewDocument(args[1], content);
            doc.LastKnownWriteTime = File.GetLastWriteTimeUtc(args[1]);
            SwitchToDocument(doc);
            
            // Set up file watcher for external change detection
            SetupFileWatcher(args[1]);
            
            // Update current browser path for Open/Save dialogs
            var folder = Path.GetDirectoryName(args[1]);
            if (!string.IsNullOrEmpty(folder))
            {
                _currentBrowserPath = folder;
            }
            
            AddToRecentFiles(args[1]);
            return;
        }
        
        // Try to restore previous session
        if (RestoreSessionState())
        {
            // Session restored successfully - update browser path from first saved document
            var firstSavedDoc = _openDocuments.FirstOrDefault(d => !string.IsNullOrEmpty(d.FilePath));
            if (firstSavedDoc != null)
            {
                var folder = Path.GetDirectoryName(firstSavedDoc.FilePath);
                if (!string.IsNullOrEmpty(folder))
                {
                    _currentBrowserPath = folder;
                }
            }
            return;
        }
        
        // No session to restore and no command-line file
        // Create a temporary blank document - the New Document dialog will be shown after window loads
        var newDoc = CreateNewDocument();
        SwitchToDocument(newDoc);
        _showNewDocumentDialogOnLoad = true;
    }
    
    private bool _showNewDocumentDialogOnLoad = false;
    
    /// <summary>
    /// Sets up a FileSystemWatcher to monitor the current file for external changes
    /// </summary>
    private void SetupFileWatcher(string? filePath)
    {
        // Dispose existing watcher
        _fileWatcher?.Dispose();
        _fileWatcher = null;
        
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;
        
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);
            
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
                return;
            
            _fileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            
            _fileWatcher.Changed += FileWatcher_Changed;
        }
        catch (Exception)
        {
            // Silently fail if we can't set up the watcher (e.g., network path issues)
            _fileWatcher?.Dispose();
            _fileWatcher = null;
        }
    }
    
    /// <summary>
    /// Handles file change notifications from FileSystemWatcher
    /// </summary>
    private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        // Ignore if we're saving the file ourselves or reloading
        if (_isSavingFile || _isReloadingFile)
            return;
        
        // Find the document that matches this file
        var changedDoc = _openDocuments.FirstOrDefault(d => 
            string.Equals(d.FilePath, e.FullPath, StringComparison.OrdinalIgnoreCase));
        
        if (changedDoc == null)
            return;
        
        // Check if the file's write time has actually changed
        try
        {
            var currentWriteTime = File.GetLastWriteTimeUtc(e.FullPath);
            if (currentWriteTime <= changedDoc.LastKnownWriteTime)
                return;
            
            // Mark that an external change was detected
            changedDoc.ExternalChangeDetected = true;
            changedDoc.LastKnownWriteTime = currentWriteTime;
            
            // Show prompt on UI thread
            Dispatcher.BeginInvoke(new Action(() => PromptForFileReload(changedDoc)));
        }
        catch (Exception)
        {
            // File might be locked or inaccessible, ignore
        }
    }
    
    /// <summary>
    /// Prompts the user to reload a file that was modified externally
    /// </summary>
    private void PromptForFileReload(DocumentModel doc)
    {
        if (!doc.ExternalChangeDetected || string.IsNullOrEmpty(doc.FilePath))
            return;
        
        // Reset the flag
        doc.ExternalChangeDetected = false;
        
        var fileName = Path.GetFileName(doc.FilePath);
        var message = doc.IsDirty
            ? $"The file '{fileName}' has been modified outside the editor.\n\nYou have unsaved changes. Do you want to reload the file and lose your changes?"
            : $"The file '{fileName}' has been modified outside the editor.\n\nDo you want to reload it?";
        
        var result = MessageBox.Show(
            message,
            "File Changed",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            ReloadDocument(doc);
        }
    }
    
    /// <summary>
    /// Reloads a document from disk
    /// </summary>
    private void ReloadDocument(DocumentModel doc)
    {
        if (string.IsNullOrEmpty(doc.FilePath) || !File.Exists(doc.FilePath))
        {
            MessageBox.Show("The file no longer exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        try
        {
            _isReloadingFile = true;
            
            var content = File.ReadAllText(doc.FilePath);
            doc.LastKnownWriteTime = File.GetLastWriteTimeUtc(doc.FilePath);
            
            // Preserve caret position if possible
            var caretOffset = doc.TextDocument.TextLength > 0 ? Math.Min(doc.CaretOffset, content.Length) : 0;
            
            doc.TextDocument.Text = content;
            doc.IsDirty = false;
            
            // If this is the active document, update the editor
            if (doc == _activeDocument)
            {
                try
                {
                    CodeEditor.CaretOffset = Math.Min(caretOffset, doc.TextDocument.TextLength);
                }
                catch { }
                
                RenderPreview();
            }
            
            _isReloadingFile = false;
        }
        catch (Exception ex)
        {
            _isReloadingFile = false;
            MessageBox.Show($"Failed to reload file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    /// <summary>
    /// Syncs the toggle button visual states with their actual states.
    /// This is needed because WPF Style.Triggers with DynamicResource can sometimes
    /// not update properly after dialogs close.
    /// </summary>
    private void SyncToggleButtonStates()
    {
        // Force re-evaluation of toggle button states by toggling IsChecked twice
        // This ensures the Style.Triggers are re-evaluated
        if (SplitViewToggle != null)
        {
            var state = SplitViewToggle.IsChecked;
            SplitViewToggle.IsChecked = !state;
            SplitViewToggle.IsChecked = state;
        }
        if (LineNumbersToggle != null)
        {
            var state = LineNumbersToggle.IsChecked;
            LineNumbersToggle.IsChecked = !state;
            LineNumbersToggle.IsChecked = state;
        }
        if (BracketMatchingToggle != null)
        {
            var state = BracketMatchingToggle.IsChecked;
            BracketMatchingToggle.IsChecked = !state;
            BracketMatchingToggle.IsChecked = state;
        }
        if (WordWrapToggle != null)
        {
            var state = WordWrapToggle.IsChecked;
            WordWrapToggle.IsChecked = !state;
            WordWrapToggle.IsChecked = state;
        }
    }
    
    private void ShowStartupNewDocumentDialog()
    {
        var dialog = new NewDocumentDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            if (dialog.SelectedRecentFilePath != null)
            {
                // User selected a recent file - close blank document and open the file
                if (_openDocuments.Count == 1 && _activeDocument != null && 
                    string.IsNullOrEmpty(_activeDocument.FilePath) && !_activeDocument.IsDirty)
                {
                    CloseDocument(_activeDocument);
                }
                
                var content = File.ReadAllText(dialog.SelectedRecentFilePath);
                var doc = CreateNewDocument(dialog.SelectedRecentFilePath, content);
                doc.LastKnownWriteTime = File.GetLastWriteTimeUtc(dialog.SelectedRecentFilePath);
                SwitchToDocument(doc);
                
                // Set up file watcher for external change detection
                SetupFileWatcher(dialog.SelectedRecentFilePath);
                
                var folder = Path.GetDirectoryName(dialog.SelectedRecentFilePath);
                if (!string.IsNullOrEmpty(folder))
                {
                    _currentBrowserPath = folder;
                }
                
                AddToRecentFiles(dialog.SelectedRecentFilePath);
            }
            else if (dialog.OpenExistingFile)
            {
                // User wants to open an existing file
                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "All Supported Files|*.mmd;*.md|Mermaid Files (*.mmd)|*.mmd|Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
                    InitialDirectory = _currentBrowserPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };
                
                if (openDialog.ShowDialog() == true)
                {
                    // Close the temporary blank document and open the selected file
                    if (_openDocuments.Count == 1 && _activeDocument != null && 
                        string.IsNullOrEmpty(_activeDocument.FilePath) && !_activeDocument.IsDirty)
                    {
                        CloseDocument(_activeDocument);
                    }
                    
                    var content = File.ReadAllText(openDialog.FileName);
                    var doc = CreateNewDocument(openDialog.FileName, content);
                    doc.LastKnownWriteTime = File.GetLastWriteTimeUtc(openDialog.FileName);
                    SwitchToDocument(doc);
                    
                    // Set up file watcher for external change detection
                    SetupFileWatcher(openDialog.FileName);
                    
                    var folder = Path.GetDirectoryName(openDialog.FileName);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        _currentBrowserPath = folder;
                    }
                    
                    AddToRecentFiles(openDialog.FileName);
                }
                // If user cancelled open dialog, keep the blank document
            }
            else if (dialog.SelectedTemplate != null)
            {
                // Replace the blank document content with the selected template
                if (_activeDocument != null)
                {
                    _activeDocument.TextDocument.Text = dialog.SelectedTemplate;
                    _activeDocument.RenderMode = dialog.IsMermaid ? RenderMode.Mermaid : RenderMode.Markdown;
                    _activeDocument.IsDirty = false;
                    _currentRenderMode = _activeDocument.RenderMode;
                    UpdateExportMenuVisibility();
                    UpdateMarkdownFormattingVisibility();
                    UpdateZoomControlsVisibility();
                    UpdateTitle();
                    RenderPreview();
                    
                    // Auto fit to window after template loads
                    _ = Dispatcher.InvokeAsync(async () =>
                    {
                        await Task.Delay(500); // Wait for render to complete
                        if (_webViewInitialized)
                        {
                            await PreviewWebView.CoreWebView2.ExecuteScriptAsync("window.fitToWindow()");
                        }
                    });
                }
            }
            // If no template selected, keep the blank document
        }
        // If user cancelled dialog, keep the blank document
        
        // Sync toggle button visual states after dialog closes
        // This is needed because WPF Style.Triggers with DynamicResource can sometimes
        // not update properly after dialogs close
        SyncToggleButtonStates();
    }
    
    #endregion
    
    #region Auto-Save and Session Restore
    
    /// <summary>
    /// Auto-save timer tick handler - saves all dirty documents to temp files and persists session state
    /// </summary>
    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        if (_isAutoSaving) return;
        _isAutoSaving = true;
        
        try
        {
            SaveActiveDocumentState();
            AutoSaveAllDocuments();
            SaveSessionState();
        }
        catch
        {
            // Silently fail - auto-save should never interrupt the user
        }
        finally
        {
            _isAutoSaving = false;
        }
    }
    
    /// <summary>
    /// Captures the active document's current editor state (caret, scroll, etc.)
    /// so it's up-to-date before saving session state
    /// </summary>
    private void SaveActiveDocumentState()
    {
        if (_activeDocument == null) return;
        
        try
        {
            _activeDocument.CaretOffset = CodeEditor.CaretOffset;
            _activeDocument.VerticalScrollOffset = CodeEditor.VerticalOffset;
            _activeDocument.HorizontalScrollOffset = CodeEditor.HorizontalOffset;
            _activeDocument.SelectionStart = CodeEditor.SelectionStart;
            _activeDocument.SelectionLength = CodeEditor.SelectionLength;
            _activeDocument.PreviewZoom = _currentZoom;
            _activeDocument.HasNavigatedAway = _hasNavigatedAway;
        }
        catch
        {
            // Silently fail
        }
    }
    
    /// <summary>
    /// Auto-saves all documents to temp files in the AutoSave directory.
    /// For untitled documents: saves content to their assigned temp file.
    /// For saved documents with unsaved changes: saves content to a temp backup file.
    /// For clean saved documents: no temp file needed (content matches the real file).
    /// </summary>
    private void AutoSaveAllDocuments()
    {
        try
        {
            // Ensure the AutoSave directory exists
            if (!Directory.Exists(AutoSaveFolder))
            {
                Directory.CreateDirectory(AutoSaveFolder);
            }
            
            foreach (var doc in _openDocuments)
            {
                // Assign a temp file path if this document doesn't have one yet
                if (string.IsNullOrEmpty(doc.TempFilePath))
                {
                    var ext = doc.RenderMode == RenderMode.Markdown ? ".md" : ".mmd";
                    doc.TempFilePath = Path.Combine(AutoSaveFolder, $"autosave_{Guid.NewGuid():N}{ext}");
                }
                
                // Always write to temp file for untitled docs, or dirty saved docs
                if (string.IsNullOrEmpty(doc.FilePath) || doc.IsDirty)
                {
                    File.WriteAllText(doc.TempFilePath, doc.TextDocument.Text);
                }
            }
        }
        catch
        {
            // Silently fail - auto-save should never interrupt the user
        }
    }
    
    /// <summary>
    /// Saves the current session state to session.json.
    /// This records all open tabs, their file paths, temp file paths, and editor state.
    /// </summary>
    private void SaveSessionState()
    {
        try
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }
            
            var session = new SessionState
            {
                ActiveDocumentIndex = _activeDocument != null ? _openDocuments.IndexOf(_activeDocument) : 0,
                Documents = _openDocuments.Select(doc => new SessionDocumentState
                {
                    FilePath = doc.FilePath,
                    TempFilePath = doc.TempFilePath,
                    RenderMode = doc.RenderMode.ToString(),
                    IsDirty = doc.IsDirty,
                    CaretOffset = doc.CaretOffset,
                    VerticalScrollOffset = doc.VerticalScrollOffset,
                    HorizontalScrollOffset = doc.HorizontalScrollOffset,
                    PreviewZoom = doc.PreviewZoom,
                    PreviewScrollLeft = doc.PreviewScrollLeft,
                    PreviewScrollTop = doc.PreviewScrollTop,
                    PreviewPanX = doc.PreviewPanX,
                    PreviewPanY = doc.PreviewPanY
                }).ToList()
            };
            
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(session, options);
            File.WriteAllText(SessionFilePath, json);
        }
        catch
        {
            // Silently fail
        }
    }
    
    /// <summary>
    /// Attempts to restore the previous session from session.json.
    /// Returns true if a session was successfully restored, false otherwise.
    /// </summary>
    private bool RestoreSessionState()
    {
        try
        {
            if (!File.Exists(SessionFilePath))
                return false;
            
            var json = File.ReadAllText(SessionFilePath);
            var session = System.Text.Json.JsonSerializer.Deserialize<SessionState>(json);
            
            if (session == null || session.Documents.Count == 0)
                return false;
            
            var restoredDocs = new List<DocumentModel>();
            
            foreach (var docState in session.Documents)
            {
                DocumentModel? doc = null;
                
                if (!string.IsNullOrEmpty(docState.FilePath) && File.Exists(docState.FilePath))
                {
                    // Saved file that still exists on disk
                    string content;
                    
                    if (docState.IsDirty && !string.IsNullOrEmpty(docState.TempFilePath) && File.Exists(docState.TempFilePath))
                    {
                        // Has unsaved changes - restore from temp file
                        content = File.ReadAllText(docState.TempFilePath);
                    }
                    else
                    {
                        // Clean file - read from disk
                        content = File.ReadAllText(docState.FilePath);
                    }
                    
                    doc = CreateNewDocument(docState.FilePath, content);
                    doc.LastKnownWriteTime = File.GetLastWriteTimeUtc(docState.FilePath);
                    doc.IsDirty = docState.IsDirty;
                    doc.TempFilePath = docState.TempFilePath;
                    
                    // Set up file watcher for external change detection
                    SetupFileWatcher(docState.FilePath);
                    
                    // Add to recent files
                    AddToRecentFiles(docState.FilePath);
                }
                else if (!string.IsNullOrEmpty(docState.TempFilePath) && File.Exists(docState.TempFilePath))
                {
                    // Untitled document - restore from temp file
                    var content = File.ReadAllText(docState.TempFilePath);
                    doc = CreateNewDocument(null, content);
                    doc.TempFilePath = docState.TempFilePath;
                    doc.IsDirty = true; // Untitled docs are always considered dirty
                }
                else if (!string.IsNullOrEmpty(docState.FilePath))
                {
                    // Saved file that no longer exists - skip it
                    continue;
                }
                else
                {
                    // No file path and no temp file - skip
                    continue;
                }
                
                if (doc != null)
                {
                    // Restore render mode
                    if (Enum.TryParse<RenderMode>(docState.RenderMode, out var renderMode))
                    {
                        doc.RenderMode = renderMode;
                    }
                    
                    // Restore editor state
                    doc.CaretOffset = docState.CaretOffset;
                    doc.VerticalScrollOffset = docState.VerticalScrollOffset;
                    doc.HorizontalScrollOffset = docState.HorizontalScrollOffset;
                    doc.PreviewZoom = docState.PreviewZoom;
                    doc.PreviewScrollLeft = docState.PreviewScrollLeft;
                    doc.PreviewScrollTop = docState.PreviewScrollTop;
                    doc.PreviewPanX = docState.PreviewPanX;
                    doc.PreviewPanY = docState.PreviewPanY;
                    
                    restoredDocs.Add(doc);
                }
            }
            
            if (restoredDocs.Count == 0)
                return false;
            
            // Switch to the previously active document
            var activeIndex = Math.Clamp(session.ActiveDocumentIndex, 0, restoredDocs.Count - 1);
            SwitchToDocument(restoredDocs[activeIndex]);
            
            // Clean up orphaned temp files
            CleanupOrphanedTempFiles();
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Removes temp files from the AutoSave directory that are not referenced by any open document.
    /// This prevents accumulation of stale temp files over time.
    /// </summary>
    private void CleanupOrphanedTempFiles()
    {
        try
        {
            if (!Directory.Exists(AutoSaveFolder))
                return;
            
            // Collect all temp file paths currently in use
            var activeTempFiles = new HashSet<string>(
                _openDocuments
                    .Where(d => !string.IsNullOrEmpty(d.TempFilePath))
                    .Select(d => d.TempFilePath!),
                StringComparer.OrdinalIgnoreCase);
            
            // Delete any temp files not in the active set
            foreach (var file in Directory.GetFiles(AutoSaveFolder, "autosave_*"))
            {
                if (!activeTempFiles.Contains(file))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch
        {
            // Silently fail
        }
    }
    
    /// <summary>
    /// Deletes a document's temp file if it exists.
    /// Called when a document is closed or saved to a real file path.
    /// </summary>
    private void DeleteTempFile(DocumentModel doc)
    {
        if (!string.IsNullOrEmpty(doc.TempFilePath))
        {
            try
            {
                if (File.Exists(doc.TempFilePath))
                {
                    File.Delete(doc.TempFilePath);
                }
            }
            catch { }
            doc.TempFilePath = null;
        }
    }
    
    #endregion
}

public class MermaidCompletionData: ICompletionData
{
    public MermaidCompletionData(string text, string description)
    {
        Text = text;
        Description = description;
    }

    public ImageSource? Image => null;
    public string Text { get; }
    public object Content => Text;
    public object Description { get; }
    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}

public class FileListItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public string Icon { get; set; } = "";
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);
}

public class NavigationItem
{
    public string DisplayText { get; set; } = "";
    public string RawText { get; set; } = "";
    public int LineNumber { get; set; }
    public int Level { get; set; }
    public string HeadingId { get; set; } = "";
    public Thickness IndentMargin => new Thickness((Level - 1) * 12, 0, 0, 0);
}

/// <summary>
/// Represents an open document in the multi-document interface
/// </summary>
public class DocumentModel : System.ComponentModel.INotifyPropertyChanged
{
    private string? _filePath;
    private bool _isDirty;
    
    public string? FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(TabHeader));
        }
    }
    
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            _isDirty = value;
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(TabHeader));
        }
    }
    
    public RenderMode RenderMode { get; set; } = RenderMode.Mermaid;
    
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }
    
    // Editor state
    public ICSharpCode.AvalonEdit.Document.TextDocument TextDocument { get; set; } = new();
    public int CaretOffset { get; set; }
    public double VerticalScrollOffset { get; set; }
    public double HorizontalScrollOffset { get; set; }
    public int SelectionStart { get; set; }
    public int SelectionLength { get; set; }
    
    // Preview state
    public double PreviewZoom { get; set; } = 1.0;
    public bool HasNavigatedAway { get; set; }
    public double PreviewScrollLeft { get; set; }
    public double PreviewScrollTop { get; set; }
    public double PreviewPanX { get; set; }
    public double PreviewPanY { get; set; }
    
    // File change detection
    public DateTime LastKnownWriteTime { get; set; }
    public bool ExternalChangeDetected { get; set; }
    
    // Auto-save / session restore
    public string? TempFilePath { get; set; }
    
    // UI element references for the tab
    public System.Windows.Controls.Button? TabButton { get; set; }
    public System.Windows.Controls.Border? TabBorder { get; set; }
    
    public string DisplayName => string.IsNullOrEmpty(FilePath) ? "Untitled" : Path.GetFileName(FilePath);
    
    public string TabHeader => IsDirty ? $"{DisplayName} *" : DisplayName;
    
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Highlights matching brackets in the code editor
/// </summary>
public class BracketHighlightRenderer : ICSharpCode.AvalonEdit.Rendering.IBackgroundRenderer
{
    private readonly ICSharpCode.AvalonEdit.Editing.TextArea _textArea;
    private int _openBracketOffset = -1;
    private int _closeBracketOffset = -1;
    
    private static readonly Dictionary<char, char> BracketPairs = new()
    {
        { '(', ')' },
        { '[', ']' },
        { '{', '}' },
        { '<', '>' }
    };
    
    private static readonly Dictionary<char, char> ReverseBracketPairs = new()
    {
        { ')', '(' },
        { ']', '[' },
        { '}', '{' },
        { '>', '<' }
    };

    public BracketHighlightRenderer(ICSharpCode.AvalonEdit.Editing.TextArea textArea)
    {
        _textArea = textArea;
    }

    public ICSharpCode.AvalonEdit.Rendering.KnownLayer Layer => ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection;

    public void UpdateBrackets(ICSharpCode.AvalonEdit.Document.TextDocument document, int caretOffset)
    {
        _openBracketOffset = -1;
        _closeBracketOffset = -1;
        
        if (caretOffset < 0 || caretOffset > document.TextLength)
        {
            _textArea.TextView.InvalidateLayer(Layer);
            return;
        }
        
        // Check character before caret (cursor is after the bracket)
        if (caretOffset > 0)
        {
            char charBefore = document.GetCharAt(caretOffset - 1);
            
            if (BracketPairs.TryGetValue(charBefore, out char closingBracket))
            {
                // Found opening bracket, search forward for closing
                _openBracketOffset = caretOffset - 1;
                _closeBracketOffset = FindMatchingBracket(document, caretOffset, charBefore, closingBracket, 1);
            }
            else if (ReverseBracketPairs.TryGetValue(charBefore, out char openingBracket))
            {
                // Found closing bracket, search backward for opening
                _closeBracketOffset = caretOffset - 1;
                _openBracketOffset = FindMatchingBracket(document, caretOffset - 2, openingBracket, charBefore, -1);
            }
        }
        
        // Also check character at caret position (cursor is before the bracket)
        if (_openBracketOffset < 0 && _closeBracketOffset < 0 && caretOffset < document.TextLength)
        {
            char charAt = document.GetCharAt(caretOffset);
            
            if (BracketPairs.TryGetValue(charAt, out char closingBracket))
            {
                // Found opening bracket, search forward for closing
                _openBracketOffset = caretOffset;
                _closeBracketOffset = FindMatchingBracket(document, caretOffset + 1, charAt, closingBracket, 1);
            }
            else if (ReverseBracketPairs.TryGetValue(charAt, out char openingBracket))
            {
                // Found closing bracket, search backward for opening
                _closeBracketOffset = caretOffset;
                _openBracketOffset = FindMatchingBracket(document, caretOffset - 1, openingBracket, charAt, -1);
            }
        }
        
        _textArea.TextView.InvalidateLayer(Layer);
    }

    private int FindMatchingBracket(ICSharpCode.AvalonEdit.Document.TextDocument document, int startOffset, char openBracket, char closeBracket, int direction)
    {
        int depth = 1;
        int offset = startOffset;
        
        while (offset >= 0 && offset < document.TextLength)
        {
            char c = document.GetCharAt(offset);
            
            if (c == openBracket)
            {
                if (direction > 0) depth++;
                else depth--;
            }
            else if (c == closeBracket)
            {
                if (direction > 0) depth--;
                else depth++;
            }
            
            if (depth == 0)
                return offset;
            
            offset += direction;
        }
        
        return -1;
    }

    public void Draw(ICSharpCode.AvalonEdit.Rendering.TextView textView, System.Windows.Media.DrawingContext drawingContext)
    {
        if (_openBracketOffset < 0 && _closeBracketOffset < 0)
            return;
        
        var builder = new ICSharpCode.AvalonEdit.Rendering.BackgroundGeometryBuilder
        {
            CornerRadius = 1
        };
        
        // Use a bright, visible highlight color that works on both light and dark themes
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 255, 215, 0)); // Gold/yellow background
        var pen = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 165, 0)), 2); // Orange border
        
        if (_openBracketOffset >= 0)
        {
            var segment = new ICSharpCode.AvalonEdit.Document.TextSegment { StartOffset = _openBracketOffset, Length = 1 };
            builder.AddSegment(textView, segment);
            var geometry = builder.CreateGeometry();
            if (geometry != null)
            {
                drawingContext.DrawGeometry(brush, pen, geometry);
            }
            builder = new ICSharpCode.AvalonEdit.Rendering.BackgroundGeometryBuilder { CornerRadius = 1 };
        }
        
        if (_closeBracketOffset >= 0)
        {
            var segment = new ICSharpCode.AvalonEdit.Document.TextSegment { StartOffset = _closeBracketOffset, Length = 1 };
            builder.AddSegment(textView, segment);
            var geometry = builder.CreateGeometry();
            if (geometry != null)
            {
                drawingContext.DrawGeometry(brush, pen, geometry);
            }
        }
    }
}

/// <summary>
/// Represents the persisted session state for restoring tabs on startup
/// </summary>
public class SessionState
{
    public int Version { get; set; } = 1;
    public int ActiveDocumentIndex { get; set; }
    public List<SessionDocumentState> Documents { get; set; } = new();
}

/// <summary>
/// Represents a single document's state within the session
/// </summary>
public class SessionDocumentState
{
    public string? FilePath { get; set; }
    public string? TempFilePath { get; set; }
    public string RenderMode { get; set; } = "Mermaid";
    public bool IsDirty { get; set; }
    public int CaretOffset { get; set; }
    public double VerticalScrollOffset { get; set; }
    public double HorizontalScrollOffset { get; set; }
    public double PreviewZoom { get; set; } = 1.0;
    public double PreviewScrollLeft { get; set; }
    public double PreviewScrollTop { get; set; }
    public double PreviewPanX { get; set; }
    public double PreviewPanY { get; set; }
}
