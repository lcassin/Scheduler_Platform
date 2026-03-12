using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;

namespace MermaidEditor;

/// <summary>
/// Handles bidirectional communication between the visual editor WebView2
/// and the C# FlowchartModel. Converts model to JSON for JS, processes
/// messages from JS, and maintains undo/redo history.
/// </summary>
public class VisualEditorBridge
{
    private readonly WebView2 _webView;
    private FlowchartModel _model;
    private readonly List<string> _undoStack = new();
    private readonly List<string> _redoStack = new();
    private const int MaxHistorySize = 100;
    private bool _isSuppressingUpdates;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Raised when the FlowchartModel is modified by the visual editor.
    /// The handler should regenerate the Mermaid text via MermaidSerializer.
    /// </summary>
    public event EventHandler<ModelChangedEventArgs>? ModelChanged;

    /// <summary>
    /// Raised when a node is selected in the visual editor.
    /// </summary>
    public event EventHandler<NodeSelectedEventArgs>? NodeSelected;

    /// <summary>
    /// Raised when selection is cleared in the visual editor.
    /// </summary>
    public event EventHandler? SelectionCleared;

    /// <summary>
    /// Raised when the visual editor is ready to receive diagram data.
    /// </summary>
    public event EventHandler? EditorReady;

    /// <summary>
    /// Gets the current FlowchartModel.
    /// </summary>
    public FlowchartModel Model => _model;

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Creates a new VisualEditorBridge for the given WebView2 control.
    /// </summary>
    /// <param name="webView">The WebView2 control hosting the visual editor.</param>
    /// <param name="model">The initial FlowchartModel to display.</param>
    public VisualEditorBridge(WebView2 webView, FlowchartModel model)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _model = model ?? throw new ArgumentNullException(nameof(model));

