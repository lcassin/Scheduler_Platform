using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// ZenUML handlers for the visual editor bridge.
/// Handles participant/message/fragment CRUD, settings, and model restore.
/// Based on the Sequence Diagram bridge pattern, adapted for ZenUML syntax.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== ZenUML Support ==========

    private ZenUMLModel? _zenUMLModel;

    /// <summary>
    /// Gets the current ZenUMLModel (may be null if not in ZenUML mode).
    /// </summary>
    public ZenUMLModel? ZenUMLModel => _zenUMLModel;

    /// <summary>
    /// Raised when the ZenUMLModel is modified by the visual editor.
    /// </summary>
    public event EventHandler<ZenUMLModelChangedEventArgs>? ZenUMLModelChanged;

    /// <summary>
    /// Sends the current ZenUMLModel to the visual editor as JSON.
    /// </summary>
    public async Task SendZenUMLToEditorAsync()
    {
        if (_zenUMLModel == null) return;
        var json = ConvertZenUMLModelToJson(_zenUMLModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadZenUMLDiagram({escaped})");
    }

    /// <summary>
    /// Restores the ZenUML diagram for undo/redo.
    /// </summary>
    private async Task RestoreZenUMLToEditorAsync()
    {
        if (_zenUMLModel == null) return;
        var json = ConvertZenUMLModelToJson(_zenUMLModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreZenUMLDiagram({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current ZenUML model.
    /// </summary>
    public async Task RefreshZenUMLAsync()
    {
        if (_zenUMLModel == null) return;
        var json = ConvertZenUMLModelToJson(_zenUMLModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshZenUMLDiagram({escaped})");
    }

    /// <summary>
    /// Updates the ZenUML model reference and sends to editor.
    /// </summary>
    public async Task UpdateZenUMLModelAsync(ZenUMLModel newModel)
    {
        _zenUMLModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.ZenUML;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendZenUMLToEditorAsync();
    }

    /// <summary>
    /// Converts a ZenUMLModel to the JSON format expected by the visual editor JS.
    /// Flattens the nested element tree into a sequential list with depth info.
    /// </summary>
    public static string ConvertZenUMLModelToJson(ZenUMLModel model)
    {
        var flatElements = new List<ZenUMLElementDto>();
        FlattenZenUMLElements(model.Elements, flatElements, 0);

        var dto = new ZenUMLDiagramDto
        {
            Title = model.Title,
            Participants = model.Participants.Select(p => new ZenUMLParticipantDto
            {
                Id = p.Id,
                Alias = p.Alias,
                Annotator = p.Annotator.ToString(),
                IsExplicit = p.IsExplicit
            }).ToList(),
            Elements = flatElements,
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    /// Flattens the nested ZenUML elements into a sequential list with depth/nesting info.
    /// This is similar to the Sequence Diagram's FlattenSequenceElements approach.
    /// </summary>
    private static void FlattenZenUMLElements(List<ZenUMLElement> elements, List<ZenUMLElementDto> result, int depth)
    {
        for (int i = 0; i < elements.Count; i++)
        {
            var element = elements[i];

            switch (element)
            {
                case ZenUMLMessage msg:
                    var msgDto = new ZenUMLElementDto
                    {
                        ElementType = "message",
                        FromId = msg.FromId,
                        ToId = msg.ToId,
                        Text = msg.Text,
                        MessageType = msg.MessageType.ToString(),
                        HasBlock = msg.HasBlock,
                        Depth = depth,
                        Index = result.Count
                    };
                    result.Add(msgDto);

                    if (msg.HasBlock && msg.NestedElements.Count > 0)
                    {
                        FlattenZenUMLElements(msg.NestedElements, result, depth + 1);
                        // Add block-end marker
                        result.Add(new ZenUMLElementDto
                        {
                            ElementType = "blockEnd",
                            Depth = depth,
                            Index = result.Count
                        });
                    }
                    break;

                case ZenUMLReturn ret:
                    result.Add(new ZenUMLElementDto
                    {
                        ElementType = "return",
                        Text = ret.Value,
                        Depth = depth,
                        Index = result.Count
                    });
                    break;

                case ZenUMLComment comment:
                    result.Add(new ZenUMLElementDto
                    {
                        ElementType = "comment",
                        Text = comment.Text,
                        Depth = depth,
                        Index = result.Count
                    });
                    break;

                case ZenUMLFragment fragment:
                    var fragDto = new ZenUMLElementDto
                    {
                        ElementType = "fragment",
                        FragmentType = fragment.Type.ToString(),
                        Text = fragment.Label,
                        Depth = depth,
                        Index = result.Count,
                        Sections = fragment.Sections.Select(s => new ZenUMLFragmentSectionDto
                        {
                            Keyword = s.Keyword,
                            Label = s.Label
                        }).ToList()
                    };
                    result.Add(fragDto);

                    // Flatten each section's elements
                    foreach (var section in fragment.Sections)
                    {
                        FlattenZenUMLElements(section.Elements, result, depth + 1);
                    }

                    // Add fragment-end marker
                    result.Add(new ZenUMLElementDto
                    {
                        ElementType = "fragmentEnd",
                        Depth = depth,
                        Index = result.Count
                    });
                    break;
            }
        }
    }

    // ========== ZenUML Message Handlers ==========

    private void HandleZenUMLParticipantCreated(JsonElement root)
    {
        if (_zenUMLModel == null) return;

        var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
        var alias = root.TryGetProperty("alias", out var aliasProp) && aliasProp.ValueKind != JsonValueKind.Null
            ? aliasProp.GetString() : null;
        var annotatorStr = root.TryGetProperty("annotator", out var annProp) ? annProp.GetString() ?? "None" : "None";

        if (string.IsNullOrEmpty(id)) return;

        PushUndo();
        var annotator = Enum.TryParse<ZenUMLAnnotator>(annotatorStr, true, out var parsed) ? parsed : ZenUMLAnnotator.None;

        // Check if participant already exists
        var existing = _zenUMLModel.Participants.Find(p => p.Id == id);
        if (existing != null)
        {
            existing.Annotator = annotator;
            if (alias != null) existing.Alias = alias;
            existing.IsExplicit = true;
        }
        else
        {
            int? insertAtIndex = null;
            if (root.TryGetProperty("insertAtIndex", out var idxProp) && idxProp.ValueKind == JsonValueKind.Number)
                insertAtIndex = idxProp.GetInt32();

            var participant = new ZenUMLParticipant
            {
                Id = id,
                Alias = alias,
                Annotator = annotator,
                IsExplicit = true
            };

            if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= _zenUMLModel.Participants.Count)
                _zenUMLModel.Participants.Insert(insertAtIndex.Value, participant);
            else
                _zenUMLModel.Participants.Add(participant);
        }

        RaiseZenUMLModelChanged("zu_participantCreated");
    }

    private void HandleZenUMLParticipantEdited(JsonElement root)
    {
        if (_zenUMLModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _zenUMLModel.Participants.Count) return;

        PushUndo();
        var participant = _zenUMLModel.Participants[index];

        if (root.TryGetProperty("id", out var idProp))
        {
            var newId = idProp.GetString() ?? participant.Id;
            var oldId = participant.Id;
            if (newId != oldId)
            {
                // Update all message references
                UpdateZenUMLParticipantReferences(oldId, newId);
                participant.Id = newId;
            }
        }
        if (root.TryGetProperty("alias", out var aliasProp))
            participant.Alias = aliasProp.ValueKind == JsonValueKind.Null ? null : aliasProp.GetString();
        if (root.TryGetProperty("annotator", out var annProp))
        {
            var annStr = annProp.GetString() ?? "None";
            participant.Annotator = Enum.TryParse<ZenUMLAnnotator>(annStr, true, out var parsed)
                ? parsed : ZenUMLAnnotator.None;
        }

        RaiseZenUMLModelChanged("zu_participantEdited");
    }

    private void HandleZenUMLParticipantDeleted(JsonElement root)
    {
        if (_zenUMLModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _zenUMLModel.Participants.Count) return;

        PushUndo();
        var participantId = _zenUMLModel.Participants[index].Id;

        // Remove all messages referencing this participant
        RemoveZenUMLMessagesForParticipant(_zenUMLModel.Elements, participantId);

        _zenUMLModel.Participants.RemoveAt(index);
        RaiseZenUMLModelChanged("zu_participantDeleted");
    }

    private void HandleZenUMLParticipantReordered(JsonElement root)
    {
        if (_zenUMLModel == null) return;
        var fromIndex = root.GetProperty("fromIndex").GetInt32();
        var toIndex = root.GetProperty("toIndex").GetInt32();
        if (fromIndex < 0 || fromIndex >= _zenUMLModel.Participants.Count) return;
        if (toIndex < 0 || toIndex >= _zenUMLModel.Participants.Count) return;

        PushUndo();
        var participant = _zenUMLModel.Participants[fromIndex];
        _zenUMLModel.Participants.RemoveAt(fromIndex);
        _zenUMLModel.Participants.Insert(toIndex, participant);
        RaiseZenUMLModelChanged("zu_participantReordered");
    }

    private void HandleZenUMLMessageCreated(JsonElement root)
    {
        if (_zenUMLModel == null) return;

        var fromId = root.TryGetProperty("fromId", out var fProp) ? fProp.GetString() ?? "" : "";
        var toId = root.TryGetProperty("toId", out var tProp) ? tProp.GetString() ?? "" : "";
        var text = root.TryGetProperty("text", out var txtProp) ? txtProp.GetString() ?? "" : "";
        var msgTypeStr = root.TryGetProperty("messageType", out var mtProp) ? mtProp.GetString() ?? "Sync" : "Sync";
        var hasBlock = root.TryGetProperty("hasBlock", out var hbProp) && hbProp.GetBoolean();

        PushUndo();
        var msgType = Enum.TryParse<ZenUMLMessageType>(msgTypeStr, true, out var parsed)
            ? parsed : ZenUMLMessageType.Sync;

        var msg = new ZenUMLMessage
        {
            FromId = fromId,
            ToId = toId,
            Text = text,
            MessageType = msgType,
            HasBlock = hasBlock
        };

        // Ensure participants exist
        if (!string.IsNullOrEmpty(fromId) && !_zenUMLModel.Participants.Any(p => p.Id == fromId))
            _zenUMLModel.Participants.Add(new ZenUMLParticipant { Id = fromId });
        if (!string.IsNullOrEmpty(toId) && !_zenUMLModel.Participants.Any(p => p.Id == toId))
            _zenUMLModel.Participants.Add(new ZenUMLParticipant { Id = toId });

        int? insertAtIndex = null;
        if (root.TryGetProperty("insertAtIndex", out var idxProp) && idxProp.ValueKind == JsonValueKind.Number)
            insertAtIndex = idxProp.GetInt32();

        if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= _zenUMLModel.Elements.Count)
            _zenUMLModel.Elements.Insert(insertAtIndex.Value, msg);
        else
            _zenUMLModel.Elements.Add(msg);

        RaiseZenUMLModelChanged("zu_messageCreated");
    }

    private void HandleZenUMLMessageEdited(JsonElement root)
    {
        if (_zenUMLModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _zenUMLModel.Elements.Count) return;
        if (_zenUMLModel.Elements[elementIndex] is not ZenUMLMessage msg) return;

        PushUndo();
        if (root.TryGetProperty("fromId", out var fProp))
            msg.FromId = fProp.GetString() ?? msg.FromId;
        if (root.TryGetProperty("toId", out var tProp))
            msg.ToId = tProp.GetString() ?? msg.ToId;
        if (root.TryGetProperty("text", out var txtProp))
            msg.Text = txtProp.GetString() ?? msg.Text;
        if (root.TryGetProperty("messageType", out var mtProp))
        {
            var mtStr = mtProp.GetString();
            if (mtStr != null && Enum.TryParse<ZenUMLMessageType>(mtStr, true, out var parsed))
                msg.MessageType = parsed;
        }
        if (root.TryGetProperty("hasBlock", out var hbProp))
            msg.HasBlock = hbProp.GetBoolean();

        // Ensure participants exist
        if (!string.IsNullOrEmpty(msg.FromId) && !_zenUMLModel.Participants.Any(p => p.Id == msg.FromId))
            _zenUMLModel.Participants.Add(new ZenUMLParticipant { Id = msg.FromId });
        if (!string.IsNullOrEmpty(msg.ToId) && !_zenUMLModel.Participants.Any(p => p.Id == msg.ToId))
            _zenUMLModel.Participants.Add(new ZenUMLParticipant { Id = msg.ToId });

        RaiseZenUMLModelChanged("zu_messageEdited");
    }

    private void HandleZenUMLMessageDeleted(JsonElement root)
    {
        if (_zenUMLModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _zenUMLModel.Elements.Count) return;

        PushUndo();
        _zenUMLModel.Elements.RemoveAt(elementIndex);
        RaiseZenUMLModelChanged("zu_messageDeleted");
    }

    private void HandleZenUMLReturnCreated(JsonElement root)
    {
        if (_zenUMLModel == null) return;

        var value = root.TryGetProperty("value", out var vProp) ? vProp.GetString() ?? "" : "";

        PushUndo();
        var ret = new ZenUMLReturn { Value = value };

        int? insertAtIndex = null;
        if (root.TryGetProperty("insertAtIndex", out var idxProp) && idxProp.ValueKind == JsonValueKind.Number)
            insertAtIndex = idxProp.GetInt32();

        if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= _zenUMLModel.Elements.Count)
            _zenUMLModel.Elements.Insert(insertAtIndex.Value, ret);
        else
            _zenUMLModel.Elements.Add(ret);

        RaiseZenUMLModelChanged("zu_returnCreated");
    }

    private void HandleZenUMLReturnEdited(JsonElement root)
    {
        if (_zenUMLModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _zenUMLModel.Elements.Count) return;
        if (_zenUMLModel.Elements[elementIndex] is not ZenUMLReturn ret) return;

        PushUndo();
        if (root.TryGetProperty("value", out var vProp))
            ret.Value = vProp.GetString() ?? "";

        RaiseZenUMLModelChanged("zu_returnEdited");
    }

    private void HandleZenUMLReturnDeleted(JsonElement root)
    {
        if (_zenUMLModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _zenUMLModel.Elements.Count) return;

        PushUndo();
        _zenUMLModel.Elements.RemoveAt(elementIndex);
        RaiseZenUMLModelChanged("zu_returnDeleted");
    }

    private void HandleZenUMLFragmentCreated(JsonElement root)
    {
        if (_zenUMLModel == null) return;

        var fragTypeStr = root.TryGetProperty("fragmentType", out var ftProp) ? ftProp.GetString() ?? "If" : "If";
        var label = root.TryGetProperty("label", out var lblProp) ? lblProp.GetString() ?? "" : "";

        PushUndo();
        var fragType = Enum.TryParse<ZenUMLFragmentType>(fragTypeStr, true, out var parsed)
            ? parsed : ZenUMLFragmentType.If;

        var keyword = fragType switch
        {
            ZenUMLFragmentType.If => "if",
            ZenUMLFragmentType.While => "while",
            ZenUMLFragmentType.For => "for",
            ZenUMLFragmentType.ForEach => "forEach",
            ZenUMLFragmentType.Loop => "loop",
            ZenUMLFragmentType.Opt => "opt",
            ZenUMLFragmentType.Par => "par",
            ZenUMLFragmentType.Try => "try",
            _ => "if"
        };

        var fragment = new ZenUMLFragment
        {
            Type = fragType,
            Label = label
        };
        fragment.Sections.Add(new ZenUMLFragmentSection
        {
            Keyword = keyword,
            Label = label
        });

        int? insertAtIndex = null;
        if (root.TryGetProperty("insertAtIndex", out var idxProp) && idxProp.ValueKind == JsonValueKind.Number)
            insertAtIndex = idxProp.GetInt32();

        if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= _zenUMLModel.Elements.Count)
            _zenUMLModel.Elements.Insert(insertAtIndex.Value, fragment);
        else
            _zenUMLModel.Elements.Add(fragment);

        RaiseZenUMLModelChanged("zu_fragmentCreated");
    }

    private void HandleZenUMLFragmentEdited(JsonElement root)
    {
        if (_zenUMLModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _zenUMLModel.Elements.Count) return;
        if (_zenUMLModel.Elements[elementIndex] is not ZenUMLFragment frag) return;

        PushUndo();
        if (root.TryGetProperty("fragmentType", out var ftProp))
        {
            var ftStr = ftProp.GetString();
            if (ftStr != null && Enum.TryParse<ZenUMLFragmentType>(ftStr, true, out var parsed))
                frag.Type = parsed;
        }
        if (root.TryGetProperty("label", out var lblProp))
        {
            frag.Label = lblProp.GetString() ?? "";
            // Update the first section's label too
            if (frag.Sections.Count > 0)
                frag.Sections[0].Label = frag.Label;
        }

        RaiseZenUMLModelChanged("zu_fragmentEdited");
    }

    private void HandleZenUMLFragmentDeleted(JsonElement root)
    {
        if (_zenUMLModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _zenUMLModel.Elements.Count) return;

        PushUndo();
        _zenUMLModel.Elements.RemoveAt(elementIndex);
        RaiseZenUMLModelChanged("zu_fragmentDeleted");
    }

    private void HandleZenUMLElementReordered(JsonElement root)
    {
        if (_zenUMLModel == null) return;
        var fromIndex = root.GetProperty("fromIndex").GetInt32();
        var toIndex = root.GetProperty("toIndex").GetInt32();
        if (fromIndex < 0 || fromIndex >= _zenUMLModel.Elements.Count) return;
        if (toIndex < 0 || toIndex >= _zenUMLModel.Elements.Count) return;

        PushUndo();
        var element = _zenUMLModel.Elements[fromIndex];
        _zenUMLModel.Elements.RemoveAt(fromIndex);
        _zenUMLModel.Elements.Insert(toIndex, element);
        RaiseZenUMLModelChanged("zu_elementReordered");
    }

    private void HandleZenUMLSettingsChanged(JsonElement root)
    {
        if (_zenUMLModel == null) return;

        PushUndo();
        if (root.TryGetProperty("title", out var tProp))
            _zenUMLModel.Title = tProp.ValueKind == JsonValueKind.Null ? null : tProp.GetString();
        RaiseZenUMLModelChanged("zu_settingsChanged");
    }

    // ========== Helper Methods ==========

    /// <summary>
    /// Updates all message FromId/ToId references when a participant ID changes.
    /// </summary>
    private void UpdateZenUMLParticipantReferences(string oldId, string newId)
    {
        if (_zenUMLModel == null) return;
        UpdateZenUMLElementReferences(_zenUMLModel.Elements, oldId, newId);
    }

    private static void UpdateZenUMLElementReferences(List<ZenUMLElement> elements, string oldId, string newId)
    {
        foreach (var element in elements)
        {
            if (element is ZenUMLMessage msg)
            {
                if (msg.FromId == oldId) msg.FromId = newId;
                if (msg.ToId == oldId) msg.ToId = newId;
                UpdateZenUMLElementReferences(msg.NestedElements, oldId, newId);
            }
            else if (element is ZenUMLFragment frag)
            {
                foreach (var section in frag.Sections)
                    UpdateZenUMLElementReferences(section.Elements, oldId, newId);
            }
        }
    }

    /// <summary>
    /// Removes all messages that reference the given participant ID (recursively).
    /// </summary>
    private static void RemoveZenUMLMessagesForParticipant(List<ZenUMLElement> elements, string participantId)
    {
        elements.RemoveAll(e =>
        {
            if (e is ZenUMLMessage msg)
                return msg.FromId == participantId || msg.ToId == participantId;
            return false;
        });

        // Recursively clean nested elements
        foreach (var element in elements)
        {
            if (element is ZenUMLMessage msg)
                RemoveZenUMLMessagesForParticipant(msg.NestedElements, participantId);
            else if (element is ZenUMLFragment frag)
            {
                foreach (var section in frag.Sections)
                    RemoveZenUMLMessagesForParticipant(section.Elements, participantId);
            }
        }
    }

    // ========== Restore from JSON ==========

    private void RestoreZenUMLModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<ZenUMLDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _zenUMLModel = new ZenUMLModel
        {
            Title = dto.Title,
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        if (dto.Participants != null)
        {
            foreach (var p in dto.Participants)
            {
                _zenUMLModel.Participants.Add(new ZenUMLParticipant
                {
                    Id = p.Id ?? "",
                    Alias = p.Alias,
                    Annotator = Enum.TryParse<ZenUMLAnnotator>(p.Annotator, true, out var ann) ? ann : ZenUMLAnnotator.None,
                    IsExplicit = p.IsExplicit
                });
            }
        }

        // Elements are restored from the flat DTO list — rebuild tree structure
        if (dto.Elements != null)
        {
            int idx = 0;
            _zenUMLModel.Elements = RestoreZenUMLElements(dto.Elements, ref idx, 0);
        }
    }

    /// <summary>
    /// Rebuilds the nested ZenUML element tree from a flat DTO list with depth info.
    /// </summary>
    private static List<ZenUMLElement> RestoreZenUMLElements(List<ZenUMLElementDto> flatList, ref int idx, int depth)
    {
        var elements = new List<ZenUMLElement>();

        while (idx < flatList.Count)
        {
            var dto = flatList[idx];

            // Stop if we hit an end marker or something at a lower depth
            if (dto.ElementType == "blockEnd" || dto.ElementType == "fragmentEnd")
            {
                idx++;
                return elements;
            }

            if (dto.Depth < depth)
                return elements;

            switch (dto.ElementType)
            {
                case "message":
                    var msg = new ZenUMLMessage
                    {
                        FromId = dto.FromId ?? "",
                        ToId = dto.ToId ?? "",
                        Text = dto.Text ?? "",
                        MessageType = Enum.TryParse<ZenUMLMessageType>(dto.MessageType, true, out var mt) ? mt : ZenUMLMessageType.Sync,
                        HasBlock = dto.HasBlock
                    };
                    idx++;
                    if (msg.HasBlock)
                    {
                        msg.NestedElements = RestoreZenUMLElements(flatList, ref idx, depth + 1);
                    }
                    elements.Add(msg);
                    break;

                case "return":
                    elements.Add(new ZenUMLReturn { Value = dto.Text ?? "" });
                    idx++;
                    break;

                case "comment":
                    elements.Add(new ZenUMLComment { Text = dto.Text ?? "" });
                    idx++;
                    break;

                case "fragment":
                    var fragType = Enum.TryParse<ZenUMLFragmentType>(dto.FragmentType, true, out var ft) ? ft : ZenUMLFragmentType.If;
                    var fragment = new ZenUMLFragment
                    {
                        Type = fragType,
                        Label = dto.Text ?? ""
                    };
                    if (dto.Sections != null)
                    {
                        foreach (var sDto in dto.Sections)
                        {
                            fragment.Sections.Add(new ZenUMLFragmentSection
                            {
                                Keyword = sDto.Keyword,
                                Label = sDto.Label
                            });
                        }
                    }
                    idx++;
                    // Restore each section's elements
                    foreach (var section in fragment.Sections)
                    {
                        section.Elements = RestoreZenUMLElements(flatList, ref idx, depth + 1);
                    }
                    elements.Add(fragment);
                    break;

                default:
                    idx++;
                    break;
            }
        }

        return elements;
    }

    private void RaiseZenUMLModelChanged(string changeType)
    {
        if (_zenUMLModel != null)
            ZenUMLModelChanged?.Invoke(this, new ZenUMLModelChangedEventArgs(changeType, _zenUMLModel));
    }

    // ========== ZenUML DTOs ==========

    private class ZenUMLDiagramDto
    {
        public string? Title { get; set; }
        public List<ZenUMLParticipantDto>? Participants { get; set; }
        public List<ZenUMLElementDto>? Elements { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class ZenUMLParticipantDto
    {
        public string? Id { get; set; }
        public string? Alias { get; set; }
        public string? Annotator { get; set; }
        public bool IsExplicit { get; set; }
    }

    private class ZenUMLElementDto
    {
        public string? ElementType { get; set; }
        public string? FromId { get; set; }
        public string? ToId { get; set; }
        public string? Text { get; set; }
        public string? MessageType { get; set; }
        public bool HasBlock { get; set; }
        public string? FragmentType { get; set; }
        public List<ZenUMLFragmentSectionDto>? Sections { get; set; }
        public int Depth { get; set; }
        public int Index { get; set; }
    }

    private class ZenUMLFragmentSectionDto
    {
        public string? Keyword { get; set; }
        public string? Label { get; set; }
    }
}

/// <summary>
/// Event args for when the ZenUMLModel changes via the visual editor.
/// </summary>
public class ZenUMLModelChangedEventArgs : EventArgs
{
    public string ChangeType { get; }
    public ZenUMLModel Model { get; }

    public ZenUMLModelChangedEventArgs(string changeType, ZenUMLModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}
