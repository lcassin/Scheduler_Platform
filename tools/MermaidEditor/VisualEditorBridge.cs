using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;

namespace MermaidEditor;

/// <summary>
/// Tracks which diagram type is currently active in the visual editor.
/// </summary>
public enum ActiveDiagramType
{
    Flowchart,
    Sequence,
    ClassDiagram,
    StateDiagram,
    ERDiagram
}

/// <summary>
/// Handles bidirectional communication between the visual editor WebView2
/// and the C# diagram models. Converts models to JSON for JS, processes
/// messages from JS, and maintains undo/redo history.
/// Supports flowchart, sequence, and class diagram types.
/// </summary>
public partial class VisualEditorBridge
{
    private readonly WebView2 _webView;
    private FlowchartModel _model;
    private SequenceDiagramModel? _sequenceModel;
    private ClassDiagramModel? _classDiagramModel;
    private StateDiagramModel? _stateDiagramModel;
    private ERDiagramModel? _erDiagramModel;
    private ActiveDiagramType _activeDiagramType = ActiveDiagramType.Flowchart;
    private readonly List<string> _undoStack = new();
    private readonly List<string> _redoStack = new();
    private const int MaxHistorySize = 100;
    private bool _isSuppressingUpdates;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Raised when the FlowchartModel is modified by the visual editor.
    /// The handler should regenerate the Mermaid text via MermaidSerializer.
    /// </summary>
    public event EventHandler<ModelChangedEventArgs>? ModelChanged;

    /// <summary>
    /// Raised when the SequenceDiagramModel is modified by the visual editor.
    /// The handler should regenerate the Mermaid text via MermaidSerializer.
    /// </summary>
    public event EventHandler<SequenceModelChangedEventArgs>? SequenceModelChanged;

    /// <summary>
    /// Raised when the ClassDiagramModel is modified by the visual editor.
    /// The handler should regenerate the Mermaid text via MermaidSerializer.
    /// </summary>
    public event EventHandler<ClassDiagramModelChangedEventArgs>? ClassDiagramModelChanged;

    /// <summary>
    /// Raised when the StateDiagramModel is modified by the visual editor.
    /// </summary>
    public event EventHandler<StateDiagramModelChangedEventArgs>? StateDiagramModelChanged;

    /// <summary>
    /// Raised when the ERDiagramModel is modified by the visual editor.
    /// </summary>
    public event EventHandler<ERDiagramModelChangedEventArgs>? ERDiagramModelChanged;

    /// <summary>
    /// Raised when a node is selected in the visual editor.
    /// </summary>
    public event EventHandler<NodeSelectedEventArgs>? NodeSelected;

    /// <summary>
    /// Raised when selection is cleared in the visual editor.
    /// </summary>
    public event EventHandler? SelectionCleared;

    /// <summary>
    /// Raised when the visual editor is ready to receive diagram data.
    /// </summary>
    public event EventHandler? EditorReady;

    /// <summary>
    /// Gets the current FlowchartModel.
    /// </summary>
    public FlowchartModel Model => _model;

    /// <summary>
    /// Gets the current SequenceDiagramModel (may be null if not in sequence mode).
    /// </summary>
    public SequenceDiagramModel? SequenceModel => _sequenceModel;

    /// <summary>
    /// Gets the current ClassDiagramModel (may be null if not in class diagram mode).
    /// </summary>
    public ClassDiagramModel? ClassDiagramModel => _classDiagramModel;

    /// <summary>
    /// Gets the current StateDiagramModel (may be null if not in state diagram mode).
    /// </summary>
    public StateDiagramModel? StateDiagramModel => _stateDiagramModel;

    /// <summary>
    /// Gets the current ERDiagramModel (may be null if not in ER diagram mode).
    /// </summary>
    public ERDiagramModel? ERDiagramModel => _erDiagramModel;

