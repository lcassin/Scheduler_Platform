using System.IO;
using System.IO.Packaging;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
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
    private string? _currentFilePath;
    private bool _isDirty;
    private double _currentZoom = 1.0;
    private readonly DispatcherTimer _renderTimer;
    private bool _webViewInitialized;
    private CompletionWindow? _completionWindow;
    private RenderMode _currentRenderMode = RenderMode.Mermaid;
    private TaskCompletionSource<string>? _pngExportTcs;
    private string _currentBrowserPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private bool _isBrowsingFiles;
    private string? _lastExportDirectory;
    private string? _currentVirtualHostFolder;
    private const string VirtualHostName = "localfiles.mermaideditor";

    private const string DefaultMermaidCode = @"flowchart TD
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

        SetupCodeEditor();
        
        // Check for command-line arguments (file passed via file association)
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1]))
        {
            // Load the file passed as argument
            LoadFile(args[1]);
        }
        else
        {
            // Load default content (not dirty)
            CodeEditor.Text = DefaultMermaidCode;
            _isDirty = false;
        }
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
            
            // Navigate file browser to the file's folder and select the file
            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder))
            {
                NavigateBrowserToFolder(folder, filePath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            CodeEditor.Text = DefaultMermaidCode;
            _isDirty = false;
        }
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
            await PreviewWebView.EnsureCoreWebView2Async();
            _webViewInitialized = true;
            RenderMermaid();
            
            // Style the toolbar overflow button programmatically
            StyleToolbarOverflowButtons();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2: {ex.Message}\n\nMake sure WebView2 Runtime is installed.",
                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        if (_isDirty)
        {
            var result = MessageBox.Show("You have unsaved changes. Do you want to save before closing?",
                "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    Save_Click(this, new RoutedEventArgs());
                    break;
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;
            }
        }
    }

    private void CodeEditor_TextChanged(object? sender, EventArgs e)
    {
        _isDirty = true;
        UpdateTitle();
        _renderTimer.Stop();
        _renderTimer.Start();
    }

    private void RenderTimer_Tick(object? sender, EventArgs e)
    {
        _renderTimer.Stop();
        RenderPreview();
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
            // Find all clickable elements in the SVG (nodes, edges, labels)
            const clickableElements = svg.querySelectorAll('[id*=""flowchart-""], [id*=""stateDiagram-""], [id*=""classDiagram-""], [id*=""sequenceDiagram-""], .node, .cluster, .actor, .messageText, .labelText, .edgeLabel, .nodeLabel, g[class*=""node""]');
            
            clickableElements.forEach(el => {{
                el.style.cursor = 'pointer';
                el.addEventListener('click', function(e) {{
                    e.stopPropagation();
                    
                    // Try to extract the node ID from the element
                    let nodeId = '';
                    let textContent = '';
                    
                    // Get the element's ID
                    const elId = el.id || el.getAttribute('id') || '';
                    
                    // Try to extract node ID from Mermaid's ID format (e.g., 'flowchart-A-0')
                    if (elId) {{
                        const parts = elId.split('-');
                        if (parts.length >= 2) {{
                            nodeId = parts[1]; // Get the node name (e.g., 'A' from 'flowchart-A-0')
                        }}
                    }}
                    
                    // Also get text content from the element or its children
                    const textEl = el.querySelector('text, .nodeLabel, span, foreignObject') || el;
                    textContent = (textEl.textContent || textEl.innerText || '').trim();
                    
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
            
            // Also handle clicks on text elements directly
            const textElements = svg.querySelectorAll('text, .nodeLabel');
            textElements.forEach(el => {{
                if (!el.closest('[id*=""flowchart-""]')) {{ // Don't double-handle
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

        PreviewWebView.NavigateToString(html);
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
        
        // First, try to find by node ID (for Mermaid diagrams)
        if (!string.IsNullOrEmpty(nodeId))
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                
                // Look for node definitions like "A[text]", "A{text}", "A((text))", "A-->B", etc.
                // Also look for node ID at the start of arrows or in brackets
                var patterns = new[]
                {
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(nodeId)}\s*[\[\(\{{\<]",  // A[, A(, A{, A<
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(nodeId)}\s*-->",          // A-->
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(nodeId)}\s*---",          // A---
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(nodeId)}\s*-\.\-",        // A-.-
                    $@"-->\s*{System.Text.RegularExpressions.Regex.Escape(nodeId)}\b",          // -->A
                    $@"---\s*{System.Text.RegularExpressions.Regex.Escape(nodeId)}\b",          // ---A
                    $@"^\s*{System.Text.RegularExpressions.Regex.Escape(nodeId)}\s*:",          // A: (for state diagrams)
                    $@"state\s+{System.Text.RegularExpressions.Regex.Escape(nodeId)}\b",        // state A
                };
                
                foreach (var pattern in patterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        bestLineIndex = i;
                        bestMatchStart = match.Index;
                        bestMatchLength = nodeId.Length;
                        break;
                    }
                }
                
                if (bestLineIndex >= 0) break;
            }
        }
        
        // If no match by nodeId, try to find by text content
        if (bestLineIndex < 0 && !string.IsNullOrEmpty(text))
        {
            // For Markdown, handle different element types
            if (!string.IsNullOrEmpty(elementType))
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    
                    if (elementType == "heading")
                    {
                        // Look for heading markers followed by the text
                        if (line.TrimStart().StartsWith("#") && line.Contains(text, StringComparison.OrdinalIgnoreCase))
                        {
                            bestLineIndex = i;
                            var idx = line.IndexOf(text, StringComparison.OrdinalIgnoreCase);
                            if (idx >= 0)
                            {
                                bestMatchStart = idx;
                                bestMatchLength = text.Length;
                            }
                            break;
                        }
                    }
                    else if (elementType == "code")
                    {
                        // Look for code blocks or inline code
                        if (line.Contains(text))
                        {
                            bestLineIndex = i;
                            var idx = line.IndexOf(text);
                            if (idx >= 0)
                            {
                                bestMatchStart = idx;
                                bestMatchLength = text.Length;
                            }
                            break;
                        }
                    }
                    else
                    {
                        // Generic text search
                        if (line.Contains(text, StringComparison.OrdinalIgnoreCase))
                        {
                            bestLineIndex = i;
                            var idx = line.IndexOf(text, StringComparison.OrdinalIgnoreCase);
                            if (idx >= 0)
                            {
                                bestMatchStart = idx;
                                bestMatchLength = text.Length;
                            }
                            break;
                        }
                    }
                }
            }
            else
            {
                // For Mermaid, search for the text in brackets or quotes
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    
                    // Look for text in brackets: [text], (text), {text}, "text"
                    var bracketPatterns = new[]
                    {
                        $@"\[{System.Text.RegularExpressions.Regex.Escape(text)}\]",
                        $@"\({System.Text.RegularExpressions.Regex.Escape(text)}\)",
                        $@"\{{{System.Text.RegularExpressions.Regex.Escape(text)}\}}",
                        $@"""{System.Text.RegularExpressions.Regex.Escape(text)}""",
                        $@"'{System.Text.RegularExpressions.Regex.Escape(text)}'",
                    };
                    
                    foreach (var pattern in bracketPatterns)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(line, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            bestLineIndex = i;
                            bestMatchStart = match.Index + 1; // Skip the opening bracket
                            bestMatchLength = text.Length;
                            break;
                        }
                    }
                    
                    if (bestLineIndex >= 0) break;
                    
                    // Also try plain text search as fallback
                    if (line.Contains(text, StringComparison.OrdinalIgnoreCase))
                    {
                        bestLineIndex = i;
                        var idx = line.IndexOf(text, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            bestMatchStart = idx;
                            bestMatchLength = text.Length;
                        }
                        break;
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
        
        // Update header text and syntax highlighting based on mode
        if (_currentRenderMode == RenderMode.Markdown)
        {
            EditorHeaderText.Text = "Markdown Code";
            CodeEditor.SyntaxHighlighting = null; // Use default for Markdown
        }
        else
        {
            EditorHeaderText.Text = "Mermaid Code";
            CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Mermaid");
        }
        
        // Update export menu visibility based on file type
        UpdateExportMenuVisibility();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (_isDirty)
        {
            var result = MessageBox.Show("You have unsaved changes. Do you want to save first?",
                "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Save_Click(sender, e);
            }
            else if (result == MessageBoxResult.Cancel)
            {
                return;
            }
        }

        // Show template selection dialog
        var dialog = new NewDocumentDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedTemplate != null)
        {
            CodeEditor.Text = dialog.SelectedTemplate;
            _currentFilePath = null;
            _isDirty = false;
            
            if (dialog.IsMermaid)
            {
                _currentRenderMode = RenderMode.Mermaid;
                EditorHeaderText.Text = "Mermaid Code";
                CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Mermaid");
            }
            else
            {
                _currentRenderMode = RenderMode.Markdown;
                EditorHeaderText.Text = "Markdown Code";
                CodeEditor.SyntaxHighlighting = null;
            }
            
            UpdateExportMenuVisibility();
            UpdateTitle();
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (_isDirty)
        {
            var result = MessageBox.Show("You have unsaved changes. Do you want to save first?",
                "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Save_Click(sender, e);
            }
            else if (result == MessageBoxResult.Cancel)
            {
                return;
            }
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Mermaid Files (*.mmd;*.mermaid)|*.mmd;*.mermaid|Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
            Title = "Open Mermaid File"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                CodeEditor.Text = File.ReadAllText(dialog.FileName);
                _currentFilePath = dialog.FileName;
                SetRenderModeFromFile(dialog.FileName);
                _isDirty = false;
                UpdateTitle();
                RenderPreview();
                StatusText.Text = "File opened";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveAs_Click(sender, e);
            return;
        }

        try
        {
            File.WriteAllText(_currentFilePath, CodeEditor.Text);
            _isDirty = false;
            UpdateTitle();
            StatusText.Text = "File saved";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Mermaid Files (*.mmd)|*.mmd|Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
            Title = "Save Mermaid File",
            DefaultExt = ".mmd"
        };

        if (dialog.ShowDialog() == true)
        {
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
        }
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (CodeEditor.CanRedo)
        {
            CodeEditor.Redo();
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
        // Open the docx as a ZIP package and fix the [Content_Types].xml
        using var package = Package.Open(docxPath, FileMode.Open, FileAccess.ReadWrite);
        
        // Get the content types part
        var contentTypesPart = package.GetPart(new Uri("/[Content_Types].xml", UriKind.Relative));
        
        // Read the current content
        string content;
        using (var stream = contentTypesPart.GetStream(FileMode.Open, FileAccess.Read))
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
            
            // Write the fixed content back
            using var stream = contentTypesPart.GetStream(FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(stream);
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
        int widthEmu, heightEmu;
        using (var stream = new MemoryStream(imageBytes))
        {
            using var bitmap = new System.Drawing.Bitmap(stream);
            // Convert pixels to EMUs (914400 EMUs per inch, assuming 96 DPI)
            widthEmu = (int)(bitmap.Width * 914400 / 96);
            heightEmu = (int)(bitmap.Height * 914400 / 96);

            // Limit max width to 6 inches
            var maxWidthEmu = 6 * 914400;
            if (widthEmu > maxWidthEmu)
            {
                var ratio = (double)maxWidthEmu / widthEmu;
                widthEmu = maxWidthEmu;
                heightEmu = (int)(heightEmu * ratio);
            }
        }

        var relationshipId = mainPart.GetIdOfPart(imagePart);
        var imageName = Path.GetFileNameWithoutExtension(imagePath);
        return CreateImageElement(relationshipId, widthEmu, heightEmu, imageName);
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

    private async void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewInitialized) return;
        _currentZoom = Math.Min(_currentZoom * 1.25, 10);
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.setZoom({_currentZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        ZoomLevelText.Text = $"{_currentZoom * 100:F0}%";
    }

    private async void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewInitialized) return;
        _currentZoom = Math.Max(_currentZoom / 1.25, 0.1);
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.setZoom({_currentZoom.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        ZoomLevelText.Text = $"{_currentZoom * 100:F0}%";
    }

    private async void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewInitialized) return;
        _currentZoom = 1.0;
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync("window.resetView()");
        ZoomLevelText.Text = "100%";
    }

    private async void FitToWindow_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewInitialized) return;
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync("window.fitToWindow()");
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

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(

            "Mermaid Editor v1.7\n\n" +
            "A simple IDE for editing Mermaid diagrams and Markdown files.\n\n" +
            "Features:\n" +
            "- Live preview as you type\n" +
            "- Mermaid diagram rendering with pan/zoom\n" +
            "- Markdown rendering with GitHub styling\n" +
            "- On Click Syntax highlighting and IntelliSense\n" +
            "- Export to PNG, SVG, EMF and Word\n" +
            "- File open/save support\n" +
            "- Drag and drop file support\n\n" +
            "Supported file types:\n" +
            "- .mmd, .mermaid - Mermaid diagrams\n" +
            "- .md - Markdown files\n\n" +
			"� 2026 Lee Cassin",
            "About Mermaid Editor",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // File Browser Methods
    private void RefreshFileList()
    {
        try
        {
            var items = new List<FileListItem>();
            
            // Add directories
            foreach (var dir in Directory.GetDirectories(_currentBrowserPath))
            {
                var dirInfo = new DirectoryInfo(dir);
                items.Add(new FileListItem
                {
                    Name = dirInfo.Name,
                    FullPath = dir,
                    IsDirectory = true,
                    Icon = "\uD83D\uDCC1" // Folder icon
                });
            }
            
            // Add .mmd, .mermaid, and .md files
            var extensions = new[] { "*.mmd", "*.mermaid", "*.md" };
            foreach (var ext in extensions)
            {
                foreach (var file in Directory.GetFiles(_currentBrowserPath, ext))
                {
                    var fileInfo = new FileInfo(file);
                    items.Add(new FileListItem
                    {
                        Name = fileInfo.Name,
                        FullPath = file,
                        IsDirectory = false,
                        Icon = fileInfo.Extension.ToLower() == ".md" ? "\uD83D\uDCC4" : "\uD83D\uDCC8" // Document or chart icon
                    });
                }
            }
            
            // Sort: directories first, then files alphabetically
            items = items.OrderBy(x => !x.IsDirectory).ThenBy(x => x.Name).ToList();
            
            FileListBox.ItemsSource = items;
            CurrentPathTextBox.Text = _currentBrowserPath;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to read directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = _currentBrowserPath,
            Description = "Select a folder to browse"
        };
        
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _currentBrowserPath = dialog.SelectedPath;
            RefreshFileList();
        }
    }

    private void ParentFolder_Click(object sender, RoutedEventArgs e)
    {
        var parent = Directory.GetParent(_currentBrowserPath);
        if (parent != null)
        {
            _currentBrowserPath = parent.FullName;
            RefreshFileList();
        }
    }

    private void FileListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (FileListBox.SelectedItem is FileListItem item && !item.IsDirectory)
        {
            // Preview the file without fully opening it for editing
            _isBrowsingFiles = true;
            try
            {
                var content = File.ReadAllText(item.FullPath);
                var ext = Path.GetExtension(item.FullPath).ToLowerInvariant();
                
                // Set render mode based on file type
                if (ext == ".md")
                {
                    _currentRenderMode = RenderMode.Markdown;
                }
                else
                {
                    _currentRenderMode = RenderMode.Mermaid;
                }
                
                // Render preview directly without changing the editor
                if (_webViewInitialized)
                {
                    if (_currentRenderMode == RenderMode.Mermaid)
                    {
                        RenderMermaidPreview(content);
                    }
                    else
                    {
                        RenderMarkdownPreview(content, item.FullPath);
                    }
                }
                
                StatusText.Text = $"Previewing: {item.Name}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error previewing file: {ex.Message}";
            }
            finally
            {
                _isBrowsingFiles = false;
            }
        }
    }

    private void FileListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FileListBox.SelectedItem is FileListItem item)
        {
            if (item.IsDirectory)
            {
                // Navigate into directory
                _currentBrowserPath = item.FullPath;
                RefreshFileList();
            }
            else
            {
                // Open file for editing
                if (_isDirty)
                {
                    var result = MessageBox.Show("You have unsaved changes. Do you want to save first?",
                        "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        Save_Click(this, new RoutedEventArgs());
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                }
                
                LoadFile(item.FullPath);
                LeftPanelTabs.SelectedItem = CodeTab; // Switch to Code tab
                StatusText.Text = "File opened from browser";
            }
        }
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
        PreviewWebView.NavigateToString(html);
    }

    private void NavigateBrowserToFolder(string folderPath, string? selectFilePath = null)
    {
        if (Directory.Exists(folderPath))
        {
            _currentBrowserPath = folderPath;
            RefreshFileList();
            
            // Select the specified file in the list if provided
            if (!string.IsNullOrEmpty(selectFilePath) && FileListBox.ItemsSource is List<FileListItem> items)
            {
                var fileItem = items.FirstOrDefault(x => x.FullPath.Equals(selectFilePath, StringComparison.OrdinalIgnoreCase));
                if (fileItem != null)
                {
                    FileListBox.SelectedItem = fileItem;
                    FileListBox.ScrollIntoView(fileItem);
                }
            }
        }
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
                var filePath = files[0];
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                
                if (ext == ".mmd" || ext == ".mermaid" || ext == ".md")
                {
                    if (_isDirty)
                    {
                        var result = MessageBox.Show("You have unsaved changes. Do you want to save first?",
                            "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            Save_Click(this, new RoutedEventArgs());
                        }
                        else if (result == MessageBoxResult.Cancel)
                        {
                            return;
                        }
                    }

                    try
                    {
                        CodeEditor.Text = File.ReadAllText(filePath);
                        _currentFilePath = filePath;
                        SetRenderModeFromFile(filePath);
                        _isDirty = false;
                        UpdateTitle();
                        RenderPreview();
                        
                        // Navigate file browser to the file's folder and select the file
                        var folder = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(folder))
                        {
                            NavigateBrowserToFolder(folder, filePath);
                        }
                        
                        StatusText.Text = "File opened via drag and drop";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Please drop a .mmd, .mermaid, or .md file.", "Invalid File Type", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
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
