using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// Pie chart handlers for the visual editor bridge.
/// Handles slice CRUD, settings changes, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== Pie Chart Support ==========

    private PieChartModel? _pieChartModel;

    /// <summary>
    /// Gets the current PieChartModel (may be null if not in pie chart mode).
    /// </summary>
    public PieChartModel? PieChartModel => _pieChartModel;

    /// <summary>
    /// Raised when the PieChartModel is modified by the visual editor.
    /// </summary>
    public event EventHandler<PieChartModelChangedEventArgs>? PieChartModelChanged;

    /// <summary>
    /// Sends the current PieChartModel to the visual editor as JSON.
    /// </summary>
    public async Task SendPieChartToEditorAsync()
    {
        if (_pieChartModel == null) return;
        var json = ConvertPieChartModelToJson(_pieChartModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadPieChart({escaped})");
    }

    /// <summary>
    /// Restores the pie chart for undo/redo.
    /// </summary>
    private async Task RestorePieChartToEditorAsync()
    {
        if (_pieChartModel == null) return;
        var json = ConvertPieChartModelToJson(_pieChartModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restorePieChart({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current pie chart model.
    /// </summary>
    public async Task RefreshPieChartAsync()
    {
        if (_pieChartModel == null) return;
        var json = ConvertPieChartModelToJson(_pieChartModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshPieChart({escaped})");
    }

    /// <summary>
    /// Updates the pie chart model reference and sends to editor.
    /// </summary>
    public async Task UpdatePieChartModelAsync(PieChartModel newModel)
    {
        _pieChartModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.Pie;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendPieChartToEditorAsync();
    }

    /// <summary>
    /// Converts a PieChartModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertPieChartModelToJson(PieChartModel model)
    {
        var dto = new PieChartDiagramDto
        {
            Title = model.Title,
            ShowData = model.ShowData,
            Slices = model.Slices.Select(s => new PieSliceDto
            {
                Label = s.Label,
                Value = s.Value
            }).ToList(),
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    // ========== Pie Chart Message Handlers ==========

    private void HandlePieSliceCreated(JsonElement root)
    {
        if (_pieChartModel == null) return;
        var label = root.GetProperty("label").GetString();
        if (string.IsNullOrEmpty(label)) return;

        PushUndo();
        var slice = new PieSlice
        {
            Label = label,
            Value = root.TryGetProperty("value", out var vProp) ? vProp.GetDouble() : 1.0
        };
        _pieChartModel.Slices.Add(slice);
        RaisePieChartModelChanged("pie_sliceCreated");
    }

    private void HandlePieSliceEdited(JsonElement root)
    {
        if (_pieChartModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _pieChartModel.Slices.Count) return;

        PushUndo();
        var slice = _pieChartModel.Slices[index];
        if (root.TryGetProperty("label", out var lProp))
            slice.Label = lProp.GetString() ?? slice.Label;
        if (root.TryGetProperty("value", out var vProp))
            slice.Value = vProp.GetDouble();
        RaisePieChartModelChanged("pie_sliceEdited");
    }

    private void HandlePieSliceDeleted(JsonElement root)
    {
        if (_pieChartModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _pieChartModel.Slices.Count) return;

        PushUndo();
        _pieChartModel.Slices.RemoveAt(index);
        RaisePieChartModelChanged("pie_sliceDeleted");
    }

    private void HandlePieSettingsChanged(JsonElement root)
    {
        if (_pieChartModel == null) return;

        PushUndo();
        if (root.TryGetProperty("title", out var tProp))
            _pieChartModel.Title = tProp.GetString();
        if (root.TryGetProperty("showData", out var sdProp))
            _pieChartModel.ShowData = sdProp.GetBoolean();
        RaisePieChartModelChanged("pie_settingsChanged");
    }

    private void RestorePieChartModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<PieChartDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _pieChartModel = new PieChartModel
        {
            Title = dto.Title,
            ShowData = dto.ShowData,
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        if (dto.Slices != null)
        {
            foreach (var s in dto.Slices)
            {
                _pieChartModel.Slices.Add(new PieSlice
                {
                    Label = s.Label ?? string.Empty,
                    Value = s.Value
                });
            }
        }
    }

    private void RaisePieChartModelChanged(string changeType)
    {
        if (_pieChartModel != null)
            PieChartModelChanged?.Invoke(this, new PieChartModelChangedEventArgs(changeType, _pieChartModel));
    }

    // ========== Pie Chart DTOs ==========

    private class PieChartDiagramDto
    {
        public string? Title { get; set; }
        public bool ShowData { get; set; }
        public List<PieSliceDto>? Slices { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class PieSliceDto
    {
        public string? Label { get; set; }
        public double Value { get; set; }
    }
}

/// <summary>
/// Event args for when the PieChartModel changes via the visual editor.
/// </summary>
public class PieChartModelChangedEventArgs : EventArgs
{
    public string ChangeType { get; }
    public PieChartModel Model { get; }

    public PieChartModelChangedEventArgs(string changeType, PieChartModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}
