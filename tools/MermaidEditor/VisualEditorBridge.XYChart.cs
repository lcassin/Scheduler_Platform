using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// XY Chart handlers for the visual editor bridge.
/// Handles data series CRUD, settings changes, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== XY Chart Support ==========

    private XYChartModel? _xyChartModel;

    /// <summary>
    /// Gets the current XYChartModel (may be null if not in XY chart mode).
    /// </summary>
    public XYChartModel? XYChartModel => _xyChartModel;

    /// <summary>
    /// Raised when the XYChartModel is modified by the visual editor.
    /// </summary>
    public event EventHandler<XYChartModelChangedEventArgs>? XYChartModelChanged;

    /// <summary>
    /// Sends the current XYChartModel to the visual editor as JSON.
    /// </summary>
    public async Task SendXYChartToEditorAsync()
    {
        if (_xyChartModel == null) return;
        var json = ConvertXYChartModelToJson(_xyChartModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadXYChart({escaped})");
    }

    /// <summary>
    /// Restores the XY chart for undo/redo.
    /// </summary>
    private async Task RestoreXYChartToEditorAsync()
    {
        if (_xyChartModel == null) return;
        var json = ConvertXYChartModelToJson(_xyChartModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreXYChart({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current XY chart model.
    /// </summary>
    public async Task RefreshXYChartAsync()
    {
        if (_xyChartModel == null) return;
        var json = ConvertXYChartModelToJson(_xyChartModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshXYChart({escaped})");
    }

    /// <summary>
    /// Updates the XY chart model reference and sends to editor.
    /// </summary>
    public async Task UpdateXYChartModelAsync(XYChartModel newModel)
    {
        _xyChartModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.XYChart;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendXYChartToEditorAsync();
    }

    /// <summary>
    /// Converts an XYChartModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertXYChartModelToJson(XYChartModel model)
    {
        var dto = new XYChartDiagramDto
        {
            Title = model.Title,
            Horizontal = model.Horizontal,
            XAxisTitle = model.XAxisTitle,
            XAxisCategories = model.XAxisCategories,
            XAxisMin = model.XAxisMin,
            XAxisMax = model.XAxisMax,
            YAxisTitle = model.YAxisTitle,
            YAxisMin = model.YAxisMin,
            YAxisMax = model.YAxisMax,
            DataSeries = model.DataSeries.Select(s => new XYChartDataSeriesDto
            {
                Type = s.Type,
                Data = s.Data
            }).ToList(),
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    // ========== XY Chart Message Handlers ==========

    private void HandleXYChartSeriesCreated(JsonElement root)
    {
        if (_xyChartModel == null) return;

        var type = root.TryGetProperty("seriesType", out var tProp) ? tProp.GetString() ?? "bar" : "bar";

        PushUndo();
        var series = new XYChartDataSeries { Type = type };

        if (root.TryGetProperty("data", out var dProp) && dProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var val in dProp.EnumerateArray())
            {
                series.Data.Add(val.GetDouble());
            }
        }

        // Insert at specific index or append
        if (root.TryGetProperty("index", out var iProp))
        {
            var idx = iProp.GetInt32();
            if (idx >= 0 && idx <= _xyChartModel.DataSeries.Count)
                _xyChartModel.DataSeries.Insert(idx, series);
            else
                _xyChartModel.DataSeries.Add(series);
        }
        else
        {
            _xyChartModel.DataSeries.Add(series);
        }

        RaiseXYChartModelChanged("xy_seriesCreated");
    }

    private void HandleXYChartSeriesEdited(JsonElement root)
    {
        if (_xyChartModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _xyChartModel.DataSeries.Count) return;

        PushUndo();
        var series = _xyChartModel.DataSeries[index];

        if (root.TryGetProperty("seriesType", out var tProp))
            series.Type = tProp.GetString() ?? series.Type;

        if (root.TryGetProperty("data", out var dProp) && dProp.ValueKind == JsonValueKind.Array)
        {
            series.Data.Clear();
            foreach (var val in dProp.EnumerateArray())
            {
                series.Data.Add(val.GetDouble());
            }
        }

        RaiseXYChartModelChanged("xy_seriesEdited");
    }

    private void HandleXYChartSeriesDeleted(JsonElement root)
    {
        if (_xyChartModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _xyChartModel.DataSeries.Count) return;

        PushUndo();
        _xyChartModel.DataSeries.RemoveAt(index);
        RaiseXYChartModelChanged("xy_seriesDeleted");
    }

    private void HandleXYChartSeriesMoved(JsonElement root)
    {
        if (_xyChartModel == null) return;
        var fromIndex = root.GetProperty("fromIndex").GetInt32();
        var toIndex = root.GetProperty("toIndex").GetInt32();
        if (fromIndex < 0 || fromIndex >= _xyChartModel.DataSeries.Count) return;
        if (toIndex < 0 || toIndex >= _xyChartModel.DataSeries.Count) return;

        PushUndo();
        var series = _xyChartModel.DataSeries[fromIndex];
        _xyChartModel.DataSeries.RemoveAt(fromIndex);
        _xyChartModel.DataSeries.Insert(toIndex, series);
        RaiseXYChartModelChanged("xy_seriesMoved");
    }

    private void HandleXYChartSettingsChanged(JsonElement root)
    {
        if (_xyChartModel == null) return;

        PushUndo();

        if (root.TryGetProperty("title", out var tProp))
            _xyChartModel.Title = tProp.ValueKind == JsonValueKind.Null ? null : tProp.GetString();
        if (root.TryGetProperty("horizontal", out var hProp))
            _xyChartModel.Horizontal = hProp.GetBoolean();

        // X-axis settings
        if (root.TryGetProperty("xAxisTitle", out var xTitleProp))
            _xyChartModel.XAxisTitle = xTitleProp.ValueKind == JsonValueKind.Null ? null : xTitleProp.GetString();

        if (root.TryGetProperty("xAxisCategories", out var xCatProp))
        {
            if (xCatProp.ValueKind == JsonValueKind.Array)
            {
                _xyChartModel.XAxisCategories = xCatProp.EnumerateArray()
                    .Select(v => v.GetString() ?? string.Empty)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToList();
                _xyChartModel.XAxisMin = null;
                _xyChartModel.XAxisMax = null;
            }
            else if (xCatProp.ValueKind == JsonValueKind.Null)
            {
                _xyChartModel.XAxisCategories = null;
            }
        }

        if (root.TryGetProperty("xAxisMin", out var xMinProp))
            _xyChartModel.XAxisMin = xMinProp.ValueKind == JsonValueKind.Null ? null : xMinProp.GetDouble();
        if (root.TryGetProperty("xAxisMax", out var xMaxProp))
            _xyChartModel.XAxisMax = xMaxProp.ValueKind == JsonValueKind.Null ? null : xMaxProp.GetDouble();

        // Y-axis settings
        if (root.TryGetProperty("yAxisTitle", out var yTitleProp))
            _xyChartModel.YAxisTitle = yTitleProp.ValueKind == JsonValueKind.Null ? null : yTitleProp.GetString();
        if (root.TryGetProperty("yAxisMin", out var yMinProp))
            _xyChartModel.YAxisMin = yMinProp.ValueKind == JsonValueKind.Null ? null : yMinProp.GetDouble();
        if (root.TryGetProperty("yAxisMax", out var yMaxProp))
            _xyChartModel.YAxisMax = yMaxProp.ValueKind == JsonValueKind.Null ? null : yMaxProp.GetDouble();

        RaiseXYChartModelChanged("xy_settingsChanged");
    }

    private void RestoreXYChartModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<XYChartDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _xyChartModel = new XYChartModel
        {
            Title = dto.Title,
            Horizontal = dto.Horizontal,
            XAxisTitle = dto.XAxisTitle,
            XAxisCategories = dto.XAxisCategories,
            XAxisMin = dto.XAxisMin,
            XAxisMax = dto.XAxisMax,
            YAxisTitle = dto.YAxisTitle,
            YAxisMin = dto.YAxisMin,
            YAxisMax = dto.YAxisMax,
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        if (dto.DataSeries != null)
        {
            foreach (var s in dto.DataSeries)
            {
                _xyChartModel.DataSeries.Add(new XYChartDataSeries
                {
                    Type = s.Type ?? "bar",
                    Data = s.Data ?? new List<double>()
                });
            }
        }
    }

    private void RaiseXYChartModelChanged(string changeType)
    {
        if (_xyChartModel != null)
            XYChartModelChanged?.Invoke(this, new XYChartModelChangedEventArgs(changeType, _xyChartModel));
    }

    // ========== XY Chart DTOs ==========

    private class XYChartDiagramDto
    {
        public string? Title { get; set; }
        public bool Horizontal { get; set; }
        public string? XAxisTitle { get; set; }
        public List<string>? XAxisCategories { get; set; }
        public double? XAxisMin { get; set; }
        public double? XAxisMax { get; set; }
        public string? YAxisTitle { get; set; }
        public double? YAxisMin { get; set; }
        public double? YAxisMax { get; set; }
        public List<XYChartDataSeriesDto>? DataSeries { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class XYChartDataSeriesDto
    {
        public string? Type { get; set; }
        public List<double>? Data { get; set; }
    }
}

/// <summary>
/// Event args for when the XYChartModel changes via the visual editor.
/// </summary>
public class XYChartModelChangedEventArgs : EventArgs
{
    public string ChangeType { get; }
    public XYChartModel Model { get; }

    public XYChartModelChangedEventArgs(string changeType, XYChartModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}