    /// <summary>
    /// Gets the currently active diagram type.
    /// </summary>
    public ActiveDiagramType ActiveDiagramType => _activeDiagramType;

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Creates a new VisualEditorBridge for the given WebView2 control.
    /// </summary>
    /// <param name="webView">The WebView2 control hosting the visual editor.</param>
    /// <param name="model">The initial FlowchartModel to display.</param>
    public VisualEditorBridge(WebView2 webView, FlowchartModel model)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _model = model ?? throw new ArgumentNullException(nameof(model));

        _webView.WebMessageReceived += OnWebMessageReceived;
    }

    /// <summary>
    /// Detaches event handlers. Call when disposing.
    /// </summary>
    public void Detach()
    {
        _webView.WebMessageReceived -= OnWebMessageReceived;
    }

    // ========== Shared Methods ==========

    /// <summary>
    /// Sets the visual editor theme.
    /// </summary>
    /// <param name="theme">"dark" or "light".</param>
    public async Task SetThemeAsync(string theme)
    {
        var escaped = JsonSerializer.Serialize(theme);
        await _webView.ExecuteScriptAsync($"window.setTheme({escaped})");
    }

    /// <summary>
    /// Triggers auto-layout in the visual editor.
    /// </summary>
    public async Task ApplyAutoLayoutAsync()
    {
        await _webView.ExecuteScriptAsync("window.applyAutoLayout()");
    }

    /// <summary>
    /// Undoes the last model change.
    /// </summary>
    public async Task UndoAsync()
    {
        if (_undoStack.Count == 0) return;

        // Save current state to redo
        var currentJson = GetCurrentModelJson();
        _redoStack.Add(currentJson);
        if (_redoStack.Count > MaxHistorySize)
            _redoStack.RemoveAt(0);

        // Restore previous state
        var previousJson = _undoStack[_undoStack.Count - 1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        _isSuppressingUpdates = true;
        try
        {
            if (_activeDiagramType == ActiveDiagramType.StateDiagram)
            {
                RestoreStateDiagramModelFromJson(previousJson);
                await RestoreStateDiagramToEditorAsync();
                RaiseStateDiagramModelChanged("undo");
            }
            else if (_activeDiagramType == ActiveDiagramType.ERDiagram)
            {
                RestoreERDiagramModelFromJson(previousJson);
                await RestoreERDiagramToEditorAsync();
                RaiseERDiagramModelChanged("undo");
            }
            else if (_activeDiagramType == ActiveDiagramType.ClassDiagram)
            {
                RestoreClassDiagramModelFromJson(previousJson);
                await RestoreClassDiagramToEditorAsync();
                RaiseClassDiagramModelChanged("undo");
            }
            else if (_activeDiagramType == ActiveDiagramType.Sequence)
            {
                RestoreSequenceModelFromJson(previousJson);
                await RefreshSequenceDiagramAsync();
                RaiseSequenceModelChanged("undo");
            }
            else
            {
                RestoreModelFromJson(previousJson);
                await RestoreDiagramToEditorAsync();
                RaiseModelChanged("undo");
            }
        }
        finally
        {
            _isSuppressingUpdates = false;
        }
    }

    /// <summary>
    /// Redoes the last undone change.
    /// </summary>
    public async Task RedoAsync()
    {
        if (_redoStack.Count == 0) return;

        // Save current state to undo
        var currentJson = GetCurrentModelJson();
        _undoStack.Add(currentJson);
        if (_undoStack.Count > MaxHistorySize)
            _undoStack.RemoveAt(0);

        // Restore redo state
        var redoJson = _redoStack[_redoStack.Count - 1];
        _redoStack.RemoveAt(_redoStack.Count - 1);

        _isSuppressingUpdates = true;
        try
        {
            if (_activeDiagramType == ActiveDiagramType.StateDiagram)
            {
                RestoreStateDiagramModelFromJson(redoJson);
                await RestoreStateDiagramToEditorAsync();
                RaiseStateDiagramModelChanged("redo");
            }
            else if (_activeDiagramType == ActiveDiagramType.ERDiagram)
            {
                RestoreERDiagramModelFromJson(redoJson);
                await RestoreERDiagramToEditorAsync();
                RaiseERDiagramModelChanged("redo");
            }
            else if (_activeDiagramType == ActiveDiagramType.ClassDiagram)
            {
                RestoreClassDiagramModelFromJson(redoJson);
                await RestoreClassDiagramToEditorAsync();
                RaiseClassDiagramModelChanged("redo");
            }
            else if (_activeDiagramType == ActiveDiagramType.Sequence)
            {
                RestoreSequenceModelFromJson(redoJson);
                await RefreshSequenceDiagramAsync();
                RaiseSequenceModelChanged("redo");
            }
            else
            {
                RestoreModelFromJson(redoJson);
                await RestoreDiagramToEditorAsync();
                RaiseModelChanged("redo");
            }
        }
        finally
        {
            _isSuppressingUpdates = false;
        }
    }

    /// <summary>
    /// Gets the JSON snapshot of the currently active model for undo/redo.
    /// </summary>
    private string GetCurrentModelJson()
    {
        if (_activeDiagramType == ActiveDiagramType.StateDiagram && _stateDiagramModel != null)
            return ConvertStateDiagramModelToJson(_stateDiagramModel);
        if (_activeDiagramType == ActiveDiagramType.ERDiagram && _erDiagramModel != null)
            return ConvertERDiagramModelToJson(_erDiagramModel);
        if (_activeDiagramType == ActiveDiagramType.ClassDiagram && _classDiagramModel != null)
            return ConvertClassDiagramModelToJson(_classDiagramModel);
        if (_activeDiagramType == ActiveDiagramType.Sequence && _sequenceModel != null)
            return ConvertSequenceModelToJson(_sequenceModel);
        return ConvertModelToJson(_model);
    }

    // ========== WebView2 Message Handler ==========

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_isSuppressingUpdates) return;

        try
        {
            var json = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
                return;

            var messageType = typeProp.GetString();

            switch (messageType)
            {
                case "editorReady":
                    EditorReady?.Invoke(this, EventArgs.Empty);
                    break;

                case "nodeMoved":
                    HandleNodeMoved(root);
                    break;

                case "nodeEdited":
                    HandleNodeEdited(root);
                    break;

                case "nodeCreated":
                    HandleNodeCreated(root);
                    break;

                case "nodeDeleted":
                    HandleNodeDeleted(root);
                    break;

                case "edgeCreated":
                    HandleEdgeCreated(root);
                    break;

                case "edgeEdited":
                    HandleEdgeEdited(root);
                    break;

                case "edgeDeleted":
                    HandleEdgeDeleted(root);
                    break;

                case "nodeSelected":
                    HandleNodeSelected(root);
                    break;

                case "edgeSelected":
                    // Edge selection doesn't need model changes
                    break;

                case "selectionCleared":
                    SelectionCleared?.Invoke(this, EventArgs.Empty);
                    break;

                case "autoLayoutApplied":
                    // Legacy: individual nodeMoved messages (no longer used)
                    break;

                case "autoLayoutComplete":
                    HandleAutoLayoutComplete(root);
                    break;

                case "nodeResized":
                    HandleNodeResized(root);
                    break;

                case "nodeShapeChanged":
                    HandleNodeShapeChanged(root);
                    break;

                case "nodeStyleChanged":
                    HandleNodeStyleChanged(root);
                    break;

                case "subgraphCreated":
                    HandleSubgraphCreated(root);
                    break;

                case "subgraphEdited":
                    HandleSubgraphEdited(root);
                    break;

                case "subgraphDeleted":
                    HandleSubgraphDeleted(root);
                    break;

                case "nodeSubgraphChanged":
                    HandleNodeSubgraphChanged(root);
                    break;

                case "subgraphSelected":
                    // Subgraph selection doesn't need model changes
                    break;

                case "undo":
                    _ = UndoAsync();
                    break;

                case "redo":
                    _ = RedoAsync();
                    break;

                // ===== Class Diagram Messages =====

                case "cls_classCreated":
                    HandleClsClassCreated(root);
                    break;

                case "cls_classEdited":
                    HandleClsClassEdited(root);
                    break;

                case "cls_classDeleted":
                    HandleClsClassDeleted(root);
                    break;

                case "cls_memberAdded":
                    HandleClsMemberAdded(root);
                    break;

                case "cls_memberEdited":
                    HandleClsMemberEdited(root);
                    break;

                case "cls_memberDeleted":
                    HandleClsMemberDeleted(root);
                    break;

                case "cls_memberMoved":
                    HandleClsMemberMoved(root);
                    break;

                case "cls_relationshipCreated":
                    HandleClsRelationshipCreated(root);
                    break;

                case "cls_relationshipEdited":
                    HandleClsRelationshipEdited(root);
                    break;

                case "cls_relationshipDeleted":
                    HandleClsRelationshipDeleted(root);
                    break;

                case "cls_autoLayoutComplete":
                    HandleClsAutoLayoutComplete(root);
                    break;

                case "cls_classMoved":
                    HandleClsClassMoved(root);
                    break;

                case "cls_classSelected":
                case "cls_relationshipSelected":
                    // Selection doesn't need model changes
                    break;

                // ===== Sequence Diagram Messages =====

                case "seq_participantReordered":
                    HandleSeqParticipantReordered(root);
                    break;

                case "seq_participantEdited":
                    HandleSeqParticipantEdited(root);
                    break;

                case "seq_participantCreated":
                    HandleSeqParticipantCreated(root);
                    break;

                case "seq_participantDeleted":
                    HandleSeqParticipantDeleted(root);
                    break;

                case "seq_messageCreated":
                    HandleSeqMessageCreated(root);
                    break;

                case "seq_messageEdited":
                    HandleSeqMessageEdited(root);
                    break;

                case "seq_messageDeleted":
                    HandleSeqMessageDeleted(root);
                    break;

                case "seq_noteCreated":
                    HandleSeqNoteCreated(root);
                    break;

                case "seq_noteEdited":
                    HandleSeqNoteEdited(root);
                    break;

                case "seq_noteDeleted":
                    HandleSeqNoteDeleted(root);
                    break;

                case "seq_fragmentCreated":
                    HandleSeqFragmentCreated(root);
                    break;

                case "seq_fragmentEdited":
                    HandleSeqFragmentEdited(root);
                    break;

                case "seq_fragmentDeleted":
                    HandleSeqFragmentDeleted(root);
                    break;

                case "seq_fragmentSectionEdited":
                    HandleSeqFragmentSectionEdited(root);
                    break;

                case "seq_fragmentSectionAdded":
                    HandleSeqFragmentSectionAdded(root);
                    break;

                case "seq_fragmentInnerMessageEdited":
                    HandleSeqFragmentInnerMessageEdited(root);
                    break;

                case "seq_fragmentInnerMessageDeleted":
                    HandleSeqFragmentInnerMessageDeleted(root);
                    break;

                case "seq_fragmentInnerMessageCreated":
                    HandleSeqFragmentInnerMessageCreated(root);
                    break;

                case "seq_messageMovedToFragment":
                    HandleSeqMessageMovedToFragment(root);
                    break;

                case "seq_elementReordered":
                    HandleSeqElementReordered(root);
                    break;

                case "seq_participantSelected":
                case "seq_messageSelected":
                case "seq_fragmentSelected":
                    // Selection doesn't need model changes
                    break;

                // ===== State Diagram Messages =====

                case "st_stateMoved":
                    HandleStStateMoved(root);
                    break;

                case "st_stateCreated":
                    HandleStStateCreated(root);
                    break;

                case "st_nestedStateCreated":
                    HandleStNestedStateCreated(root);
                    break;

                case "st_insertStateOnEdge":
                    HandleStInsertStateOnEdge(root);
                    break;

                case "st_insertPseudoOnEdge":
                    HandleStInsertPseudoOnEdge(root);
                    break;

                case "st_insertSpecialStateOnEdge":
                    HandleStInsertSpecialStateOnEdge(root);
                    break;

                case "st_stateEdited":
                    HandleStStateEdited(root);
                    break;

                case "st_stateDeleted":
                    HandleStStateDeleted(root);
                    break;

                case "st_transitionCreated":
                    HandleStTransitionCreated(root);
                    break;

                case "st_transitionEdited":
                    HandleStTransitionEdited(root);
                    break;

                case "st_transitionDeleted":
                    HandleStTransitionDeleted(root);
                    break;

                case "st_nestedTransitionCreated":
                    HandleStNestedTransitionCreated(root);
                    break;

                case "st_nestedTransitionEdited":
                    HandleStNestedTransitionEdited(root);
                    break;

                case "st_nestedTransitionDeleted":
                    HandleStNestedTransitionDeleted(root);
                    break;

                case "st_insertStateOnNestedEdge":
                    HandleStInsertStateOnNestedEdge(root);
                    break;

                case "st_insertPseudoOnNestedEdge":
                    HandleStInsertPseudoOnNestedEdge(root);
                    break;

                case "st_insertSpecialStateOnNestedEdge":
                    HandleStInsertSpecialStateOnNestedEdge(root);
                    break;

                case "st_noteCreated":
                    HandleStNoteCreated(root);
                    break;

                case "st_noteEdited":
                    HandleStNoteEdited(root);
                    break;

                case "st_noteDeleted":
                    HandleStNoteDeleted(root);
                    break;

                case "st_autoLayoutComplete":
                    HandleStAutoLayoutComplete(root);
                    break;

                case "st_allPositionsUpdate":
                    HandleStAllPositionsUpdate(root);
                    break;

                case "st_stateSelected":
                case "st_transitionSelected":
                    // Selection doesn't need model changes
                    break;

                // ===== ER Diagram Messages =====

                case "er_entityCreated":
                    HandleErEntityCreated(root);
                    break;

                case "er_entityEdited":
                    HandleErEntityEdited(root);
                    break;

                case "er_entityDeleted":
                    HandleErEntityDeleted(root);
                    break;

                case "er_attributeAdded":
                    HandleErAttributeAdded(root);
                    break;

                case "er_attributeEdited":
                    HandleErAttributeEdited(root);
                    break;

                case "er_attributeDeleted":
                    HandleErAttributeDeleted(root);
                    break;

                case "er_relationshipCreated":
                    HandleErRelationshipCreated(root);
                    break;

                case "er_relationshipEdited":
                    HandleErRelationshipEdited(root);
                    break;

                case "er_relationshipDeleted":
                    HandleErRelationshipDeleted(root);
                    break;

                case "er_entityMoved":
                    HandleErEntityMoved(root);
                    break;

                case "er_autoLayoutComplete":
                    HandleErAutoLayoutComplete(root);
                    break;

                case "er_entitySelected":
                case "er_relationshipSelected":
                    // Selection doesn't need model changes
                    break;
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Ignore malformed messages or messages with missing/invalid properties
        }

        // After processing any message, notify JS of current undo/redo availability
        _ = SendUndoRedoStateAsync();
    }

    /// <summary>
    /// Sends the current undo/redo availability to the JS editor so toolbar buttons
    /// can be enabled/disabled appropriately.
    /// </summary>
    private async Task SendUndoRedoStateAsync()
    {
        try
        {
            var canUndo = CanUndo ? "true" : "false";
            var canRedo = CanRedo ? "true" : "false";
            await _webView.ExecuteScriptAsync($"if(window.updateUndoRedoState) window.updateUndoRedoState({canUndo},{canRedo})");
        }
        catch
        {
            // Ignore errors (e.g. WebView not ready)
        }
    }

    // ========== Undo/Redo History ==========

    // ========== Undo/Redo History ==========

    private void PushUndo()
    {
        var json = GetCurrentModelJson();
        _undoStack.Add(json);
        if (_undoStack.Count > MaxHistorySize)
            _undoStack.RemoveAt(0);

        // Clear redo stack on new action
        _redoStack.Clear();
    }

    // ========== Event Raising ==========

    private void RaiseSequenceModelChanged(string changeType)
    {
        if (_sequenceModel != null)
            SequenceModelChanged?.Invoke(this, new SequenceModelChangedEventArgs(changeType, _sequenceModel));
    }

    private void RaiseClassDiagramModelChanged(string changeType)
    {
        if (_classDiagramModel != null)
            ClassDiagramModelChanged?.Invoke(this, new ClassDiagramModelChangedEventArgs(changeType, _classDiagramModel));
    }

    private void RaiseStateDiagramModelChanged(string changeType)
    {
        if (_stateDiagramModel != null)
            StateDiagramModelChanged?.Invoke(this, new StateDiagramModelChangedEventArgs(changeType, _stateDiagramModel));
    }

    private void RaiseERDiagramModelChanged(string changeType)
    {
        if (_erDiagramModel != null)
            ERDiagramModelChanged?.Invoke(this, new ERDiagramModelChangedEventArgs(changeType, _erDiagramModel));
    }
}

