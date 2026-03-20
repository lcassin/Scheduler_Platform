using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// Timeline diagram handlers for the visual editor bridge.
/// Handles event CRUD, section CRUD, settings changes, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== Timeline Support ==========

    private TimelineModel? _timelineModel;

    /// <summary>
    /// Gets the current TimelineModel (may be null if not in timeline mode).
    /// </summary>
    public TimelineModel? TimelineModel => _timelineModel;

    /// <summary>
    /// Raised when the TimelineModel is modified by the visual editor.
    /// </summary>
    public event EventHandler<TimelineModelChangedEventArgs>? TimelineModelChanged;

    /// <summary>
    /// Sends the current TimelineModel to the visual editor as JSON.
    /// </summary>
    public async Task SendTimelineToEditorAsync()
    {
        if (_timelineModel == null) return;
        var json = ConvertTimelineModelToJson(_timelineModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadTimelineDiagram({escaped})");
    }

    /// <summary>
    /// Restores the timeline for undo/redo.
    /// </summary>
    private async Task RestoreTimelineToEditorAsync()
    {
        if (_timelineModel == null) return;
        var json = ConvertTimelineModelToJson(_timelineModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreTimelineDiagram({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current timeline model.
    /// </summary>
    public async Task RefreshTimelineAsync()
    {
        if (_timelineModel == null) return;
        var json = ConvertTimelineModelToJson(_timelineModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshTimelineDiagram({escaped})");
    }

    /// <summary>
    /// Updates the timeline model reference and sends to editor.
    /// </summary>
    public async Task UpdateTimelineModelAsync(TimelineModel newModel)
    {
        _timelineModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.Timeline;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendTimelineToEditorAsync();
    }

    /// <summary>
    /// Converts a TimelineModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertTimelineModelToJson(TimelineModel model)
    {
        var dto = new TimelineDiagramDto
        {
            Title = model.Title,
            Events = model.Events.Select(e => new TimelineEventDto
            {
                TimePeriod = e.TimePeriod,
                Events = e.Events
            }).ToList(),
            Sections = model.Sections.Select(s => new TimelineSectionDto
            {
                Name = s.Name,
                Events = s.Events.Select(e => new TimelineEventDto
                {
                    TimePeriod = e.TimePeriod,
                    Events = e.Events
                }).ToList()
            }).ToList(),
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    // ========== Timeline Message Handlers ==========

    private void HandleTimelineEventCreated(JsonElement root)
    {
        if (_timelineModel == null) return;

        PushUndo();
        var timePeriod = root.GetProperty("timePeriod").GetString() ?? "New";
        var events = new List<string>();
        if (root.TryGetProperty("events", out var evtsProp) && evtsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in evtsProp.EnumerateArray())
            {
                var s = item.GetString();
                if (!string.IsNullOrEmpty(s)) events.Add(s);
            }
        }

        var newEvent = new TimelineEvent
        {
            TimePeriod = timePeriod,
            Events = events.Count > 0 ? events : new List<string> { "New Event" }
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
            var section = _timelineModel.Sections.FirstOrDefault(s => s.Name == sectionName);
            if (section != null)
            {
                if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= section.Events.Count)
                    section.Events.Insert(insertAtIndex.Value, newEvent);
                else
                    section.Events.Add(newEvent);
            }
            else
                _timelineModel.Events.Add(newEvent);
        }
        else
        {
            if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= _timelineModel.Events.Count)
                _timelineModel.Events.Insert(insertAtIndex.Value, newEvent);
            else
                _timelineModel.Events.Add(newEvent);
        }

        RaiseTimelineModelChanged("tl_eventCreated");
    }

    private void HandleTimelineEventEdited(JsonElement root)
    {
        if (_timelineModel == null) return;

        var index = root.GetProperty("index").GetInt32();
        string? sectionName = null;
        if (root.TryGetProperty("section", out var secProp) && secProp.ValueKind == JsonValueKind.String)
            sectionName = secProp.GetString();

        // Find the event
        TimelineEvent? evt = null;
        List<TimelineEvent>? sourceList = null;

        if (!string.IsNullOrEmpty(sectionName))
        {
            var section = _timelineModel.Sections.FirstOrDefault(s => s.Name == sectionName);
            if (section != null && index >= 0 && index < section.Events.Count)
            {
                evt = section.Events[index];
                sourceList = section.Events;
            }
        }
        else
        {
            if (index >= 0 && index < _timelineModel.Events.Count)
            {
                evt = _timelineModel.Events[index];
                sourceList = _timelineModel.Events;
            }
        }

        if (evt == null || sourceList == null) return;

        PushUndo();

        // Update fields
        if (root.TryGetProperty("timePeriod", out var tpProp))
            evt.TimePeriod = tpProp.GetString() ?? evt.TimePeriod;

        if (root.TryGetProperty("events", out var evtsProp) && evtsProp.ValueKind == JsonValueKind.Array)
        {
            var events = new List<string>();
            foreach (var item in evtsProp.EnumerateArray())
            {
                var s = item.GetString();
                if (!string.IsNullOrEmpty(s)) events.Add(s);
            }
            if (events.Count > 0) evt.Events = events;
        }

        // Handle section move
        if (root.TryGetProperty("newSection", out var newSecProp))
        {
            string? newSectionName = newSecProp.ValueKind == JsonValueKind.String ? newSecProp.GetString() : null;

            // If section changed, move the event
            if (newSectionName != sectionName)
            {
                sourceList.RemoveAt(index);
                if (!string.IsNullOrEmpty(newSectionName))
                {
                    var targetSection = _timelineModel.Sections.FirstOrDefault(s => s.Name == newSectionName);
                    if (targetSection != null)
                        targetSection.Events.Add(evt);
                    else
                        _timelineModel.Events.Add(evt);
                }
                else
                {
                    _timelineModel.Events.Add(evt);
                }
            }
        }

        RaiseTimelineModelChanged("tl_eventEdited");
    }

    private void HandleTimelineEventDeleted(JsonElement root)
    {
        if (_timelineModel == null) return;

        var index = root.GetProperty("index").GetInt32();
        string? sectionName = null;
        if (root.TryGetProperty("section", out var secProp) && secProp.ValueKind == JsonValueKind.String)
            sectionName = secProp.GetString();

        PushUndo();

        if (!string.IsNullOrEmpty(sectionName))
        {
            var section = _timelineModel.Sections.FirstOrDefault(s => s.Name == sectionName);
            if (section != null && index >= 0 && index < section.Events.Count)
                section.Events.RemoveAt(index);
        }
        else
        {
            if (index >= 0 && index < _timelineModel.Events.Count)
                _timelineModel.Events.RemoveAt(index);
        }

        RaiseTimelineModelChanged("tl_eventDeleted");
    }

    private void HandleTimelineSectionCreated(JsonElement root)
    {
        if (_timelineModel == null) return;
        var name = root.GetProperty("name").GetString();
        if (string.IsNullOrEmpty(name)) return;

        PushUndo();

        // Read optional insertAtIndex
        int? insertAtIndex = null;
        if (root.TryGetProperty("insertAtIndex", out var idxProp) && idxProp.ValueKind == JsonValueKind.Number)
            insertAtIndex = idxProp.GetInt32();

        var newSection = new TimelineSection { Name = name };
        if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= _timelineModel.Sections.Count)
            _timelineModel.Sections.Insert(insertAtIndex.Value, newSection);
        else
            _timelineModel.Sections.Add(newSection);

        RaiseTimelineModelChanged("tl_sectionCreated");
    }

    private void HandleTimelineSectionEdited(JsonElement root)
    {
        if (_timelineModel == null) return;
        var oldName = root.GetProperty("oldName").GetString();
        var newName = root.GetProperty("newName").GetString();
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName)) return;

        var section = _timelineModel.Sections.FirstOrDefault(s => s.Name == oldName);
        if (section == null) return;

        PushUndo();
        section.Name = newName;
        RaiseTimelineModelChanged("tl_sectionEdited");
    }

    private void HandleTimelineSectionDeleted(JsonElement root)
    {
        if (_timelineModel == null) return;
        var name = root.GetProperty("name").GetString();
        if (string.IsNullOrEmpty(name)) return;

        var section = _timelineModel.Sections.FirstOrDefault(s => s.Name == name);
        if (section == null) return;

        PushUndo();
        _timelineModel.Sections.Remove(section);
        RaiseTimelineModelChanged("tl_sectionDeleted");
    }

    private void HandleTimelineSettingsChanged(JsonElement root)
    {
        if (_timelineModel == null) return;

        PushUndo();
        if (root.TryGetProperty("title", out var tProp))
        {
            _timelineModel.Title = tProp.ValueKind == JsonValueKind.Null ? null : tProp.GetString();
        }
        RaiseTimelineModelChanged("tl_settingsChanged");
    }

    private void RestoreTimelineModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<TimelineDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _timelineModel = new TimelineModel
        {
            Title = dto.Title,
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        if (dto.Events != null)
        {
            foreach (var e in dto.Events)
            {
                _timelineModel.Events.Add(new TimelineEvent
                {
                    TimePeriod = e.TimePeriod ?? string.Empty,
                    Events = e.Events ?? new List<string>()
                });
            }
        }

        if (dto.Sections != null)
        {
            foreach (var s in dto.Sections)
            {
                var section = new TimelineSection { Name = s.Name ?? string.Empty };
                if (s.Events != null)
                {
                    foreach (var e in s.Events)
                    {
                        section.Events.Add(new TimelineEvent
                        {
                            TimePeriod = e.TimePeriod ?? string.Empty,
                            Events = e.Events ?? new List<string>()
                        });
                    }
                }
                _timelineModel.Sections.Add(section);
            }
        }
    }

    private void RaiseTimelineModelChanged(string changeType)
    {
        if (_timelineModel != null)
            TimelineModelChanged?.Invoke(this, new TimelineModelChangedEventArgs(changeType, _timelineModel));
    }

    // ========== Timeline DTOs ==========

    private class TimelineDiagramDto
    {
        public string? Title { get; set; }
        public List<TimelineEventDto>? Events { get; set; }
        public List<TimelineSectionDto>? Sections { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class TimelineSectionDto
    {
        public string? Name { get; set; }
        public List<TimelineEventDto>? Events { get; set; }
    }

    private class TimelineEventDto
    {
        public string? TimePeriod { get; set; }
        public List<string>? Events { get; set; }
    }
}

/// <summary>
/// Event args for when the TimelineModel changes via the visual editor.
/// </summary>
public class TimelineModelChangedEventArgs : EventArgs
{
    public string ChangeType { get; }
    public TimelineModel Model { get; }

    public TimelineModelChangedEventArgs(string changeType, TimelineModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}
