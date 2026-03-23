using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// Journey diagram handlers for the visual editor bridge.
/// Handles task CRUD, section CRUD, settings changes, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== Journey Support ==========

    private JourneyModel? _journeyModel;

    /// <summary>
    /// Gets the current JourneyModel (may be null if not in journey mode).
    /// </summary>
    public JourneyModel? JourneyModel => _journeyModel;

    /// <summary>
    /// Raised when the JourneyModel is modified by the visual editor.
    /// </summary>
    public event EventHandler<JourneyModelChangedEventArgs>? JourneyModelChanged;

    /// <summary>
    /// Sends the current JourneyModel to the visual editor as JSON.
    /// </summary>
    public async Task SendJourneyToEditorAsync()
    {
        if (_journeyModel == null) return;
        var json = ConvertJourneyModelToJson(_journeyModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadJourneyDiagram({escaped})");
    }

    /// <summary>
    /// Restores the journey for undo/redo.
    /// </summary>
    private async Task RestoreJourneyToEditorAsync()
    {
        if (_journeyModel == null) return;
        var json = ConvertJourneyModelToJson(_journeyModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreJourneyDiagram({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current journey model.
    /// </summary>
    public async Task RefreshJourneyAsync()
    {
        if (_journeyModel == null) return;
        var json = ConvertJourneyModelToJson(_journeyModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshJourneyDiagram({escaped})");
    }

    /// <summary>
    /// Updates the journey model reference and sends to editor.
    /// </summary>
    public async Task UpdateJourneyModelAsync(JourneyModel newModel)
    {
        _journeyModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.Journey;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendJourneyToEditorAsync();
    }

    /// <summary>
    /// Converts a JourneyModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertJourneyModelToJson(JourneyModel model)
    {
        var dto = new JourneyDiagramDto
        {
            Title = model.Title,
            Tasks = model.Tasks.Select(t => new JourneyTaskDto
            {
                Label = t.Label,
                Score = t.Score,
                Actors = t.Actors
            }).ToList(),
            Sections = model.Sections.Select(s => new JourneySectionDto
            {
                Name = s.Name,
                Tasks = s.Tasks.Select(t => new JourneyTaskDto
                {
                    Label = t.Label,
                    Score = t.Score,
                    Actors = t.Actors
                }).ToList()
            }).ToList(),
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    // ========== Journey Message Handlers ==========

    private void HandleJourneyTaskCreated(JsonElement root)
    {
        if (_journeyModel == null) return;

        PushUndo();
        var label = root.GetProperty("label").GetString() ?? "New Task";
        var score = 3;
        if (root.TryGetProperty("score", out var scoreProp) && scoreProp.ValueKind == JsonValueKind.Number)
            score = Math.Clamp(scoreProp.GetInt32(), 1, 5);

        var actors = new List<string>();
        if (root.TryGetProperty("actors", out var actorsProp) && actorsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in actorsProp.EnumerateArray())
            {
                var s = item.GetString();
                if (!string.IsNullOrEmpty(s)) actors.Add(s);
            }
        }

        var newTask = new JourneyTask
        {
            Label = label,
            Score = score,
            Actors = actors
        };

        // Determine target section
        string? sectionName = null;
        if (root.TryGetProperty("section", out var secProp) && secProp.ValueKind == JsonValueKind.String)
            sectionName = secProp.GetString();

        // Read optional insertAtIndex
        int? insertAtIndex = null;
        if (root.TryGetProperty("insertAtIndex", out var idxProp) && idxProp.ValueKind == JsonValueKind.Number)
            insertAtIndex = idxProp.GetInt32();

        if (!string.IsNullOrEmpty(sectionName))
        {
            var section = _journeyModel.Sections.FirstOrDefault(s => s.Name == sectionName);
            if (section != null)
            {
                if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= section.Tasks.Count)
                    section.Tasks.Insert(insertAtIndex.Value, newTask);
                else
                    section.Tasks.Add(newTask);
            }
            else
                _journeyModel.Tasks.Add(newTask);
        }
        else
        {
            if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= _journeyModel.Tasks.Count)
                _journeyModel.Tasks.Insert(insertAtIndex.Value, newTask);
            else
                _journeyModel.Tasks.Add(newTask);
        }

        RaiseJourneyModelChanged("jn_taskCreated");
    }

    private void HandleJourneyTaskEdited(JsonElement root)
    {
        if (_journeyModel == null) return;

        var index = root.GetProperty("index").GetInt32();
        string? sectionName = null;
        if (root.TryGetProperty("section", out var secProp) && secProp.ValueKind == JsonValueKind.String)
            sectionName = secProp.GetString();

        // Find the task
        JourneyTask? task = null;
        List<JourneyTask>? sourceList = null;

        if (!string.IsNullOrEmpty(sectionName))
        {
            var section = _journeyModel.Sections.FirstOrDefault(s => s.Name == sectionName);
            if (section != null && index >= 0 && index < section.Tasks.Count)
            {
                task = section.Tasks[index];
                sourceList = section.Tasks;
            }
        }
        else
        {
            if (index >= 0 && index < _journeyModel.Tasks.Count)
            {
                task = _journeyModel.Tasks[index];
                sourceList = _journeyModel.Tasks;
            }
        }

        if (task == null || sourceList == null) return;

        PushUndo();

        // Update fields
        if (root.TryGetProperty("label", out var labelProp))
            task.Label = labelProp.GetString() ?? task.Label;

        if (root.TryGetProperty("score", out var scoreProp) && scoreProp.ValueKind == JsonValueKind.Number)
            task.Score = Math.Clamp(scoreProp.GetInt32(), 1, 5);

        if (root.TryGetProperty("actors", out var actorsProp) && actorsProp.ValueKind == JsonValueKind.Array)
        {
            var actors = new List<string>();
            foreach (var item in actorsProp.EnumerateArray())
            {
                var s = item.GetString();
                if (!string.IsNullOrEmpty(s)) actors.Add(s);
            }
            task.Actors = actors;
        }

        // Handle section move
        if (root.TryGetProperty("newSection", out var newSecProp))
        {
            string? newSectionName = newSecProp.ValueKind == JsonValueKind.String ? newSecProp.GetString() : null;

            // If section changed, move the task
            if (newSectionName != sectionName)
            {
                sourceList.RemoveAt(index);
                if (!string.IsNullOrEmpty(newSectionName))
                {
                    var targetSection = _journeyModel.Sections.FirstOrDefault(s => s.Name == newSectionName);
                    if (targetSection != null)
                        targetSection.Tasks.Add(task);
                    else
                        _journeyModel.Tasks.Add(task);
                }
                else
                {
                    _journeyModel.Tasks.Add(task);
                }
            }
        }

        RaiseJourneyModelChanged("jn_taskEdited");
    }

    private void HandleJourneyTaskDeleted(JsonElement root)
    {
        if (_journeyModel == null) return;

        var index = root.GetProperty("index").GetInt32();
        string? sectionName = null;
        if (root.TryGetProperty("section", out var secProp) && secProp.ValueKind == JsonValueKind.String)
            sectionName = secProp.GetString();

        PushUndo();

        if (!string.IsNullOrEmpty(sectionName))
        {
            var section = _journeyModel.Sections.FirstOrDefault(s => s.Name == sectionName);
            if (section != null && index >= 0 && index < section.Tasks.Count)
                section.Tasks.RemoveAt(index);
        }
        else
        {
            if (index >= 0 && index < _journeyModel.Tasks.Count)
                _journeyModel.Tasks.RemoveAt(index);
        }

        RaiseJourneyModelChanged("jn_taskDeleted");
    }

    private void HandleJourneySectionCreated(JsonElement root)
    {
        if (_journeyModel == null) return;
        var name = root.GetProperty("name").GetString();
        if (string.IsNullOrEmpty(name)) return;

        PushUndo();

        // Read optional insertAtIndex
        int? insertAtIndex = null;
        if (root.TryGetProperty("insertAtIndex", out var idxProp) && idxProp.ValueKind == JsonValueKind.Number)
            insertAtIndex = idxProp.GetInt32();

        var newSection = new JourneySection { Name = name };
        if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= _journeyModel.Sections.Count)
            _journeyModel.Sections.Insert(insertAtIndex.Value, newSection);
        else
            _journeyModel.Sections.Add(newSection);

        RaiseJourneyModelChanged("jn_sectionCreated");
    }

    private void HandleJourneySectionEdited(JsonElement root)
    {
        if (_journeyModel == null) return;
        var oldName = root.GetProperty("oldName").GetString();
        var newName = root.GetProperty("newName").GetString();
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName)) return;

        var section = _journeyModel.Sections.FirstOrDefault(s => s.Name == oldName);
        if (section == null) return;

        PushUndo();
        section.Name = newName;
        RaiseJourneyModelChanged("jn_sectionEdited");
    }

    private void HandleJourneySectionDeleted(JsonElement root)
    {
        if (_journeyModel == null) return;
        var name = root.GetProperty("name").GetString();
        if (string.IsNullOrEmpty(name)) return;

        var section = _journeyModel.Sections.FirstOrDefault(s => s.Name == name);
        if (section == null) return;

        PushUndo();
        _journeyModel.Sections.Remove(section);
        RaiseJourneyModelChanged("jn_sectionDeleted");
    }

    private void HandleJourneySettingsChanged(JsonElement root)
    {
        if (_journeyModel == null) return;

        PushUndo();
        if (root.TryGetProperty("title", out var tProp))
        {
            _journeyModel.Title = tProp.ValueKind == JsonValueKind.Null ? null : tProp.GetString();
        }
        RaiseJourneyModelChanged("jn_settingsChanged");
    }

    private void RestoreJourneyModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<JourneyDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _journeyModel = new JourneyModel
        {
            Title = dto.Title,
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        if (dto.Tasks != null)
        {
            foreach (var t in dto.Tasks)
            {
                _journeyModel.Tasks.Add(new JourneyTask
                {
                    Label = t.Label ?? string.Empty,
                    Score = Math.Clamp(t.Score, 1, 5),
                    Actors = t.Actors ?? new List<string>()
                });
            }
        }

        if (dto.Sections != null)
        {
            foreach (var s in dto.Sections)
            {
                var section = new JourneySection { Name = s.Name ?? string.Empty };
                if (s.Tasks != null)
                {
                    foreach (var t in s.Tasks)
                    {
                        section.Tasks.Add(new JourneyTask
                        {
                            Label = t.Label ?? string.Empty,
                            Score = Math.Clamp(t.Score, 1, 5),
                            Actors = t.Actors ?? new List<string>()
                        });
                    }
                }
                _journeyModel.Sections.Add(section);
            }
        }
    }

    private void RaiseJourneyModelChanged(string changeType)
    {
        if (_journeyModel != null)
            JourneyModelChanged?.Invoke(this, new JourneyModelChangedEventArgs(changeType, _journeyModel));
    }

    // ========== Journey DTOs ==========

    private class JourneyDiagramDto
    {
        public string? Title { get; set; }
        public List<JourneyTaskDto>? Tasks { get; set; }
        public List<JourneySectionDto>? Sections { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class JourneySectionDto
    {
        public string? Name { get; set; }
        public List<JourneyTaskDto>? Tasks { get; set; }
    }

    private class JourneyTaskDto
    {
        public string? Label { get; set; }
        public int Score { get; set; }
        public List<string>? Actors { get; set; }
    }
}

/// <summary>
/// Event args for when the JourneyModel changes via the visual editor.
/// </summary>
public class JourneyModelChangedEventArgs : EventArgs
{
    public string ChangeType { get; }
    public JourneyModel Model { get; }

    public JourneyModelChangedEventArgs(string changeType, JourneyModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}
