using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// Mind map handlers for the visual editor bridge.
/// Handles node CRUD, tree operations, and model restore.
/// The mind map is rendered using the flowchart infrastructure (nodes + edges + panzoom).
/// The C# bridge converts the tree model to flowchart-compatible JSON for the JS editor.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== Mind Map Support ==========

    private MindMapModel? _mindMapModel;

    /// <summary>
    /// Gets the current MindMapModel (may be null if not in mind map mode).
    /// </summary>
    public MindMapModel? MindMapModel => _mindMapModel;

    /// <summary>
    /// Raised when the MindMapModel is modified by the visual editor.
    /// </summary>
    public event EventHandler<MindMapModelChangedEventArgs>? MindMapModelChanged;

    /// <summary>
    /// Sends the current MindMapModel to the visual editor as flowchart-compatible JSON.
    /// </summary>
    public async Task SendMindMapToEditorAsync()
    {
        if (_mindMapModel == null) return;
        var json = ConvertMindMapToFlowchartJson(_mindMapModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadMindMap({escaped})");
    }

    /// <summary>
    /// Restores the mind map for undo/redo.
    /// </summary>
    private async Task RestoreMindMapToEditorAsync()
    {
        if (_mindMapModel == null) return;
        var json = ConvertMindMapToFlowchartJson(_mindMapModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreMindMap({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current mind map model.
    /// </summary>
    public async Task RefreshMindMapAsync()
    {
        if (_mindMapModel == null) return;
        var json = ConvertMindMapToFlowchartJson(_mindMapModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshMindMap({escaped})");
    }

    /// <summary>
    /// Updates the mind map model reference and sends to editor.
    /// </summary>
    public async Task UpdateMindMapModelAsync(MindMapModel newModel)
    {
        _mindMapModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.MindMap;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendMindMapToEditorAsync();
    }

    // ========== Flowchart-compatible JSON Conversion ==========

    /// <summary>
    /// Converts a MindMapModel to flowchart-compatible JSON (nodes + edges).
    /// Each tree node gets an ID encoding its tree path:
    /// - Root: "mm_root"
    /// - Root's 1st child: "mm_0"
    /// - Root's 2nd child's 3rd child: "mm_1_2"
    /// This allows the JS to use the full flowchart rendering infrastructure.
    /// </summary>
    public static string ConvertMindMapToFlowchartJson(MindMapModel model)
    {
        var nodes = new List<MindMapFlowchartNodeDto>();
        var edges = new List<MindMapFlowchartEdgeDto>();

        FlattenMindMapTree(model.Root, "mm_root", 0, nodes, edges);

        // Apply saved positions from the model
        if (model.HasPositionData && model.NodePositions.Count > 0)
        {
            foreach (var node in nodes)
            {
                if (model.NodePositions.TryGetValue(node.Id, out var pos))
                {
                    node.X = pos.X;
                    node.Y = pos.Y;
                }
            }
        }

        var dto = new MindMapFlowchartDto
        {
            Direction = "LR",
            Nodes = nodes,
            Edges = edges,
            Subgraphs = new List<object>(),
            Styles = new List<object>(),
            IsMindMap = true,
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    /// Converts a MindMapModel to the original tree JSON format (for undo/redo serialization).
    /// </summary>
    public static string ConvertMindMapModelToJson(MindMapModel model)
    {
        var dto = new MindMapDiagramDto
        {
            Root = ConvertMindMapNodeToDto(model.Root),
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static void FlattenMindMapTree(
        MindMapNode node, string nodeId, int level,
        List<MindMapFlowchartNodeDto> nodes, List<MindMapFlowchartEdgeDto> edges)
    {
        var shape = MapMindMapShape(node.Shape, level);

        nodes.Add(new MindMapFlowchartNodeDto
        {
            Id = nodeId,
            Label = node.Label,
            Shape = shape,
            X = 0,
            Y = 0,
            Width = 0,
            Height = 0,
            CssClass = node.CssClass,
            MindMapLevel = level,
            MindMapShape = node.Shape.ToString()
        });

        for (int i = 0; i < node.Children.Count; i++)
        {
            var childId = nodeId == "mm_root" ? $"mm_{i}" : $"{nodeId}_{i}";
            var child = node.Children[i];

            edges.Add(new MindMapFlowchartEdgeDto
            {
                From = nodeId,
                To = childId,
                Label = null,
                Style = "Solid",
                ArrowType = "None"
            });

            FlattenMindMapTree(child, childId, level + 1, nodes, edges);
        }
    }

    /// <summary>
    /// Maps a MindMapNodeShape to a flowchart shape string.
    /// </summary>
    private static string MapMindMapShape(MindMapNodeShape shape, int level)
    {
        return shape switch
        {
            MindMapNodeShape.Square => "Rectangle",
            MindMapNodeShape.Rounded => "Rounded",
            MindMapNodeShape.Circle => "Circle",
            MindMapNodeShape.Hexagon => "Hexagon",
            MindMapNodeShape.Bang => "Asymmetric",
            MindMapNodeShape.Cloud => "Stadium",
            _ => level == 0 ? "Stadium" : "Rounded"
        };
    }

    private static MindMapNodeDto ConvertMindMapNodeToDto(MindMapNode node)
    {
        return new MindMapNodeDto
        {
            Id = node.Id,
            Label = node.Label,
            Shape = node.Shape.ToString(),
            Icon = node.Icon,
            CssClass = node.CssClass,
            Children = node.Children.Select(ConvertMindMapNodeToDto).ToList()
        };
    }

    // ========== Node ID <-> Tree Path Conversion ==========

    /// <summary>
    /// Converts a flowchart node ID (e.g., "mm_1_2") to a tree path (e.g., [1, 2]).
    /// "mm_root" returns an empty array (root node).
    /// </summary>
    private static int[] NodeIdToPath(string nodeId)
    {
        if (nodeId == "mm_root") return Array.Empty<int>();
        var parts = nodeId.Substring(3).Split('_');
        return parts.Select(int.Parse).ToArray();
    }

    // ========== Mind Map Message Handlers ==========

    private void HandleMindMapNodeCreated(JsonElement root)
    {
        if (_mindMapModel == null) return;
        var label = root.GetProperty("label").GetString();
        if (string.IsNullOrEmpty(label)) return;

        // The JS sends parentId (flowchart node ID) instead of parentPath
        var parentId = root.TryGetProperty("parentId", out var pidProp)
            ? pidProp.GetString() : "mm_root";
        var parentPath = NodeIdToPath(parentId ?? "mm_root");

        PushUndo();
        var newNode = new MindMapNode { Label = label };

        if (root.TryGetProperty("shape", out var shapeProp))
        {
            if (Enum.TryParse<MindMapNodeShape>(shapeProp.GetString(), true, out var shape))
                newNode.Shape = shape;
        }

        var parent = parentPath.Length == 0 ? _mindMapModel.Root : FindMindMapNode(parentPath);
        parent?.Children.Add(newNode);

        RaiseMindMapModelChanged("mm_nodeCreated");
    }

    private void HandleMindMapNodeEdited(JsonElement root)
    {
        if (_mindMapModel == null) return;

        // The JS sends nodeId (flowchart node ID) instead of path
        var nodeId = root.TryGetProperty("nodeId", out var nidProp)
            ? nidProp.GetString() : null;
        if (nodeId == null) return;
        var path = NodeIdToPath(nodeId);

        var node = path.Length == 0 ? _mindMapModel.Root : FindMindMapNode(path);
        if (node == null) return;

        PushUndo();
        if (root.TryGetProperty("label", out var lProp))
            node.Label = lProp.GetString() ?? node.Label;
        if (root.TryGetProperty("shape", out var shapeProp))
        {
            if (Enum.TryParse<MindMapNodeShape>(shapeProp.GetString(), true, out var shape))
                node.Shape = shape;
        }
        if (root.TryGetProperty("icon", out var iconProp))
            node.Icon = iconProp.GetString();
        if (root.TryGetProperty("cssClass", out var cssProp))
            node.CssClass = cssProp.GetString();

        RaiseMindMapModelChanged("mm_nodeEdited");
    }

    private void HandleMindMapNodeDeleted(JsonElement root)
    {
        if (_mindMapModel == null) return;

        // The JS sends nodeId (flowchart node ID) instead of path
        var nodeId = root.TryGetProperty("nodeId", out var nidProp)
            ? nidProp.GetString() : null;
        if (nodeId == null || nodeId == "mm_root") return; // Cannot delete root

        var path = NodeIdToPath(nodeId);
        if (path.Length == 0) return; // Cannot delete root

        PushUndo();
        var parentPath = path[..^1];
        var childIndex = path[^1];

        var parent = parentPath.Length == 0 ? _mindMapModel.Root : FindMindMapNode(parentPath);
        if (parent != null && childIndex >= 0 && childIndex < parent.Children.Count)
        {
            parent.Children.RemoveAt(childIndex);
        }

        RaiseMindMapModelChanged("mm_nodeDeleted");
    }

    /// <summary>
    /// Finds a mind map node by path (array of child indices from root).
    /// Empty path returns root.
    /// </summary>
    private MindMapNode? FindMindMapNode(int[] path)
    {
        if (_mindMapModel == null) return null;
        var current = _mindMapModel.Root;
        foreach (var idx in path)
        {
            if (idx < 0 || idx >= current.Children.Count) return null;
            current = current.Children[idx];
        }
        return current;
    }

    private static int[] DeserializeIntArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array) return Array.Empty<int>();
        return element.EnumerateArray().Select(e => e.GetInt32()).ToArray();
    }

    private void RestoreMindMapModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<MindMapDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _mindMapModel = new MindMapModel
        {
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        if (dto.Root != null)
        {
            _mindMapModel.Root = ConvertDtoToMindMapNode(dto.Root);
        }
    }

    private static MindMapNode ConvertDtoToMindMapNode(MindMapNodeDto dto)
    {
        var node = new MindMapNode
        {
            Id = dto.Id,
            Label = dto.Label ?? string.Empty,
            Icon = dto.Icon,
            CssClass = dto.CssClass
        };

        if (!string.IsNullOrEmpty(dto.Shape) && Enum.TryParse<MindMapNodeShape>(dto.Shape, true, out var s))
            node.Shape = s;

        if (dto.Children != null)
        {
            foreach (var childDto in dto.Children)
            {
                node.Children.Add(ConvertDtoToMindMapNode(childDto));
            }
        }

        return node;
    }

    // ========== Mind Map Position Handlers ==========

    private void HandleMindMapNodeMoved(JsonElement root)
    {
        if (_mindMapModel == null) return;

        var nodeId = root.GetProperty("nodeId").GetString();
        var x = root.GetProperty("x").GetDouble();
        var y = root.GetProperty("y").GetDouble();

        if (string.IsNullOrEmpty(nodeId)) return;

        PushUndo();
        _mindMapModel.NodePositions[nodeId] = new System.Windows.Point(x, y);
        _mindMapModel.HasPositionData = true;
        RaiseMindMapModelChanged("mm_nodeMoved");
    }

    private void HandleMindMapAutoLayoutComplete(JsonElement root)
    {
        if (_mindMapModel == null) return;

        if (!root.TryGetProperty("positions", out var positionsArray))
            return;

        PushUndo();

        _mindMapModel.NodePositions.Clear();
        foreach (var pos in positionsArray.EnumerateArray())
        {
            var nodeId = pos.GetProperty("nodeId").GetString();
            if (string.IsNullOrEmpty(nodeId)) continue;

            var x = pos.GetProperty("x").GetDouble();
            var y = pos.GetProperty("y").GetDouble();
            _mindMapModel.NodePositions[nodeId] = new System.Windows.Point(x, y);
        }

        _mindMapModel.HasPositionData = _mindMapModel.NodePositions.Count > 0;
        RaiseMindMapModelChanged("mm_autoLayoutComplete");
    }

    private void RaiseMindMapModelChanged(string changeType)
    {
        if (_mindMapModel != null)
            MindMapModelChanged?.Invoke(this, new MindMapModelChangedEventArgs(changeType, _mindMapModel));
    }

    // ========== Mind Map DTOs ==========

    /// <summary>Tree-format DTO for undo/redo serialization.</summary>
    private class MindMapDiagramDto
    {
        public MindMapNodeDto? Root { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class MindMapNodeDto
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public string? Shape { get; set; }
        public string? Icon { get; set; }
        public string? CssClass { get; set; }
        public List<MindMapNodeDto>? Children { get; set; }
    }

    /// <summary>Flowchart-compatible DTO sent to JS for rendering.</summary>
    private class MindMapFlowchartDto
    {
        public string Direction { get; set; } = "LR";
        public List<MindMapFlowchartNodeDto> Nodes { get; set; } = new();
        public List<MindMapFlowchartEdgeDto> Edges { get; set; } = new();
        public List<object> Subgraphs { get; set; } = new();
        public List<object> Styles { get; set; } = new();
        public bool IsMindMap { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class MindMapFlowchartNodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Shape { get; set; } = "Rounded";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string? CssClass { get; set; }
        /// <summary>Tree level (0 = root) for styling purposes.</summary>
        public int MindMapLevel { get; set; }
        /// <summary>Original MindMapNodeShape name for the property panel.</summary>
        public string? MindMapShape { get; set; }
    }

    private class MindMapFlowchartEdgeDto
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string? Label { get; set; }
        public string Style { get; set; } = "Solid";
        public string ArrowType { get; set; } = "None";
    }
}

/// <summary>
/// Event args for when the MindMapModel changes via the visual editor.
/// </summary>
public class MindMapModelChangedEventArgs : EventArgs
{
    public string ChangeType { get; }
    public MindMapModel Model { get; }

    public MindMapModelChangedEventArgs(string changeType, MindMapModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}