        _webView.WebMessageReceived += OnWebMessageReceived;
    }

    /// <summary>
    /// Detaches event handlers. Call when disposing.
    /// </summary>
    public void Detach()
    {
        _webView.WebMessageReceived -= OnWebMessageReceived;
    }

    /// <summary>
    /// Sends the current FlowchartModel to the visual editor as JSON.
    /// </summary>
    public async Task SendDiagramToEditorAsync()
    {
        var json = ConvertModelToJson(_model);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadDiagram({escaped})");
    }

    /// <summary>
    /// Updates the model reference (e.g., after text re-parse) and sends to editor.
    /// </summary>
    /// <param name="newModel">The new FlowchartModel from text parsing.</param>
    public async Task UpdateModelAsync(FlowchartModel newModel)
    {
        _model = newModel ?? throw new ArgumentNullException(nameof(newModel));
        await SendDiagramToEditorAsync();
    }

    /// <summary>
    /// Sets the visual editor theme.
    /// </summary>
    /// <param name="theme">"dark" or "light".</param>
    public async Task SetThemeAsync(string theme)
    {
        var escaped = JsonSerializer.Serialize(theme);
        await _webView.ExecuteScriptAsync($"window.setTheme({escaped})");
    }

    /// <summary>
    /// Triggers auto-layout in the visual editor.
    /// </summary>
    public async Task ApplyAutoLayoutAsync()
    {
        await _webView.ExecuteScriptAsync("window.applyAutoLayout()");
    }

    /// <summary>
    /// Undoes the last model change.
    /// </summary>
    public async Task UndoAsync()
    {
        if (_undoStack.Count == 0) return;

        // Save current state to redo
        var currentJson = ConvertModelToJson(_model);
        _redoStack.Add(currentJson);
        if (_redoStack.Count > MaxHistorySize)
            _redoStack.RemoveAt(0);

        // Restore previous state
        var previousJson = _undoStack[_undoStack.Count - 1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        _isSuppressingUpdates = true;
        try
        {
            RestoreModelFromJson(previousJson);
            await SendDiagramToEditorAsync();
            RaiseModelChanged("undo");
        }
        finally
        {
            _isSuppressingUpdates = false;
        }
    }

    /// <summary>
    /// Redoes the last undone change.
    /// </summary>
    public async Task RedoAsync()
    {
        if (_redoStack.Count == 0) return;

        // Save current state to undo
        var currentJson = ConvertModelToJson(_model);
        _undoStack.Add(currentJson);
        if (_undoStack.Count > MaxHistorySize)
            _undoStack.RemoveAt(0);

        // Restore redo state
        var redoJson = _redoStack[_redoStack.Count - 1];
        _redoStack.RemoveAt(_redoStack.Count - 1);

        _isSuppressingUpdates = true;
        try
        {
            RestoreModelFromJson(redoJson);
            await SendDiagramToEditorAsync();
            RaiseModelChanged("redo");
        }
        finally
        {
            _isSuppressingUpdates = false;
        }
    }

    /// <summary>
    /// Converts a FlowchartModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertModelToJson(FlowchartModel model)
    {
        var dto = new DiagramDto
        {
            Direction = model.Direction,
            Nodes = model.Nodes.Select(n => new NodeDto
            {
                Id = n.Id,
                Label = n.Label,
                Shape = n.Shape.ToString(),
                X = n.Position.X,
                Y = n.Position.Y,
                Width = n.Size.Width,
                Height = n.Size.Height,
                CssClass = n.CssClass
            }).ToList(),
            Edges = model.Edges.Select(e => new EdgeDto
            {
                From = e.FromNodeId,
                To = e.ToNodeId,
                Label = string.IsNullOrEmpty(e.Label) ? null : e.Label,
                Style = e.Style.ToString(),
                ArrowType = e.ArrowType.ToString(),
                IsBidirectional = e.IsBidirectional,
                LinkLength = e.LinkLength
            }).ToList(),
            Subgraphs = model.Subgraphs.Select(s => new SubgraphDto
            {
                Id = s.Id,
                Label = s.Label,
                NodeIds = s.NodeIds,
                Direction = s.Direction
            }).ToList(),
            Styles = model.Styles.Select(s => new StyleDto
            {
                IsClassDef = s.IsClassDef,
                Target = s.Target,
                StyleString = s.StyleString
            }).ToList(),
            Comments = model.Comments.Select(c => new CommentDto
            {
                Text = c.Text,
                OriginalLineIndex = c.OriginalLineIndex
            }).ToList(),
            ClassAssignments = model.ClassAssignments.Select(ca => new ClassAssignmentDto
            {
                NodeIds = ca.NodeIds,
                ClassName = ca.ClassName
            }).ToList(),
            DiagramKeyword = model.DiagramKeyword,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    // ========== WebView2 Message Handler ==========

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_isSuppressingUpdates) return;

        try
        {
            var json = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
                return;

            var messageType = typeProp.GetString();

            switch (messageType)
            {
                case "editorReady":
                    EditorReady?.Invoke(this, EventArgs.Empty);
                    break;

                case "nodeMoved":
                    HandleNodeMoved(root);
                    break;

                case "nodeEdited":
                    HandleNodeEdited(root);
                    break;

                case "nodeCreated":
                    HandleNodeCreated(root);
                    break;

                case "nodeDeleted":
                    HandleNodeDeleted(root);
                    break;

                case "edgeCreated":
                    HandleEdgeCreated(root);
                    break;

                case "edgeEdited":
                    HandleEdgeEdited(root);
                    break;

                case "edgeDeleted":
                    HandleEdgeDeleted(root);
                    break;

                case "nodeSelected":
                    HandleNodeSelected(root);
                    break;

                case "edgeSelected":
                    // Edge selection doesn't need model changes
                    break;

                case "selectionCleared":
                    SelectionCleared?.Invoke(this, EventArgs.Empty);
                    break;

                case "autoLayoutApplied":
                    // Legacy: individual nodeMoved messages (no longer used)
                    break;

                case "autoLayoutComplete":
                    HandleAutoLayoutComplete(root);
                    break;

                case "nodeShapeChanged":
                    HandleNodeShapeChanged(root);
                    break;

                case "nodeStyleChanged":
                    HandleNodeStyleChanged(root);
                    break;

                case "undo":
                    _ = UndoAsync();
                    break;

                case "redo":
                    _ = RedoAsync();
                    break;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed messages
        }
    }

    private void HandleNodeMoved(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        var x = root.GetProperty("x").GetDouble();
        var y = root.GetProperty("y").GetDouble();

        if (string.IsNullOrEmpty(nodeId)) return;

        var node = _model.Nodes.Find(n => n.Id == nodeId);
        if (node == null) return;

        PushUndo();
        node.Position = new System.Windows.Point(x, y);
        RaiseModelChanged("nodeMoved");
    }

    private void HandleNodeEdited(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        var label = root.GetProperty("label").GetString();

        if (string.IsNullOrEmpty(nodeId)) return;

        var node = _model.Nodes.Find(n => n.Id == nodeId);
        if (node == null) return;

        PushUndo();
        node.Label = label;

        if (root.TryGetProperty("width", out var wProp) && root.TryGetProperty("height", out var hProp))
        {
            node.Size = new System.Windows.Size(wProp.GetDouble(), hProp.GetDouble());
        }

        RaiseModelChanged("nodeEdited");
    }

    private void HandleNodeCreated(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        var label = root.GetProperty("label").GetString();
        var shape = root.GetProperty("shape").GetString();
        var x = root.GetProperty("x").GetDouble();
        var y = root.GetProperty("y").GetDouble();

        if (string.IsNullOrEmpty(nodeId)) return;

        PushUndo();

        var nodeShape = Enum.TryParse<NodeShape>(shape, out var parsed) ? parsed : NodeShape.Rectangle;

        var newNode = new FlowchartNode
        {
            Id = nodeId,
            Label = label,
            Shape = nodeShape,
            Position = new System.Windows.Point(x, y)
        };

        if (root.TryGetProperty("width", out var wProp) && root.TryGetProperty("height", out var hProp))
        {
            newNode.Size = new System.Windows.Size(wProp.GetDouble(), hProp.GetDouble());
        }

        _model.Nodes.Add(newNode);
        RaiseModelChanged("nodeCreated");
    }

    private void HandleNodeDeleted(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        if (string.IsNullOrEmpty(nodeId)) return;

        PushUndo();
        _model.Nodes.RemoveAll(n => n.Id == nodeId);
        _model.Edges.RemoveAll(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId);

        // Remove from subgraphs
        foreach (var sg in _model.Subgraphs)
        {
            sg.NodeIds.Remove(nodeId);
        }

        RaiseModelChanged("nodeDeleted");
    }

    private void HandleEdgeCreated(JsonElement root)
    {
        var from = root.GetProperty("from").GetString();
        var to = root.GetProperty("to").GetString();

        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;

        PushUndo();

        var style = EdgeStyle.Solid;
        if (root.TryGetProperty("style", out var styleProp))
        {
            Enum.TryParse<EdgeStyle>(styleProp.GetString(), out style);
        }

        var arrowType = ArrowType.Arrow;
        if (root.TryGetProperty("arrowType", out var arrowProp))
        {
            Enum.TryParse<ArrowType>(arrowProp.GetString(), out arrowType);
        }

        var label = root.TryGetProperty("label", out var labelProp) ? labelProp.GetString() : null;

        var newEdge = new FlowchartEdge
        {
            FromNodeId = from,
            ToNodeId = to,
            Label = string.IsNullOrEmpty(label) ? null : label,
            Style = style,
            ArrowType = arrowType
        };

        _model.Edges.Add(newEdge);
        RaiseModelChanged("edgeCreated");
    }

    private void HandleEdgeEdited(JsonElement root)
    {
        var edgeIndex = root.GetProperty("edgeIndex").GetInt32();
        var label = root.GetProperty("label").GetString();

        if (edgeIndex < 0 || edgeIndex >= _model.Edges.Count) return;

        PushUndo();
        _model.Edges[edgeIndex].Label = string.IsNullOrEmpty(label) ? null : label;
        RaiseModelChanged("edgeEdited");
    }

    private void HandleEdgeDeleted(JsonElement root)
    {
        var from = root.GetProperty("from").GetString();
        var to = root.GetProperty("to").GetString();

        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;

        PushUndo();
        _model.Edges.RemoveAll(e => e.FromNodeId == from && e.ToNodeId == to);
        RaiseModelChanged("edgeDeleted");
    }

    private void HandleNodeSelected(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        if (string.IsNullOrEmpty(nodeId)) return;

        NodeSelected?.Invoke(this, new NodeSelectedEventArgs(nodeId));
    }

    private void HandleNodeShapeChanged(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        var shape = root.GetProperty("shape").GetString();

        if (string.IsNullOrEmpty(nodeId)) return;

        var node = _model.Nodes.Find(n => n.Id == nodeId);
        if (node == null) return;

        PushUndo();
        node.Shape = Enum.TryParse<NodeShape>(shape, out var parsed) ? parsed : NodeShape.Rectangle;

        if (root.TryGetProperty("width", out var wProp) && root.TryGetProperty("height", out var hProp))
        {
            node.Size = new System.Windows.Size(wProp.GetDouble(), hProp.GetDouble());
        }

        RaiseModelChanged("nodeShapeChanged");
    }

    private void HandleNodeStyleChanged(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        if (string.IsNullOrEmpty(nodeId)) return;

        var node = _model.Nodes.Find(n => n.Id == nodeId);
        if (node == null) return;

        PushUndo();

        // Apply fill color as an inline style
        if (root.TryGetProperty("fillColor", out var fillProp))
        {
            var fillColor = fillProp.GetString();
            if (!string.IsNullOrEmpty(fillColor))
            {
                // Add or update a style definition for this node
                var existingStyle = _model.Styles.Find(s => !s.IsClassDef && s.Target == nodeId);
                var styleString = $"fill:{fillColor}";
                if (existingStyle != null)
                {
                    existingStyle.StyleString = styleString;
                }
                else
                {
                    _model.Styles.Add(new StyleDefinition
                    {
                        IsClassDef = false,
                        Target = nodeId,
                        StyleString = styleString
                    });
                }
            }
        }

        RaiseModelChanged("nodeStyleChanged");
    }

    private void HandleAutoLayoutComplete(JsonElement root)
    {
        if (!root.TryGetProperty("positions", out var positionsArray))
            return;

        PushUndo();

        foreach (var pos in positionsArray.EnumerateArray())
        {
            var nodeId = pos.GetProperty("nodeId").GetString();
            if (string.IsNullOrEmpty(nodeId)) continue;

            var node = _model.Nodes.Find(n => n.Id == nodeId);
            if (node == null) continue;

            node.Position = new System.Windows.Point(
                pos.GetProperty("x").GetDouble(),
                pos.GetProperty("y").GetDouble()
            );

            if (pos.TryGetProperty("width", out var wProp) && pos.TryGetProperty("height", out var hProp))
            {
                node.Size = new System.Windows.Size(wProp.GetDouble(), hProp.GetDouble());
            }
        }

        RaiseModelChanged("autoLayoutComplete");
    }

    // ========== Undo/Redo History ==========

    private void PushUndo()
    {
        var json = ConvertModelToJson(_model);
        _undoStack.Add(json);
        if (_undoStack.Count > MaxHistorySize)
            _undoStack.RemoveAt(0);

        // Clear redo stack on new action
        _redoStack.Clear();
    }

    private void RestoreModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<DiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _model.Direction = dto.Direction ?? "TD";
        _model.DiagramKeyword = dto.DiagramKeyword ?? "flowchart";
        _model.DeclarationLineIndex = dto.DeclarationLineIndex;
        _model.Nodes.Clear();
        _model.Edges.Clear();
        _model.Subgraphs.Clear();
        _model.Styles.Clear();
        _model.Comments.Clear();
        _model.ClassAssignments.Clear();

        if (dto.Nodes != null)
        {
            foreach (var n in dto.Nodes)
            {
                var shape = Enum.TryParse<NodeShape>(n.Shape, out var parsed) ? parsed : NodeShape.Rectangle;
                _model.Nodes.Add(new FlowchartNode
                {
                    Id = n.Id ?? string.Empty,
                    Label = n.Label,
                    Shape = shape,
                    Position = new System.Windows.Point(n.X, n.Y),
                    Size = new System.Windows.Size(n.Width, n.Height),
                    CssClass = n.CssClass
                });
            }
        }

        if (dto.Edges != null)
        {
            foreach (var e in dto.Edges)
            {
                var style = Enum.TryParse<EdgeStyle>(e.Style, out var sParsed) ? sParsed : EdgeStyle.Solid;
                var arrow = Enum.TryParse<ArrowType>(e.ArrowType, out var aParsed) ? aParsed : ArrowType.Arrow;
                _model.Edges.Add(new FlowchartEdge
                {
                    FromNodeId = e.From ?? string.Empty,
                    ToNodeId = e.To ?? string.Empty,
                    Label = e.Label,
                    Style = style,
                    ArrowType = arrow,
                    IsBidirectional = e.IsBidirectional,
                    LinkLength = e.LinkLength
                });
            }
        }

        if (dto.Subgraphs != null)
        {
            foreach (var s in dto.Subgraphs)
            {
                _model.Subgraphs.Add(new FlowchartSubgraph
                {
                    Id = s.Id ?? string.Empty,
                    Label = s.Label ?? string.Empty,
                    NodeIds = s.NodeIds ?? new List<string>(),
                    Direction = s.Direction
                });
            }
        }

        if (dto.Styles != null)
        {
            foreach (var s in dto.Styles)
            {
                _model.Styles.Add(new StyleDefinition
                {
                    IsClassDef = s.IsClassDef,
                    Target = s.Target ?? string.Empty,
                    StyleString = s.StyleString ?? string.Empty
                });
            }
        }

        if (dto.Comments != null)
        {
            foreach (var c in dto.Comments)
            {
                _model.Comments.Add(new CommentEntry
                {
                    Text = c.Text ?? string.Empty,
                    OriginalLineIndex = c.OriginalLineIndex
                });
            }
        }

        if (dto.ClassAssignments != null)
        {
            foreach (var ca in dto.ClassAssignments)
            {
                _model.ClassAssignments.Add(new ClassAssignment
                {
                    NodeIds = ca.NodeIds ?? string.Empty,
                    ClassName = ca.ClassName ?? string.Empty
                });
            }
        }
    }

    // ========== Event Raising ==========

    private void RaiseModelChanged(string changeType)
    {
        ModelChanged?.Invoke(this, new ModelChangedEventArgs(changeType, _model));
    }

    // ========== DTOs for JSON Serialization ==========

    private class DiagramDto
    {
        public string? Direction { get; set; }
        public List<NodeDto>? Nodes { get; set; }
        public List<EdgeDto>? Edges { get; set; }
        public List<SubgraphDto>? Subgraphs { get; set; }
        public List<StyleDto>? Styles { get; set; }
        public List<CommentDto>? Comments { get; set; }
        public List<ClassAssignmentDto>? ClassAssignments { get; set; }
        public string? DiagramKeyword { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class NodeDto
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public string? Shape { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string? CssClass { get; set; }
    }

    private class EdgeDto
    {
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Label { get; set; }
        public string? Style { get; set; }
        public string? ArrowType { get; set; }
        public bool IsBidirectional { get; set; }
        public int LinkLength { get; set; } = 2;
    }

    private class SubgraphDto
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public List<string>? NodeIds { get; set; }
        public string? Direction { get; set; }
    }

    private class StyleDto
    {
        public bool IsClassDef { get; set; }
        public string? Target { get; set; }
        public string? StyleString { get; set; }
    }

    private class CommentDto
    {
        public string? Text { get; set; }
        public int OriginalLineIndex { get; set; }
    }

    private class ClassAssignmentDto
    {
        public string? NodeIds { get; set; }
        public string? ClassName { get; set; }
    }
}

/// <summary>
/// Event args for when the FlowchartModel changes via the visual editor.
/// </summary>
public class ModelChangedEventArgs : EventArgs
{
    /// <summary>
    /// The type of change (e.g., "nodeMoved", "nodeEdited", "nodeCreated", "edgeCreated", etc.).
    /// </summary>
    public string ChangeType { get; }

    /// <summary>
    /// The updated FlowchartModel.
    /// </summary>
    public FlowchartModel Model { get; }

    public ModelChangedEventArgs(string changeType, FlowchartModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}

/// <summary>
/// Event args for when a node is selected in the visual editor.
/// </summary>
public class NodeSelectedEventArgs : EventArgs
{
    /// <summary>
    /// The ID of the selected node.
    /// </summary>
    public string NodeId { get; }

    public NodeSelectedEventArgs(string nodeId)
    {
        NodeId = nodeId;
    }
}
