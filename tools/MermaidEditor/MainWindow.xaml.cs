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
    
    // File change detection
    private FileSystemWatcher? _fileWatcher;
    private bool _isReloadingFile; // Prevent recursive change notifications during reload
    private bool _isSavingFile; // Prevent change notification when we save the file ourselves

    private const string DefaultMermaidCode= @"flowchart TD
    A[Start] --> B{Is it working?}
    B -->|Yes| C[Great!]
    B -->|No| D[Debug]
    D --> B";

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

        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _renderTimer.Tick += RenderTimer_Tick;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SourceInitialized += MainWindow_SourceInitialized;

        SetupCodeEditor();
        
        // Initialize multi-document tab system
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
        // Enable dark title bar on Windows 10/11
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int value = 1; // Enable dark mode
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
                
                // Set caption color to dark gray (#1E1E1E) to override Windows accent color
                // Color format is 0x00BBGGRR (BGR, not RGB)
                int captionColor = 0x001E1E1E; // #1E1E1E in BGR format
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
            }
        }
        catch
        {
            // Silently fail if DWM API is not available (older Windows versions)
        }
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
        };

        RegisterMermaidSyntaxHighlighting();
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
            "</Keywords>" +
            "<Keywords color=\"Keyword\">" +
            "<Word>subgraph</Word><Word>end</Word><Word>direction</Word>" +
            "<Word>participant</Word><Word>actor</Word><Word>activate</Word><Word>deactivate</Word>" +
            "<Word>Note</Word><Word>note</Word><Word>loop</Word><Word>alt</Word><Word>else</Word>" +
            "<Word>opt</Word><Word>par</Word><Word>critical</Word><Word>break</Word><Word>rect</Word>" +
            "<Word>class</Word><Word>state</Word><Word>section</Word><Word>title</Word>" +
            "<Word>TB</Word><Word>TD</Word><Word>BT</Word><Word>RL</Word><Word>LR</Word>" +
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
        var wordStart = GetWordStart();
        var currentWord = GetCurrentWord(wordStart);
        
        if (string.IsNullOrEmpty(currentWord) || currentWord.Length < 1)
            return;

        var completionData = GetCompletionData(currentWord);
        if (completionData.Count == 0)
            return;

        _completionWindow = new CompletionWindow(CodeEditor.TextArea);
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
            // Load and apply saved theme
            ThemeManager.LoadTheme();
            UpdateThemeMenuCheckmarks();
            UpdateEditorTheme();
            
            await PreviewWebView.EnsureCoreWebView2Async();
            _webViewInitialized = true;
            
            // Set up navigation completed handler to update back button state
            PreviewWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            
            RenderPreview();
            
            // Style the toolbar overflow button programmatically
            StyleToolbarOverflowButtons();
            
            // Load recent files
            LoadRecentFiles();
            
            // Show New Document dialog if no file was opened via command line
            if (_showNewDocumentDialogOnLoad)
            {
                _showNewDocumentDialogOnLoad = false;
                ShowStartupNewDocumentDialog();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2: {ex.Message}\n\nMake sure WebView2 Runtime is installed.",
                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void CoreWebView2_NavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_isRenderingContent)
        {
            // This navigation is from our NavigateToString call - this is the base render
            _isRenderingContent = false;
            _hasNavigatedAway = false;
            PreviewBackButton.IsEnabled = false;
        }
        else if (_isGoingBack)
        {
            // This navigation is from clicking the back button - stay disabled
            _isGoingBack = false;
            _hasNavigatedAway = false;
            PreviewBackButton.IsEnabled = false;
        }
        else
        {
            // User has navigated away from rendered content (clicked a link)
            _hasNavigatedAway = true;
            PreviewBackButton.IsEnabled = true;
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
                var toggleButtons = FindVisualChildren<System.Windows.Controls.Primitives.ToggleButton>(toolBar);
                foreach (var toggleButton in toggleButtons)
                {
                    // Style the toggle button itself
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
                            return;
                        }
                    }
                    break;
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
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

    private void RenderMermaid()
    {
        if (!_webViewInitialized) return;

        var mermaidCode = CodeEditor.Text;
        var escapedCode = System.Text.Json.JsonSerializer.Serialize(mermaidCode);

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
            min-width: 2000px;
        }}
        #diagram.has-error {{
            min-width: auto;
            max-width: calc(100vw - 60px);
        }}
        #diagram svg {{
            display: block;
            max-width: none !important;
            min-width: 100% !important;
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
        let panzoomInstance = null;
        let currentZoom = {_currentZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)};
        
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
            
            // Fix SVG and container dimensions after render
            if (svg) {{
                // Get actual SVG dimensions
                let svgWidth = 0;
                let svgHeight = 0;
                
                // Try viewBox first
                const viewBox = svg.getAttribute('viewBox');
                if (viewBox) {{
                    const parts = viewBox.split(' ');
                    if (parts.length === 4) {{
                        svgWidth = parseFloat(parts[2]);
                        svgHeight = parseFloat(parts[3]);
                    }}
                }}
                
                // Fall back to getBBox
                if (svgWidth === 0 || svgHeight === 0) {{
                    try {{
                        const bbox = svg.getBBox();
                        svgWidth = bbox.width + 40;
                        svgHeight = bbox.height + 40;
                    }} catch (e) {{ }}
                }}
                
                // Set SVG dimensions
                if (svgWidth > 0 && svgHeight > 0) {{
                    svg.style.width = svgWidth + 'px';
                    svg.style.height = svgHeight + 'px';
                    svg.style.minWidth = svgWidth + 'px';
                    svg.style.minHeight = svgHeight + 'px';
                    
                    // Shrink container to fit SVG (remove the large min-width)
                    diagram.style.minWidth = 'auto';
                    diagram.style.width = 'auto';
                }}
            }}
            
            panzoomInstance = panzoom(diagram, {{
                maxZoom: 10,
                minZoom: 0.1,
                initialZoom: 1,
                bounds: false,
                boundsPadding: 0.1
            }});
            
            // Reset position to top-left after initialization
            panzoomInstance.moveTo(0, 0);
            panzoomInstance.zoomAbs(0, 0, 1);
            currentZoom = 1;
            
            panzoomInstance.on('zoom', function(e) {{
                currentZoom = e.getTransform().scale;
                window.chrome.webview.postMessage({{ type: 'zoom', level: currentZoom }});
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
                
                // Set explicit dimensions on the clone
                svgClone.setAttribute('width', svgWidth);
                svgClone.setAttribute('height', svgHeight);
                
                // Create a canvas with scaled dimensions
                const canvas = document.createElement('canvas');
                canvas.width = svgWidth * scale;
                canvas.height = svgHeight * scale;
                const ctx = canvas.getContext('2d');
                
                // Fill with white background
                ctx.fillStyle = 'white';
                ctx.fillRect(0, 0, canvas.width, canvas.height);
                
                // Serialize SVG to string and create a data URL (more reliable than blob URL)
                const svgData = new XMLSerializer().serializeToString(svgClone);
                const svgBase64 = btoa(unescape(encodeURIComponent(svgData)));
                const dataUrl = 'data:image/svg+xml;base64,' + svgBase64;
                
                const img = new Image();
                img.onload = function() {{
                    ctx.scale(scale, scale);
                    ctx.drawImage(img, 0, 0);
                    
                    // Convert to base64 PNG and send via postMessage
                    const pngData = canvas.toDataURL('image/png');
                    window.chrome.webview.postMessage({{ type: 'pngExport', data: pngData }});
                }};
                img.onerror = function(e) {{
                    window.chrome.webview.postMessage({{ type: 'pngExportError', error: 'Failed to load SVG as image: ' + (e.message || 'unknown error') }});
                }};
                img.src = dataUrl;
            }} catch (e) {{
                window.chrome.webview.postMessage({{ type: 'pngExportError', error: e.message }});
            }}
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

    private void RenderMarkdown()
    {
        if (!_webViewInitialized) return;

        var markdownCode = CodeEditor.Text;
        var escapedCode = System.Text.Json.JsonSerializer.Serialize(markdownCode);
        
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

        var html = $@"<!DOCTYPE html>
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
    </style>
</head>
<body>
    <article class=""markdown-body"" id=""content""></article>
    <script>
        const markdownContent = {escapedCode};
        
        marked.setOptions({{
            highlight: function(code, lang) {{
                if (lang && hljs.getLanguage(lang)) {{
                    try {{
                        return hljs.highlight(code, {{ language: lang }}).value;
                    }} catch (e) {{}}
                }}
                return code;
            }},
            breaks: true,
            gfm: true
        }});
        
        document.getElementById('content').innerHTML = marked.parse(markdownContent);
        
        // Add click handlers for click-to-highlight feature
        setupClickHandlers();
        
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
            
            // Add click handlers to list items
            content.querySelectorAll('li').forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
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
            content.querySelectorAll('strong').forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
                    e.stopPropagation();
                    const text = el.textContent.trim();
                    if (text) {{
                        window.chrome.webview.postMessage({{ 
                            type: 'elementClick', 
                            text: text,
                            elementType: 'bold'
                        }});
                    }}
                }});
            }});
            
            // Add click handlers to italic text (em)
            content.querySelectorAll('em').forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
                    e.stopPropagation();
                    const text = el.textContent.trim();
                    if (text) {{
                        window.chrome.webview.postMessage({{ 
                            type: 'elementClick', 
                            text: text,
                            elementType: 'italic'
                        }});
                    }}
                }});
            }});
            
            // Add click handlers to paragraphs (but not if they contain other clickable elements)
            content.querySelectorAll('p').forEach(el => {{
                if (!el.querySelector('code')) {{
                    el.style.cursor = 'pointer';
                    el.addEventListener('click', function(e) {{
                        if (e.target === el) {{
                            const text = el.textContent.trim().substring(0, 50); // First 50 chars
                            if (text) {{
                                window.chrome.webview.postMessage({{ 
                                    type: 'elementClick', 
                                    text: text,
                                    elementType: 'paragraph'
                                }});
                            }}
                        }}
                    }});
                }}
            }});
        }}
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
                    ZoomLevelText.Text = $"{_currentZoom * 100:F0}%";
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
            }
        }
        catch
        {
        }
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
            CodeEditor.SyntaxHighlighting = null; // Use default for Markdown
        }
        else
        {
            CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Mermaid");
        }
        
        // Update export menu visibility based on file type
        UpdateExportMenuVisibility();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        // Show template selection dialog
        var dialog = new NewDocumentDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedTemplate != null)
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
                CodeEditor.SyntaxHighlighting = null;
            }
            
            UpdateExportMenuVisibility();
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Mermaid Files (*.mmd;*.mermaid)|*.mmd;*.mermaid|Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
            Title = "Open Mermaid File"
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
        // Show Word export for both (Mermaid embeds as PNG, Markdown converts to formatted doc)
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
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.setZoom({_currentZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        ZoomLevelText.Text = $"{e.NewValue:F0}%";
    }
    
    private async Task ApplyZoom()
    {
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
        MessageBox.Show(
            "Mermaid Editor v2.0.0\n\n" +
            "A visual IDE for editing Mermaid diagrams and Markdown files.\n\n" +
            "Features:\n" +
            "- Live preview as you type\n" +
            "- Mermaid diagram rendering with pan/zoom\n" +
            "- Markdown rendering with GitHub styling\n" +
            "- Syntax highlighting and IntelliSense\n" +
            "- Click-to-navigate between preview and code\n" +
            "- Navigation dropdown for quick section jumping\n" +
            "- Export to PNG, SVG, EMF, and Word\n" +
            "- Word export embeds images for Markdown files\n" +
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
            "See LICENSE file for details.",
            "About Mermaid Editor",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ThemeDark_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(AppTheme.Dark);
    }

    private void ThemeLight_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(AppTheme.Light);
    }

    private void ThemeTwilight_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(AppTheme.Twilight);
    }

    private void ApplyTheme(AppTheme theme)
    {
        ThemeManager.ApplyTheme(theme);
        UpdateThemeMenuCheckmarks();
        UpdateEditorTheme();
        UpdateTitleBarTheme();
        UpdateTabStyles(); // Update tab colors for new theme
        RenderPreview(); // Re-render preview with new theme
    }

    private void UpdateThemeMenuCheckmarks()
    {
        ThemeDarkMenuItem.IsChecked = ThemeManager.CurrentTheme == AppTheme.Dark;
        ThemeLightMenuItem.IsChecked = ThemeManager.CurrentTheme == AppTheme.Light;
        ThemeTwilightMenuItem.IsChecked = ThemeManager.CurrentTheme == AppTheme.Twilight;
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

    private void RegisterThemeSyntaxHighlighting()
    {
        var isDark = ThemeManager.IsDarkTheme;
        
        // Color values based on theme
        var commentColor = isDark ? "#6A9955" : "#008000";
        var keywordColor = isDark ? "#569CD6" : "#0000FF";
        var diagramTypeColor = isDark ? "#C586C0" : "#AF00DB";
        
        var xshd = "<?xml version=\"1.0\"?>" +
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
            "</Keywords>" +
            "<Keywords color=\"Keyword\">" +
            "<Word>subgraph</Word><Word>end</Word><Word>direction</Word>" +
            "<Word>participant</Word><Word>actor</Word><Word>activate</Word><Word>deactivate</Word>" +
            "<Word>Note</Word><Word>note</Word><Word>loop</Word><Word>alt</Word><Word>else</Word>" +
            "<Word>opt</Word><Word>par</Word><Word>critical</Word><Word>break</Word><Word>rect</Word>" +
            "<Word>class</Word><Word>state</Word><Word>section</Word><Word>title</Word>" +
            "<Word>TB</Word><Word>TD</Word><Word>BT</Word><Word>RL</Word><Word>LR</Word>" +
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
            min-width: 2000px;
        }}
        #diagram.has-error {{
            min-width: auto;
            max-width: calc(100vw - 60px);
        }}
        #diagram svg {{
            display: block;
            max-width: none !important;
            min-width: 100% !important;
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
        mermaid.initialize({{ startOnLoad: true, theme: 'default', securityLevel: 'loose' }});
        mermaid.run().then(() => {{
            const diagram = document.getElementById('diagram');
            const svg = document.querySelector('#diagram svg');
            
            // Fix SVG and container dimensions after render
            if (svg) {{
                let svgWidth = 0;
                let svgHeight = 0;
                
                const viewBox = svg.getAttribute('viewBox');
                if (viewBox) {{
                    const parts = viewBox.split(' ');
                    if (parts.length === 4) {{
                        svgWidth = parseFloat(parts[2]);
                        svgHeight = parseFloat(parts[3]);
                    }}
                }}
                
                if (svgWidth === 0 || svgHeight === 0) {{
                    try {{
                        const bbox = svg.getBBox();
                        svgWidth = bbox.width + 40;
                        svgHeight = bbox.height + 40;
                    }} catch (e) {{ }}
                }}
                
                if (svgWidth > 0 && svgHeight > 0) {{
                    svg.style.width = svgWidth + 'px';
                    svg.style.height = svgHeight + 'px';
                    svg.style.minWidth = svgWidth + 'px';
                    svg.style.minHeight = svgHeight + 'px';
                    
                    diagram.style.minWidth = 'auto';
                    diagram.style.width = 'auto';
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
        
        NavigationDropdown.ItemsSource = items;
        if (items.Count > 0)
        {
            NavigationDropdown.SelectedIndex = 0;
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
        
        // Handle click on the border
        tabBorder.MouseLeftButtonDown += (s, e) =>
        {
            if (s is System.Windows.Controls.Border border && border.Tag is DocumentModel clickedDoc)
            {
                SwitchToDocument(clickedDoc);
            }
        };
        
        // Add context menu for tab operations with theme-aware styling
        var contextMenu = new System.Windows.Controls.ContextMenu();
        contextMenu.SetResourceReference(System.Windows.Controls.ContextMenu.BackgroundProperty, "ThemeToolbarBackgroundBrush");
        contextMenu.SetResourceReference(System.Windows.Controls.ContextMenu.BorderBrushProperty, "ThemeBorderBrush");
        contextMenu.SetResourceReference(System.Windows.Controls.ContextMenu.ForegroundProperty, "ThemeForegroundBrush");
        
        // Create a style for menu items with theme-aware colors
        var menuItemStyle = new System.Windows.Style(typeof(System.Windows.Controls.MenuItem));
        menuItemStyle.Setters.Add(new Setter(System.Windows.Controls.MenuItem.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
        menuItemStyle.Setters.Add(new Setter(System.Windows.Controls.MenuItem.ForegroundProperty, new DynamicResourceExtension("ThemeForegroundBrush")));
        menuItemStyle.Setters.Add(new Setter(System.Windows.Controls.MenuItem.PaddingProperty, new Thickness(8, 4, 8, 4)));
        
        // Add trigger for hover state
        var hoverTrigger = new Trigger { Property = System.Windows.Controls.MenuItem.IsHighlightedProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(System.Windows.Controls.MenuItem.BackgroundProperty, new DynamicResourceExtension("ThemeHoverBrush")));
        menuItemStyle.Triggers.Add(hoverTrigger);
        
        contextMenu.Resources.Add(typeof(System.Windows.Controls.MenuItem), menuItemStyle);
        
        // Get the filename for the Close menu item
        var fileName = string.IsNullOrEmpty(doc.FilePath) ? "Untitled" : System.IO.Path.GetFileName(doc.FilePath);
        var closeItem = new System.Windows.Controls.MenuItem { Header = $"Close \"{fileName}\"", Tag = doc };
        closeItem.Click += (s, e) => CloseDocument(doc);
        
        var closeAllItem = new System.Windows.Controls.MenuItem { Header = "Close All" };
        closeAllItem.Click += (s, e) => CloseAllDocuments();
        
        var closeAllButThisItem = new System.Windows.Controls.MenuItem { Header = "Close All But This", Tag = doc };
        closeAllButThisItem.Click += (s, e) => CloseAllDocumentsExcept(doc);
        
        // Create a custom separator using a disabled MenuItem with a Border as content
        // This avoids the white icon gutter that WPF's default Separator has
        var separatorItem = new System.Windows.Controls.MenuItem
        {
            IsEnabled = false,
            IsHitTestVisible = false,
            Focusable = false,
            Height = 9,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
        };
        // Create a custom template for the separator MenuItem with theme-aware colors
        var sepTemplate = new ControlTemplate(typeof(System.Windows.Controls.MenuItem));
        var sepBorder = new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
        sepBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "ThemeToolbarBackgroundBrush");
        sepBorder.SetValue(System.Windows.Controls.Border.HeightProperty, 9.0);
        var sepLine = new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
        sepLine.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "ThemeBorderBrush");
        sepLine.SetValue(System.Windows.Controls.Border.HeightProperty, 1.0);
        sepLine.SetValue(System.Windows.Controls.Border.MarginProperty, new Thickness(8, 4, 8, 4));
        sepBorder.AppendChild(sepLine);
        sepTemplate.VisualTree = sepBorder;
        separatorItem.Template = sepTemplate;
        
        contextMenu.Items.Add(closeItem);
        contextMenu.Items.Add(separatorItem);
        contextMenu.Items.Add(closeAllItem);
        contextMenu.Items.Add(closeAllButThisItem);
        
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
    private void SwitchToDocument(DocumentModel doc)
    {
        if (_activeDocument == doc) return;
        
        _isSwitchingDocuments = true;
        
        // Save current document state
        if (_activeDocument != null)
        {
            _activeDocument.CaretOffset = CodeEditor.CaretOffset;
            _activeDocument.VerticalScrollOffset = CodeEditor.VerticalOffset;
            _activeDocument.HorizontalScrollOffset = CodeEditor.HorizontalOffset;
            _activeDocument.PreviewZoom = _currentZoom;
            _activeDocument.HasNavigatedAway = _hasNavigatedAway;
            _activeDocument.IsSelected = false;
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
        }
        catch { }
        
        // Update UI
        UpdateTabStyles();
        UpdateTitle();
        UpdateNavigationDropdown();
        UpdateExportMenuVisibility();
        UpdateUndoRedoState();
        
        _isSwitchingDocuments = false;
        
        // Re-render preview for the new document
        // This will trigger NavigateToString which resets _hasNavigatedAway to false
        // and keeps the back button disabled for fresh renders
        RenderPreview();
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
        // Create initial document
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1]))
        {
            // Open file from command line
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
        }
        else
        {
            // Create a temporary blank document - the New Document dialog will be shown after window loads
            var doc = CreateNewDocument();
            SwitchToDocument(doc);
            _showNewDocumentDialogOnLoad = true;
        }
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
    
    private void ShowStartupNewDocumentDialog()
    {
        var dialog = new NewDocumentDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            if (dialog.OpenExistingFile)
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
                    RenderPreview();
                }
            }
            // If no template selected, keep the blank document
        }
        // If user cancelled dialog, keep the blank document
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
    
    // Preview state
    public double PreviewZoom { get; set; } = 1.0;
    public bool HasNavigatedAway { get; set; }
    
    // File change detection
    public DateTime LastKnownWriteTime { get; set; }
    public bool ExternalChangeDetected { get; set; }
    
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