/// <summary>
/// Event args for when the FlowchartModel changes via the visual editor.
/// </summary>
public class ModelChangedEventArgs : EventArgs
{
    /// <summary>
    /// The type of change (e.g., "nodeMoved", "nodeEdited", "nodeCreated", "edgeCreated", etc.).
    /// </summary>
    public string ChangeType { get; }

    /// <summary>
    /// The updated FlowchartModel.
    /// </summary>
    public FlowchartModel Model { get; }

    public ModelChangedEventArgs(string changeType, FlowchartModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}

/// <summary>
/// Event args for when a node is selected in the visual editor.
/// </summary>
public class NodeSelectedEventArgs : EventArgs
{
    /// <summary>
    /// The ID of the selected node.
    /// </summary>
    public string NodeId { get; }

    public NodeSelectedEventArgs(string nodeId)
    {
        NodeId = nodeId;
    }
}

/// <summary>
/// Event args for when the SequenceDiagramModel changes via the visual editor.
/// </summary>
public class SequenceModelChangedEventArgs : EventArgs
{
    /// <summary>
    /// The type of change (e.g., "seq_participantCreated", "seq_messageEdited", etc.).
    /// </summary>
    public string ChangeType { get; }

    /// <summary>
    /// The updated SequenceDiagramModel.
    /// </summary>
    public SequenceDiagramModel Model { get; }

