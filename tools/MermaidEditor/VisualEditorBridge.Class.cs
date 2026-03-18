using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// Class diagram handlers for the visual editor bridge.
/// Handles class CRUD, member operations, relationships, auto-layout, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== Class Diagram Support ==========

    /// <summary>
    /// Sends the current ClassDiagramModel to the visual editor as JSON.
    /// </summary>
    public async Task SendClassDiagramToEditorAsync()
    {
        if (_classDiagramModel == null) return;
        var json = ConvertClassDiagramModelToJson(_classDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadClassDiagram({escaped})");
    }

    /// <summary>
    /// Restores the class diagram for undo/redo (preserves all positions).
    /// </summary>
    private async Task RestoreClassDiagramToEditorAsync()
    {
        if (_classDiagramModel == null) return;
        var json = ConvertClassDiagramModelToJson(_classDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreClassDiagram({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current class diagram model without resetting the view.
    /// </summary>
    public async Task RefreshClassDiagramAsync()
    {
        if (_classDiagramModel == null) return;
        var json = ConvertClassDiagramModelToJson(_classDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshClassDiagram({escaped})");
    }

    /// <summary>
    /// Updates the class diagram model reference and sends to editor.
    /// </summary>
    public async Task UpdateClassDiagramModelAsync(ClassDiagramModel newModel)
    {
        _classDiagramModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.ClassDiagram;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendClassDiagramToEditorAsync();
    }

    /// <summary>
    /// Converts a ClassDiagramModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertClassDiagramModelToJson(ClassDiagramModel model)
    {
        var classes = model.Classes.Select(c => new ClsDiagClassDto
        {
            Id = c.Id,
            Label = c.Label,
            Annotation = c.Annotation,
            GenericType = c.GenericType,
            Members = c.Members.Select(m => new ClsDiagMemberDto
            {
                RawText = m.RawText,
                Visibility = m.Visibility.ToString(),
                IsMethod = m.IsMethod,
                Name = m.Name,
                Type = m.Type,
                Parameters = m.Parameters,
                Classifier = m.Classifier.ToString()
            }).ToList(),
            CssClass = c.CssClass,
            IsExplicit = c.IsExplicit,
            X = c.Position.X,
            Y = c.Position.Y,
            HasManualPosition = c.HasManualPosition
        }).ToList();

        var relationships = model.Relationships.Select(r => new ClsDiagRelDto
        {
            FromId = r.FromId,
            ToId = r.ToId,
            LeftEnd = r.LeftEnd.ToString(),
            RightEnd = r.RightEnd.ToString(),
            LinkStyle = r.LinkStyle.ToString(),
            Label = r.Label,
            FromCardinality = r.FromCardinality,
            ToCardinality = r.ToCardinality
        }).ToList();

        var namespaces = model.Namespaces.Select(n => new ClsDiagNamespaceDto
        {
            Name = n.Name,
            ClassIds = n.ClassIds
        }).ToList();

        var notes = model.Notes.Select(n => new ClsDiagNoteDto
        {
            Text = n.Text,
            ForClass = n.ForClass
        }).ToList();

        var dto = new ClsDiagramDto
        {
            Direction = model.Direction,
            Classes = classes,
            Relationships = relationships,
            Namespaces = namespaces,
            Notes = notes,
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    // ========== Class Diagram Auto-Layout & Move ==========

    private void HandleClsAutoLayoutComplete(JsonElement root)
    {
        if (!root.TryGetProperty("positions", out var positionsArray))
        {
            RaiseClassDiagramModelChanged("cls_autoLayoutComplete");
            return;
        }

        PushUndo();
        foreach (var pos in positionsArray.EnumerateArray())
        {
            var classId = pos.GetProperty("classId").GetString();
            if (string.IsNullOrEmpty(classId)) continue;

            var cls = _classDiagramModel?.Classes.Find(c => c.Id == classId);
            if (cls == null) continue;

            cls.Position = new System.Windows.Point(
                pos.GetProperty("x").GetDouble(),
                pos.GetProperty("y").GetDouble()
            );
            cls.HasManualPosition = true;
        }

        RaiseClassDiagramModelChanged("cls_autoLayoutComplete");
    }

    private void HandleClsClassMoved(JsonElement root)
    {
        var classId = root.GetProperty("classId").GetString();
        var x = root.GetProperty("x").GetDouble();
        var y = root.GetProperty("y").GetDouble();

        if (string.IsNullOrEmpty(classId)) return;

        var cls = _classDiagramModel?.Classes.Find(c => c.Id == classId);
        if (cls == null) return;

        PushUndo();
        cls.Position = new System.Windows.Point(x, y);
        cls.HasManualPosition = true;
        RaiseClassDiagramModelChanged("cls_classMoved");
    }

    // ========== Class Diagram Message Handlers ==========

    private void HandleClsClassCreated(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        if (string.IsNullOrEmpty(classId)) return;
        if (_classDiagramModel.Classes.Any(c => c.Id == classId)) return;

        PushUndo();
        var newClass = new ClassDefinition
        {
            Id = classId,
            IsExplicit = true
        };
        if (root.TryGetProperty("annotation", out var annProp))
            newClass.Annotation = annProp.GetString();

        // Support optional members array for copy/paste (avoids race condition
        // where class renders with 0 members before member messages arrive)
        if (root.TryGetProperty("members", out var membersArr) && membersArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var memberEl in membersArr.EnumerateArray())
            {
                var rawText = memberEl.GetProperty("rawText").GetString() ?? "";
                var member = ParseClassMemberRawText(rawText);
                newClass.Members.Add(member);
            }
        }

        _classDiagramModel.Classes.Add(newClass);
        RaiseClassDiagramModelChanged("cls_classCreated");
    }

    private void HandleClsClassEdited(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        if (string.IsNullOrEmpty(classId)) return;

        var cls = _classDiagramModel.Classes.Find(c => c.Id == classId);
        if (cls == null) return;

        PushUndo();
        if (root.TryGetProperty("label", out var labelProp))
        {
            var label = labelProp.GetString();
            cls.Label = string.IsNullOrEmpty(label) ? null : label;
        }
        if (root.TryGetProperty("annotation", out var annProp))
        {
            var ann = annProp.GetString();
            cls.Annotation = string.IsNullOrEmpty(ann) ? null : ann;
        }
        if (root.TryGetProperty("genericType", out var genProp))
        {
            var gen = genProp.GetString();
            cls.GenericType = string.IsNullOrEmpty(gen) ? null : gen;
        }
        RaiseClassDiagramModelChanged("cls_classEdited");
    }

    private void HandleClsClassDeleted(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        if (string.IsNullOrEmpty(classId)) return;

        PushUndo();
        _classDiagramModel.Classes.RemoveAll(c => c.Id == classId);
        _classDiagramModel.Relationships.RemoveAll(r => r.FromId == classId || r.ToId == classId);
        // Remove from namespaces
        foreach (var ns in _classDiagramModel.Namespaces)
            ns.ClassIds.Remove(classId);
        RaiseClassDiagramModelChanged("cls_classDeleted");
    }

    private void HandleClsMemberAdded(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        var rawText = root.GetProperty("rawText").GetString() ?? "";
        if (string.IsNullOrEmpty(classId)) return;

        var cls = _classDiagramModel.Classes.Find(c => c.Id == classId);
        if (cls == null) return;

        PushUndo();
        var member = ParseClassMemberRawText(rawText);
        cls.Members.Add(member);
        RaiseClassDiagramModelChanged("cls_memberAdded");
    }

    private void HandleClsMemberEdited(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        var memberIndex = root.GetProperty("memberIndex").GetInt32();
        var rawText = root.GetProperty("rawText").GetString() ?? "";
        if (string.IsNullOrEmpty(classId)) return;

        var cls = _classDiagramModel.Classes.Find(c => c.Id == classId);
        if (cls == null || memberIndex < 0 || memberIndex >= cls.Members.Count) return;

        PushUndo();
        var parsed = ParseClassMemberRawText(rawText);
        var member = cls.Members[memberIndex];
        member.RawText = parsed.RawText;
        member.IsMethod = parsed.IsMethod;
        member.Visibility = parsed.Visibility;
        member.Name = parsed.Name;
        member.Type = parsed.Type;
        member.Parameters = parsed.Parameters;
        member.Classifier = parsed.Classifier;
        RaiseClassDiagramModelChanged("cls_memberEdited");
    }

    private void HandleClsMemberDeleted(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        var memberIndex = root.GetProperty("memberIndex").GetInt32();
        if (string.IsNullOrEmpty(classId)) return;

        var cls = _classDiagramModel.Classes.Find(c => c.Id == classId);
        if (cls == null || memberIndex < 0 || memberIndex >= cls.Members.Count) return;

        PushUndo();
        cls.Members.RemoveAt(memberIndex);
        RaiseClassDiagramModelChanged("cls_memberDeleted");
    }

    private void HandleClsMemberMoved(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        var memberIndex = root.GetProperty("memberIndex").GetInt32();
        var direction = root.GetProperty("direction").GetString() ?? "";
        if (string.IsNullOrEmpty(classId)) return;

        var cls = _classDiagramModel.Classes.Find(c => c.Id == classId);
        if (cls == null || memberIndex < 0 || memberIndex >= cls.Members.Count) return;

        var newIndex = direction == "up" ? memberIndex - 1 : memberIndex + 1;
        if (newIndex < 0 || newIndex >= cls.Members.Count) return;

        PushUndo();
        var member = cls.Members[memberIndex];
        cls.Members.RemoveAt(memberIndex);
        cls.Members.Insert(newIndex, member);
        RaiseClassDiagramModelChanged("cls_memberMoved");
    }

    /// <summary>
    /// Parses raw member text (e.g. "+newMethod()" or "+void getName(String key)")
    /// into a fully structured ClassMember, matching the same logic as MermaidParser.ParseClassMember.
    /// </summary>
    private static ClassMember ParseClassMemberRawText(string rawText)
    {
        var member = new ClassMember { RawText = rawText };
        var remaining = rawText.Trim();

        // Parse visibility prefix
        if (remaining.Length > 0)
        {
            switch (remaining[0])
            {
                case '+': member.Visibility = MemberVisibility.Public; remaining = remaining[1..]; break;
                case '-': member.Visibility = MemberVisibility.Private; remaining = remaining[1..]; break;
                case '#': member.Visibility = MemberVisibility.Protected; remaining = remaining[1..]; break;
                case '~': member.Visibility = MemberVisibility.Package; remaining = remaining[1..]; break;
            }
        }

        // Check for classifier suffix (* or $) at the very end
        if (remaining.EndsWith("*"))
        {
            member.Classifier = MemberClassifier.Abstract;
            remaining = remaining[..^1];
        }
        else if (remaining.EndsWith("$"))
        {
            member.Classifier = MemberClassifier.Static;
            remaining = remaining[..^1];
        }

        // Check if it's a method (contains parentheses)
        var parenOpen = remaining.IndexOf('(');
        if (parenOpen >= 0)
        {
            member.IsMethod = true;
            var parenClose = remaining.LastIndexOf(')');
            if (parenClose > parenOpen)
            {
                member.Parameters = remaining[(parenOpen + 1)..parenClose];
                // Method name is everything before the (
                var beforeParen = remaining[..parenOpen].Trim();
                member.Name = beforeParen;

                // Return type: check for "Type name(" pattern (type before name)
                // or text after closing paren
                var afterParen = remaining[(parenClose + 1)..].Trim();
                if (!string.IsNullOrEmpty(afterParen))
                {
                    member.Type = afterParen;
                }
                else
                {
                    // Check if beforeParen has "Type name" (space-separated)
                    var parts = beforeParen.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        member.Type = parts[0];
                        member.Name = parts[1];
                    }
                }
            }
            else
            {
                member.Name = remaining;
            }
        }
        else
        {
            // Field
            member.IsMethod = false;
            var colonIdx = remaining.IndexOf(':');
            if (colonIdx >= 0)
            {
                member.Name = remaining[..colonIdx].Trim();
                member.Type = remaining[(colonIdx + 1)..].Trim();
            }
            else
            {
                var parts = remaining.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    member.Type = parts[0];
                    member.Name = parts[1];
                }
                else if (parts.Length == 1)
                {
                    member.Name = parts[0];
                }
            }
        }

        return member;
    }

    private void HandleClsRelationshipCreated(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var fromId = root.GetProperty("fromId").GetString();
        var toId = root.GetProperty("toId").GetString();
        if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId)) return;

        PushUndo();
        var rel = new ClassRelationship
        {
            FromId = fromId,
            ToId = toId,
            LinkStyle = ClassLinkStyle.Solid
        };

        if (root.TryGetProperty("leftEnd", out var leftProp))
        {
            if (Enum.TryParse<ClassRelationEnd>(leftProp.GetString(), out var le))
                rel.LeftEnd = le;
        }
        if (root.TryGetProperty("rightEnd", out var rightProp))
        {
            if (Enum.TryParse<ClassRelationEnd>(rightProp.GetString(), out var re))
                rel.RightEnd = re;
        }
        if (root.TryGetProperty("linkStyle", out var linkProp))
        {
            if (Enum.TryParse<ClassLinkStyle>(linkProp.GetString(), out var ls))
                rel.LinkStyle = ls;
        }
        if (root.TryGetProperty("label", out var labelProp))
            rel.Label = labelProp.GetString();
        if (root.TryGetProperty("fromCardinality", out var fromCardProp))
            rel.FromCardinality = fromCardProp.GetString();
        if (root.TryGetProperty("toCardinality", out var toCardProp))
            rel.ToCardinality = toCardProp.GetString();

        _classDiagramModel.Relationships.Add(rel);
        RaiseClassDiagramModelChanged("cls_relationshipCreated");
    }

    private void HandleClsRelationshipEdited(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var relIndex = root.GetProperty("relationshipIndex").GetInt32();
        if (relIndex < 0 || relIndex >= _classDiagramModel.Relationships.Count) return;

        PushUndo();
        var rel = _classDiagramModel.Relationships[relIndex];
        if (root.TryGetProperty("fromId", out var fromIdProp))
            rel.FromId = fromIdProp.GetString() ?? rel.FromId;
        if (root.TryGetProperty("toId", out var toIdProp))
            rel.ToId = toIdProp.GetString() ?? rel.ToId;
        if (root.TryGetProperty("leftEnd", out var leftProp))
        {
            if (Enum.TryParse<ClassRelationEnd>(leftProp.GetString(), out var le))
                rel.LeftEnd = le;
        }
        if (root.TryGetProperty("rightEnd", out var rightProp))
        {
            if (Enum.TryParse<ClassRelationEnd>(rightProp.GetString(), out var re))
                rel.RightEnd = re;
        }
        if (root.TryGetProperty("linkStyle", out var linkProp))
        {
            if (Enum.TryParse<ClassLinkStyle>(linkProp.GetString(), out var ls))
                rel.LinkStyle = ls;
        }
        if (root.TryGetProperty("label", out var labelProp))
            rel.Label = labelProp.GetString();
        if (root.TryGetProperty("fromCardinality", out var fromCardProp))
            rel.FromCardinality = fromCardProp.GetString();
        if (root.TryGetProperty("toCardinality", out var toCardProp))
            rel.ToCardinality = toCardProp.GetString();
        RaiseClassDiagramModelChanged("cls_relationshipEdited");
    }

    private void HandleClsRelationshipDeleted(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var relIndex = root.GetProperty("relationshipIndex").GetInt32();
        if (relIndex < 0 || relIndex >= _classDiagramModel.Relationships.Count) return;

        PushUndo();
        _classDiagramModel.Relationships.RemoveAt(relIndex);
        RaiseClassDiagramModelChanged("cls_relationshipDeleted");
    }

    // ========== Class Diagram Model Restore (for undo/redo) ==========

    private void RestoreClassDiagramModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<ClsDiagramDto>(json, JsonOptions);
        if (dto == null || _classDiagramModel == null) return;

        _classDiagramModel.Direction = dto.Direction;
        _classDiagramModel.PreambleLines = dto.PreambleLines ?? new List<string>();
        _classDiagramModel.DeclarationLineIndex = dto.DeclarationLineIndex;
        _classDiagramModel.Classes.Clear();
        _classDiagramModel.Relationships.Clear();
        _classDiagramModel.Namespaces.Clear();
        _classDiagramModel.Notes.Clear();

        if (dto.Classes != null)
        {
            foreach (var c in dto.Classes)
            {
                var cls = new ClassDefinition
                {
                    Id = c.Id ?? string.Empty,
                    Label = c.Label,
                    Annotation = c.Annotation,
                    GenericType = c.GenericType,
                    CssClass = c.CssClass,
                    IsExplicit = c.IsExplicit,
                    Position = new System.Windows.Point(c.X, c.Y),
                    HasManualPosition = c.HasManualPosition
                };
                if (c.Members != null)
                {
                    foreach (var m in c.Members)
                    {
                        var vis = Enum.TryParse<MemberVisibility>(m.Visibility, out var v) ? v : MemberVisibility.None;
                        var classifier = Enum.TryParse<MemberClassifier>(m.Classifier, out var mc) ? mc : MemberClassifier.None;
                        cls.Members.Add(new ClassMember
                        {
                            RawText = m.RawText ?? string.Empty,
                            Visibility = vis,
                            IsMethod = m.IsMethod,
                            Name = m.Name ?? string.Empty,
                            Type = m.Type,
                            Parameters = m.Parameters,
                            Classifier = classifier
                        });
                    }
                }
                _classDiagramModel.Classes.Add(cls);
            }
        }

        if (dto.Relationships != null)
        {
            foreach (var r in dto.Relationships)
            {
                var leftEnd = Enum.TryParse<ClassRelationEnd>(r.LeftEnd, out var le) ? le : ClassRelationEnd.None;
                var rightEnd = Enum.TryParse<ClassRelationEnd>(r.RightEnd, out var re) ? re : ClassRelationEnd.None;
                var linkStyle = Enum.TryParse<ClassLinkStyle>(r.LinkStyle, out var ls) ? ls : ClassLinkStyle.Solid;
                _classDiagramModel.Relationships.Add(new ClassRelationship
                {
                    FromId = r.FromId ?? string.Empty,
                    ToId = r.ToId ?? string.Empty,
                    LeftEnd = leftEnd,
                    RightEnd = rightEnd,
                    LinkStyle = linkStyle,
                    Label = r.Label,
                    FromCardinality = r.FromCardinality,
                    ToCardinality = r.ToCardinality
                });
            }
        }

        if (dto.Namespaces != null)
        {
            foreach (var n in dto.Namespaces)
            {
                _classDiagramModel.Namespaces.Add(new ClassNamespace
                {
                    Name = n.Name ?? string.Empty,
                    ClassIds = n.ClassIds ?? new List<string>()
                });
            }
        }

        if (dto.Notes != null)
        {
            foreach (var n in dto.Notes)
            {
                _classDiagramModel.Notes.Add(new ClassNote
                {
                    Text = n.Text ?? string.Empty,
                    ForClass = n.ForClass
                });
            }
        }
    }

    // ========== Class Diagram DTOs ==========

    // ========== Class Diagram DTOs ==========

    private class ClsDiagramDto
    {
        public string? Direction { get; set; }
        public List<ClsDiagClassDto>? Classes { get; set; }
        public List<ClsDiagRelDto>? Relationships { get; set; }
        public List<ClsDiagNamespaceDto>? Namespaces { get; set; }
        public List<ClsDiagNoteDto>? Notes { get; set; }
        public List<string> PreambleLines { get; set; } = new();
        public int DeclarationLineIndex { get; set; }
    }

    private class ClsDiagClassDto
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public string? Annotation { get; set; }
        public string? GenericType { get; set; }
        public List<ClsDiagMemberDto>? Members { get; set; }
        public string? CssClass { get; set; }
        public bool IsExplicit { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public bool HasManualPosition { get; set; }
    }

    private class ClsDiagMemberDto
    {
        public string? RawText { get; set; }
        public string? Visibility { get; set; }
        public bool IsMethod { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Parameters { get; set; }
        public string? Classifier { get; set; }
    }

    private class ClsDiagRelDto
    {
        public string? FromId { get; set; }
        public string? ToId { get; set; }
        public string? LeftEnd { get; set; }
        public string? RightEnd { get; set; }
        public string? LinkStyle { get; set; }
        public string? Label { get; set; }
        public string? FromCardinality { get; set; }
        public string? ToCardinality { get; set; }
    }

    private class ClsDiagNamespaceDto
    {
        public string? Name { get; set; }
        public List<string>? ClassIds { get; set; }
    }

    private class ClsDiagNoteDto
    {
        public string? Text { get; set; }
        public string? ForClass { get; set; }
    }
}
