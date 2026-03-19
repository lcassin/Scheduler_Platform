using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// Gantt chart handlers for the visual editor bridge.
/// Handles task CRUD, section operations, timeline interactions, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== Gantt Chart Support ==========

    private GanttModel? _ganttModel;

    /// <summary>
    /// Gets the current GanttModel (may be null if not in gantt mode).
    /// </summary>
    public GanttModel? GanttModel => _ganttModel;

    /// <summary>
    /// Raised when the GanttModel is modified by the visual editor.
    /// </summary>
    public event EventHandler<GanttModelChangedEventArgs>? GanttModelChanged;

    /// <summary>
    /// Sends the current GanttModel to the visual editor as JSON.
    /// </summary>
    public async Task SendGanttToEditorAsync()
    {
        if (_ganttModel == null) return;
        var json = ConvertGanttModelToJson(_ganttModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadGanttDiagram({escaped})");
    }

    /// <summary>
    /// Restores the gantt chart for undo/redo.
    /// </summary>
    private async Task RestoreGanttToEditorAsync()
    {
        if (_ganttModel == null) return;
        var json = ConvertGanttModelToJson(_ganttModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreGanttDiagram({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current gantt model.
    /// </summary>
    public async Task RefreshGanttAsync()
    {
        if (_ganttModel == null) return;
        var json = ConvertGanttModelToJson(_ganttModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshGanttDiagram({escaped})");
    }

    /// <summary>
    /// Updates the gantt model reference and sends to editor.
    /// </summary>
    public async Task UpdateGanttModelAsync(GanttModel newModel)
    {
        _ganttModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.Gantt;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendGanttToEditorAsync();
    }

    /// <summary>
    /// Converts a GanttModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertGanttModelToJson(GanttModel model)
    {
        var tasks = new List<GanttTaskDto>();

        // Collect top-level tasks
        foreach (var task in model.Tasks)
        {
            tasks.Add(ConvertGanttTaskToDto(task, null));
        }

        // Collect section tasks
        foreach (var section in model.Sections)
        {
            foreach (var task in section.Tasks)
            {
                tasks.Add(ConvertGanttTaskToDto(task, section.Name));
            }
        }

        var dto = new GanttDiagramDto
        {
            Title = model.Title,
            DateFormat = model.DateFormat,
            AxisFormat = model.AxisFormat,
            Excludes = model.Excludes,
            ExcludesWeekends = model.ExcludesWeekends,
            Sections = model.Sections.Select(s => s.Name).ToList(),
            Tasks = tasks,
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static GanttTaskDto ConvertGanttTaskToDto(GanttTask task, string? sectionName)
    {
        return new GanttTaskDto
        {
            Label = task.Label,
            Id = task.Id,
            Tags = task.Tags,
            StartDate = task.StartDate,
            EndDate = task.EndDate,
            IsMilestone = task.IsMilestone,
            Section = sectionName
        };
    }

    // ========== Gantt Message Handlers ==========

    private void HandleGanttTaskCreated(JsonElement root)
    {
        if (_ganttModel == null) return;
        var label = root.GetProperty("label").GetString();
        if (string.IsNullOrEmpty(label)) return;

        PushUndo();
        var task = new GanttTask { Label = label };

        if (root.TryGetProperty("id", out var idProp))
            task.Id = idProp.GetString();
        if (root.TryGetProperty("startDate", out var sdProp))
            task.StartDate = sdProp.GetString();
        if (root.TryGetProperty("endDate", out var edProp))
            task.EndDate = edProp.GetString();
        if (root.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsProp.EnumerateArray())
            {
                var tagStr = tag.GetString();
                if (!string.IsNullOrEmpty(tagStr))
                    task.Tags.Add(tagStr);
            }
        }
        task.IsMilestone = task.Tags.Contains("milestone");

        // Read optional insertAtIndex for positional insertion
        int? insertAtIndex = null;
        if (root.TryGetProperty("insertAtIndex", out var idxProp) && idxProp.ValueKind == JsonValueKind.Number)
            insertAtIndex = idxProp.GetInt32();

        // Add to section if specified
        if (root.TryGetProperty("section", out var secProp))
        {
            var sectionName = secProp.GetString();
            if (!string.IsNullOrEmpty(sectionName))
            {
                var section = _ganttModel.Sections.Find(s => s.Name == sectionName);
                if (section != null)
                {
                    if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value < section.Tasks.Count)
                        section.Tasks.Insert(insertAtIndex.Value, task);
                    else
                        section.Tasks.Add(task);
                    RaiseGanttModelChanged("gantt_taskCreated");
                    return;
                }
            }
        }

        if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value < _ganttModel.Tasks.Count)
            _ganttModel.Tasks.Insert(insertAtIndex.Value, task);
        else
            _ganttModel.Tasks.Add(task);
        RaiseGanttModelChanged("gantt_taskCreated");
    }

    private void HandleGanttTaskEdited(JsonElement root)
    {
        if (_ganttModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        var sectionName = root.TryGetProperty("section", out var sp) ? sp.GetString() : null;

        var task = FindGanttTask(index, sectionName);
        if (task == null) return;

        PushUndo();
        if (root.TryGetProperty("label", out var lProp))
            task.Label = lProp.GetString() ?? task.Label;
        if (root.TryGetProperty("id", out var idProp))
            task.Id = idProp.GetString();
        if (root.TryGetProperty("startDate", out var sdProp))
            task.StartDate = sdProp.GetString();
        if (root.TryGetProperty("endDate", out var edProp))
            task.EndDate = edProp.GetString();
        if (root.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
        {
            task.Tags.Clear();
            foreach (var tag in tagsProp.EnumerateArray())
            {
                var tagStr = tag.GetString();
                if (!string.IsNullOrEmpty(tagStr))
                    task.Tags.Add(tagStr);
            }
            task.IsMilestone = task.Tags.Contains("milestone");
        }

        RaiseGanttModelChanged("gantt_taskEdited");
    }

    private void HandleGanttTaskDeleted(JsonElement root)
    {
        if (_ganttModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        var sectionName = root.TryGetProperty("section", out var sp) ? sp.GetString() : null;

        PushUndo();
        if (!string.IsNullOrEmpty(sectionName))
        {
            var section = _ganttModel.Sections.Find(s => s.Name == sectionName);
            if (section != null && index >= 0 && index < section.Tasks.Count)
                section.Tasks.RemoveAt(index);
        }
        else if (index >= 0 && index < _ganttModel.Tasks.Count)
        {
            _ganttModel.Tasks.RemoveAt(index);
        }

        RaiseGanttModelChanged("gantt_taskDeleted");
    }

    private void HandleGanttSectionCreated(JsonElement root)
    {
        if (_ganttModel == null) return;
        var name = root.GetProperty("name").GetString();
        if (string.IsNullOrEmpty(name)) return;

        PushUndo();
        int? insertAtIndex = null;
        if (root.TryGetProperty("insertAtIndex", out var idxProp) && idxProp.ValueKind == JsonValueKind.Number)
            insertAtIndex = idxProp.GetInt32();

        var newSection = new GanttSection { Name = name };
        if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value < _ganttModel.Sections.Count)
            _ganttModel.Sections.Insert(insertAtIndex.Value, newSection);
        else
            _ganttModel.Sections.Add(newSection);
        RaiseGanttModelChanged("gantt_sectionCreated");
    }

    private void HandleGanttSectionEdited(JsonElement root)
    {
        if (_ganttModel == null) return;
        var oldName = root.GetProperty("oldName").GetString();
        var newName = root.GetProperty("name").GetString();
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName)) return;

        var section = _ganttModel.Sections.Find(s => s.Name == oldName);
        if (section == null) return;

        PushUndo();
        section.Name = newName;
        RaiseGanttModelChanged("gantt_sectionEdited");
    }

    private void HandleGanttSectionDeleted(JsonElement root)
    {
        if (_ganttModel == null) return;
        var name = root.GetProperty("name").GetString();
        if (string.IsNullOrEmpty(name)) return;

        PushUndo();
        _ganttModel.Sections.RemoveAll(s => s.Name == name);
        RaiseGanttModelChanged("gantt_sectionDeleted");
    }

    private void HandleGanttSettingsChanged(JsonElement root)
    {
        if (_ganttModel == null) return;

        PushUndo();
        if (root.TryGetProperty("title", out var tProp))
            _ganttModel.Title = tProp.GetString();
        if (root.TryGetProperty("dateFormat", out var dfProp))
            _ganttModel.DateFormat = dfProp.GetString() ?? "YYYY-MM-DD";
        if (root.TryGetProperty("axisFormat", out var afProp))
            _ganttModel.AxisFormat = afProp.GetString();
        if (root.TryGetProperty("excludes", out var exProp))
        {
            _ganttModel.Excludes = exProp.GetString();
            _ganttModel.ExcludesWeekends = _ganttModel.Excludes?.Contains("weekends", StringComparison.OrdinalIgnoreCase) ?? false;
        }
        RaiseGanttModelChanged("gantt_settingsChanged");
    }

    private GanttTask? FindGanttTask(int index, string? sectionName)
    {
        if (!string.IsNullOrEmpty(sectionName))
        {
            var section = _ganttModel?.Sections.Find(s => s.Name == sectionName);
            if (section != null && index >= 0 && index < section.Tasks.Count)
                return section.Tasks[index];
        }
        else if (_ganttModel != null && index >= 0 && index < _ganttModel.Tasks.Count)
        {
            return _ganttModel.Tasks[index];
        }
        return null;
    }

    private void RestoreGanttModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<GanttDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _ganttModel = new GanttModel
        {
            Title = dto.Title,
            DateFormat = dto.DateFormat ?? "YYYY-MM-DD",
            AxisFormat = dto.AxisFormat,
            Excludes = dto.Excludes,
            ExcludesWeekends = dto.ExcludesWeekends,
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        // Rebuild sections from DTO
        var sectionMap = new Dictionary<string, GanttSection>();
        if (dto.Sections != null)
        {
            foreach (var sName in dto.Sections)
            {
                var section = new GanttSection { Name = sName };
                _ganttModel.Sections.Add(section);
                sectionMap[sName] = section;
            }
        }

        if (dto.Tasks != null)
        {
            foreach (var t in dto.Tasks)
            {
                var task = new GanttTask
                {
                    Label = t.Label ?? string.Empty,
                    Id = t.Id,
                    Tags = t.Tags ?? new List<string>(),
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    IsMilestone = t.IsMilestone
                };

                if (!string.IsNullOrEmpty(t.Section) && sectionMap.TryGetValue(t.Section, out var section))
                    section.Tasks.Add(task);
                else
                    _ganttModel.Tasks.Add(task);
            }
        }
    }

    private void RaiseGanttModelChanged(string changeType)
    {
        if (_ganttModel != null)
            GanttModelChanged?.Invoke(this, new GanttModelChangedEventArgs(changeType, _ganttModel));
    }

    // ========== Gantt DTOs ==========

    private class GanttDiagramDto
    {
        public string? Title { get; set; }
        public string? DateFormat { get; set; }
        public string? AxisFormat { get; set; }
        public string? Excludes { get; set; }
        public bool ExcludesWeekends { get; set; }
        public List<string>? Sections { get; set; }
        public List<GanttTaskDto>? Tasks { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class GanttTaskDto
    {
        public string? Label { get; set; }
        public string? Id { get; set; }
        public List<string>? Tags { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public bool IsMilestone { get; set; }
        public string? Section { get; set; }
    }
}

/// <summary>
/// Event args for when the GanttModel changes via the visual editor.
/// </summary>
public class GanttModelChangedEventArgs : EventArgs
{
    public string ChangeType { get; }
    public GanttModel Model { get; }

    public GanttModelChangedEventArgs(string changeType, GanttModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}
