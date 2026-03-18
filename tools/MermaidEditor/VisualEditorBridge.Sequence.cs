using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// Sequence diagram handlers for the visual editor bridge.
/// Handles participant, message, note, and fragment CRUD, reordering, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== Sequence Diagram Support ==========

    /// <summary>
    /// Sends the current SequenceDiagramModel to the visual editor as JSON.
    /// </summary>
    public async Task SendSequenceDiagramToEditorAsync()
    {
        if (_sequenceModel == null) return;
        var json = ConvertSequenceModelToJson(_sequenceModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadSequenceDiagram({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current sequence model without resetting the view.
    /// Used after toolbar/button-triggered model changes.
    /// </summary>
    public async Task RefreshSequenceDiagramAsync()
    {
        if (_sequenceModel == null) return;
        var json = ConvertSequenceModelToJson(_sequenceModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshSequenceDiagram({escaped})");
    }

    /// <summary>
    /// Updates the sequence model reference and sends to editor.
    /// </summary>
    public async Task UpdateSequenceModelAsync(SequenceDiagramModel newModel)
    {
        _sequenceModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.Sequence;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendSequenceDiagramToEditorAsync();
    }

    /// <summary>
    /// Converts a SequenceDiagramModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertSequenceModelToJson(SequenceDiagramModel model)
    {
        var participants = model.Participants.Select(p => new SeqParticipantDto
        {
            Id = p.Id,
            Alias = p.Alias,
            Type = p.Type.ToString(),
            IsExplicit = p.IsExplicit,
            IsDestroyed = p.IsDestroyed
        }).ToList();

        var elements = FlattenSequenceElements(model.Elements);

        var dto = new SeqDiagramDto
        {
            Participants = participants,
            Elements = elements,
            AutoNumber = model.AutoNumber,
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    /// Flattens sequence elements (including fragment contents) into a list of DTOs.
    /// </summary>
    private static List<SeqElementDto> FlattenSequenceElements(List<SequenceElement> elements)
    {
        var result = new List<SeqElementDto>();
        foreach (var elem in elements)
        {
            switch (elem)
            {
                case SequenceMessage msg:
                    result.Add(new SeqElementDto
                    {
                        ElementType = "message",
                        FromId = msg.FromId,
                        ToId = msg.ToId,
                        Text = msg.Text,
                        ArrowType = msg.ArrowType.ToString(),
                        ActivateTarget = msg.ActivateTarget,
                        DeactivateSource = msg.DeactivateSource
                    });
                    break;

                case SequenceNote note:
                    result.Add(new SeqElementDto
                    {
                        ElementType = "note",
                        Text = note.Text,
                        NotePosition = note.Position.ToString(),
                        OverParticipants = note.OverParticipants
                    });
                    break;

                case SequenceFragment fragment:
                    var fragDto = new SeqElementDto
                    {
                        ElementType = "fragment",
                        FragmentType = fragment.Type.ToString(),
                        Text = fragment.Label,
                        Sections = fragment.Sections.Select(s => new SeqFragmentSectionDto
                        {
                            Label = s.Label,
                            Elements = FlattenSequenceElements(s.Elements)
                        }).ToList(),
                        OverParticipantStart = fragment.OverParticipantStart,
                        OverParticipantEnd = fragment.OverParticipantEnd
                    };
                    result.Add(fragDto);
                    break;

                case SequenceActivation activation:
                    result.Add(new SeqElementDto
                    {
                        ElementType = activation.IsActivate ? "activate" : "deactivate",
                        ParticipantId = activation.ParticipantId
                    });
                    break;
            }
        }
        return result;
    }

    // ========== Sequence Diagram Message Handlers ==========

    private void HandleSeqParticipantReordered(JsonElement root)
    {
        if (_sequenceModel == null) return;
        if (!root.TryGetProperty("participantIds", out var idsArray)) return;

        var newOrder = new List<string>();
        foreach (var id in idsArray.EnumerateArray())
        {
            var pid = id.GetString();
            if (!string.IsNullOrEmpty(pid)) newOrder.Add(pid);
        }

        PushUndo();
        var reordered = new List<SequenceParticipant>();
        foreach (var pid in newOrder)
        {
            var p = _sequenceModel.Participants.Find(x => x.Id == pid);
            if (p != null) reordered.Add(p);
        }
        // Add any participants not in the reorder list (shouldn't happen, but defensive)
        foreach (var p in _sequenceModel.Participants)
        {
            if (!reordered.Contains(p)) reordered.Add(p);
        }
        _sequenceModel.Participants = reordered;
        RaiseSequenceModelChanged("seq_participantReordered");
    }

    private void HandleSeqParticipantEdited(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var participantId = root.GetProperty("participantId").GetString();
        if (string.IsNullOrEmpty(participantId)) return;

        var participant = _sequenceModel.Participants.Find(p => p.Id == participantId);
        if (participant == null) return;

        PushUndo();
        if (root.TryGetProperty("alias", out var aliasProp))
        {
            var alias = aliasProp.GetString();
            participant.Alias = string.IsNullOrEmpty(alias) ? null : alias;
        }
        if (root.TryGetProperty("type", out var typeProp))
        {
            var typeStr = typeProp.GetString();
            if (Enum.TryParse<SequenceParticipantType>(typeStr, out var pType))
                participant.Type = pType;
        }
        RaiseSequenceModelChanged("seq_participantEdited");
    }

    private void HandleSeqParticipantCreated(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var participantId = root.GetProperty("participantId").GetString();
        if (string.IsNullOrEmpty(participantId)) return;

        // Don't create duplicates
        if (_sequenceModel.Participants.Any(p => p.Id == participantId)) return;

        PushUndo();
        var newParticipant = new SequenceParticipant
        {
            Id = participantId,
            IsExplicit = true,
            Type = SequenceParticipantType.Participant
        };

        if (root.TryGetProperty("alias", out var aliasProp))
        {
            var alias = aliasProp.GetString();
            newParticipant.Alias = string.IsNullOrEmpty(alias) ? null : alias;
        }
        if (root.TryGetProperty("type", out var typeProp))
        {
            var typeStr = typeProp.GetString();
            if (Enum.TryParse<SequenceParticipantType>(typeStr, out var pType))
                newParticipant.Type = pType;
        }

        _sequenceModel.Participants.Add(newParticipant);
        RaiseSequenceModelChanged("seq_participantCreated");
    }

    private void HandleSeqParticipantDeleted(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var participantId = root.GetProperty("participantId").GetString();
        if (string.IsNullOrEmpty(participantId)) return;

        PushUndo();
        _sequenceModel.Participants.RemoveAll(p => p.Id == participantId);
        // Remove messages involving this participant
        _sequenceModel.Elements.RemoveAll(e =>
            e is SequenceMessage msg && (msg.FromId == participantId || msg.ToId == participantId));
        RaiseSequenceModelChanged("seq_participantDeleted");
    }

    private void HandleSeqMessageCreated(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var fromId = root.GetProperty("fromId").GetString();
        var toId = root.GetProperty("toId").GetString();
        var text = root.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId)) return;

        PushUndo();

        var arrowType = SequenceArrowType.SolidArrow;
        if (root.TryGetProperty("arrowType", out var arrowProp))
        {
            var arrowStr = arrowProp.GetString();
            if (Enum.TryParse<SequenceArrowType>(arrowStr, out var parsed))
                arrowType = parsed;
        }

        var msg = new SequenceMessage
        {
            FromId = fromId,
            ToId = toId,
            Text = text,
            ArrowType = arrowType
        };

        // Insert at specified index or append
        if (root.TryGetProperty("insertIndex", out var idxProp))
        {
            var idx = idxProp.GetInt32();
            if (idx >= 0 && idx <= _sequenceModel.Elements.Count)
                _sequenceModel.Elements.Insert(idx, msg);
            else
                _sequenceModel.Elements.Add(msg);
        }
        else
        {
            _sequenceModel.Elements.Add(msg);
        }

        RaiseSequenceModelChanged("seq_messageCreated");
    }

    private void HandleSeqMessageEdited(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _sequenceModel.Elements.Count) return;
        if (_sequenceModel.Elements[elementIndex] is not SequenceMessage msg) return;

        PushUndo();
        if (root.TryGetProperty("fromId", out var fromProp))
            msg.FromId = fromProp.GetString() ?? msg.FromId;
        if (root.TryGetProperty("toId", out var toProp))
            msg.ToId = toProp.GetString() ?? msg.ToId;
        if (root.TryGetProperty("text", out var textProp))
            msg.Text = textProp.GetString() ?? "";
        if (root.TryGetProperty("arrowType", out var arrowProp))
        {
            var arrowStr = arrowProp.GetString();
            if (Enum.TryParse<SequenceArrowType>(arrowStr, out var parsed))
                msg.ArrowType = parsed;
        }
        RaiseSequenceModelChanged("seq_messageEdited");
    }

    private void HandleSeqMessageDeleted(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _sequenceModel.Elements.Count) return;

        PushUndo();
        _sequenceModel.Elements.RemoveAt(elementIndex);
        RaiseSequenceModelChanged("seq_messageDeleted");
    }

    /// <summary>
    /// Resolves a message element nested inside a fragment section.
    /// </summary>
    private SequenceMessage? GetFragmentInnerMessage(int elementIndex, int sectionIndex, int subIndex)
    {
        if (_sequenceModel == null) return null;
        if (elementIndex < 0 || elementIndex >= _sequenceModel.Elements.Count) return null;
        if (_sequenceModel.Elements[elementIndex] is not SequenceFragment frag) return null;
        if (sectionIndex < 0 || sectionIndex >= frag.Sections.Count) return null;
        var section = frag.Sections[sectionIndex];
        if (subIndex < 0 || subIndex >= section.Elements.Count) return null;
        return section.Elements[subIndex] as SequenceMessage;
    }

    private void HandleSeqFragmentInnerMessageEdited(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();
        var sectionIndex = root.GetProperty("sectionIndex").GetInt32();
        var subIndex = root.GetProperty("subIndex").GetInt32();

        var msg = GetFragmentInnerMessage(elementIndex, sectionIndex, subIndex);
        if (msg == null) return;

        PushUndo();
        if (root.TryGetProperty("fromId", out var fromProp))
            msg.FromId = fromProp.GetString() ?? msg.FromId;
        if (root.TryGetProperty("toId", out var toProp))
            msg.ToId = toProp.GetString() ?? msg.ToId;
        if (root.TryGetProperty("text", out var textProp))
            msg.Text = textProp.GetString() ?? "";
        if (root.TryGetProperty("arrowType", out var arrowProp))
        {
            var arrowStr = arrowProp.GetString();
            if (Enum.TryParse<SequenceArrowType>(arrowStr, out var parsed))
                msg.ArrowType = parsed;
        }
        RaiseSequenceModelChanged("seq_fragmentInnerMessageEdited");
    }

    private void HandleSeqFragmentInnerMessageDeleted(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();
        var sectionIndex = root.GetProperty("sectionIndex").GetInt32();
        var subIndex = root.GetProperty("subIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _sequenceModel.Elements.Count) return;
        if (_sequenceModel.Elements[elementIndex] is not SequenceFragment frag) return;
        if (sectionIndex < 0 || sectionIndex >= frag.Sections.Count) return;
        var section = frag.Sections[sectionIndex];
        if (subIndex < 0 || subIndex >= section.Elements.Count) return;

        PushUndo();
        section.Elements.RemoveAt(subIndex);
        RaiseSequenceModelChanged("seq_fragmentInnerMessageDeleted");
    }

    private void HandleSeqFragmentInnerMessageCreated(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();
        var sectionIndex = root.GetProperty("sectionIndex").GetInt32();
        var subIndex = root.GetProperty("subIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _sequenceModel.Elements.Count) return;
        if (_sequenceModel.Elements[elementIndex] is not SequenceFragment frag) return;
        if (sectionIndex < 0 || sectionIndex >= frag.Sections.Count) return;
        var section = frag.Sections[sectionIndex];

        var fromId = root.TryGetProperty("fromId", out var fromProp) ? fromProp.GetString() ?? "" : "";
        var toId = root.TryGetProperty("toId", out var toProp) ? toProp.GetString() ?? "" : "";
        var text = root.TryGetProperty("text", out var txtProp) ? txtProp.GetString() ?? "Message" : "Message";
        var arrowType = SequenceArrowType.SolidArrow;
        if (root.TryGetProperty("arrowType", out var arrProp))
        {
            var arrowStr = arrProp.GetString();
            if (Enum.TryParse<SequenceArrowType>(arrowStr, out var parsed))
                arrowType = parsed;
        }

        PushUndo();
        var msg = new SequenceMessage
        {
            FromId = fromId,
            ToId = toId,
            Text = text,
            ArrowType = arrowType
        };

        if (subIndex >= 0 && subIndex <= section.Elements.Count)
            section.Elements.Insert(subIndex, msg);
        else
            section.Elements.Add(msg);

        RaiseSequenceModelChanged("seq_fragmentInnerMessageCreated");
    }

    /// <summary>
    /// Moves a top-level message into a fragment section.
    /// Removes the message from elements[] and appends it to the target fragment's section.
    /// </summary>
    private void HandleSeqMessageMovedToFragment(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var messageIndex = root.GetProperty("messageIndex").GetInt32();
        var fragmentIndex = root.GetProperty("fragmentIndex").GetInt32();
        var sectionIndex = root.TryGetProperty("sectionIndex", out var secProp) ? secProp.GetInt32() : 0;

        if (messageIndex < 0 || messageIndex >= _sequenceModel.Elements.Count) return;
        if (_sequenceModel.Elements[messageIndex] is not SequenceMessage msg) return;

        // The fragment index might shift after removal if the message is before the fragment
        var adjustedFragIndex = messageIndex < fragmentIndex ? fragmentIndex - 1 : fragmentIndex;

        if (adjustedFragIndex < 0 || adjustedFragIndex >= _sequenceModel.Elements.Count - 1) return;

        // Peek at the target before removing to validate
        var targetEl = messageIndex < fragmentIndex
            ? _sequenceModel.Elements[fragmentIndex]
            : _sequenceModel.Elements[fragmentIndex];
        if (targetEl is not SequenceFragment frag) return;
        if (sectionIndex < 0 || sectionIndex >= frag.Sections.Count) return;

        PushUndo();
        // Remove the message from top-level elements
        _sequenceModel.Elements.RemoveAt(messageIndex);

        // If the fragment has a manual participant range, remap the message's from/to
        // to fit within that range (so the preview matches the visual editor)
        RemapMessageToFragmentRange(msg, frag);

        // Add it to the target fragment section
        frag.Sections[sectionIndex].Elements.Add(msg);
        RaiseSequenceModelChanged("seq_messageMovedToFragment");
    }

    private void HandleSeqNoteCreated(JsonElement root)
    {
        if (_sequenceModel == null) return;

        var positionStr = root.TryGetProperty("position", out var posProp) ? posProp.GetString() ?? "RightOf" : "RightOf";
        var overParticipants = root.TryGetProperty("overParticipants", out var overProp) ? overProp.GetString() ?? "" : "";
        var text = root.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "Note" : "Note";

        PushUndo();
        var note = new SequenceNote
        {
            Text = text,
            OverParticipants = overParticipants
        };
        if (Enum.TryParse<SequenceNotePosition>(positionStr, out var pos))
            note.Position = pos;

        // Insert at specified index or append
        if (root.TryGetProperty("insertIndex", out var noteIdxProp))
        {
            var idx = noteIdxProp.GetInt32();
            if (idx >= 0 && idx <= _sequenceModel.Elements.Count)
                _sequenceModel.Elements.Insert(idx, note);
            else
                _sequenceModel.Elements.Add(note);
        }
        else
        {
            _sequenceModel.Elements.Add(note);
        }
        RaiseSequenceModelChanged("seq_noteCreated");
    }

    private void HandleSeqNoteEdited(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _sequenceModel.Elements.Count) return;
        if (_sequenceModel.Elements[elementIndex] is not SequenceNote note) return;

        PushUndo();
        if (root.TryGetProperty("text", out var textProp))
            note.Text = textProp.GetString() ?? "";
        if (root.TryGetProperty("position", out var posProp))
        {
            var posStr = posProp.GetString();
            if (Enum.TryParse<SequenceNotePosition>(posStr, out var parsed))
                note.Position = parsed;
        }
        if (root.TryGetProperty("overParticipants", out var overProp))
            note.OverParticipants = overProp.GetString() ?? "";
        RaiseSequenceModelChanged("seq_noteEdited");
    }

    private void HandleSeqNoteDeleted(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _sequenceModel.Elements.Count) return;

        PushUndo();
        _sequenceModel.Elements.RemoveAt(elementIndex);
        RaiseSequenceModelChanged("seq_noteDeleted");
    }

    private void HandleSeqFragmentCreated(JsonElement root)
    {
        if (_sequenceModel == null) return;

        var fragTypeStr = root.TryGetProperty("fragmentType", out var ftProp) ? ftProp.GetString() ?? "alt" : "alt";
        var condition = root.TryGetProperty("condition", out var condProp) ? condProp.GetString() ?? "" : "";

        PushUndo();
        var fragment = new SequenceFragment
        {
            Label = condition,
            OverParticipantStart = root.TryGetProperty("overParticipantStart", out var opsProp) ? opsProp.GetString() : null,
            OverParticipantEnd = root.TryGetProperty("overParticipantEnd", out var opeProp) ? opeProp.GetString() : null
        };
        if (Enum.TryParse<SequenceFragmentType>(fragTypeStr, true, out var fType))
            fragment.Type = fType;

        // Build sections from JSON
        if (root.TryGetProperty("sections", out var sectionsProp) && sectionsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var secEl in sectionsProp.EnumerateArray())
            {
                var section = new SequenceFragmentSection
                {
                    Label = secEl.TryGetProperty("label", out var lblProp) ? lblProp.GetString() : null
                };

                if (secEl.TryGetProperty("elements", out var elemsProp) && elemsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in elemsProp.EnumerateArray())
                    {
                        var elemType = elem.TryGetProperty("elementType", out var etProp) ? etProp.GetString() : null;
                        if (elemType == "message")
                        {
                            var fromId = elem.TryGetProperty("fromId", out var fProp) ? fProp.GetString() ?? "" : "";
                            var toId = elem.TryGetProperty("toId", out var tProp) ? tProp.GetString() ?? "" : "";
                            var msgText = elem.TryGetProperty("text", out var mtProp) ? mtProp.GetString() ?? "" : "";
                            var arrowType = SequenceArrowType.SolidArrow;
                            if (elem.TryGetProperty("arrowType", out var atProp))
                            {
                                if (Enum.TryParse<SequenceArrowType>(atProp.GetString(), out var parsed))
                                    arrowType = parsed;
                            }
                            section.Elements.Add(new SequenceMessage
                            {
                                FromId = fromId,
                                ToId = toId,
                                Text = msgText,
                                ArrowType = arrowType
                            });
                        }
                    }
                }

                fragment.Sections.Add(section);
            }
        }
        else
        {
            // Default: single section with no elements
            fragment.Sections.Add(new SequenceFragmentSection { Label = condition });
        }

        // Insert at specified index or append
        if (root.TryGetProperty("insertIndex", out var fragIdxProp))
        {
            var idx = fragIdxProp.GetInt32();
            if (idx >= 0 && idx <= _sequenceModel.Elements.Count)
                _sequenceModel.Elements.Insert(idx, fragment);
            else
                _sequenceModel.Elements.Add(fragment);
        }
        else
        {
            _sequenceModel.Elements.Add(fragment);
        }
        RaiseSequenceModelChanged("seq_fragmentCreated");
    }

    private void HandleSeqFragmentEdited(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _sequenceModel.Elements.Count) return;
        if (_sequenceModel.Elements[elementIndex] is not SequenceFragment frag) return;

        PushUndo();
        if (root.TryGetProperty("fragmentType", out var ftProp))
        {
            var ftStr = ftProp.GetString();
            if (ftStr != null && Enum.TryParse<SequenceFragmentType>(ftStr, true, out var parsed))
            {
                frag.Type = parsed;

                // Multi-section types: Alt (else), Par (and), Critical (option)
                // Single-section types: Loop, Opt, Break, Rect
                bool supportsMultipleSections = parsed is SequenceFragmentType.Alt
                    or SequenceFragmentType.Par
                    or SequenceFragmentType.Critical;

                if (!supportsMultipleSections && frag.Sections.Count > 1)
                {
                    // Merge all child elements into the first section, then remove extras
                    var firstSection = frag.Sections[0];
                    for (int i = 1; i < frag.Sections.Count; i++)
                    {
                        foreach (var el in frag.Sections[i].Elements)
                            firstSection.Elements.Add(el);
                    }
                    frag.Sections.RemoveRange(1, frag.Sections.Count - 1);
                }
            }
        }
        if (root.TryGetProperty("text", out var textProp))
            frag.Label = textProp.GetString() ?? "";
        if (root.TryGetProperty("overParticipantStart", out var opsProp))
            frag.OverParticipantStart = opsProp.GetString();
        if (root.TryGetProperty("overParticipantEnd", out var opeProp))
            frag.OverParticipantEnd = opeProp.GetString();

        // Auto-eject messages that fall outside the new participant range
        EjectMessagesOutsideFragmentRange(frag, elementIndex);

        RaiseSequenceModelChanged("seq_fragmentEdited");
    }

    /// <summary>
    /// When a fragment's participant range (overParticipantStart/overParticipantEnd) is set,
    /// eject any inner messages whose from/to participants both fall outside the new range.
    /// Ejected messages become top-level elements inserted right after the fragment.
    /// </summary>
    private void EjectMessagesOutsideFragmentRange(SequenceFragment frag, int fragmentElementIndex)
    {
        if (_sequenceModel == null) return;

        // Only eject if a manual range is set
        var startId = frag.OverParticipantStart;
        var endId = frag.OverParticipantEnd;
        if (string.IsNullOrEmpty(startId) && string.IsNullOrEmpty(endId)) return;

        // Build participant index lookup
        var participantIndices = new Dictionary<string, int>();
        for (int i = 0; i < _sequenceModel.Participants.Count; i++)
            participantIndices[_sequenceModel.Participants[i].Id] = i;

        // Determine the valid range of participant indices
        int minIdx = 0;
        int maxIdx = _sequenceModel.Participants.Count - 1;
        if (!string.IsNullOrEmpty(startId) && participantIndices.TryGetValue(startId, out var si))
            minIdx = si;
        if (!string.IsNullOrEmpty(endId) && participantIndices.TryGetValue(endId, out var ei))
            maxIdx = ei;
        // Ensure min <= max (swap if backwards)
        if (minIdx > maxIdx) (minIdx, maxIdx) = (maxIdx, minIdx);

        // Collect messages to eject from each section
        var ejected = new List<SequenceElement>();
        foreach (var section in frag.Sections)
        {
            var toRemove = new List<SequenceElement>();
            foreach (var el in section.Elements)
            {
                if (el is SequenceMessage msg)
                {
                    bool fromInRange = participantIndices.TryGetValue(msg.FromId, out var fi) && fi >= minIdx && fi <= maxIdx;
                    bool toInRange = participantIndices.TryGetValue(msg.ToId, out var ti) && ti >= minIdx && ti <= maxIdx;
                    // Eject if NEITHER endpoint is within the fragment's range
                    if (!fromInRange && !toInRange)
                    {
                        toRemove.Add(el);
                    }
                }
            }
            foreach (var el in toRemove)
            {
                section.Elements.Remove(el);
                ejected.Add(el);
            }
        }

        // Insert ejected messages as top-level elements right after the fragment
        if (ejected.Count > 0)
        {
            var insertIdx = fragmentElementIndex + 1;
            foreach (var el in ejected)
            {
                _sequenceModel.Elements.Insert(insertIdx, el);
                insertIdx++;
            }
        }
    }

    /// <summary>
    /// When a message is moved into a fragment that has a manual participant range,
    /// remap the message's FromId/ToId so they fall within the fragment's range.
    /// If both endpoints are outside the range, set both to the fragment's start/end.
    /// If only one endpoint is outside, clamp it to the nearest edge of the range.
    /// </summary>
    private void RemapMessageToFragmentRange(SequenceMessage msg, SequenceFragment frag)
    {
        if (_sequenceModel == null) return;
        var startId = frag.OverParticipantStart;
        var endId = frag.OverParticipantEnd;
        if (string.IsNullOrEmpty(startId) && string.IsNullOrEmpty(endId)) return;

        // Build participant index lookup
        var participantIndices = new Dictionary<string, int>();
        for (int i = 0; i < _sequenceModel.Participants.Count; i++)
            participantIndices[_sequenceModel.Participants[i].Id] = i;

        int minIdx = 0;
        int maxIdx = _sequenceModel.Participants.Count - 1;
        if (!string.IsNullOrEmpty(startId) && participantIndices.TryGetValue(startId, out var si))
            minIdx = si;
        if (!string.IsNullOrEmpty(endId) && participantIndices.TryGetValue(endId, out var ei))
            maxIdx = ei;
        if (minIdx > maxIdx) (minIdx, maxIdx) = (maxIdx, minIdx);

        bool fromInRange = participantIndices.TryGetValue(msg.FromId, out var fi) && fi >= minIdx && fi <= maxIdx;
        bool toInRange = participantIndices.TryGetValue(msg.ToId, out var ti) && ti >= minIdx && ti <= maxIdx;

        if (!fromInRange)
        {
            // Clamp to nearest edge of fragment range
            msg.FromId = _sequenceModel.Participants[minIdx].Id;
        }
        if (!toInRange)
        {
            msg.ToId = _sequenceModel.Participants[maxIdx].Id;
        }
        // Avoid self-message if from and to got clamped to same participant
        if (msg.FromId == msg.ToId && minIdx != maxIdx)
        {
            if (!fromInRange)
                msg.FromId = _sequenceModel.Participants[minIdx].Id;
            if (!toInRange)
                msg.ToId = _sequenceModel.Participants[maxIdx].Id;
        }
    }

    private void HandleSeqFragmentDeleted(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _sequenceModel.Elements.Count) return;

        PushUndo();
        _sequenceModel.Elements.RemoveAt(elementIndex);
        RaiseSequenceModelChanged("seq_fragmentDeleted");
    }

    private void HandleSeqFragmentSectionEdited(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();
        var sectionIndex = root.GetProperty("sectionIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _sequenceModel.Elements.Count) return;
        if (_sequenceModel.Elements[elementIndex] is not SequenceFragment frag) return;
        if (sectionIndex < 0 || sectionIndex >= frag.Sections.Count) return;

        PushUndo();
        if (root.TryGetProperty("label", out var labelProp))
            frag.Sections[sectionIndex].Label = labelProp.GetString();

        RaiseSequenceModelChanged("seq_fragmentSectionEdited");
    }

    private void HandleSeqFragmentSectionAdded(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var elementIndex = root.GetProperty("elementIndex").GetInt32();

        if (elementIndex < 0 || elementIndex >= _sequenceModel.Elements.Count) return;
        if (_sequenceModel.Elements[elementIndex] is not SequenceFragment frag) return;

        PushUndo();
        frag.Sections.Add(new SequenceFragmentSection { Label = "else" });
        RaiseSequenceModelChanged("seq_fragmentSectionAdded");
    }

    private void HandleSeqElementReordered(JsonElement root)
    {
        if (_sequenceModel == null) return;
        var fromIndex = root.GetProperty("fromIndex").GetInt32();
        var toIndex = root.GetProperty("toIndex").GetInt32();

        if (fromIndex < 0 || fromIndex >= _sequenceModel.Elements.Count) return;
        if (toIndex < 0 || toIndex >= _sequenceModel.Elements.Count) return;
        if (fromIndex == toIndex) return;

        PushUndo();
        var element = _sequenceModel.Elements[fromIndex];
        _sequenceModel.Elements.RemoveAt(fromIndex);
        _sequenceModel.Elements.Insert(toIndex, element);
        RaiseSequenceModelChanged("seq_elementReordered");
    }

    // ========== Sequence Model Restore (for undo/redo) ==========

    private void RestoreSequenceModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<SeqDiagramDto>(json, JsonOptions);
        if (dto == null || _sequenceModel == null) return;

        _sequenceModel.AutoNumber = dto.AutoNumber;
        _sequenceModel.PreambleLines = dto.PreambleLines ?? new List<string>();
        _sequenceModel.DeclarationLineIndex = dto.DeclarationLineIndex;
        _sequenceModel.Participants.Clear();
        _sequenceModel.Elements.Clear();

        if (dto.Participants != null)
        {
            foreach (var p in dto.Participants)
            {
                var pType = Enum.TryParse<SequenceParticipantType>(p.Type, out var parsed)
                    ? parsed : SequenceParticipantType.Participant;
                _sequenceModel.Participants.Add(new SequenceParticipant
                {
                    Id = p.Id ?? string.Empty,
                    Alias = p.Alias,
                    Type = pType,
                    IsExplicit = p.IsExplicit,
                    IsDestroyed = p.IsDestroyed
                });
            }
        }

        if (dto.Elements != null)
        {
            _sequenceModel.Elements = RestoreSequenceElements(dto.Elements);
        }
    }

    private static List<SequenceElement> RestoreSequenceElements(List<SeqElementDto> dtos)
    {
        var result = new List<SequenceElement>();
        foreach (var dto in dtos)
        {
            switch (dto.ElementType)
            {
                case "message":
                    var arrowType = Enum.TryParse<SequenceArrowType>(dto.ArrowType, out var aParsed)
                        ? aParsed : SequenceArrowType.SolidArrow;
                    result.Add(new SequenceMessage
                    {
                        FromId = dto.FromId ?? string.Empty,
                        ToId = dto.ToId ?? string.Empty,
                        Text = dto.Text ?? string.Empty,
                        ArrowType = arrowType,
                        ActivateTarget = dto.ActivateTarget,
                        DeactivateSource = dto.DeactivateSource
                    });
                    break;

                case "note":
                    var notePos = Enum.TryParse<SequenceNotePosition>(dto.NotePosition, out var nParsed)
                        ? nParsed : SequenceNotePosition.RightOf;
                    result.Add(new SequenceNote
                    {
                        Text = dto.Text ?? string.Empty,
                        Position = notePos,
                        OverParticipants = dto.OverParticipants ?? string.Empty
                    });
                    break;

                case "fragment":
                    var fragType = Enum.TryParse<SequenceFragmentType>(dto.FragmentType, out var fParsed)
                        ? fParsed : SequenceFragmentType.Loop;
                    var fragment = new SequenceFragment
                    {
                        Type = fragType,
                        Label = dto.Text ?? string.Empty,
                        OverParticipantStart = dto.OverParticipantStart,
                        OverParticipantEnd = dto.OverParticipantEnd
                    };
                    if (dto.Sections != null)
                    {
                        fragment.Sections = dto.Sections.Select(s => new SequenceFragmentSection
                        {
                            Label = s.Label,
                            Elements = RestoreSequenceElements(s.Elements ?? new List<SeqElementDto>())
                        }).ToList();
                    }
                    result.Add(fragment);
                    break;

                case "activate":
                    result.Add(new SequenceActivation
                    {
                        ParticipantId = dto.ParticipantId ?? string.Empty,
                        IsActivate = true
                    });
                    break;

                case "deactivate":
                    result.Add(new SequenceActivation
                    {
                        ParticipantId = dto.ParticipantId ?? string.Empty,
                        IsActivate = false
                    });
                    break;
            }
        }
        return result;
    }

    // ========== Sequence Diagram DTOs ==========

    // ========== Sequence Diagram DTOs ==========

    private class SeqDiagramDto
    {
        public List<SeqParticipantDto>? Participants { get; set; }
        public List<SeqElementDto>? Elements { get; set; }
        public bool AutoNumber { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class SeqParticipantDto
    {
        public string? Id { get; set; }
        public string? Alias { get; set; }
        public string? Type { get; set; }
        public bool IsExplicit { get; set; }
        public bool IsDestroyed { get; set; }
    }

    private class SeqElementDto
    {
        public string? ElementType { get; set; }
        // Message fields
        public string? FromId { get; set; }
        public string? ToId { get; set; }
        public string? Text { get; set; }
        public string? ArrowType { get; set; }
        public bool ActivateTarget { get; set; }
        public bool DeactivateSource { get; set; }
        // Note fields
        public string? NotePosition { get; set; }
        public string? OverParticipants { get; set; }
        // Fragment fields
        public string? FragmentType { get; set; }
        public List<SeqFragmentSectionDto>? Sections { get; set; }
        public string? OverParticipantStart { get; set; }
        public string? OverParticipantEnd { get; set; }
        // Activation fields
        public string? ParticipantId { get; set; }
    }

    private class SeqFragmentSectionDto
    {
        public string? Label { get; set; }
        public List<SeqElementDto>? Elements { get; set; }
    }
}
