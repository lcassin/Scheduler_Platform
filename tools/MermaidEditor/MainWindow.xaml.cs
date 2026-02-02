using System.IO;
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

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        NewCommand = new RelayCommand(_ => New_Click(this, new RoutedEventArgs()));
        OpenCommand = new RelayCommand(_ => Open_Click(this, new RoutedEventArgs()));
        SaveCommand = new RelayCommand(_ => Save_Click(this, new RoutedEventArgs()));
        SaveAsCommand = new RelayCommand(_ => SaveAs_Click(this, new RoutedEventArgs()));

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
            
            // Navigate file browser to the file's folder
            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder))
            {
                NavigateBrowserToFolder(folder);
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
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2: {ex.Message}\n\nMake sure WebView2 Runtime is installed.",
                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
    <script src=""https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js""></script>
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
        }}
        #diagram {{
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            display: inline-block;
            min-width: 2000px;
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
            background: #ffebee;
            border-radius: 8px;
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
        
        mermaid.initialize({{ 
            startOnLoad: true,
            theme: 'default',
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
        }}).catch(err => {{
            document.getElementById('diagram').innerHTML = '<div class=""error"">Error: ' + err.message + '</div>';
        }});
        
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

        var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
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
            }
        }
        catch
        {
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

        CodeEditor.Text = DefaultMermaidCode;
        _currentFilePath = null;
        _isDirty = false;
        _currentRenderMode = RenderMode.Mermaid;
        EditorHeaderText.Text = "Mermaid Code";
        CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Mermaid");
        UpdateExportMenuVisibility();
        UpdateTitle();
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

    private (string directory, string filename) GetExportDefaults(string extension)
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
            filename = Path.GetFileNameWithoutExtension(_currentFilePath) + extension;
        }
        
        return (directory, filename);
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

        var (directory, filename) = GetExportDefaults(".png");
        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image (*.png)|*.png",
            Title = $"Export as PNG ({scale}x Resolution)",
            DefaultExt = ".png",
            InitialDirectory = directory,
            FileName = filename
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
        var (directory, filename) = GetExportDefaults(".png");
        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image (*.png)|*.png",
            Title = "Export as PNG",
            DefaultExt = ".png",
            InitialDirectory = directory,
            FileName = filename
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

        var (directory, filename) = GetExportDefaults(".svg");
        var dialog = new SaveFileDialog
        {
            Filter = "SVG Image (*.svg)|*.svg",
            Title = "Export as SVG",
            DefaultExt = ".svg",
            InitialDirectory = directory,
            FileName = filename
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

        var (directory, filename) = GetExportDefaults(".emf");
        var dialog = new SaveFileDialog
        {
            Filter = "Enhanced Metafile (*.emf)|*.emf",
            Title = "Export as EMF",
            DefaultExt = ".emf",
            InitialDirectory = directory,
            FileName = filename
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

    private void ExportWord_Click(object sender, RoutedEventArgs e)
    {
        var (directory, filename) = GetExportDefaults(".docx");
        var dialog = new SaveFileDialog
        {
            Filter = "Word Document (*.docx)|*.docx",
            Title = "Export as Word Document",
            DefaultExt = ".docx",
            InitialDirectory = directory,
            FileName = filename
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                UpdateLastExportDirectory(dialog.FileName);
                StatusText.Text = "Exporting Word document...";
                
                var markdown = CodeEditor.Text;
                ConvertMarkdownToWord(markdown, dialog.FileName);
                
                StatusText.Text = "Exported as Word document";
                MessageBox.Show("Word document exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export Word document: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Export failed";
            }
        }
    }

    private void ConvertMarkdownToWord(string markdown, string outputPath)
    {
        using var document = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        var lines = markdown.Split('\n');
        var inCodeBlock = false;
        var codeBlockContent = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd('\r');

            // Handle code blocks
            if (trimmedLine.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    // End code block - add accumulated content
                    foreach (var codeLine in codeBlockContent)
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
                                new Text(codeLine) { Space = SpaceProcessingModeValues.Preserve }
                            )
                        );
                        body.AppendChild(codePara);
                    }
                    codeBlockContent.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBlockContent.Add(trimmedLine);
                continue;
            }

            // Handle headers
            if (trimmedLine.StartsWith("# "))
            {
                AddHeading(body, trimmedLine.Substring(2), "28", true);
            }
            else if (trimmedLine.StartsWith("## "))
            {
                AddHeading(body, trimmedLine.Substring(3), "26", true);
            }
            else if (trimmedLine.StartsWith("### "))
            {
                AddHeading(body, trimmedLine.Substring(4), "24", true);
            }
            else if (trimmedLine.StartsWith("#### "))
            {
                AddHeading(body, trimmedLine.Substring(5), "22", true);
            }
            else if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                // Empty paragraph
                body.AppendChild(new Paragraph());
            }
            else
            {
                // Regular paragraph with inline formatting
                AddFormattedParagraph(body, trimmedLine);
            }
        }
    }

    private void AddHeading(Body body, string text, string fontSize, bool bold)
    {
        var para = new Paragraph();
        var run = new Run();
        var runProps = new RunProperties();
        
        runProps.AppendChild(new FontSize { Val = fontSize });
        if (bold)
        {
            runProps.AppendChild(new Bold());
        }
        
        run.AppendChild(runProps);
        run.AppendChild(new Text(text));
        para.AppendChild(run);
        body.AppendChild(para);
    }

    private void AddFormattedParagraph(Body body, string text)
    {
        var para = new Paragraph();
        
        // Parse inline formatting (bold, italic, code)
        var pattern = @"(\*\*\*(.+?)\*\*\*|\*\*(.+?)\*\*|\*(.+?)\*|`(.+?)`|([^*`]+))";
        var matches = Regex.Matches(text, pattern);

        foreach (Match match in matches)
        {
            var run = new Run();
            var runProps = new RunProperties();
            string content;

            if (match.Groups[2].Success) // Bold + Italic (***text***)
            {
                content = match.Groups[2].Value;
                runProps.AppendChild(new Bold());
                runProps.AppendChild(new Italic());
            }
            else if (match.Groups[3].Success) // Bold (**text**)
            {
                content = match.Groups[3].Value;
                runProps.AppendChild(new Bold());
            }
            else if (match.Groups[4].Success) // Italic (*text*)
            {
                content = match.Groups[4].Value;
                runProps.AppendChild(new Italic());
            }
            else if (match.Groups[5].Success) // Code (`text`)
            {
                content = match.Groups[5].Value;
                runProps.AppendChild(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" });
                runProps.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "E8E8E8" });
            }
            else // Plain text
            {
                content = match.Groups[6].Value;
            }

            if (runProps.HasChildren)
            {
                run.AppendChild(runProps);
            }
            run.AppendChild(new Text(content) { Space = SpaceProcessingModeValues.Preserve });
            para.AppendChild(run);
        }

        body.AppendChild(para);
    }

    private void UpdateExportMenuVisibility()
    {
        // Show diagram exports only for Mermaid files
        // Show Word export only for Markdown files
        var isMermaid = _currentRenderMode == RenderMode.Mermaid;
        
        ExportPngMenuItem.Visibility = isMermaid ? Visibility.Visible : Visibility.Collapsed;
        ExportSvgMenuItem.Visibility = isMermaid ? Visibility.Visible : Visibility.Collapsed;
        ExportEmfMenuItem.Visibility = isMermaid ? Visibility.Visible : Visibility.Collapsed;
        ExportWordMenuItem.Visibility = isMermaid ? Visibility.Collapsed : Visibility.Visible;
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
            "Mermaid Editor v1.2\n\n" +
            "A simple IDE for editing Mermaid diagrams and Markdown files.\n\n" +
            "Features:\n" +
            "- Live preview as you type\n" +
            "- Mermaid diagram rendering with pan/zoom\n" +
            "- Markdown rendering with GitHub styling\n" +
            "- Syntax highlighting and IntelliSense\n" +
            "- Export to PNG and SVG\n" +
            "- File open/save support\n" +
            "- Drag and drop file support\n\n" +
            "Supported file types:\n" +
            "- .mmd, .mermaid - Mermaid diagrams\n" +
            "- .md - Markdown files\n\n" +
            "Built with WPF, AvalonEdit, and WebView2",
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
                        RenderMarkdownPreview(content);
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
    <script src=""https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js""></script>
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
        }}
        #diagram {{
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            display: inline-block;
            min-width: 2000px;
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

    private void RenderMarkdownPreview(string code)
    {
        var html = $@"<!DOCTYPE html>
<html>
<head>
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

    private void NavigateBrowserToFolder(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            _currentBrowserPath = folderPath;
            RefreshFileList();
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
                        
                        // Navigate file browser to the file's folder
                        var folder = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(folder))
                        {
                            NavigateBrowserToFolder(folder);
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
