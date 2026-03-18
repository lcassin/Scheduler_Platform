using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// ER diagram handlers for the visual editor bridge.
/// Handles entity CRUD, attribute operations, relationships, auto-layout, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== ER Diagram Support ==========

    /// <summary>
    /// Sends the current ERDiagramModel to the visual editor as JSON.
    /// </summary>
    public async Task SendERDiagramToEditorAsync()
    {
        if (_erDiagramModel == null) return;
        var json = ConvertERDiagramModelToJson(_erDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadERDiagram({escaped})");
    }

    /// <summary>
    /// Restores the ER diagram for undo/redo (preserves all positions).
    /// </summary>
    private async Task RestoreERDiagramToEditorAsync()
    {
        if (_erDiagramModel == null) return;
        var json = ConvertERDiagramModelToJson(_erDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreERDiagram({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current ER diagram model without resetting the view.
    /// </summary>
    public async Task RefreshERDiagramAsync()
    {
        if (_erDiagramModel == null) return;
        var json = ConvertERDiagramModelToJson(_erDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshERDiagram({escaped})");
    }

    /// <summary>
    /// Updates the ER diagram model reference and sends to editor.
    /// </summary>
    public async Task UpdateERDiagramModelAsync(ERDiagramModel newModel)
    {
        _erDiagramModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.ERDiagram;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendERDiagramToEditorAsync();
    }

    /// <summary>
    /// Converts an ERDiagramModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertERDiagramModelToJson(ERDiagramModel model)
    {
        var entities = model.Entities.Select(e => new ErDiagEntityDto
        {
            Name = e.Name,
            IsExplicit = e.IsExplicit,
            Attributes = e.Attributes.Select(a => new ErDiagAttributeDto
            {
                Type = a.Type,
                Name = a.Name,
                Key = a.Key,
                Comment = a.Comment
            }).ToList(),
            X = e.Position.X,
            Y = e.Position.Y,
            HasManualPosition = e.HasManualPosition
        }).ToList();

        var relationships = model.Relationships.Select(r => new ErDiagRelDto
        {
            FromEntity = r.FromEntity,
            ToEntity = r.ToEntity,
            LeftCardinality = r.LeftCardinality.ToString(),
            RightCardinality = r.RightCardinality.ToString(),
            IsIdentifying = r.IsIdentifying,
            Label = r.Label
        }).ToList();

        var dto = new ErDiagramDto
        {
            Entities = entities,
            Relationships = relationships,
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    // ========== ER Diagram Auto-Layout & Move ==========

    private void HandleErAutoLayoutComplete(JsonElement root)
    {
        if (!root.TryGetProperty("positions", out var positionsArray))
        {
            RaiseERDiagramModelChanged("er_autoLayoutComplete");
            return;
        }

        PushUndo();
        foreach (var pos in positionsArray.EnumerateArray())
        {
            var entityName = pos.GetProperty("entityName").GetString();
            if (string.IsNullOrEmpty(entityName)) continue;

            var entity = _erDiagramModel?.Entities.Find(e => e.Name == entityName);
            if (entity == null) continue;

            entity.Position = new System.Windows.Point(
                pos.GetProperty("x").GetDouble(),
                pos.GetProperty("y").GetDouble()
            );
            entity.HasManualPosition = true;
        }

        RaiseERDiagramModelChanged("er_autoLayoutComplete");
    }

    private void HandleErEntityMoved(JsonElement root)
    {
        var entityName = root.GetProperty("entityName").GetString();
        var x = root.GetProperty("x").GetDouble();
        var y = root.GetProperty("y").GetDouble();

        if (string.IsNullOrEmpty(entityName)) return;

        var entity = _erDiagramModel?.Entities.Find(e => e.Name == entityName);
        if (entity == null) return;

        PushUndo();
        entity.Position = new System.Windows.Point(x, y);
        entity.HasManualPosition = true;
        RaiseERDiagramModelChanged("er_entityMoved");
    }

    // ========== ER Diagram Message Handlers ==========

    private void HandleErEntityCreated(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var name = root.GetProperty("name").GetString();
        if (string.IsNullOrEmpty(name)) return;
        if (_erDiagramModel.Entities.Any(e => e.Name == name)) return;

        PushUndo();
        var entity = new EREntity
        {
            Name = name,
            IsExplicit = true
        };

        // Support optional attributes array for copy/paste (avoids race condition
        // where entity renders with 0 attributes before attribute messages arrive)
        if (root.TryGetProperty("attributes", out var attrsArr) && attrsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var attrEl in attrsArr.EnumerateArray())
            {
                var attr = new ERAttribute
                {
                    Name = attrEl.TryGetProperty("name", out var n) ? n.GetString() ?? "field" : "field",
                    Type = attrEl.TryGetProperty("attrType", out var t) ? t.GetString() ?? "string" : "string"
                };
                if (attrEl.TryGetProperty("key", out var k))
                {
                    var keyStr = k.GetString();
                    if (!string.IsNullOrEmpty(keyStr))
                        attr.Key = keyStr;
                }
                if (attrEl.TryGetProperty("comment", out var c))
                    attr.Comment = c.GetString();
                entity.Attributes.Add(attr);
            }
        }

        _erDiagramModel.Entities.Add(entity);
        RaiseERDiagramModelChanged("er_entityCreated");
    }

    private void HandleErEntityEdited(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var oldName = root.GetProperty("oldName").GetString();
        if (string.IsNullOrEmpty(oldName)) return;

        var entity = _erDiagramModel.Entities.Find(e => e.Name == oldName);
        if (entity == null) return;

        PushUndo();
        if (root.TryGetProperty("name", out var nameProp))
        {
            var newName = nameProp.GetString();
            if (!string.IsNullOrEmpty(newName) && newName != oldName)
            {
                // Update references in relationships
                foreach (var r in _erDiagramModel.Relationships)
                {
                    if (r.FromEntity == oldName) r.FromEntity = newName;
                    if (r.ToEntity == oldName) r.ToEntity = newName;
                }
                entity.Name = newName;
            }
        }
        RaiseERDiagramModelChanged("er_entityEdited");
    }

    private void HandleErEntityDeleted(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var name = root.GetProperty("name").GetString();
        if (string.IsNullOrEmpty(name)) return;

        PushUndo();
        _erDiagramModel.Entities.RemoveAll(e => e.Name == name);
        _erDiagramModel.Relationships.RemoveAll(r => r.FromEntity == name || r.ToEntity == name);
        RaiseERDiagramModelChanged("er_entityDeleted");
    }

    private void HandleErAttributeAdded(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var entityName = root.GetProperty("entityName").GetString();
        if (string.IsNullOrEmpty(entityName)) return;

        var entity = _erDiagramModel.Entities.Find(e => e.Name == entityName);
        if (entity == null) return;

        PushUndo();
        var attr = new ERAttribute
        {
            Type = root.TryGetProperty("attrType", out var tProp) ? tProp.GetString() ?? "string" : "string",
            Name = root.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "field" : "field"
        };
        if (root.TryGetProperty("key", out var kProp))
            attr.Key = kProp.GetString();
        if (root.TryGetProperty("comment", out var cProp))
            attr.Comment = cProp.GetString();
        entity.Attributes.Add(attr);
        RaiseERDiagramModelChanged("er_attributeAdded");
    }

    private void HandleErAttributeEdited(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var entityName = root.GetProperty("entityName").GetString();
        var index = root.GetProperty("index").GetInt32();
        if (string.IsNullOrEmpty(entityName)) return;

        var entity = _erDiagramModel.Entities.Find(e => e.Name == entityName);
        if (entity == null || index < 0 || index >= entity.Attributes.Count) return;

        PushUndo();
        var attr = entity.Attributes[index];
        if (root.TryGetProperty("attrType", out var tProp))
            attr.Type = tProp.GetString() ?? "string";
        if (root.TryGetProperty("name", out var nProp))
            attr.Name = nProp.GetString() ?? "field";
        if (root.TryGetProperty("key", out var kProp))
            attr.Key = kProp.GetString();
        if (root.TryGetProperty("comment", out var cProp))
            attr.Comment = cProp.GetString();
        RaiseERDiagramModelChanged("er_attributeEdited");
    }

    private void HandleErAttributeDeleted(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var entityName = root.GetProperty("entityName").GetString();
        var index = root.GetProperty("index").GetInt32();
        if (string.IsNullOrEmpty(entityName)) return;

        var entity = _erDiagramModel.Entities.Find(e => e.Name == entityName);
        if (entity == null || index < 0 || index >= entity.Attributes.Count) return;

        PushUndo();
        entity.Attributes.RemoveAt(index);
        RaiseERDiagramModelChanged("er_attributeDeleted");
    }

    private void HandleErRelationshipCreated(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var fromEntity = root.GetProperty("fromEntity").GetString();
        var toEntity = root.GetProperty("toEntity").GetString();
        if (string.IsNullOrEmpty(fromEntity) || string.IsNullOrEmpty(toEntity)) return;

        PushUndo();
        var rel = new ERRelationship
        {
            FromEntity = fromEntity,
            ToEntity = toEntity
        };
        if (root.TryGetProperty("leftCardinality", out var lcProp))
        {
            if (Enum.TryParse<ERCardinality>(lcProp.GetString(), true, out var lc))
                rel.LeftCardinality = lc;
        }
        if (root.TryGetProperty("rightCardinality", out var rcProp))
        {
            if (Enum.TryParse<ERCardinality>(rcProp.GetString(), true, out var rc))
                rel.RightCardinality = rc;
        }
        if (root.TryGetProperty("isIdentifying", out var idProp))
            rel.IsIdentifying = idProp.GetBoolean();
        if (root.TryGetProperty("label", out var labelProp))
            rel.Label = labelProp.GetString();
        _erDiagramModel.Relationships.Add(rel);
        RaiseERDiagramModelChanged("er_relationshipCreated");
    }

    private void HandleErRelationshipEdited(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _erDiagramModel.Relationships.Count) return;

        PushUndo();
        var rel = _erDiagramModel.Relationships[index];
        if (root.TryGetProperty("leftCardinality", out var lcProp))
        {
            if (Enum.TryParse<ERCardinality>(lcProp.GetString(), true, out var lc))
                rel.LeftCardinality = lc;
        }
        if (root.TryGetProperty("rightCardinality", out var rcProp))
        {
            if (Enum.TryParse<ERCardinality>(rcProp.GetString(), true, out var rc))
                rel.RightCardinality = rc;
        }
        if (root.TryGetProperty("isIdentifying", out var idProp))
            rel.IsIdentifying = idProp.GetBoolean();
        if (root.TryGetProperty("label", out var labelProp))
            rel.Label = labelProp.GetString();
        RaiseERDiagramModelChanged("er_relationshipEdited");
    }

    private void HandleErRelationshipDeleted(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _erDiagramModel.Relationships.Count) return;

        PushUndo();
        _erDiagramModel.Relationships.RemoveAt(index);
        RaiseERDiagramModelChanged("er_relationshipDeleted");
    }

    private void RestoreERDiagramModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<ErDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _erDiagramModel = new ERDiagramModel
        {
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        if (dto.Entities != null)
        {
            foreach (var e in dto.Entities)
            {
                var entity = new EREntity
                {
                    Name = e.Name ?? string.Empty,
                    IsExplicit = e.IsExplicit,
                    Position = new System.Windows.Point(e.X, e.Y),
                    HasManualPosition = e.HasManualPosition
                };
                if (e.Attributes != null)
                {
                    foreach (var a in e.Attributes)
                    {
                        entity.Attributes.Add(new ERAttribute
                        {
                            Type = a.Type ?? "string",
                            Name = a.Name ?? "field",
                            Key = a.Key,
                            Comment = a.Comment
                        });
                    }
                }
                _erDiagramModel.Entities.Add(entity);
            }
        }

        if (dto.Relationships != null)
        {
            foreach (var r in dto.Relationships)
            {
                var leftCard = ERCardinality.ExactlyOne;
                var rightCard = ERCardinality.ExactlyOne;
                if (!string.IsNullOrEmpty(r.LeftCardinality))
                    Enum.TryParse(r.LeftCardinality, true, out leftCard);
                if (!string.IsNullOrEmpty(r.RightCardinality))
                    Enum.TryParse(r.RightCardinality, true, out rightCard);

                _erDiagramModel.Relationships.Add(new ERRelationship
                {
                    FromEntity = r.FromEntity ?? string.Empty,
                    ToEntity = r.ToEntity ?? string.Empty,
                    LeftCardinality = leftCard,
                    RightCardinality = rightCard,
                    IsIdentifying = r.IsIdentifying,
                    Label = r.Label
                });
            }
        }
    }

    // ========== ER Diagram DTOs ==========

    // ========== ER Diagram DTOs ==========

    private class ErDiagramDto
    {
        public List<ErDiagEntityDto>? Entities { get; set; }
        public List<ErDiagRelDto>? Relationships { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class ErDiagEntityDto
    {
        public string? Name { get; set; }
        public bool IsExplicit { get; set; }
        public List<ErDiagAttributeDto>? Attributes { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public bool HasManualPosition { get; set; }
    }

    private class ErDiagAttributeDto
    {
        public string? Type { get; set; }
        public string? Name { get; set; }
        public string? Key { get; set; }
        public string? Comment { get; set; }
    }

    private class ErDiagRelDto
    {
        public string? FromEntity { get; set; }
        public string? ToEntity { get; set; }
        public string? LeftCardinality { get; set; }
        public string? RightCardinality { get; set; }
        public bool IsIdentifying { get; set; }
        public string? Label { get; set; }
    }
}
