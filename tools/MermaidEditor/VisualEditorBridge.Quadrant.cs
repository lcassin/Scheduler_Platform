using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// Quadrant chart handlers for the visual editor bridge.
/// Handles point CRUD, settings changes, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== Quadrant Chart Support ==========

    private QuadrantChartModel? _quadrantChartModel;

    /// <summary>
    /// Gets the current QuadrantChartModel (may be null if not in quadrant chart mode).
    /// </summary>
    public QuadrantChartModel? QuadrantChartModel => _quadrantChartModel;

    /// <summary>
    /// Raised when the QuadrantChartModel is modified by the visual editor.
    /// </summary>
    public event EventHandler<QuadrantChartModelChangedEventArgs>? QuadrantChartModelChanged;

    /// <summary>
    /// Sends the current QuadrantChartModel to the visual editor as JSON.
    /// </summary>
    public async Task SendQuadrantChartToEditorAsync()
    {
        if (_quadrantChartModel == null) return;
        var json = ConvertQuadrantChartModelToJson(_quadrantChartModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadQuadrantDiagram({escaped})");
    }

    /// <summary>
    /// Restores the quadrant chart for undo/redo.
    /// </summary>
    private async Task RestoreQuadrantChartToEditorAsync()
    {
        if (_quadrantChartModel == null) return;
        var json = ConvertQuadrantChartModelToJson(_quadrantChartModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreQuadrantDiagram({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current quadrant chart model.
    /// </summary>
    public async Task RefreshQuadrantChartAsync()
    {
        if (_quadrantChartModel == null) return;
        var json = ConvertQuadrantChartModelToJson(_quadrantChartModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshQuadrantDiagram({escaped})");
    }

    /// <summary>
    /// Updates the quadrant chart model reference and sends to editor.
    /// </summary>
    public async Task UpdateQuadrantChartModelAsync(QuadrantChartModel newModel)
    {
        _quadrantChartModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.QuadrantChart;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendQuadrantChartToEditorAsync();
    }

    /// <summary>
    /// Converts a QuadrantChartModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertQuadrantChartModelToJson(QuadrantChartModel model)
    {
        var dto = new QuadrantChartDiagramDto
        {
            Title = model.Title,
            XAxisLeft = model.XAxisLeft,
            XAxisRight = model.XAxisRight,
            YAxisBottom = model.YAxisBottom,
            YAxisTop = model.YAxisTop,
            Quadrant1 = model.Quadrant1,
            Quadrant2 = model.Quadrant2,
            Quadrant3 = model.Quadrant3,
            Quadrant4 = model.Quadrant4,
            Points = model.Points.Select(p => new QuadrantPointDto
            {
                Label = p.Label,
                X = p.X,
                Y = p.Y
            }).ToList(),
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    // ========== Quadrant Chart Message Handlers ==========

    private void HandleQuadrantPointCreated(JsonElement root)
    {
        if (_quadrantChartModel == null) return;

        PushUndo();
        var label = root.GetProperty("label").GetString() ?? "New Point";
        double x = 0.5, y = 0.5;
        if (root.TryGetProperty("x", out var xProp) && xProp.ValueKind == JsonValueKind.Number)
            x = Math.Clamp(xProp.GetDouble(), 0.0, 1.0);
        if (root.TryGetProperty("y", out var yProp) && yProp.ValueKind == JsonValueKind.Number)
            y = Math.Clamp(yProp.GetDouble(), 0.0, 1.0);

        var newPoint = new QuadrantPoint
        {
            Label = label,
            X = x,
            Y = y
        };

        // Read optional insertAtIndex
        int? insertAtIndex = null;
        if (root.TryGetProperty("insertAtIndex", out var idxProp) && idxProp.ValueKind == JsonValueKind.Number)
            insertAtIndex = idxProp.GetInt32();

        if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= _quadrantChartModel.Points.Count)
            _quadrantChartModel.Points.Insert(insertAtIndex.Value, newPoint);
        else
            _quadrantChartModel.Points.Add(newPoint);

        RaiseQuadrantChartModelChanged("qc_pointCreated");
    }

    private void HandleQuadrantPointEdited(JsonElement root)
    {
        if (_quadrantChartModel == null) return;

        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _quadrantChartModel.Points.Count) return;

        var point = _quadrantChartModel.Points[index];
        PushUndo();

        if (root.TryGetProperty("label", out var labelProp))
            point.Label = labelProp.GetString() ?? point.Label;

        if (root.TryGetProperty("x", out var xProp) && xProp.ValueKind == JsonValueKind.Number)
            point.X = Math.Clamp(xProp.GetDouble(), 0.0, 1.0);

        if (root.TryGetProperty("y", out var yProp) && yProp.ValueKind == JsonValueKind.Number)
            point.Y = Math.Clamp(yProp.GetDouble(), 0.0, 1.0);

        RaiseQuadrantChartModelChanged("qc_pointEdited");
    }

    private void HandleQuadrantPointDeleted(JsonElement root)
    {
        if (_quadrantChartModel == null) return;

        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _quadrantChartModel.Points.Count) return;

        PushUndo();
        _quadrantChartModel.Points.RemoveAt(index);
        RaiseQuadrantChartModelChanged("qc_pointDeleted");
    }

    private void HandleQuadrantPointMoved(JsonElement root)
    {
        if (_quadrantChartModel == null) return;

        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _quadrantChartModel.Points.Count) return;

        PushUndo();
        var point = _quadrantChartModel.Points[index];

        if (root.TryGetProperty("x", out var xProp) && xProp.ValueKind == JsonValueKind.Number)
            point.X = Math.Clamp(xProp.GetDouble(), 0.0, 1.0);

        if (root.TryGetProperty("y", out var yProp) && yProp.ValueKind == JsonValueKind.Number)
            point.Y = Math.Clamp(yProp.GetDouble(), 0.0, 1.0);

        RaiseQuadrantChartModelChanged("qc_pointMoved");
    }

    private void HandleQuadrantSettingsChanged(JsonElement root)
    {
        if (_quadrantChartModel == null) return;

        PushUndo();

        if (root.TryGetProperty("title", out var tProp))
            _quadrantChartModel.Title = tProp.ValueKind == JsonValueKind.Null ? null : tProp.GetString();

        if (root.TryGetProperty("xAxisLeft", out var xLProp))
            _quadrantChartModel.XAxisLeft = xLProp.ValueKind == JsonValueKind.Null ? null : xLProp.GetString();

        if (root.TryGetProperty("xAxisRight", out var xRProp))
            _quadrantChartModel.XAxisRight = xRProp.ValueKind == JsonValueKind.Null ? null : xRProp.GetString();

        if (root.TryGetProperty("yAxisBottom", out var yBProp))
            _quadrantChartModel.YAxisBottom = yBProp.ValueKind == JsonValueKind.Null ? null : yBProp.GetString();

        if (root.TryGetProperty("yAxisTop", out var yTProp))
            _quadrantChartModel.YAxisTop = yTProp.ValueKind == JsonValueKind.Null ? null : yTProp.GetString();

        if (root.TryGetProperty("quadrant1", out var q1Prop))
            _quadrantChartModel.Quadrant1 = q1Prop.ValueKind == JsonValueKind.Null ? null : q1Prop.GetString();

        if (root.TryGetProperty("quadrant2", out var q2Prop))
            _quadrantChartModel.Quadrant2 = q2Prop.ValueKind == JsonValueKind.Null ? null : q2Prop.GetString();

        if (root.TryGetProperty("quadrant3", out var q3Prop))
            _quadrantChartModel.Quadrant3 = q3Prop.ValueKind == JsonValueKind.Null ? null : q3Prop.GetString();

        if (root.TryGetProperty("quadrant4", out var q4Prop))
            _quadrantChartModel.Quadrant4 = q4Prop.ValueKind == JsonValueKind.Null ? null : q4Prop.GetString();

        RaiseQuadrantChartModelChanged("qc_settingsChanged");
    }

    private void RestoreQuadrantChartModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<QuadrantChartDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _quadrantChartModel = new QuadrantChartModel
        {
            Title = dto.Title,
            XAxisLeft = dto.XAxisLeft,
            XAxisRight = dto.XAxisRight,
            YAxisBottom = dto.YAxisBottom,
            YAxisTop = dto.YAxisTop,
            Quadrant1 = dto.Quadrant1,
            Quadrant2 = dto.Quadrant2,
            Quadrant3 = dto.Quadrant3,
            Quadrant4 = dto.Quadrant4,
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        if (dto.Points != null)
        {
            foreach (var p in dto.Points)
            {
                _quadrantChartModel.Points.Add(new QuadrantPoint
                {
                    Label = p.Label ?? string.Empty,
                    X = Math.Clamp(p.X, 0.0, 1.0),
                    Y = Math.Clamp(p.Y, 0.0, 1.0)
                });
            }
        }
    }

    private void RaiseQuadrantChartModelChanged(string changeType)
    {
        if (_quadrantChartModel != null)
            QuadrantChartModelChanged?.Invoke(this, new QuadrantChartModelChangedEventArgs(changeType, _quadrantChartModel));
    }

    // ========== Quadrant Chart DTOs ==========

    private class QuadrantChartDiagramDto
    {
        public string? Title { get; set; }
        public string? XAxisLeft { get; set; }
        public string? XAxisRight { get; set; }
        public string? YAxisBottom { get; set; }
        public string? YAxisTop { get; set; }
        public string? Quadrant1 { get; set; }
        public string? Quadrant2 { get; set; }
        public string? Quadrant3 { get; set; }
        public string? Quadrant4 { get; set; }
        public List<QuadrantPointDto>? Points { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class QuadrantPointDto
    {
        public string? Label { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}

/// <summary>
/// Event args for when the QuadrantChartModel changes via the visual editor.
/// </summary>
public class QuadrantChartModelChangedEventArgs : EventArgs
{
    public string ChangeType { get; }
    public QuadrantChartModel Model { get; }

    public QuadrantChartModelChangedEventArgs(string changeType, QuadrantChartModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}
