using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// Block Diagram handlers for the visual editor bridge.
/// Handles block CRUD, edge CRUD, group management, settings changes, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== Block Diagram Support ==========

    private BlockDiagramModel? _blockDiagramModel;

    /// <summary>
    /// Gets the current BlockDiagramModel (may be null if not in block diagram mode).
    /// </summary>
    public BlockDiagramModel? BlockDiagramModel => _blockDiagramModel;

    /// <summary>
    /// Raised when the BlockDiagramModel is modified by the visual editor.
    /// </summary>
    public event EventHandler<BlockDiagramModelChangedEventArgs>? BlockDiagramModelChanged;

    /// <summary>
    /// Sends the current BlockDiagramModel to the visual editor as JSON.
    /// </summary>
    public async Task SendBlockDiagramToEditorAsync()
    {
        if (_blockDiagramModel == null) return;
        var json = ConvertBlockDiagramModelToJson(_blockDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadBlockDiagram({escaped})");
    }

    /// <summary>
    /// Restores the block diagram for undo/redo.
    /// </summary>
    private async Task RestoreBlockDiagramToEditorAsync()
    {
        if (_blockDiagramModel == null) return;
        var json = ConvertBlockDiagramModelToJson(_blockDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreBlockDiagram({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current block diagram model.
    /// </summary>
    public async Task RefreshBlockDiagramAsync()
    {
        if (_blockDiagramModel == null) return;
        var json = ConvertBlockDiagramModelToJson(_blockDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshBlockDiagram({escaped})");
    }

    /// <summary>
    /// Updates the block diagram model reference and sends to editor.
    /// </summary>
    public async Task UpdateBlockDiagramModelAsync(BlockDiagramModel newModel)
    {
        _blockDiagramModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.Block;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendBlockDiagramToEditorAsync();
    }

    /// <summary>
    /// Converts a BlockDiagramModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertBlockDiagramModelToJson(BlockDiagramModel model)
    {
        var dto = new BlockDiagramDto
        {
            Columns = model.Columns,
            ColumnsExplicit = model.ColumnsExplicit,
            Items = ConvertBlockItems(model.Items),
            Edges = model.Edges.Select(e => new BlockDiagramEdgeDto
            {
                FromId = e.FromId,
                ToId = e.ToId,
                Label = e.Label,
                Style = e.Style
            }).ToList(),
            StyleDefs = model.StyleDefs.Select(s => new BlockDiagramStyleDefDto
            {
                Name = s.Name,
                Styles = s.Styles
            }).ToList(),
            ClassAssignments = model.ClassAssignments.Select(c => new BlockDiagramClassAssignmentDto
            {
                Ids = c.Ids,
                ClassName = c.ClassName
            }).ToList(),
            InlineStyles = model.InlineStyles.Select(s => new BlockDiagramInlineStyleDto
            {
                Id = s.Id,
                Styles = s.Styles
            }).ToList(),
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static List<BlockDiagramItemDto> ConvertBlockItems(List<BlockDiagramItem> items)
    {
        var result = new List<BlockDiagramItemDto>();
        foreach (var item in items)
        {
            switch (item)
            {
                case BlockDiagramBlock block:
                    result.Add(new BlockDiagramItemDto
                    {
                        Type = "block",
                        Id = block.Id,
                        Label = block.Label,
                        Shape = block.Shape.ToString(),
                        Width = block.Width
                    });
                    break;
                case BlockDiagramSpace space:
                    result.Add(new BlockDiagramItemDto
                    {
                        Type = "space",
                        Width = space.Width
                    });
                    break;
                case BlockDiagramArrow arrow:
                    result.Add(new BlockDiagramItemDto
                    {
                        Type = "arrow",
                        Id = arrow.Id,
                        Label = arrow.Label,
                        Direction = arrow.Direction,
                        Width = arrow.Width
                    });
                    break;
                case BlockDiagramGroup group:
                    result.Add(new BlockDiagramItemDto
                    {
                        Type = "group",
                        Id = group.Id,
                        Label = group.Label,
                        Columns = group.Columns,
                        ColumnsExplicit = group.ColumnsExplicit,
                        Width = group.Width,
                        Items = ConvertBlockItems(group.Items)
                    });
                    break;
            }
        }
        return result;
    }

    // ========== Block Diagram Message Handlers ==========

    private void HandleBlockDiagramBlockCreated(JsonElement root)
    {
        if (_blockDiagramModel == null) return;

        PushUndo();

        var id = root.GetProperty("id").GetString() ?? $"block{_blockDiagramModel.Items.Count + 1}";
        var label = root.TryGetProperty("label", out var lProp) ? lProp.GetString() : null;
        var shapeStr = root.TryGetProperty("shape", out var sProp) ? sProp.GetString() ?? "Rectangle" : "Rectangle";
        var width = root.TryGetProperty("width", out var wProp) ? wProp.GetInt32() : 1;
        var groupId = root.TryGetProperty("groupId", out var gProp) ? gProp.GetString() : null;

        if (!Enum.TryParse<BlockShape>(shapeStr, true, out var shape))
            shape = BlockShape.Rectangle;

        var block = new BlockDiagramBlock { Id = id, Label = label, Shape = shape, Width = width };

        var targetList = FindItemList(groupId);
        var index = root.TryGetProperty("index", out var iProp) ? iProp.GetInt32() : -1;
        if (index >= 0 && index <= targetList.Count)
            targetList.Insert(index, block);
        else
            targetList.Add(block);

        RaiseBlockDiagramModelChanged("bd_blockCreated");
    }

    private void HandleBlockDiagramBlockEdited(JsonElement root)
    {
        if (_blockDiagramModel == null) return;

        var id = root.GetProperty("id").GetString();
        if (id == null) return;

        var item = FindBlockById(id);
        if (item == null) return;

        PushUndo();

        if (item is BlockDiagramBlock block)
        {
            if (root.TryGetProperty("label", out var lProp))
                block.Label = lProp.ValueKind == JsonValueKind.Null ? null : lProp.GetString();
            if (root.TryGetProperty("shape", out var sProp))
            {
                var shapeStr = sProp.GetString() ?? "Rectangle";
                if (Enum.TryParse<BlockShape>(shapeStr, true, out var shape))
                    block.Shape = shape;
            }
            if (root.TryGetProperty("width", out var wProp))
                block.Width = wProp.GetInt32();
            if (root.TryGetProperty("newId", out var nProp))
            {
                var newId = nProp.GetString();
                if (!string.IsNullOrEmpty(newId) && newId != block.Id)
                {
                    UpdateBlockReferences(block.Id, newId);
                    block.Id = newId;
                }
            }
        }
        else if (item is BlockDiagramArrow arrow)
        {
            if (root.TryGetProperty("label", out var lProp))
                arrow.Label = lProp.ValueKind == JsonValueKind.Null ? null : lProp.GetString();
            if (root.TryGetProperty("direction", out var dProp))
                arrow.Direction = dProp.GetString() ?? arrow.Direction;
            if (root.TryGetProperty("width", out var wProp))
                arrow.Width = wProp.GetInt32();
            if (root.TryGetProperty("newId", out var nProp))
            {
                var newId = nProp.GetString();
                if (!string.IsNullOrEmpty(newId) && newId != arrow.Id)
                {
                    UpdateBlockReferences(arrow.Id, newId);
                    arrow.Id = newId;
                }
            }
        }
        else if (item is BlockDiagramGroup group)
        {
            if (root.TryGetProperty("label", out var lProp))
                group.Label = lProp.ValueKind == JsonValueKind.Null ? null : lProp.GetString();
            if (root.TryGetProperty("columns", out var cProp))
            {
                group.Columns = cProp.GetInt32();
                group.ColumnsExplicit = true;
            }
            if (root.TryGetProperty("width", out var wProp))
                group.Width = wProp.GetInt32();
            if (root.TryGetProperty("newId", out var nProp))
            {
                var newId = nProp.GetString();
                if (!string.IsNullOrEmpty(newId) && newId != group.Id)
                {
                    UpdateBlockReferences(group.Id, newId);
                    group.Id = newId;
                }
            }
        }

        RaiseBlockDiagramModelChanged("bd_blockEdited");
    }

    private void HandleBlockDiagramBlockDeleted(JsonElement root)
    {
        if (_blockDiagramModel == null) return;

        var id = root.GetProperty("id").GetString();
        if (id == null) return;

        PushUndo();

        // Space elements have no ID — JS sends "__space__" sentinel with groupId + index
        if (id == "__space__")
        {
            var groupId = root.TryGetProperty("groupId", out var gProp) ? gProp.GetString() : null;
            var index = root.TryGetProperty("index", out var iProp) ? iProp.GetInt32() : -1;
            var targetList = FindItemList(groupId);
            if (index >= 0 && index < targetList.Count && targetList[index] is BlockDiagramSpace)
            {
                targetList.RemoveAt(index);
            }
        }
        else
        {
            RemoveItemById(id, _blockDiagramModel.Items);
            // Also remove any edges referencing this block
            _blockDiagramModel.Edges.RemoveAll(e => e.FromId == id || e.ToId == id);
        }

        RaiseBlockDiagramModelChanged("bd_blockDeleted");
    }

    private void HandleBlockDiagramBlockMoved(JsonElement root)
    {
        if (_blockDiagramModel == null) return;

        var id = root.GetProperty("id").GetString();
        if (id == null) return;

        var toGroupId = root.TryGetProperty("toGroupId", out var gProp) ? gProp.GetString() : null;
        var toIndex = root.TryGetProperty("toIndex", out var iProp) ? iProp.GetInt32() : -1;

        PushUndo();

        // Remove from current location
        var item = FindAndRemoveItemById(id, _blockDiagramModel.Items);
        if (item == null) return;

        // Insert at new location
        var targetList = FindItemList(toGroupId);
        if (toIndex >= 0 && toIndex <= targetList.Count)
            targetList.Insert(toIndex, item);
        else
            targetList.Add(item);

        RaiseBlockDiagramModelChanged("bd_blockMoved");
    }

    private void HandleBlockDiagramEdgeCreated(JsonElement root)
    {
        if (_blockDiagramModel == null) return;

        PushUndo();

        var edge = new BlockDiagramEdge
        {
            FromId = root.GetProperty("fromId").GetString() ?? string.Empty,
            ToId = root.GetProperty("toId").GetString() ?? string.Empty,
            Label = root.TryGetProperty("label", out var lProp) ? lProp.GetString() : null,
            Style = root.TryGetProperty("style", out var sProp) ? sProp.GetString() ?? "-->" : "-->"
        };

        var index = root.TryGetProperty("index", out var iProp) ? iProp.GetInt32() : -1;
        if (index >= 0 && index <= _blockDiagramModel.Edges.Count)
            _blockDiagramModel.Edges.Insert(index, edge);
        else
            _blockDiagramModel.Edges.Add(edge);

        RaiseBlockDiagramModelChanged("bd_edgeCreated");
    }

    private void HandleBlockDiagramEdgeEdited(JsonElement root)
    {
        if (_blockDiagramModel == null) return;

        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _blockDiagramModel.Edges.Count) return;

        PushUndo();
        var edge = _blockDiagramModel.Edges[index];

        if (root.TryGetProperty("fromId", out var fProp))
            edge.FromId = fProp.GetString() ?? edge.FromId;
        if (root.TryGetProperty("toId", out var tProp))
            edge.ToId = tProp.GetString() ?? edge.ToId;
        if (root.TryGetProperty("label", out var lProp))
            edge.Label = lProp.ValueKind == JsonValueKind.Null ? null : lProp.GetString();
        if (root.TryGetProperty("style", out var sProp))
            edge.Style = sProp.GetString() ?? edge.Style;

        RaiseBlockDiagramModelChanged("bd_edgeEdited");
    }

    private void HandleBlockDiagramEdgeDeleted(JsonElement root)
    {
        if (_blockDiagramModel == null) return;

        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _blockDiagramModel.Edges.Count) return;

        PushUndo();
        _blockDiagramModel.Edges.RemoveAt(index);
        RaiseBlockDiagramModelChanged("bd_edgeDeleted");
    }

    private void HandleBlockDiagramSpaceCreated(JsonElement root)
    {
        if (_blockDiagramModel == null) return;

        PushUndo();

        var width = root.TryGetProperty("width", out var wProp) ? wProp.GetInt32() : 1;
        var groupId = root.TryGetProperty("groupId", out var gProp) ? gProp.GetString() : null;
        var space = new BlockDiagramSpace { Width = width };

        var targetList = FindItemList(groupId);
        var index = root.TryGetProperty("index", out var iProp) ? iProp.GetInt32() : -1;
        if (index >= 0 && index <= targetList.Count)
            targetList.Insert(index, space);
        else
            targetList.Add(space);

        RaiseBlockDiagramModelChanged("bd_spaceCreated");
    }

    private void HandleBlockDiagramArrowCreated(JsonElement root)
    {
        if (_blockDiagramModel == null) return;

        PushUndo();

        var id = root.GetProperty("id").GetString() ?? $"arrow{_blockDiagramModel.Items.Count + 1}";
        var label = root.TryGetProperty("label", out var lProp) ? lProp.GetString() : null;
        var direction = root.TryGetProperty("direction", out var dProp) ? dProp.GetString() ?? "down" : "down";
        var width = root.TryGetProperty("width", out var wProp) ? wProp.GetInt32() : 1;
        var groupId = root.TryGetProperty("groupId", out var gProp) ? gProp.GetString() : null;

        var arrow = new BlockDiagramArrow { Id = id, Label = label, Direction = direction, Width = width };

        var targetList = FindItemList(groupId);
        var index = root.TryGetProperty("index", out var iProp) ? iProp.GetInt32() : -1;
        if (index >= 0 && index <= targetList.Count)
            targetList.Insert(index, arrow);
        else
            targetList.Add(arrow);

        RaiseBlockDiagramModelChanged("bd_arrowCreated");
    }

    private void HandleBlockDiagramGroupCreated(JsonElement root)
    {
        if (_blockDiagramModel == null) return;

        PushUndo();

        var id = root.GetProperty("id").GetString() ?? $"group{_blockDiagramModel.Items.Count + 1}";
        var label = root.TryGetProperty("label", out var lProp) ? lProp.GetString() : null;
        var columns = root.TryGetProperty("columns", out var cProp) ? cProp.GetInt32() : 1;
        var width = root.TryGetProperty("width", out var wProp) ? wProp.GetInt32() : 1;
        var parentGroupId = root.TryGetProperty("groupId", out var gProp) ? gProp.GetString() : null;

        var group = new BlockDiagramGroup
        {
            Id = id,
            Label = label,
            Columns = columns,
            ColumnsExplicit = columns > 1,
            Width = width
        };

        var targetList = FindItemList(parentGroupId);
        var index = root.TryGetProperty("index", out var iProp) ? iProp.GetInt32() : -1;
        if (index >= 0 && index <= targetList.Count)
            targetList.Insert(index, group);
        else
            targetList.Add(group);

        RaiseBlockDiagramModelChanged("bd_groupCreated");
    }

    private void HandleBlockDiagramSettingsChanged(JsonElement root)
    {
        if (_blockDiagramModel == null) return;

        PushUndo();

        if (root.TryGetProperty("columns", out var cProp))
        {
            _blockDiagramModel.Columns = cProp.GetInt32();
            _blockDiagramModel.ColumnsExplicit = true;
        }

        // Handle style defs
        if (root.TryGetProperty("styleDefs", out var sdProp) && sdProp.ValueKind == JsonValueKind.Array)
        {
            _blockDiagramModel.StyleDefs.Clear();
            foreach (var sd in sdProp.EnumerateArray())
            {
                _blockDiagramModel.StyleDefs.Add(new BlockDiagramStyleDef
                {
                    Name = sd.GetProperty("name").GetString() ?? string.Empty,
                    Styles = sd.GetProperty("styles").GetString() ?? string.Empty
                });
            }
        }

        // Handle class assignments
        if (root.TryGetProperty("classAssignments", out var caProp) && caProp.ValueKind == JsonValueKind.Array)
        {
            _blockDiagramModel.ClassAssignments.Clear();
            foreach (var ca in caProp.EnumerateArray())
            {
                _blockDiagramModel.ClassAssignments.Add(new BlockDiagramClassAssignment
                {
                    Ids = ca.GetProperty("ids").GetString() ?? string.Empty,
                    ClassName = ca.GetProperty("className").GetString() ?? string.Empty
                });
            }
        }

        // Handle inline styles
        if (root.TryGetProperty("inlineStyles", out var isProp) && isProp.ValueKind == JsonValueKind.Array)
        {
            _blockDiagramModel.InlineStyles.Clear();
            foreach (var s in isProp.EnumerateArray())
            {
                _blockDiagramModel.InlineStyles.Add(new BlockDiagramInlineStyle
                {
                    Id = s.GetProperty("id").GetString() ?? string.Empty,
                    Styles = s.GetProperty("styles").GetString() ?? string.Empty
                });
            }
        }

        RaiseBlockDiagramModelChanged("bd_settingsChanged");
    }

    private void HandleBlockDiagramSpaceEdited(JsonElement root)
    {
        if (_blockDiagramModel == null) return;

        var index = root.GetProperty("index").GetInt32();
        var groupId = root.TryGetProperty("groupId", out var gProp) ? gProp.GetString() : null;

        var targetList = FindItemList(groupId);
        if (index < 0 || index >= targetList.Count) return;
        if (targetList[index] is not BlockDiagramSpace space) return;

        PushUndo();
        if (root.TryGetProperty("width", out var wProp))
            space.Width = wProp.GetInt32();

        RaiseBlockDiagramModelChanged("bd_spaceEdited");
    }

    // ========== Block Diagram Helper Methods ==========

    /// <summary>
    /// Finds the item list for a given group ID (or top-level if null).
    /// </summary>
    private List<BlockDiagramItem> FindItemList(string? groupId)
    {
        if (_blockDiagramModel == null) return new List<BlockDiagramItem>();
        if (string.IsNullOrEmpty(groupId)) return _blockDiagramModel.Items;

        var group = FindGroupById(groupId, _blockDiagramModel.Items);
        return group?.Items ?? _blockDiagramModel.Items;
    }

    private BlockDiagramGroup? FindGroupById(string id, List<BlockDiagramItem> items)
    {
        foreach (var item in items)
        {
            if (item is BlockDiagramGroup group)
            {
                if (group.Id == id) return group;
                var nested = FindGroupById(id, group.Items);
                if (nested != null) return nested;
            }
        }
        return null;
    }

    private BlockDiagramItem? FindBlockById(string id)
    {
        if (_blockDiagramModel == null) return null;
        return FindBlockByIdRecursive(id, _blockDiagramModel.Items);
    }

    private static BlockDiagramItem? FindBlockByIdRecursive(string id, List<BlockDiagramItem> items)
    {
        foreach (var item in items)
        {
            if (item is BlockDiagramBlock block && block.Id == id) return block;
            if (item is BlockDiagramArrow arrow && arrow.Id == id) return arrow;
            if (item is BlockDiagramGroup group)
            {
                if (group.Id == id) return group;
                var nested = FindBlockByIdRecursive(id, group.Items);
                if (nested != null) return nested;
            }
        }
        return null;
    }

    private bool RemoveItemById(string id, List<BlockDiagramItem> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if ((item is BlockDiagramBlock b && b.Id == id) ||
                (item is BlockDiagramArrow a && a.Id == id) ||
                (item is BlockDiagramGroup g && g.Id == id))
            {
                items.RemoveAt(i);
                return true;
            }
            if (item is BlockDiagramGroup group && RemoveItemById(id, group.Items))
                return true;
        }
        return false;
    }

    private BlockDiagramItem? FindAndRemoveItemById(string id, List<BlockDiagramItem> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if ((item is BlockDiagramBlock b && b.Id == id) ||
                (item is BlockDiagramArrow a && a.Id == id) ||
                (item is BlockDiagramGroup g && g.Id == id))
            {
                items.RemoveAt(i);
                return item;
            }
            if (item is BlockDiagramGroup group)
            {
                var found = FindAndRemoveItemById(id, group.Items);
                if (found != null) return found;
            }
        }
        return null;
    }

    /// <summary>
    /// Updates all references to an old block/arrow/group ID when it is renamed.
    /// Covers edges (FromId/ToId), class assignments (comma-separated Ids), and inline styles (Id).
    /// </summary>
    private void UpdateBlockReferences(string oldId, string newId)
    {
        if (_blockDiagramModel == null) return;

        // Update edge references
        foreach (var edge in _blockDiagramModel.Edges)
        {
            if (edge.FromId == oldId) edge.FromId = newId;
            if (edge.ToId == oldId) edge.ToId = newId;
        }

        // Update class assignment references (comma-separated ID lists)
        foreach (var ca in _blockDiagramModel.ClassAssignments)
        {
            var ids = ca.Ids.Split(',').Select(s => s.Trim()).ToList();
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == oldId) ids[i] = newId;
            }
            ca.Ids = string.Join(", ", ids);
        }

        // Update inline style references
        foreach (var style in _blockDiagramModel.InlineStyles)
        {
            if (style.Id == oldId) style.Id = newId;
        }
    }

    // ========== Block Diagram Model Restore ==========

    private void RestoreBlockDiagramModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<BlockDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _blockDiagramModel = new BlockDiagramModel
        {
            Columns = dto.Columns,
            ColumnsExplicit = dto.ColumnsExplicit,
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        if (dto.Items != null)
            _blockDiagramModel.Items = RestoreBlockItems(dto.Items);

        if (dto.Edges != null)
        {
            foreach (var e in dto.Edges)
            {
                _blockDiagramModel.Edges.Add(new BlockDiagramEdge
                {
                    FromId = e.FromId ?? string.Empty,
                    ToId = e.ToId ?? string.Empty,
                    Label = e.Label,
                    Style = e.Style ?? "-->"
                });
            }
        }

        if (dto.StyleDefs != null)
        {
            foreach (var s in dto.StyleDefs)
            {
                _blockDiagramModel.StyleDefs.Add(new BlockDiagramStyleDef
                {
                    Name = s.Name ?? string.Empty,
                    Styles = s.Styles ?? string.Empty
                });
            }
        }

        if (dto.ClassAssignments != null)
        {
            foreach (var c in dto.ClassAssignments)
            {
                _blockDiagramModel.ClassAssignments.Add(new BlockDiagramClassAssignment
                {
                    Ids = c.Ids ?? string.Empty,
                    ClassName = c.ClassName ?? string.Empty
                });
            }
        }

        if (dto.InlineStyles != null)
        {
            foreach (var s in dto.InlineStyles)
            {
                _blockDiagramModel.InlineStyles.Add(new BlockDiagramInlineStyle
                {
                    Id = s.Id ?? string.Empty,
                    Styles = s.Styles ?? string.Empty
                });
            }
        }
    }

    private static List<BlockDiagramItem> RestoreBlockItems(List<BlockDiagramItemDto> dtoItems)
    {
        var items = new List<BlockDiagramItem>();
        foreach (var dto in dtoItems)
        {
            switch (dto.Type)
            {
                case "block":
                    if (!Enum.TryParse<BlockShape>(dto.Shape ?? "Rectangle", true, out var shape))
                        shape = BlockShape.Rectangle;
                    items.Add(new BlockDiagramBlock
                    {
                        Id = dto.Id ?? string.Empty,
                        Label = dto.Label,
                        Shape = shape,
                        Width = dto.Width
                    });
                    break;
                case "space":
                    items.Add(new BlockDiagramSpace { Width = dto.Width });
                    break;
                case "arrow":
                    items.Add(new BlockDiagramArrow
                    {
                        Id = dto.Id ?? string.Empty,
                        Label = dto.Label,
                        Direction = dto.Direction ?? "down",
                        Width = dto.Width
                    });
                    break;
                case "group":
                    var group = new BlockDiagramGroup
                    {
                        Id = dto.Id ?? string.Empty,
                        Label = dto.Label,
                        Columns = dto.Columns,
                        ColumnsExplicit = dto.ColumnsExplicit,
                        Width = dto.Width
                    };
                    if (dto.Items != null)
                        group.Items = RestoreBlockItems(dto.Items);
                    items.Add(group);
                    break;
            }
        }
        return items;
    }

    private void RaiseBlockDiagramModelChanged(string changeType)
    {
        if (_blockDiagramModel != null)
            BlockDiagramModelChanged?.Invoke(this, new BlockDiagramModelChangedEventArgs(changeType, _blockDiagramModel));
    }

    // ========== Block Diagram DTOs ==========

    private class BlockDiagramDto
    {
        public int Columns { get; set; } = 1;
        public bool ColumnsExplicit { get; set; }
        public List<BlockDiagramItemDto>? Items { get; set; }
        public List<BlockDiagramEdgeDto>? Edges { get; set; }
        public List<BlockDiagramStyleDefDto>? StyleDefs { get; set; }
        public List<BlockDiagramClassAssignmentDto>? ClassAssignments { get; set; }
        public List<BlockDiagramInlineStyleDto>? InlineStyles { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class BlockDiagramItemDto
    {
        public string? Type { get; set; }
        public string? Id { get; set; }
        public string? Label { get; set; }
        public string? Shape { get; set; }
        public string? Direction { get; set; }
        public int Width { get; set; } = 1;
        public int Columns { get; set; } = 1;
        public bool ColumnsExplicit { get; set; }
        public List<BlockDiagramItemDto>? Items { get; set; }
    }

    private class BlockDiagramEdgeDto
    {
        public string? FromId { get; set; }
        public string? ToId { get; set; }
        public string? Label { get; set; }
        public string? Style { get; set; }
    }

    private class BlockDiagramStyleDefDto
    {
        public string? Name { get; set; }
        public string? Styles { get; set; }
    }

    private class BlockDiagramClassAssignmentDto
    {
        public string? Ids { get; set; }
        public string? ClassName { get; set; }
    }

    private class BlockDiagramInlineStyleDto
    {
        public string? Id { get; set; }
        public string? Styles { get; set; }
    }
}

/// <summary>
/// Event args for when the BlockDiagramModel changes via the visual editor.
/// </summary>
public class BlockDiagramModelChangedEventArgs : EventArgs
{
    public string ChangeType { get; }
    public BlockDiagramModel Model { get; }

    public BlockDiagramModelChangedEventArgs(string changeType, BlockDiagramModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}
