using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// Mind map handlers for the visual editor bridge.
/// Handles node CRUD, tree operations, and model restore.
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
    /// Sends the current MindMapModel to the visual editor as JSON.
    /// </summary>
    public async Task SendMindMapToEditorAsync()
    {
        if (_mindMapModel == null) return;
        var json = ConvertMindMapModelToJson(_mindMapModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadMindMap({escaped})");
    }

    /// <summary>
    /// Restores the mind map for undo/redo.
    /// </summary>
    private async Task RestoreMindMapToEditorAsync()
    {
        if (_mindMapModel == null) return;
        var json = ConvertMindMapModelToJson(_mindMapModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreMindMap({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current mind map model.
    /// </summary>
    public async Task RefreshMindMapAsync()
    {
        if (_mindMapModel == null) return;
        var json = ConvertMindMapModelToJson(_mindMapModel);
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

    /// <summary>
    /// Converts a MindMapModel to the JSON format expected by the visual editor JS.
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

    // ========== Mind Map Message Handlers ==========

    private void HandleMindMapNodeCreated(JsonElement root)
    {
        if (_mindMapModel == null) return;
        var label = root.GetProperty("label").GetString();
        if (string.IsNullOrEmpty(label)) return;

        var parentPath = root.TryGetProperty("parentPath", out var pp)
            ? DeserializeIntArray(pp) : null;

        PushUndo();
        var newNode = new MindMapNode { Label = label };

        if (root.TryGetProperty("shape", out var shapeProp))
        {
            if (Enum.TryParse<MindMapNodeShape>(shapeProp.GetString(), true, out var shape))
                newNode.Shape = shape;
        }

        var parent = parentPath != null ? FindMindMapNode(parentPath) : _mindMapModel.Root;
        parent?.Children.Add(newNode);

        RaiseMindMapModelChanged("mm_nodeCreated");
    }

    private void HandleMindMapNodeEdited(JsonElement root)
    {
        if (_mindMapModel == null) return;
        var path = root.TryGetProperty("path", out var pp) ? DeserializeIntArray(pp) : null;
        if (path == null) return;

        var node = FindMindMapNode(path);
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
        var path = root.TryGetProperty("path", out var pp) ? DeserializeIntArray(pp) : null;
        if (path == null || path.Length == 0) return; // Cannot delete root

        PushUndo();
        // Navigate to parent and remove the child
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

        if (!string.IsNullOrEmpty(dto.Shape))
            Enum.TryParse(dto.Shape, true, out MindMapNodeShape shape);
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

    private void RaiseMindMapModelChanged(string changeType)
    {
        if (_mindMapModel != null)
            MindMapModelChanged?.Invoke(this, new MindMapModelChangedEventArgs(changeType, _mindMapModel));
    }

    // ========== Mind Map DTOs ==========

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
