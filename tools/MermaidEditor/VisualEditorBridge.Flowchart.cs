using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// Flowchart diagram handlers for the visual editor bridge.
/// Handles node/edge CRUD, subgraph operations, auto-layout, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== Flowchart Send/Restore/Update ==========

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
    /// Restores the flowchart diagram for undo/redo (preserves all positions).
    /// </summary>
    private async Task RestoreDiagramToEditorAsync()
    {
        var json = ConvertModelToJson(_model);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreDiagram({escaped})");
    }

    /// <summary>
    /// Updates the model reference (e.g., after text re-parse) and sends to editor.
    /// </summary>
    /// <param name="newModel">The new FlowchartModel from text parsing.</param>
    public async Task UpdateModelAsync(FlowchartModel newModel)
    {
        _model = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.Flowchart;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendDiagramToEditorAsync();
    }

    // ========== Flowchart JSON Conversion ==========

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
                CssClass = n.CssClass,
                HasManualPosition = n.HasManualPosition
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
            DeclarationLineIndex = model.DeclarationLineIndex,
            PreambleLines = model.PreambleLines
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    // ========== Flowchart Message Handlers ==========

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
        node.HasManualPosition = true;
        RaiseModelChanged("nodeMoved");
    }

    private void HandleNodeResized(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        var width = root.GetProperty("width").GetDouble();
        var height = root.GetProperty("height").GetDouble();

        if (string.IsNullOrEmpty(nodeId)) return;

        var node = _model.Nodes.Find(n => n.Id == nodeId);
        if (node == null) return;

        PushUndo();
        node.Size = new System.Windows.Size(width, height);
        RaiseModelChanged("nodeResized");
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

        // If a subgraphId was provided, add the node to that subgraph
        if (root.TryGetProperty("subgraphId", out var sgProp))
        {
            var subgraphId = sgProp.GetString();
            if (!string.IsNullOrEmpty(subgraphId))
            {
                var subgraph = _model.Subgraphs.Find(s => s.Id == subgraphId);
                if (subgraph != null && !subgraph.NodeIds.Contains(nodeId!))
                {
                    subgraph.NodeIds.Add(nodeId!);
                }
            }
        }

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

        // Support updating from/to (used by edge splitting when inserting a node on an edge)
        if (root.TryGetProperty("from", out var fromProp))
        {
            var from = fromProp.GetString();
            if (!string.IsNullOrEmpty(from))
                _model.Edges[edgeIndex].FromNodeId = from;
        }
        if (root.TryGetProperty("to", out var toProp))
        {
            var to = toProp.GetString();
            if (!string.IsNullOrEmpty(to))
                _model.Edges[edgeIndex].ToNodeId = to;
        }

        RaiseModelChanged("edgeEdited");
    }

    private void HandleEdgeDeleted(JsonElement root)
    {
        if (root.TryGetProperty("edgeIndex", out var idxProp))
        {
            var edgeIndex = idxProp.GetInt32();
            if (edgeIndex >= 0 && edgeIndex < _model.Edges.Count)
            {
                PushUndo();
                _model.Edges.RemoveAt(edgeIndex);
                RaiseModelChanged("edgeDeleted");
            }
        }
        else
        {
            // Fallback: remove by from/to (legacy)
            var from = root.GetProperty("from").GetString();
            var to = root.GetProperty("to").GetString();
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;

            PushUndo();
            _model.Edges.RemoveAll(e => e.FromNodeId == from && e.ToNodeId == to);
            RaiseModelChanged("edgeDeleted");
        }
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
        bool changed = false;

        // Apply fill color as an inline style
        if (root.TryGetProperty("fillColor", out var fillProp))
        {
            var fillColor = fillProp.GetString();
            if (!string.IsNullOrEmpty(fillColor))
            {
                // Add or update a style definition for this node, preserving other properties
                var existingStyle = _model.Styles.Find(s => !s.IsClassDef && s.Target == nodeId);
                if (existingStyle != null)
                {
                    // Preserve existing properties, only replace fill
                    var props = existingStyle.StyleString
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !p.StartsWith("fill:", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    props.Insert(0, $"fill:{fillColor}");
                    existingStyle.StyleString = string.Join(",", props);
                }
                else
                {
                    _model.Styles.Add(new StyleDefinition
                    {
                        IsClassDef = false,
                        Target = nodeId,
                        StyleString = $"fill:{fillColor}"
                    });
                }
                changed = true;
            }
        }

        if (changed)
        {
            RaiseModelChanged("nodeStyleChanged");
        }
        else
        {
            // No actual change — remove the undo snapshot we just pushed
            if (_undoStack.Count > 0) _undoStack.RemoveAt(_undoStack.Count - 1);
        }
    }

    private void HandleSubgraphEdited(JsonElement root)
    {
        var subgraphId = root.GetProperty("subgraphId").GetString();
        var label = root.GetProperty("label").GetString();

        if (string.IsNullOrEmpty(subgraphId)) return;

        var sg = _model.Subgraphs.Find(s => s.Id == subgraphId);
        if (sg == null) return;

        PushUndo();
        sg.Label = label ?? sg.Label;
        RaiseModelChanged("subgraphEdited");
    }

    private void HandleSubgraphDeleted(JsonElement root)
    {
        var subgraphId = root.GetProperty("subgraphId").GetString();
        if (string.IsNullOrEmpty(subgraphId)) return;

        var sg = _model.Subgraphs.Find(s => s.Id == subgraphId);
        if (sg == null) return;

        PushUndo();
        _model.Subgraphs.Remove(sg);

        // Remove any edges that reference the deleted subgraph as source or target
        _model.Edges.RemoveAll(e =>
            e.FromNodeId == subgraphId || e.ToNodeId == subgraphId);

        RaiseModelChanged("subgraphDeleted");
    }

    private void HandleNodeSubgraphChanged(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        if (string.IsNullOrEmpty(nodeId)) return;

        PushUndo();

        // Remove from all subgraphs first
        foreach (var sg in _model.Subgraphs)
        {
            sg.NodeIds.Remove(nodeId);
        }

        // Add to target subgraph if specified
        if (root.TryGetProperty("subgraphId", out var sgIdProp) && sgIdProp.ValueKind != JsonValueKind.Null)
        {
            var targetSgId = sgIdProp.GetString();
            if (!string.IsNullOrEmpty(targetSgId))
            {
                var targetSg = _model.Subgraphs.Find(s => s.Id == targetSgId);
                if (targetSg != null)
                {
                    targetSg.NodeIds.Add(nodeId);
                }
            }
        }

        RaiseModelChanged("nodeSubgraphChanged");
    }

    private void HandleSubgraphCreated(JsonElement root)
    {
        var subgraphId = root.GetProperty("subgraphId").GetString();
        var label = root.GetProperty("label").GetString();

        if (string.IsNullOrEmpty(subgraphId)) return;

        PushUndo();

        var nodeIds = new List<string>();
        if (root.TryGetProperty("nodeIds", out var nodeIdsArray))
        {
            foreach (var nid in nodeIdsArray.EnumerateArray())
            {
                var id = nid.GetString();
                if (!string.IsNullOrEmpty(id))
                {
                    // Remove from any existing subgraph
                    foreach (var existingSg in _model.Subgraphs)
                    {
                        existingSg.NodeIds.Remove(id);
                    }
                    nodeIds.Add(id);
                }
            }
        }

        _model.Subgraphs.Add(new FlowchartSubgraph
        {
            Id = subgraphId,
            Label = label ?? "New Subgraph",
            NodeIds = nodeIds
        });

        RaiseModelChanged("subgraphCreated");
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

    // ========== Flowchart Model Restore (for undo/redo) ==========

    private void RestoreModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<DiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _model.Direction = dto.Direction ?? "TD";
        _model.DiagramKeyword = dto.DiagramKeyword ?? "flowchart";
        _model.DeclarationLineIndex = dto.DeclarationLineIndex;
        _model.PreambleLines = dto.PreambleLines ?? new List<string>();
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
                    CssClass = n.CssClass,
                    HasManualPosition = n.HasManualPosition
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

    // ========== Flowchart Event Raising ==========

    private void RaiseModelChanged(string changeType)
    {
        ModelChanged?.Invoke(this, new ModelChangedEventArgs(changeType, _model));
    }

    // ========== Flowchart DTOs ==========

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
        public List<string>? PreambleLines { get; set; }
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
        public bool HasManualPosition { get; set; }
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