    public SequenceModelChangedEventArgs(string changeType, SequenceDiagramModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}

/// <summary>
/// Event args for when the ClassDiagramModel changes via the visual editor.
/// </summary>
public class ClassDiagramModelChangedEventArgs : EventArgs
{
    /// <summary>
    /// The type of change (e.g., "cls_classCreated", "cls_relationshipEdited", etc.).
    /// </summary>
    public string ChangeType { get; }

    /// <summary>
    /// The updated ClassDiagramModel.
    /// </summary>
    public ClassDiagramModel Model { get; }

    public ClassDiagramModelChangedEventArgs(string changeType, ClassDiagramModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}

/// <summary>
/// Event args for when the StateDiagramModel changes via the visual editor.
/// </summary>
public class StateDiagramModelChangedEventArgs : EventArgs
{
    public string ChangeType { get; }
    public StateDiagramModel Model { get; }

    public StateDiagramModelChangedEventArgs(string changeType, StateDiagramModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}

/// <summary>
/// Event args for when the ERDiagramModel changes via the visual editor.
/// </summary>
public class ERDiagramModelChangedEventArgs : EventArgs
{
    public string ChangeType { get; }
    public ERDiagramModel Model { get; }

    public ERDiagramModelChangedEventArgs(string changeType, ERDiagramModel model)
    {
        ChangeType = changeType;
        Model = model;
    }
}
