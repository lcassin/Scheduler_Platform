using System.Text.Json;

namespace MermaidEditor;

/// <summary>
/// GitGraph handlers for the visual editor bridge.
/// Handles commit/branch/merge CRUD, command reordering, settings, and model restore.
/// </summary>
public partial class VisualEditorBridge
{
    // ========== GitGraph Support ==========

    private GitGraphModel? _gitGraphModel;

    /// <summary>
    /// Gets the current GitGraphModel (may be null if not in gitGraph mode).
    /// </summary>
    public GitGraphModel? GitGraphModel => _gitGraphModel;

    /// <summary>
    /// Raised when the GitGraphModel is modified by the visual editor.
    /// </summary>
    public event EventHandler<GitGraphModelChangedEventArgs>? GitGraphModelChanged;

    /// <summary>
    /// Sends the current GitGraphModel to the visual editor as JSON.
    /// </summary>
    public async Task SendGitGraphToEditorAsync()
    {
        if (_gitGraphModel == null) return;
        var json = ConvertGitGraphModelToJson(_gitGraphModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadGitGraphDiagram({escaped})");
    }

    /// <summary>
    /// Restores the gitGraph for undo/redo.
    /// </summary>
    private async Task RestoreGitGraphToEditorAsync()
    {
        if (_gitGraphModel == null) return;
        var json = ConvertGitGraphModelToJson(_gitGraphModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.restoreGitGraphDiagram({escaped})");
    }

    /// <summary>
    /// Refreshes the visual editor with the current gitGraph model.
    /// </summary>
    public async Task RefreshGitGraphAsync()
    {
        if (_gitGraphModel == null) return;
        var json = ConvertGitGraphModelToJson(_gitGraphModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.refreshGitGraphDiagram({escaped})");
    }

    /// <summary>
    /// Updates the gitGraph model reference and sends to editor.
    /// </summary>
    public async Task UpdateGitGraphModelAsync(GitGraphModel newModel)
    {
        _gitGraphModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.GitGraph;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendGitGraphToEditorAsync();
    }

    /// <summary>
    /// Converts a GitGraphModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertGitGraphModelToJson(GitGraphModel model)
    {
        var dto = new GitGraphDiagramDto
        {
            Title = model.Title,
            Orientation = model.Orientation,
            Commands = model.Commands.Select(c => new GitGraphCommandDto
            {
                Type = c.Type,
                Id = c.Id,
                Tag = c.Tag,
                CommitType = c.CommitType,
                BranchName = c.BranchName,
                Order = c.Order,
                Parent = c.Parent
            }).ToList(),
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    // ========== GitGraph Message Handlers ==========

    private void HandleGitGraphCommandCreated(JsonElement root)
    {
        if (_gitGraphModel == null) return;

        PushUndo();
        var cmd = ParseGitGraphCommandFromJson(root);

        int? insertAtIndex = null;
        if (root.TryGetProperty("insertAtIndex", out var idxProp) && idxProp.ValueKind == JsonValueKind.Number)
            insertAtIndex = idxProp.GetInt32();

        if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= _gitGraphModel.Commands.Count)
            _gitGraphModel.Commands.Insert(insertAtIndex.Value, cmd);
        else
            _gitGraphModel.Commands.Add(cmd);

        RaiseGitGraphModelChanged("gg_commandCreated");
    }

    private void HandleGitGraphCommandEdited(JsonElement root)
    {
        if (_gitGraphModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _gitGraphModel.Commands.Count) return;

        PushUndo();
        var cmd = _gitGraphModel.Commands[index];

        if (root.TryGetProperty("commandType", out var typeProp))
            cmd.Type = typeProp.GetString() ?? cmd.Type;
        if (root.TryGetProperty("id", out var idProp))
            cmd.Id = idProp.ValueKind == JsonValueKind.Null ? null : idProp.GetString();
        if (root.TryGetProperty("tag", out var tagProp))
            cmd.Tag = tagProp.ValueKind == JsonValueKind.Null ? null : tagProp.GetString();
        if (root.TryGetProperty("commitType", out var ctProp))
            cmd.CommitType = ctProp.ValueKind == JsonValueKind.Null ? null : ctProp.GetString();
        if (root.TryGetProperty("branchName", out var bnProp))
            cmd.BranchName = bnProp.ValueKind == JsonValueKind.Null ? null : bnProp.GetString();
        if (root.TryGetProperty("order", out var orderProp))
            cmd.Order = orderProp.ValueKind == JsonValueKind.Number ? orderProp.GetInt32() : null;
        if (root.TryGetProperty("parent", out var parentProp))
            cmd.Parent = parentProp.ValueKind == JsonValueKind.Null ? null : parentProp.GetString();

        RaiseGitGraphModelChanged("gg_commandEdited");
    }

    private void HandleGitGraphCommandDeleted(JsonElement root)
    {
        if (_gitGraphModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _gitGraphModel.Commands.Count) return;

        PushUndo();
        _gitGraphModel.Commands.RemoveAt(index);
        RaiseGitGraphModelChanged("gg_commandDeleted");
    }

    private void HandleGitGraphCommandMoved(JsonElement root)
    {
        if (_gitGraphModel == null) return;
        var fromIndex = root.GetProperty("fromIndex").GetInt32();
        var toIndex = root.GetProperty("toIndex").GetInt32();
        if (fromIndex < 0 || fromIndex >= _gitGraphModel.Commands.Count) return;
        if (toIndex < 0 || toIndex >= _gitGraphModel.Commands.Count) return;

        PushUndo();
        var cmd = _gitGraphModel.Commands[fromIndex];
        _gitGraphModel.Commands.RemoveAt(fromIndex);
        _gitGraphModel.Commands.Insert(toIndex, cmd);
        RaiseGitGraphModelChanged("gg_commandMoved");
    }

    private void HandleGitGraphSettingsChanged(JsonElement root)
    {
        if (_gitGraphModel == null) return;

        PushUndo();
        if (root.TryGetProperty("title", out var tProp))
            _gitGraphModel.Title = tProp.ValueKind == JsonValueKind.Null ? null : tProp.GetString();
        if (root.TryGetProperty("orientation", out var oProp))
            _gitGraphModel.Orientation = oProp.ValueKind == JsonValueKind.Null ? null : oProp.GetString();
        RaiseGitGraphModelChanged("gg_settingsChanged");
    }

    private GitGraphCommand ParseGitGraphCommandFromJson(JsonElement root)
    {
        var cmd = new GitGraphCommand();

        if (root.TryGetProperty("commandType", out var typeProp))
            cmd.Type = typeProp.GetString() ?? "commit";
        if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            cmd.Id = idProp.GetString();
        if (root.TryGetProperty("tag", out var tagProp) && tagProp.ValueKind == JsonValueKind.String)
            cmd.Tag = tagProp.GetString();
        if (root.TryGetProperty("commitType", out var ctProp) && ctProp.ValueKind == JsonValueKind.String)
            cmd.CommitType = ctProp.GetString();
        if (root.TryGetProperty("branchName", out var bnProp) && bnProp.ValueKind == JsonValueKind.String)
            cmd.BranchName = bnProp.GetString();
        if (root.TryGetProperty("order", out var orderProp) && orderProp.ValueKind == JsonValueKind.Number)
            cmd.Order = orderProp.GetInt32();
        if (root.TryGetProperty("parent", out var parentProp) && parentProp.ValueKind == JsonValueKind.String)
            cmd.Parent = parentProp.GetString();

        return cmd;
    }

    private void RestoreGitGraphModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<GitGraphDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _gitGraphModel = new GitGraphModel
        {
            Title = dto.Title,
            Orientation = dto.Orientation,
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        if (dto.Commands != null)
        {
            foreach (var c in dto.Commands)
            {
                _gitGraphModel.Commands.Add(new GitGraphCommand
                {
                    Type = c.Type ?? "commit",
                    Id = c.Id,
                    Tag = c.Tag,
                    CommitType = c.CommitType,
                    BranchName = c.BranchName,
                    Order = c.Order,
                    Parent = c.Parent
                });
            }
        }
    }

    private void RaiseGitGraphModelChanged(string changeType)
    {
        if (_gitGraphModel != null)
            GitGraphModelChanged?.Invoke(this, new GitGraphModelChangedEventArgs(changeType, _gitGraphModel));
    }

    // ========== GitGraph DTOs ==========

    private class GitGraphDiagramDto
    {
        public string? Title { get; set; }
        public string? Orientation { get; set; }
        public List<GitGraphCommandDto>? Commands { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class GitGraphCommandDto
    {
        public string? Type { get; set; }
        public string? Id { get; set; }
        public string? Tag { get; set; }
        public string? CommitType { get; set; }
        public string? BranchName { get; set; }
        public int? Order { get; set; }
        public string? Parent { get; set; }
    }
}

/// <summary>
/// Event args for when the GitGraphModel changes via the visual editor.
/// </summary>
public class GitGraphModelChangedEventArgs : EventArgs
{
    public string ChangeType { get; }
    public GitGraphModel Model { get; }

    public GitGraphModelChangedEventArgs(string changeType, GitGraphModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}
