using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// State diagram handlers for the visual editor bridge.
/// Handles state CRUD, nested states, transitions, notes, auto-layout, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== State Diagram Support ==========

    /// <summary>
    /// Sends the current StateDiagramModel to the visual editor as JSON.
    /// </summary>
    public async Task SendStateDiagramToEditorAsync()
    {
        if (_stateDiagramModel == null) return;
        var json = ConvertStateDiagramModelToJson(_stateDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadStateDiagram({escaped})");
    }

    /// <summary>
    /// Restores the state diagram for undo/redo (preserves all positions).
    /// </summary>
    private async Task RestoreStateDiagramToEditorAsync()
    {
        if (_stateDiagramModel == null) return;
        var json = ConvertStateDiagramModelToJson(_stateDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreStateDiagram({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current state diagram model without resetting the view.
    /// </summary>
    public async Task RefreshStateDiagramAsync()
    {
        if (_stateDiagramModel == null) return;
        var json = ConvertStateDiagramModelToJson(_stateDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshStateDiagram({escaped})");
    }

    /// <summary>
    /// Updates the state diagram model reference and sends to editor.
    /// </summary>
    public async Task UpdateStateDiagramModelAsync(StateDiagramModel newModel)
    {
        _stateDiagramModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.StateDiagram;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendStateDiagramToEditorAsync();
    }

    /// <summary>
    /// Converts a StateDiagramModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertStateDiagramModelToJson(StateDiagramModel model)
    {
        var states = model.States.Select(s => ConvertStateToDto(s)).ToList();

        var transitions = model.Transitions.Select(t => new StDiagTransitionDto
        {
            FromId = t.FromId,
            ToId = t.ToId,
            Label = t.Label
        }).ToList();

        var notes = model.Notes.Select(n => new StDiagNoteDto
        {
            Text = n.Text,
            StateId = n.StateId,
            Position = n.Position.ToString()
        }).ToList();

        // Convert pseudo node positions to serializable format [x, y]
        var pseudoPositions = model.PseudoNodePositions.ToDictionary(
            kvp => kvp.Key,
            kvp => new[] { kvp.Value.X, kvp.Value.Y }
        );

        // Convert note positions to serializable format [x, y]
        var notePositions = model.NotePositions.ToDictionary(
            kvp => kvp.Key,
            kvp => new[] { kvp.Value.X, kvp.Value.Y }
        );

        var dto = new StDiagramDto
        {
            Direction = model.Direction,
            IsV2 = model.IsV2,
            States = states,
            Transitions = transitions,
            Notes = notes,
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex,
            PseudoNodePositions = pseudoPositions,
            NotePositions = notePositions
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static StDiagStateDto ConvertStateToDto(StateDefinition state)
    {
        return new StDiagStateDto
        {
            Id = state.Id,
            Label = state.Label,
            Type = state.Type.ToString(),
            CssClass = state.CssClass,
            IsExplicit = state.IsExplicit,
            NestedStates = state.NestedStates.Select(ns => ConvertStateToDto(ns)).ToList(),
            NestedTransitions = state.NestedTransitions.Select(t => new StDiagTransitionDto
            {
                FromId = t.FromId,
                ToId = t.ToId,
                Label = t.Label
            }).ToList(),
            X = state.Position.X,
            Y = state.Position.Y,
            Width = state.Size.Width,
            Height = state.Size.Height,
            HasManualPosition = state.HasManualPosition
        };
    }

    // ========== State Diagram Auto-Layout & Move ==========

    private void HandleStAutoLayoutComplete(JsonElement root)
    {
        if (!root.TryGetProperty("positions", out var positionsArray))
        {
            RaiseStateDiagramModelChanged("st_autoLayoutComplete");
            return;
        }

        PushUndo();
        foreach (var pos in positionsArray.EnumerateArray())
        {
            var stateId = pos.GetProperty("stateId").GetString();
            if (string.IsNullOrEmpty(stateId)) continue;

            StateDefinition? state = null;
            if (_stateDiagramModel != null)
            {
                foreach (var s in _stateDiagramModel.States)
                {
                    if (s.Id == stateId) { state = s; break; }
                    state = FindStateRecursive(s.NestedStates, stateId);
                    if (state != null) break;
                }
            }
            if (state == null) continue;

            state.Position = new System.Windows.Point(
                pos.GetProperty("x").GetDouble(),
                pos.GetProperty("y").GetDouble()
            );
            state.HasManualPosition = true;
        }

        RaiseStateDiagramModelChanged("st_autoLayoutComplete");
    }

    private void HandleStAllPositionsUpdate(JsonElement root)
    {
        if (!root.TryGetProperty("positions", out var positionsArray))
            return;

        if (_stateDiagramModel == null) return;

        foreach (var pos in positionsArray.EnumerateArray())
        {
            var stateId = pos.GetProperty("stateId").GetString();
            if (string.IsNullOrEmpty(stateId)) continue;

            // Store pseudo-state positions separately (they don't have StateDefinition objects)
            if (stateId.StartsWith("[*]_"))
            {
                _stateDiagramModel.PseudoNodePositions[stateId] = new System.Windows.Point(
                    pos.GetProperty("x").GetDouble(),
                    pos.GetProperty("y").GetDouble()
                );
                continue;
            }

            // Store note positions separately
            if (stateId.StartsWith("note_"))
            {
                _stateDiagramModel.NotePositions[stateId] = new System.Windows.Point(
                    pos.GetProperty("x").GetDouble(),
                    pos.GetProperty("y").GetDouble()
                );
                continue;
            }

            StateDefinition? state = null;
            foreach (var s in _stateDiagramModel.States)
            {
                if (s.Id == stateId) { state = s; break; }
                state = FindStateRecursive(s.NestedStates, stateId);
                if (state != null) break;
            }
            if (state == null) continue;

            state.Position = new System.Windows.Point(
                pos.GetProperty("x").GetDouble(),
                pos.GetProperty("y").GetDouble()
            );

            // Save width/height (primarily for composite states to preserve container dimensions)
            if (pos.TryGetProperty("width", out var wProp) && pos.TryGetProperty("height", out var hProp))
            {
                var w = wProp.GetDouble();
                var h = hProp.GetDouble();
                if (w > 0 && h > 0)
                    state.Size = new System.Windows.Size(w, h);
            }

            state.HasManualPosition = true;
        }

        // Don't push undo or raise model changed — the st_stateMoved handler already did that
    }

    private void HandleStStateMoved(JsonElement root)
    {
        var stateId = root.GetProperty("stateId").GetString();
        var x = root.GetProperty("x").GetDouble();
        var y = root.GetProperty("y").GetDouble();

        if (string.IsNullOrEmpty(stateId)) return;

        StateDefinition? state = null;
        if (_stateDiagramModel != null)
        {
            foreach (var s in _stateDiagramModel.States)
            {
                if (s.Id == stateId) { state = s; break; }
                state = FindStateRecursive(s.NestedStates, stateId);
                if (state != null) break;
            }
        }
        if (state == null) return;

        PushUndo();
        state.Position = new System.Windows.Point(x, y);
        state.HasManualPosition = true;
        RaiseStateDiagramModelChanged("st_stateMoved");
    }

    // ========== State Diagram Message Handlers ==========

    private void HandleStStateCreated(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var stateId = root.GetProperty("stateId").GetString();
        if (string.IsNullOrEmpty(stateId)) return;
        if (_stateDiagramModel.States.Any(s => s.Id == stateId)) return;

        PushUndo();
        var newState = new StateDefinition
        {
            Id = stateId,
            IsExplicit = true,
            Type = StateType.Simple
        };
        if (root.TryGetProperty("label", out var labelProp))
            newState.Label = labelProp.GetString();
        if (root.TryGetProperty("stateType", out var typeProp))
        {
            var typeStr = typeProp.GetString();
            if (Enum.TryParse<StateType>(typeStr, true, out var st))
                newState.Type = st;
        }
        _stateDiagramModel.States.Add(newState);
        RaiseStateDiagramModelChanged("st_stateCreated");
    }

    private void HandleStNestedStateCreated(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var parentId = root.GetProperty("parentId").GetString();
        var stateId = root.GetProperty("stateId").GetString();
        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(stateId)) return;

        var parent = FindStateRecursive(_stateDiagramModel.States, parentId);
        if (parent == null) return;
        if (parent.NestedStates.Any(s => s.Id == stateId)) return;

        PushUndo();
        // Ensure the parent is marked as Composite
        if (parent.Type == StateType.Simple)
            parent.Type = StateType.Composite;

        var newState = new StateDefinition
        {
            Id = stateId,
            IsExplicit = true,
            Type = StateType.Simple
        };
        if (root.TryGetProperty("label", out var labelProp))
            newState.Label = labelProp.GetString();
        parent.NestedStates.Add(newState);
        RaiseStateDiagramModelChanged("st_nestedStateCreated");
    }

    private void HandleStInsertStateOnEdge(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        var stateId = root.GetProperty("stateId").GetString();
        if (string.IsNullOrEmpty(stateId)) return;
        if (index < 0 || index >= _stateDiagramModel.Transitions.Count) return;

        PushUndo();
        var oldTransition = _stateDiagramModel.Transitions[index];
        var originalFrom = oldTransition.FromId;
        var originalTo = oldTransition.ToId;
        var originalLabel = oldTransition.Label;

        // Create the new state
        var newState = new StateDefinition
        {
            Id = stateId,
            IsExplicit = true,
            Type = StateType.Simple
        };
        if (root.TryGetProperty("label", out var labelProp))
            newState.Label = labelProp.GetString();
        _stateDiagramModel.States.Add(newState);

        // Replace old transition: originalFrom -> newState (keep original label)
        oldTransition.ToId = stateId;

        // Add new transition: newState -> originalTo (no label)
        _stateDiagramModel.Transitions.Insert(index + 1, new StateTransition
        {
            FromId = stateId,
            ToId = originalTo
        });

        RaiseStateDiagramModelChanged("st_insertStateOnEdge");
    }

    private void HandleStInsertPseudoOnEdge(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _stateDiagramModel.Transitions.Count) return;

        PushUndo();
        var oldTransition = _stateDiagramModel.Transitions[index];
        var originalTo = oldTransition.ToId;

        // Replace old transition: originalFrom -> [*] (end)
        oldTransition.ToId = "[*]";

        // Add new transition: [*] (start) -> originalTo
        _stateDiagramModel.Transitions.Insert(index + 1, new StateTransition
        {
            FromId = "[*]",
            ToId = originalTo
        });

        RaiseStateDiagramModelChanged("st_insertPseudoOnEdge");
    }

    /// <summary>Insert a special state (Fork/Join/Choice) on an existing top-level transition edge, splitting it into two.</summary>
    private void HandleStInsertSpecialStateOnEdge(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        var stateId = root.GetProperty("stateId").GetString();
        var stateTypeStr = root.GetProperty("stateType").GetString();
        if (string.IsNullOrEmpty(stateId) || string.IsNullOrEmpty(stateTypeStr)) return;
        if (index < 0 || index >= _stateDiagramModel.Transitions.Count) return;
        if (!Enum.TryParse<StateType>(stateTypeStr, true, out var stateType)) return;

        PushUndo();
        var oldTransition = _stateDiagramModel.Transitions[index];
        var originalTo = oldTransition.ToId;

        // Create the new special state
        var newState = new StateDefinition
        {
            Id = stateId,
            IsExplicit = true,
            Type = stateType
        };
        _stateDiagramModel.States.Add(newState);

        // Replace old transition: originalFrom -> newState (keep original label)
        oldTransition.ToId = stateId;

        // Add new transition: newState -> originalTo (no label)
        _stateDiagramModel.Transitions.Insert(index + 1, new StateTransition
        {
            FromId = stateId,
            ToId = originalTo
        });

        RaiseStateDiagramModelChanged("st_insertSpecialStateOnEdge");
    }

    /// <summary>Insert a special state (Fork/Join/Choice) on an existing nested transition edge, splitting it into two.</summary>
    private void HandleStInsertSpecialStateOnNestedEdge(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var parentId = root.GetProperty("parentId").GetString();
        var index = root.GetProperty("index").GetInt32();
        var stateId = root.GetProperty("stateId").GetString();
        var stateTypeStr = root.GetProperty("stateType").GetString();
        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(stateId) || string.IsNullOrEmpty(stateTypeStr)) return;
        if (!Enum.TryParse<StateType>(stateTypeStr, true, out var stateType)) return;

        var parent = FindStateRecursive(_stateDiagramModel.States, parentId);
        if (parent == null || index < 0 || index >= parent.NestedTransitions.Count) return;

        PushUndo();
        var oldTransition = parent.NestedTransitions[index];
        var originalTo = oldTransition.ToId;

        // Create the new special state inside the parent
        var newState = new StateDefinition
        {
            Id = stateId,
            IsExplicit = true,
            Type = stateType
        };
        parent.NestedStates.Add(newState);

        // Replace old transition: originalFrom -> newState
        oldTransition.ToId = stateId;

        // Add new transition: newState -> originalTo
        parent.NestedTransitions.Insert(index + 1, new StateTransition
        {
            FromId = stateId,
            ToId = originalTo
        });

        RaiseStateDiagramModelChanged("st_insertSpecialStateOnNestedEdge");
    }

    private void HandleStStateEdited(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var stateId = root.GetProperty("stateId").GetString();
        if (string.IsNullOrEmpty(stateId)) return;

        var state = FindStateRecursive(_stateDiagramModel.States, stateId);
        if (state == null) return;

        PushUndo();
        if (root.TryGetProperty("label", out var labelProp))
            state.Label = labelProp.GetString();
        if (root.TryGetProperty("stateType", out var typeProp))
        {
            var typeStr = typeProp.GetString();
            if (Enum.TryParse<StateType>(typeStr, true, out var st))
                state.Type = st;
        }
        // Mark as explicit so the serializer outputs the declaration (label, type stereotype, etc.)
        state.IsExplicit = true;
        RaiseStateDiagramModelChanged("st_stateEdited");
    }

    private void HandleStStateDeleted(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var stateId = root.GetProperty("stateId").GetString();
        if (string.IsNullOrEmpty(stateId)) return;

        PushUndo();
        // Try top-level first
        int removed = _stateDiagramModel.States.RemoveAll(s => s.Id == stateId);
        if (removed == 0)
        {
            // Not found at top level - search recursively in nested states
            RemoveNestedStateRecursive(_stateDiagramModel.States, stateId);
        }
        _stateDiagramModel.Transitions.RemoveAll(t => t.FromId == stateId || t.ToId == stateId);
        _stateDiagramModel.Notes.RemoveAll(n => n.StateId == stateId);
        RaiseStateDiagramModelChanged("st_stateDeleted");
    }

    /// <summary>Recursively searches nested states and removes the one matching the given id.</summary>
    private static bool RemoveNestedStateRecursive(List<StateDefinition> states, string id)
    {
        foreach (var s in states)
        {
            int removed = s.NestedStates.RemoveAll(ns => ns.Id == id);
            if (removed > 0)
            {
                // Also clean up nested transitions referencing the deleted state
                s.NestedTransitions.RemoveAll(t => t.FromId == id || t.ToId == id);
                return true;
            }
            if (RemoveNestedStateRecursive(s.NestedStates, id))
                return true;
        }
        return false;
    }

    private void HandleStTransitionCreated(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var fromId = root.GetProperty("fromId").GetString();
        var toId = root.GetProperty("toId").GetString();
        if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId)) return;

        PushUndo();
        var transition = new StateTransition
        {
            FromId = fromId,
            ToId = toId
        };
        if (root.TryGetProperty("label", out var labelProp))
            transition.Label = labelProp.GetString();
        _stateDiagramModel.Transitions.Add(transition);
        RaiseStateDiagramModelChanged("st_transitionCreated");
    }

    private void HandleStTransitionEdited(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _stateDiagramModel.Transitions.Count) return;

        PushUndo();
        if (root.TryGetProperty("label", out var labelProp))
            _stateDiagramModel.Transitions[index].Label = labelProp.GetString();
        if (root.TryGetProperty("fromId", out var fromProp))
            _stateDiagramModel.Transitions[index].FromId = fromProp.GetString() ?? string.Empty;
        if (root.TryGetProperty("toId", out var toProp))
            _stateDiagramModel.Transitions[index].ToId = toProp.GetString() ?? string.Empty;
        RaiseStateDiagramModelChanged("st_transitionEdited");
    }

    private void HandleStTransitionDeleted(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _stateDiagramModel.Transitions.Count) return;

        PushUndo();
        _stateDiagramModel.Transitions.RemoveAt(index);
        RaiseStateDiagramModelChanged("st_transitionDeleted");
    }

    private void HandleStNestedTransitionCreated(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var parentId = root.GetProperty("parentId").GetString();
        var fromId = root.GetProperty("fromId").GetString();
        var toId = root.GetProperty("toId").GetString();
        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId)) return;

        var parent = FindStateRecursive(_stateDiagramModel.States, parentId);
        if (parent == null) return;

        PushUndo();
        var transition = new StateTransition
        {
            FromId = fromId,
            ToId = toId
        };
        if (root.TryGetProperty("label", out var labelProp))
            transition.Label = labelProp.GetString();
        parent.NestedTransitions.Add(transition);
        RaiseStateDiagramModelChanged("st_nestedTransitionCreated");
    }

    private void HandleStNestedTransitionEdited(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var parentId = root.GetProperty("parentId").GetString();
        var index = root.GetProperty("index").GetInt32();
        if (string.IsNullOrEmpty(parentId)) return;

        var parent = FindStateRecursive(_stateDiagramModel.States, parentId);
        if (parent == null || index < 0 || index >= parent.NestedTransitions.Count) return;

        PushUndo();
        if (root.TryGetProperty("label", out var labelProp))
            parent.NestedTransitions[index].Label = labelProp.GetString();
        RaiseStateDiagramModelChanged("st_nestedTransitionEdited");
    }

    private void HandleStNestedTransitionDeleted(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var parentId = root.GetProperty("parentId").GetString();
        var index = root.GetProperty("index").GetInt32();
        if (string.IsNullOrEmpty(parentId)) return;

        var parent = FindStateRecursive(_stateDiagramModel.States, parentId);
        if (parent == null || index < 0 || index >= parent.NestedTransitions.Count) return;

        PushUndo();
        parent.NestedTransitions.RemoveAt(index);
        RaiseStateDiagramModelChanged("st_nestedTransitionDeleted");
    }

    /// <summary>Insert a new nested state on an existing nested transition edge, splitting it into two.</summary>
    private void HandleStInsertStateOnNestedEdge(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var parentId = root.GetProperty("parentId").GetString();
        var index = root.GetProperty("index").GetInt32();
        var stateId = root.GetProperty("stateId").GetString();
        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(stateId)) return;

        var parent = FindStateRecursive(_stateDiagramModel.States, parentId);
        if (parent == null || index < 0 || index >= parent.NestedTransitions.Count) return;

        PushUndo();
        var oldTransition = parent.NestedTransitions[index];
        var originalTo = oldTransition.ToId;

        // Create the new nested state inside the parent
        var newState = new StateDefinition
        {
            Id = stateId,
            IsExplicit = true,
            Type = StateType.Simple
        };
        if (root.TryGetProperty("label", out var labelProp))
            newState.Label = labelProp.GetString();
        parent.NestedStates.Add(newState);

        // Replace old transition: originalFrom -> newState (keep original label)
        oldTransition.ToId = stateId;

        // Add new transition: newState -> originalTo (no label)
        parent.NestedTransitions.Insert(index + 1, new StateTransition
        {
            FromId = stateId,
            ToId = originalTo
        });

        RaiseStateDiagramModelChanged("st_insertStateOnNestedEdge");
    }

    /// <summary>Insert pseudo states ([*] end and [*] start) on a nested transition edge.</summary>
    private void HandleStInsertPseudoOnNestedEdge(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var parentId = root.GetProperty("parentId").GetString();
        var index = root.GetProperty("index").GetInt32();
        if (string.IsNullOrEmpty(parentId)) return;

        var parent = FindStateRecursive(_stateDiagramModel.States, parentId);
        if (parent == null || index < 0 || index >= parent.NestedTransitions.Count) return;

        PushUndo();
        var oldTransition = parent.NestedTransitions[index];
        var originalTo = oldTransition.ToId;

        // Replace old transition: originalFrom -> [*] (end)
        oldTransition.ToId = "[*]";

        // Add new transition: [*] (start) -> originalTo
        parent.NestedTransitions.Insert(index + 1, new StateTransition
        {
            FromId = "[*]",
            ToId = originalTo
        });

        RaiseStateDiagramModelChanged("st_insertPseudoOnNestedEdge");
    }

    private void HandleStNoteCreated(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var stateId = root.GetProperty("stateId").GetString();
        var text = root.GetProperty("text").GetString();
        if (string.IsNullOrEmpty(stateId) || string.IsNullOrEmpty(text)) return;

        PushUndo();
        var note = new StateNote
        {
            StateId = stateId,
            Text = text,
            Position = StateNotePosition.RightOf
        };
        if (root.TryGetProperty("position", out var posProp))
        {
            var posStr = posProp.GetString();
            if (Enum.TryParse<StateNotePosition>(posStr, true, out var pos))
                note.Position = pos;
        }
        _stateDiagramModel.Notes.Add(note);
        RaiseStateDiagramModelChanged("st_noteCreated");
    }

    private void HandleStNoteEdited(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _stateDiagramModel.Notes.Count) return;

        PushUndo();
        if (root.TryGetProperty("text", out var textProp))
            _stateDiagramModel.Notes[index].Text = textProp.GetString() ?? string.Empty;
        if (root.TryGetProperty("position", out var posProp))
        {
            var posStr = posProp.GetString();
            if (Enum.TryParse<StateNotePosition>(posStr, true, out var pos))
                _stateDiagramModel.Notes[index].Position = pos;
        }
        RaiseStateDiagramModelChanged("st_noteEdited");
    }

    private void HandleStNoteDeleted(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _stateDiagramModel.Notes.Count) return;

        PushUndo();
        _stateDiagramModel.Notes.RemoveAt(index);
        RaiseStateDiagramModelChanged("st_noteDeleted");
    }

    private static StateDefinition? FindStateRecursive(List<StateDefinition> states, string id)
    {
        foreach (var s in states)
        {
            if (s.Id == id) return s;
            var found = FindStateRecursive(s.NestedStates, id);
            if (found != null) return found;
        }
        return null;
    }

    private void RestoreStateDiagramModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<StDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _stateDiagramModel = new StateDiagramModel
        {
            Direction = dto.Direction,
            IsV2 = dto.IsV2,
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        if (dto.States != null)
        {
            foreach (var s in dto.States)
                _stateDiagramModel.States.Add(RestoreStateFromDto(s));
        }

        if (dto.Transitions != null)
        {
            foreach (var t in dto.Transitions)
            {
                _stateDiagramModel.Transitions.Add(new StateTransition
                {
                    FromId = t.FromId ?? string.Empty,
                    ToId = t.ToId ?? string.Empty,
                    Label = t.Label
                });
            }
        }

        if (dto.Notes != null)
        {
            foreach (var n in dto.Notes)
            {
                var pos = StateNotePosition.RightOf;
                if (!string.IsNullOrEmpty(n.Position))
                    Enum.TryParse(n.Position, true, out pos);

                _stateDiagramModel.Notes.Add(new StateNote
                {
                    Text = n.Text ?? string.Empty,
                    StateId = n.StateId ?? string.Empty,
                    Position = pos
                });
            }
        }
    }

    private static StateDefinition RestoreStateFromDto(StDiagStateDto dto)
    {
        var type = StateType.Simple;
        if (!string.IsNullOrEmpty(dto.Type))
            Enum.TryParse(dto.Type, true, out type);

        var state = new StateDefinition
        {
            Id = dto.Id ?? string.Empty,
            Label = dto.Label,
            Type = type,
            CssClass = dto.CssClass,
            IsExplicit = dto.IsExplicit,
            Position = new System.Windows.Point(dto.X, dto.Y),
            HasManualPosition = dto.HasManualPosition
        };

        if (dto.NestedStates != null)
        {
            foreach (var ns in dto.NestedStates)
                state.NestedStates.Add(RestoreStateFromDto(ns));
        }

        if (dto.NestedTransitions != null)
        {
            foreach (var t in dto.NestedTransitions)
            {
                state.NestedTransitions.Add(new StateTransition
                {
                    FromId = t.FromId ?? string.Empty,
                    ToId = t.ToId ?? string.Empty,
                    Label = t.Label
                });
            }
        }

        return state;
    }

    // ========== State Diagram DTOs ==========

    // ========== State Diagram DTOs ==========

    private class StDiagramDto
    {
        public string? Direction { get; set; }
        public bool IsV2 { get; set; } = true;
        public List<StDiagStateDto>? States { get; set; }
        public List<StDiagTransitionDto>? Transitions { get; set; }
        public List<StDiagNoteDto>? Notes { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
        public Dictionary<string, double[]>? PseudoNodePositions { get; set; }
        public Dictionary<string, double[]>? NotePositions { get; set; }
    }

    private class StDiagStateDto
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public string? Type { get; set; }
        public string? CssClass { get; set; }
        public bool IsExplicit { get; set; }
        public List<StDiagStateDto>? NestedStates { get; set; }
        public List<StDiagTransitionDto>? NestedTransitions { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool HasManualPosition { get; set; }
    }

    private class StDiagTransitionDto
    {
        public string? FromId { get; set; }
        public string? ToId { get; set; }
        public string? Label { get; set; }
    }

    private class StDiagNoteDto
    {
        public string? Text { get; set; }
        public string? StateId { get; set; }
        public string? Position { get; set; }
    }
}
