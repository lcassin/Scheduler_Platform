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
public class VisualEditorBridge
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

    /// <summary>
    /// Sends the current FlowchartModel to the visual editor as JSON.
    /// </summary>
    public async Task SendDiagramToEditorAsync()
    {
        var json = ConvertModelToJson(_model);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadDiagram({escaped})");
    }

    /// <summary>
    /// Updates the model reference (e.g., after text re-parse) and sends to editor.
    /// </summary>
    /// <param name="newModel">The new FlowchartModel from text parsing.</param>
    public async Task UpdateModelAsync(FlowchartModel newModel)
    {
        _model = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.Flowchart;
        await SendDiagramToEditorAsync();
    }

    // ========== Class Diagram Support ==========

    /// <summary>
    /// Sends the current ClassDiagramModel to the visual editor as JSON.
    /// </summary>
    public async Task SendClassDiagramToEditorAsync()
    {
        if (_classDiagramModel == null) return;
        var json = ConvertClassDiagramModelToJson(_classDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadClassDiagram({escaped})");
    }

    /// <summary>
    /// Updates the class diagram model reference and sends to editor.
    /// </summary>
    public async Task UpdateClassDiagramModelAsync(ClassDiagramModel newModel)
    {
        _classDiagramModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.ClassDiagram;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendClassDiagramToEditorAsync();
    }

    /// <summary>
    /// Converts a ClassDiagramModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertClassDiagramModelToJson(ClassDiagramModel model)
    {
        var classes = model.Classes.Select(c => new ClsDiagClassDto
        {
            Id = c.Id,
            Label = c.Label,
            Annotation = c.Annotation,
            GenericType = c.GenericType,
            Members = c.Members.Select(m => new ClsDiagMemberDto
            {
                RawText = m.RawText,
                Visibility = m.Visibility.ToString(),
                IsMethod = m.IsMethod,
                Name = m.Name,
                Type = m.Type,
                Parameters = m.Parameters,
                Classifier = m.Classifier.ToString()
            }).ToList(),
            CssClass = c.CssClass,
            IsExplicit = c.IsExplicit
        }).ToList();

        var relationships = model.Relationships.Select(r => new ClsDiagRelDto
        {
            FromId = r.FromId,
            ToId = r.ToId,
            LeftEnd = r.LeftEnd.ToString(),
            RightEnd = r.RightEnd.ToString(),
            LinkStyle = r.LinkStyle.ToString(),
            Label = r.Label,
            FromCardinality = r.FromCardinality,
            ToCardinality = r.ToCardinality
        }).ToList();

        var namespaces = model.Namespaces.Select(n => new ClsDiagNamespaceDto
        {
            Name = n.Name,
            ClassIds = n.ClassIds
        }).ToList();

        var notes = model.Notes.Select(n => new ClsDiagNoteDto
        {
            Text = n.Text,
            ForClass = n.ForClass
        }).ToList();

        var dto = new ClsDiagramDto
        {
            Direction = model.Direction,
            Classes = classes,
            Relationships = relationships,
            Namespaces = namespaces,
            Notes = notes,
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

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
                        }).ToList()
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

        var dto = new StDiagramDto
        {
            Direction = model.Direction,
            IsV2 = model.IsV2,
            States = states,
            Transitions = transitions,
            Notes = notes,
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
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
            }).ToList()
        };
    }

    // ========== ER Diagram Support ==========

    /// <summary>
    /// Sends the current ERDiagramModel to the visual editor as JSON.
    /// </summary>
    public async Task SendERDiagramToEditorAsync()
    {
        if (_erDiagramModel == null) return;
        var json = ConvertERDiagramModelToJson(_erDiagramModel);
        var escaped = JsonSerializer.Serialize(json);
        await _webView.ExecuteScriptAsync($"window.loadERDiagram({escaped})");
    }

    /// <summary>
    /// Updates the ER diagram model reference and sends to editor.
    /// </summary>
    public async Task UpdateERDiagramModelAsync(ERDiagramModel newModel)
    {
        _erDiagramModel = newModel ?? throw new ArgumentNullException(nameof(newModel));
        _activeDiagramType = ActiveDiagramType.ERDiagram;
        _undoStack.Clear();
        _redoStack.Clear();
        await SendERDiagramToEditorAsync();
    }

    /// <summary>
    /// Converts an ERDiagramModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertERDiagramModelToJson(ERDiagramModel model)
    {
        var entities = model.Entities.Select(e => new ErDiagEntityDto
        {
            Name = e.Name,
            IsExplicit = e.IsExplicit,
            Attributes = e.Attributes.Select(a => new ErDiagAttributeDto
            {
                Type = a.Type,
                Name = a.Name,
                Key = a.Key,
                Comment = a.Comment
            }).ToList()
        }).ToList();

        var relationships = model.Relationships.Select(r => new ErDiagRelDto
        {
            FromEntity = r.FromEntity,
            ToEntity = r.ToEntity,
            LeftCardinality = r.LeftCardinality.ToString(),
            RightCardinality = r.RightCardinality.ToString(),
            IsIdentifying = r.IsIdentifying,
            Label = r.Label
        }).ToList();

        var dto = new ErDiagramDto
        {
            Entities = entities,
            Relationships = relationships,
            PreambleLines = model.PreambleLines,
            DeclarationLineIndex = model.DeclarationLineIndex
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

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
                await SendStateDiagramToEditorAsync();
                RaiseStateDiagramModelChanged("undo");
            }
            else if (_activeDiagramType == ActiveDiagramType.ERDiagram)
            {
                RestoreERDiagramModelFromJson(previousJson);
                await SendERDiagramToEditorAsync();
                RaiseERDiagramModelChanged("undo");
            }
            else if (_activeDiagramType == ActiveDiagramType.ClassDiagram)
            {
                RestoreClassDiagramModelFromJson(previousJson);
                await SendClassDiagramToEditorAsync();
                RaiseClassDiagramModelChanged("undo");
            }
            else if (_activeDiagramType == ActiveDiagramType.Sequence)
            {
                RestoreSequenceModelFromJson(previousJson);
                await SendSequenceDiagramToEditorAsync();
                RaiseSequenceModelChanged("undo");
            }
            else
            {
                RestoreModelFromJson(previousJson);
                await SendDiagramToEditorAsync();
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
                await SendStateDiagramToEditorAsync();
                RaiseStateDiagramModelChanged("redo");
            }
            else if (_activeDiagramType == ActiveDiagramType.ERDiagram)
            {
                RestoreERDiagramModelFromJson(redoJson);
                await SendERDiagramToEditorAsync();
                RaiseERDiagramModelChanged("redo");
            }
            else if (_activeDiagramType == ActiveDiagramType.ClassDiagram)
            {
                RestoreClassDiagramModelFromJson(redoJson);
                await SendClassDiagramToEditorAsync();
                RaiseClassDiagramModelChanged("redo");
            }
            else if (_activeDiagramType == ActiveDiagramType.Sequence)
            {
                RestoreSequenceModelFromJson(redoJson);
                await SendSequenceDiagramToEditorAsync();
                RaiseSequenceModelChanged("redo");
            }
            else
            {
                RestoreModelFromJson(redoJson);
                await SendDiagramToEditorAsync();
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

    /// <summary>
    /// Converts a FlowchartModel to the JSON format expected by the visual editor JS.
    /// </summary>
    public static string ConvertModelToJson(FlowchartModel model)
    {
        var dto = new DiagramDto
        {
            Direction = model.Direction,
            Nodes = model.Nodes.Select(n => new NodeDto
            {
                Id = n.Id,
                Label = n.Label,
                Shape = n.Shape.ToString(),
                X = n.Position.X,
                Y = n.Position.Y,
                Width = n.Size.Width,
                Height = n.Size.Height,
                CssClass = n.CssClass,
                HasManualPosition = n.HasManualPosition
            }).ToList(),
            Edges = model.Edges.Select(e => new EdgeDto
            {
                From = e.FromNodeId,
                To = e.ToNodeId,
                Label = string.IsNullOrEmpty(e.Label) ? null : e.Label,
                Style = e.Style.ToString(),
                ArrowType = e.ArrowType.ToString(),
                IsBidirectional = e.IsBidirectional,
                LinkLength = e.LinkLength
            }).ToList(),
            Subgraphs = model.Subgraphs.Select(s => new SubgraphDto
            {
                Id = s.Id,
                Label = s.Label,
                NodeIds = s.NodeIds,
                Direction = s.Direction
            }).ToList(),
            Styles = model.Styles.Select(s => new StyleDto
            {
                IsClassDef = s.IsClassDef,
                Target = s.Target,
                StyleString = s.StyleString
            }).ToList(),
            Comments = model.Comments.Select(c => new CommentDto
            {
                Text = c.Text,
                OriginalLineIndex = c.OriginalLineIndex
            }).ToList(),
            ClassAssignments = model.ClassAssignments.Select(ca => new ClassAssignmentDto
            {
                NodeIds = ca.NodeIds,
                ClassName = ca.ClassName
            }).ToList(),
            DiagramKeyword = model.DiagramKeyword,
            DeclarationLineIndex = model.DeclarationLineIndex,
            PreambleLines = model.PreambleLines
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
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

                case "cls_relationshipCreated":
                    HandleClsRelationshipCreated(root);
                    break;

                case "cls_relationshipEdited":
                    HandleClsRelationshipEdited(root);
                    break;

                case "cls_relationshipDeleted":
                    HandleClsRelationshipDeleted(root);
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

                case "seq_fragmentCreated":
                    HandleSeqFragmentCreated(root);
                    break;

                case "seq_participantSelected":
                    // Selection doesn't need model changes
                    break;

                case "seq_messageSelected":
                    // Selection doesn't need model changes
                    break;

                // ===== State Diagram Messages =====

                case "st_stateCreated":
                    HandleStStateCreated(root);
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

                case "st_noteCreated":
                    HandleStNoteCreated(root);
                    break;

                case "st_noteDeleted":
                    HandleStNoteDeleted(root);
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

                case "er_entitySelected":
                case "er_relationshipSelected":
                    // Selection doesn't need model changes
                    break;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed messages
        }
    }

    private void HandleNodeMoved(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        var x = root.GetProperty("x").GetDouble();
        var y = root.GetProperty("y").GetDouble();

        if (string.IsNullOrEmpty(nodeId)) return;

        var node = _model.Nodes.Find(n => n.Id == nodeId);
        if (node == null) return;

        PushUndo();
        node.Position = new System.Windows.Point(x, y);
        node.HasManualPosition = true;
        RaiseModelChanged("nodeMoved");
    }

    private void HandleNodeResized(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        var width = root.GetProperty("width").GetDouble();
        var height = root.GetProperty("height").GetDouble();

        if (string.IsNullOrEmpty(nodeId)) return;

        var node = _model.Nodes.Find(n => n.Id == nodeId);
        if (node == null) return;

        PushUndo();
        node.Size = new System.Windows.Size(width, height);
        RaiseModelChanged("nodeResized");
    }

    private void HandleNodeEdited(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        var label = root.GetProperty("label").GetString();

        if (string.IsNullOrEmpty(nodeId)) return;

        var node = _model.Nodes.Find(n => n.Id == nodeId);
        if (node == null) return;

        PushUndo();
        node.Label = label;

        if (root.TryGetProperty("width", out var wProp) && root.TryGetProperty("height", out var hProp))
        {
            node.Size = new System.Windows.Size(wProp.GetDouble(), hProp.GetDouble());
        }

        RaiseModelChanged("nodeEdited");
    }

    private void HandleNodeCreated(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        var label = root.GetProperty("label").GetString();
        var shape = root.GetProperty("shape").GetString();
        var x = root.GetProperty("x").GetDouble();
        var y = root.GetProperty("y").GetDouble();

        if (string.IsNullOrEmpty(nodeId)) return;

        PushUndo();

        var nodeShape = Enum.TryParse<NodeShape>(shape, out var parsed) ? parsed : NodeShape.Rectangle;

        var newNode = new FlowchartNode
        {
            Id = nodeId,
            Label = label,
            Shape = nodeShape,
            Position = new System.Windows.Point(x, y)
        };

        if (root.TryGetProperty("width", out var wProp) && root.TryGetProperty("height", out var hProp))
        {
            newNode.Size = new System.Windows.Size(wProp.GetDouble(), hProp.GetDouble());
        }

        _model.Nodes.Add(newNode);
        RaiseModelChanged("nodeCreated");
    }

    private void HandleNodeDeleted(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        if (string.IsNullOrEmpty(nodeId)) return;

        PushUndo();
        _model.Nodes.RemoveAll(n => n.Id == nodeId);
        _model.Edges.RemoveAll(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId);

        // Remove from subgraphs
        foreach (var sg in _model.Subgraphs)
        {
            sg.NodeIds.Remove(nodeId);
        }

        RaiseModelChanged("nodeDeleted");
    }

    private void HandleEdgeCreated(JsonElement root)
    {
        var from = root.GetProperty("from").GetString();
        var to = root.GetProperty("to").GetString();

        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;

        PushUndo();

        var style = EdgeStyle.Solid;
        if (root.TryGetProperty("style", out var styleProp))
        {
            Enum.TryParse<EdgeStyle>(styleProp.GetString(), out style);
        }

        var arrowType = ArrowType.Arrow;
        if (root.TryGetProperty("arrowType", out var arrowProp))
        {
            Enum.TryParse<ArrowType>(arrowProp.GetString(), out arrowType);
        }

        var label = root.TryGetProperty("label", out var labelProp) ? labelProp.GetString() : null;

        var newEdge = new FlowchartEdge
        {
            FromNodeId = from,
            ToNodeId = to,
            Label = string.IsNullOrEmpty(label) ? null : label,
            Style = style,
            ArrowType = arrowType
        };

        _model.Edges.Add(newEdge);
        RaiseModelChanged("edgeCreated");
    }

    private void HandleEdgeEdited(JsonElement root)
    {
        var edgeIndex = root.GetProperty("edgeIndex").GetInt32();
        var label = root.GetProperty("label").GetString();

        if (edgeIndex < 0 || edgeIndex >= _model.Edges.Count) return;

        PushUndo();
        _model.Edges[edgeIndex].Label = string.IsNullOrEmpty(label) ? null : label;
        RaiseModelChanged("edgeEdited");
    }

    private void HandleEdgeDeleted(JsonElement root)
    {
        if (root.TryGetProperty("edgeIndex", out var idxProp))
        {
            var edgeIndex = idxProp.GetInt32();
            if (edgeIndex >= 0 && edgeIndex < _model.Edges.Count)
            {
                PushUndo();
                _model.Edges.RemoveAt(edgeIndex);
                RaiseModelChanged("edgeDeleted");
            }
        }
        else
        {
            // Fallback: remove by from/to (legacy)
            var from = root.GetProperty("from").GetString();
            var to = root.GetProperty("to").GetString();
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;

            PushUndo();
            _model.Edges.RemoveAll(e => e.FromNodeId == from && e.ToNodeId == to);
            RaiseModelChanged("edgeDeleted");
        }
    }

    private void HandleNodeSelected(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        if (string.IsNullOrEmpty(nodeId)) return;

        NodeSelected?.Invoke(this, new NodeSelectedEventArgs(nodeId));
    }

    private void HandleNodeShapeChanged(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        var shape = root.GetProperty("shape").GetString();

        if (string.IsNullOrEmpty(nodeId)) return;

        var node = _model.Nodes.Find(n => n.Id == nodeId);
        if (node == null) return;

        PushUndo();
        node.Shape = Enum.TryParse<NodeShape>(shape, out var parsed) ? parsed : NodeShape.Rectangle;

        if (root.TryGetProperty("width", out var wProp) && root.TryGetProperty("height", out var hProp))
        {
            node.Size = new System.Windows.Size(wProp.GetDouble(), hProp.GetDouble());
        }

        RaiseModelChanged("nodeShapeChanged");
    }

    private void HandleNodeStyleChanged(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        if (string.IsNullOrEmpty(nodeId)) return;

        var node = _model.Nodes.Find(n => n.Id == nodeId);
        if (node == null) return;

        PushUndo();
        bool changed = false;

        // Apply fill color as an inline style
        if (root.TryGetProperty("fillColor", out var fillProp))
        {
            var fillColor = fillProp.GetString();
            if (!string.IsNullOrEmpty(fillColor))
            {
                // Add or update a style definition for this node, preserving other properties
                var existingStyle = _model.Styles.Find(s => !s.IsClassDef && s.Target == nodeId);
                if (existingStyle != null)
                {
                    // Preserve existing properties, only replace fill
                    var props = existingStyle.StyleString
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !p.StartsWith("fill:", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    props.Insert(0, $"fill:{fillColor}");
                    existingStyle.StyleString = string.Join(",", props);
                }
                else
                {
                    _model.Styles.Add(new StyleDefinition
                    {
                        IsClassDef = false,
                        Target = nodeId,
                        StyleString = $"fill:{fillColor}"
                    });
                }
                changed = true;
            }
        }

        if (changed)
        {
            RaiseModelChanged("nodeStyleChanged");
        }
        else
        {
            // No actual change — remove the undo snapshot we just pushed
            if (_undoStack.Count > 0) _undoStack.RemoveAt(_undoStack.Count - 1);
        }
    }

    private void HandleSubgraphEdited(JsonElement root)
    {
        var subgraphId = root.GetProperty("subgraphId").GetString();
        var label = root.GetProperty("label").GetString();

        if (string.IsNullOrEmpty(subgraphId)) return;

        var sg = _model.Subgraphs.Find(s => s.Id == subgraphId);
        if (sg == null) return;

        PushUndo();
        sg.Label = label ?? sg.Label;
        RaiseModelChanged("subgraphEdited");
    }

    private void HandleSubgraphDeleted(JsonElement root)
    {
        var subgraphId = root.GetProperty("subgraphId").GetString();
        if (string.IsNullOrEmpty(subgraphId)) return;

        var sg = _model.Subgraphs.Find(s => s.Id == subgraphId);
        if (sg == null) return;

        PushUndo();
        _model.Subgraphs.Remove(sg);
        RaiseModelChanged("subgraphDeleted");
    }

    private void HandleNodeSubgraphChanged(JsonElement root)
    {
        var nodeId = root.GetProperty("nodeId").GetString();
        if (string.IsNullOrEmpty(nodeId)) return;

        PushUndo();

        // Remove from all subgraphs first
        foreach (var sg in _model.Subgraphs)
        {
            sg.NodeIds.Remove(nodeId);
        }

        // Add to target subgraph if specified
        if (root.TryGetProperty("subgraphId", out var sgIdProp) && sgIdProp.ValueKind != JsonValueKind.Null)
        {
            var targetSgId = sgIdProp.GetString();
            if (!string.IsNullOrEmpty(targetSgId))
            {
                var targetSg = _model.Subgraphs.Find(s => s.Id == targetSgId);
                if (targetSg != null)
                {
                    targetSg.NodeIds.Add(nodeId);
                }
            }
        }

        RaiseModelChanged("nodeSubgraphChanged");
    }

    private void HandleSubgraphCreated(JsonElement root)
    {
        var subgraphId = root.GetProperty("subgraphId").GetString();
        var label = root.GetProperty("label").GetString();

        if (string.IsNullOrEmpty(subgraphId)) return;

        PushUndo();

        var nodeIds = new List<string>();
        if (root.TryGetProperty("nodeIds", out var nodeIdsArray))
        {
            foreach (var nid in nodeIdsArray.EnumerateArray())
            {
                var id = nid.GetString();
                if (!string.IsNullOrEmpty(id))
                {
                    // Remove from any existing subgraph
                    foreach (var existingSg in _model.Subgraphs)
                    {
                        existingSg.NodeIds.Remove(id);
                    }
                    nodeIds.Add(id);
                }
            }
        }

        _model.Subgraphs.Add(new FlowchartSubgraph
        {
            Id = subgraphId,
            Label = label ?? "New Subgraph",
            NodeIds = nodeIds
        });

        RaiseModelChanged("subgraphCreated");
    }

    private void HandleAutoLayoutComplete(JsonElement root)
    {
        if (!root.TryGetProperty("positions", out var positionsArray))
            return;

        PushUndo();

        foreach (var pos in positionsArray.EnumerateArray())
        {
            var nodeId = pos.GetProperty("nodeId").GetString();
            if (string.IsNullOrEmpty(nodeId)) continue;

            var node = _model.Nodes.Find(n => n.Id == nodeId);
            if (node == null) continue;

            node.Position = new System.Windows.Point(
                pos.GetProperty("x").GetDouble(),
                pos.GetProperty("y").GetDouble()
            );

            if (pos.TryGetProperty("width", out var wProp) && pos.TryGetProperty("height", out var hProp))
            {
                node.Size = new System.Windows.Size(wProp.GetDouble(), hProp.GetDouble());
            }
        }

        RaiseModelChanged("autoLayoutComplete");
    }

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

    private void RestoreModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<DiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _model.Direction = dto.Direction ?? "TD";
        _model.DiagramKeyword = dto.DiagramKeyword ?? "flowchart";
        _model.DeclarationLineIndex = dto.DeclarationLineIndex;
        _model.PreambleLines = dto.PreambleLines ?? new List<string>();
        _model.Nodes.Clear();
        _model.Edges.Clear();
        _model.Subgraphs.Clear();
        _model.Styles.Clear();
        _model.Comments.Clear();
        _model.ClassAssignments.Clear();

        if (dto.Nodes != null)
        {
            foreach (var n in dto.Nodes)
            {
                var shape = Enum.TryParse<NodeShape>(n.Shape, out var parsed) ? parsed : NodeShape.Rectangle;
                _model.Nodes.Add(new FlowchartNode
                {
                    Id = n.Id ?? string.Empty,
                    Label = n.Label,
                    Shape = shape,
                    Position = new System.Windows.Point(n.X, n.Y),
                    Size = new System.Windows.Size(n.Width, n.Height),
                    CssClass = n.CssClass,
                    HasManualPosition = n.HasManualPosition
                });
            }
        }

        if (dto.Edges != null)
        {
            foreach (var e in dto.Edges)
            {
                var style = Enum.TryParse<EdgeStyle>(e.Style, out var sParsed) ? sParsed : EdgeStyle.Solid;
                var arrow = Enum.TryParse<ArrowType>(e.ArrowType, out var aParsed) ? aParsed : ArrowType.Arrow;
                _model.Edges.Add(new FlowchartEdge
                {
                    FromNodeId = e.From ?? string.Empty,
                    ToNodeId = e.To ?? string.Empty,
                    Label = e.Label,
                    Style = style,
                    ArrowType = arrow,
                    IsBidirectional = e.IsBidirectional,
                    LinkLength = e.LinkLength
                });
            }
        }

        if (dto.Subgraphs != null)
        {
            foreach (var s in dto.Subgraphs)
            {
                _model.Subgraphs.Add(new FlowchartSubgraph
                {
                    Id = s.Id ?? string.Empty,
                    Label = s.Label ?? string.Empty,
                    NodeIds = s.NodeIds ?? new List<string>(),
                    Direction = s.Direction
                });
            }
        }

        if (dto.Styles != null)
        {
            foreach (var s in dto.Styles)
            {
                _model.Styles.Add(new StyleDefinition
                {
                    IsClassDef = s.IsClassDef,
                    Target = s.Target ?? string.Empty,
                    StyleString = s.StyleString ?? string.Empty
                });
            }
        }

        if (dto.Comments != null)
        {
            foreach (var c in dto.Comments)
            {
                _model.Comments.Add(new CommentEntry
                {
                    Text = c.Text ?? string.Empty,
                    OriginalLineIndex = c.OriginalLineIndex
                });
            }
        }

        if (dto.ClassAssignments != null)
        {
            foreach (var ca in dto.ClassAssignments)
            {
                _model.ClassAssignments.Add(new ClassAssignment
                {
                    NodeIds = ca.NodeIds ?? string.Empty,
                    ClassName = ca.ClassName ?? string.Empty
                });
            }
        }
    }

    // ========== Event Raising ==========

    private void RaiseModelChanged(string changeType)
    {
        ModelChanged?.Invoke(this, new ModelChangedEventArgs(changeType, _model));
    }

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

    // ========== Class Diagram Message Handlers ==========

    private void HandleClsClassCreated(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        if (string.IsNullOrEmpty(classId)) return;
        if (_classDiagramModel.Classes.Any(c => c.Id == classId)) return;

        PushUndo();
        var newClass = new ClassDefinition
        {
            Id = classId,
            IsExplicit = true
        };
        if (root.TryGetProperty("annotation", out var annProp))
            newClass.Annotation = annProp.GetString();
        _classDiagramModel.Classes.Add(newClass);
        RaiseClassDiagramModelChanged("cls_classCreated");
    }

    private void HandleClsClassEdited(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        if (string.IsNullOrEmpty(classId)) return;

        var cls = _classDiagramModel.Classes.Find(c => c.Id == classId);
        if (cls == null) return;

        PushUndo();
        if (root.TryGetProperty("label", out var labelProp))
        {
            var label = labelProp.GetString();
            cls.Label = string.IsNullOrEmpty(label) ? null : label;
        }
        if (root.TryGetProperty("annotation", out var annProp))
        {
            var ann = annProp.GetString();
            cls.Annotation = string.IsNullOrEmpty(ann) ? null : ann;
        }
        if (root.TryGetProperty("genericType", out var genProp))
        {
            var gen = genProp.GetString();
            cls.GenericType = string.IsNullOrEmpty(gen) ? null : gen;
        }
        RaiseClassDiagramModelChanged("cls_classEdited");
    }

    private void HandleClsClassDeleted(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        if (string.IsNullOrEmpty(classId)) return;

        PushUndo();
        _classDiagramModel.Classes.RemoveAll(c => c.Id == classId);
        _classDiagramModel.Relationships.RemoveAll(r => r.FromId == classId || r.ToId == classId);
        // Remove from namespaces
        foreach (var ns in _classDiagramModel.Namespaces)
            ns.ClassIds.Remove(classId);
        RaiseClassDiagramModelChanged("cls_classDeleted");
    }

    private void HandleClsMemberAdded(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        var rawText = root.GetProperty("rawText").GetString() ?? "";
        if (string.IsNullOrEmpty(classId)) return;

        var cls = _classDiagramModel.Classes.Find(c => c.Id == classId);
        if (cls == null) return;

        PushUndo();
        var isMethod = rawText.Contains('(');
        var visibility = MemberVisibility.None;
        var name = rawText;
        if (rawText.Length > 0)
        {
            switch (rawText[0])
            {
                case '+': visibility = MemberVisibility.Public; name = rawText[1..].TrimStart(); break;
                case '-': visibility = MemberVisibility.Private; name = rawText[1..].TrimStart(); break;
                case '#': visibility = MemberVisibility.Protected; name = rawText[1..].TrimStart(); break;
                case '~': visibility = MemberVisibility.Package; name = rawText[1..].TrimStart(); break;
            }
        }

        cls.Members.Add(new ClassMember
        {
            RawText = rawText,
            Visibility = visibility,
            IsMethod = isMethod,
            Name = name
        });
        RaiseClassDiagramModelChanged("cls_memberAdded");
    }

    private void HandleClsMemberEdited(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        var memberIndex = root.GetProperty("memberIndex").GetInt32();
        var rawText = root.GetProperty("rawText").GetString() ?? "";
        if (string.IsNullOrEmpty(classId)) return;

        var cls = _classDiagramModel.Classes.Find(c => c.Id == classId);
        if (cls == null || memberIndex < 0 || memberIndex >= cls.Members.Count) return;

        PushUndo();
        var member = cls.Members[memberIndex];
        member.RawText = rawText;
        member.IsMethod = rawText.Contains('(');
        member.Visibility = MemberVisibility.None;
        member.Name = rawText;
        if (rawText.Length > 0)
        {
            switch (rawText[0])
            {
                case '+': member.Visibility = MemberVisibility.Public; member.Name = rawText[1..].TrimStart(); break;
                case '-': member.Visibility = MemberVisibility.Private; member.Name = rawText[1..].TrimStart(); break;
                case '#': member.Visibility = MemberVisibility.Protected; member.Name = rawText[1..].TrimStart(); break;
                case '~': member.Visibility = MemberVisibility.Package; member.Name = rawText[1..].TrimStart(); break;
            }
        }
        RaiseClassDiagramModelChanged("cls_memberEdited");
    }

    private void HandleClsMemberDeleted(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var classId = root.GetProperty("classId").GetString();
        var memberIndex = root.GetProperty("memberIndex").GetInt32();
        if (string.IsNullOrEmpty(classId)) return;

        var cls = _classDiagramModel.Classes.Find(c => c.Id == classId);
        if (cls == null || memberIndex < 0 || memberIndex >= cls.Members.Count) return;

        PushUndo();
        cls.Members.RemoveAt(memberIndex);
        RaiseClassDiagramModelChanged("cls_memberDeleted");
    }

    private void HandleClsRelationshipCreated(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var fromId = root.GetProperty("fromId").GetString();
        var toId = root.GetProperty("toId").GetString();
        if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId)) return;

        PushUndo();
        var rel = new ClassRelationship
        {
            FromId = fromId,
            ToId = toId,
            LinkStyle = ClassLinkStyle.Solid
        };

        if (root.TryGetProperty("leftEnd", out var leftProp))
        {
            if (Enum.TryParse<ClassRelationEnd>(leftProp.GetString(), out var le))
                rel.LeftEnd = le;
        }
        if (root.TryGetProperty("rightEnd", out var rightProp))
        {
            if (Enum.TryParse<ClassRelationEnd>(rightProp.GetString(), out var re))
                rel.RightEnd = re;
        }
        if (root.TryGetProperty("linkStyle", out var linkProp))
        {
            if (Enum.TryParse<ClassLinkStyle>(linkProp.GetString(), out var ls))
                rel.LinkStyle = ls;
        }
        if (root.TryGetProperty("label", out var labelProp))
            rel.Label = labelProp.GetString();
        if (root.TryGetProperty("fromCardinality", out var fromCardProp))
            rel.FromCardinality = fromCardProp.GetString();
        if (root.TryGetProperty("toCardinality", out var toCardProp))
            rel.ToCardinality = toCardProp.GetString();

        _classDiagramModel.Relationships.Add(rel);
        RaiseClassDiagramModelChanged("cls_relationshipCreated");
    }

    private void HandleClsRelationshipEdited(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var relIndex = root.GetProperty("relationshipIndex").GetInt32();
        if (relIndex < 0 || relIndex >= _classDiagramModel.Relationships.Count) return;

        PushUndo();
        var rel = _classDiagramModel.Relationships[relIndex];
        if (root.TryGetProperty("leftEnd", out var leftProp))
        {
            if (Enum.TryParse<ClassRelationEnd>(leftProp.GetString(), out var le))
                rel.LeftEnd = le;
        }
        if (root.TryGetProperty("rightEnd", out var rightProp))
        {
            if (Enum.TryParse<ClassRelationEnd>(rightProp.GetString(), out var re))
                rel.RightEnd = re;
        }
        if (root.TryGetProperty("linkStyle", out var linkProp))
        {
            if (Enum.TryParse<ClassLinkStyle>(linkProp.GetString(), out var ls))
                rel.LinkStyle = ls;
        }
        if (root.TryGetProperty("label", out var labelProp))
            rel.Label = labelProp.GetString();
        if (root.TryGetProperty("fromCardinality", out var fromCardProp))
            rel.FromCardinality = fromCardProp.GetString();
        if (root.TryGetProperty("toCardinality", out var toCardProp))
            rel.ToCardinality = toCardProp.GetString();
        RaiseClassDiagramModelChanged("cls_relationshipEdited");
    }

    private void HandleClsRelationshipDeleted(JsonElement root)
    {
        if (_classDiagramModel == null) return;
        var relIndex = root.GetProperty("relationshipIndex").GetInt32();
        if (relIndex < 0 || relIndex >= _classDiagramModel.Relationships.Count) return;

        PushUndo();
        _classDiagramModel.Relationships.RemoveAt(relIndex);
        RaiseClassDiagramModelChanged("cls_relationshipDeleted");
    }

    // ========== Class Diagram Model Restore (for undo/redo) ==========

    private void RestoreClassDiagramModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<ClsDiagramDto>(json, JsonOptions);
        if (dto == null || _classDiagramModel == null) return;

        _classDiagramModel.Direction = dto.Direction;
        _classDiagramModel.PreambleLines = dto.PreambleLines ?? new List<string>();
        _classDiagramModel.DeclarationLineIndex = dto.DeclarationLineIndex;
        _classDiagramModel.Classes.Clear();
        _classDiagramModel.Relationships.Clear();
        _classDiagramModel.Namespaces.Clear();
        _classDiagramModel.Notes.Clear();

        if (dto.Classes != null)
        {
            foreach (var c in dto.Classes)
            {
                var cls = new ClassDefinition
                {
                    Id = c.Id ?? string.Empty,
                    Label = c.Label,
                    Annotation = c.Annotation,
                    GenericType = c.GenericType,
                    CssClass = c.CssClass,
                    IsExplicit = c.IsExplicit
                };
                if (c.Members != null)
                {
                    foreach (var m in c.Members)
                    {
                        var vis = Enum.TryParse<MemberVisibility>(m.Visibility, out var v) ? v : MemberVisibility.None;
                        var classifier = Enum.TryParse<MemberClassifier>(m.Classifier, out var mc) ? mc : MemberClassifier.None;
                        cls.Members.Add(new ClassMember
                        {
                            RawText = m.RawText ?? string.Empty,
                            Visibility = vis,
                            IsMethod = m.IsMethod,
                            Name = m.Name ?? string.Empty,
                            Type = m.Type,
                            Parameters = m.Parameters,
                            Classifier = classifier
                        });
                    }
                }
                _classDiagramModel.Classes.Add(cls);
            }
        }

        if (dto.Relationships != null)
        {
            foreach (var r in dto.Relationships)
            {
                var leftEnd = Enum.TryParse<ClassRelationEnd>(r.LeftEnd, out var le) ? le : ClassRelationEnd.None;
                var rightEnd = Enum.TryParse<ClassRelationEnd>(r.RightEnd, out var re) ? re : ClassRelationEnd.None;
                var linkStyle = Enum.TryParse<ClassLinkStyle>(r.LinkStyle, out var ls) ? ls : ClassLinkStyle.Solid;
                _classDiagramModel.Relationships.Add(new ClassRelationship
                {
                    FromId = r.FromId ?? string.Empty,
                    ToId = r.ToId ?? string.Empty,
                    LeftEnd = leftEnd,
                    RightEnd = rightEnd,
                    LinkStyle = linkStyle,
                    Label = r.Label,
                    FromCardinality = r.FromCardinality,
                    ToCardinality = r.ToCardinality
                });
            }
        }

        if (dto.Namespaces != null)
        {
            foreach (var n in dto.Namespaces)
            {
                _classDiagramModel.Namespaces.Add(new ClassNamespace
                {
                    Name = n.Name ?? string.Empty,
                    ClassIds = n.ClassIds ?? new List<string>()
                });
            }
        }

        if (dto.Notes != null)
        {
            foreach (var n in dto.Notes)
            {
                _classDiagramModel.Notes.Add(new ClassNote
                {
                    Text = n.Text ?? string.Empty,
                    ForClass = n.ForClass
                });
            }
        }
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
        if (root.TryGetProperty("type", out var typeProp))
        {
            var typeStr = typeProp.GetString();
            if (Enum.TryParse<StateType>(typeStr, true, out var st))
                newState.Type = st;
        }
        _stateDiagramModel.States.Add(newState);
        RaiseStateDiagramModelChanged("st_stateCreated");
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
        if (root.TryGetProperty("type", out var typeProp))
        {
            var typeStr = typeProp.GetString();
            if (Enum.TryParse<StateType>(typeStr, true, out var st))
                state.Type = st;
        }
        RaiseStateDiagramModelChanged("st_stateEdited");
    }

    private void HandleStStateDeleted(JsonElement root)
    {
        if (_stateDiagramModel == null) return;
        var stateId = root.GetProperty("stateId").GetString();
        if (string.IsNullOrEmpty(stateId)) return;

        PushUndo();
        _stateDiagramModel.States.RemoveAll(s => s.Id == stateId);
        _stateDiagramModel.Transitions.RemoveAll(t => t.FromId == stateId || t.ToId == stateId);
        _stateDiagramModel.Notes.RemoveAll(n => n.StateId == stateId);
        RaiseStateDiagramModelChanged("st_stateDeleted");
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
            IsExplicit = dto.IsExplicit
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

    // ========== ER Diagram Message Handlers ==========

    private void HandleErEntityCreated(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var name = root.GetProperty("name").GetString();
        if (string.IsNullOrEmpty(name)) return;
        if (_erDiagramModel.Entities.Any(e => e.Name == name)) return;

        PushUndo();
        _erDiagramModel.Entities.Add(new EREntity
        {
            Name = name,
            IsExplicit = true
        });
        RaiseERDiagramModelChanged("er_entityCreated");
    }

    private void HandleErEntityEdited(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var oldName = root.GetProperty("oldName").GetString();
        if (string.IsNullOrEmpty(oldName)) return;

        var entity = _erDiagramModel.Entities.Find(e => e.Name == oldName);
        if (entity == null) return;

        PushUndo();
        if (root.TryGetProperty("name", out var nameProp))
        {
            var newName = nameProp.GetString();
            if (!string.IsNullOrEmpty(newName) && newName != oldName)
            {
                // Update references in relationships
                foreach (var r in _erDiagramModel.Relationships)
                {
                    if (r.FromEntity == oldName) r.FromEntity = newName;
                    if (r.ToEntity == oldName) r.ToEntity = newName;
                }
                entity.Name = newName;
            }
        }
        RaiseERDiagramModelChanged("er_entityEdited");
    }

    private void HandleErEntityDeleted(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var name = root.GetProperty("name").GetString();
        if (string.IsNullOrEmpty(name)) return;

        PushUndo();
        _erDiagramModel.Entities.RemoveAll(e => e.Name == name);
        _erDiagramModel.Relationships.RemoveAll(r => r.FromEntity == name || r.ToEntity == name);
        RaiseERDiagramModelChanged("er_entityDeleted");
    }

    private void HandleErAttributeAdded(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var entityName = root.GetProperty("entityName").GetString();
        if (string.IsNullOrEmpty(entityName)) return;

        var entity = _erDiagramModel.Entities.Find(e => e.Name == entityName);
        if (entity == null) return;

        PushUndo();
        var attr = new ERAttribute
        {
            Type = root.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "string" : "string",
            Name = root.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "field" : "field"
        };
        if (root.TryGetProperty("key", out var kProp))
            attr.Key = kProp.GetString();
        if (root.TryGetProperty("comment", out var cProp))
            attr.Comment = cProp.GetString();
        entity.Attributes.Add(attr);
        RaiseERDiagramModelChanged("er_attributeAdded");
    }

    private void HandleErAttributeEdited(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var entityName = root.GetProperty("entityName").GetString();
        var index = root.GetProperty("index").GetInt32();
        if (string.IsNullOrEmpty(entityName)) return;

        var entity = _erDiagramModel.Entities.Find(e => e.Name == entityName);
        if (entity == null || index < 0 || index >= entity.Attributes.Count) return;

        PushUndo();
        var attr = entity.Attributes[index];
        if (root.TryGetProperty("type", out var tProp))
            attr.Type = tProp.GetString() ?? "string";
        if (root.TryGetProperty("name", out var nProp))
            attr.Name = nProp.GetString() ?? "field";
        if (root.TryGetProperty("key", out var kProp))
            attr.Key = kProp.GetString();
        if (root.TryGetProperty("comment", out var cProp))
            attr.Comment = cProp.GetString();
        RaiseERDiagramModelChanged("er_attributeEdited");
    }

    private void HandleErAttributeDeleted(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var entityName = root.GetProperty("entityName").GetString();
        var index = root.GetProperty("index").GetInt32();
        if (string.IsNullOrEmpty(entityName)) return;

        var entity = _erDiagramModel.Entities.Find(e => e.Name == entityName);
        if (entity == null || index < 0 || index >= entity.Attributes.Count) return;

        PushUndo();
        entity.Attributes.RemoveAt(index);
        RaiseERDiagramModelChanged("er_attributeDeleted");
    }

    private void HandleErRelationshipCreated(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var fromEntity = root.GetProperty("fromEntity").GetString();
        var toEntity = root.GetProperty("toEntity").GetString();
        if (string.IsNullOrEmpty(fromEntity) || string.IsNullOrEmpty(toEntity)) return;

        PushUndo();
        var rel = new ERRelationship
        {
            FromEntity = fromEntity,
            ToEntity = toEntity
        };
        if (root.TryGetProperty("leftCardinality", out var lcProp))
        {
            if (Enum.TryParse<ERCardinality>(lcProp.GetString(), true, out var lc))
                rel.LeftCardinality = lc;
        }
        if (root.TryGetProperty("rightCardinality", out var rcProp))
        {
            if (Enum.TryParse<ERCardinality>(rcProp.GetString(), true, out var rc))
                rel.RightCardinality = rc;
        }
        if (root.TryGetProperty("isIdentifying", out var idProp))
            rel.IsIdentifying = idProp.GetBoolean();
        if (root.TryGetProperty("label", out var labelProp))
            rel.Label = labelProp.GetString();
        _erDiagramModel.Relationships.Add(rel);
        RaiseERDiagramModelChanged("er_relationshipCreated");
    }

    private void HandleErRelationshipEdited(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _erDiagramModel.Relationships.Count) return;

        PushUndo();
        var rel = _erDiagramModel.Relationships[index];
        if (root.TryGetProperty("leftCardinality", out var lcProp))
        {
            if (Enum.TryParse<ERCardinality>(lcProp.GetString(), true, out var lc))
                rel.LeftCardinality = lc;
        }
        if (root.TryGetProperty("rightCardinality", out var rcProp))
        {
            if (Enum.TryParse<ERCardinality>(rcProp.GetString(), true, out var rc))
                rel.RightCardinality = rc;
        }
        if (root.TryGetProperty("isIdentifying", out var idProp))
            rel.IsIdentifying = idProp.GetBoolean();
        if (root.TryGetProperty("label", out var labelProp))
            rel.Label = labelProp.GetString();
        RaiseERDiagramModelChanged("er_relationshipEdited");
    }

    private void HandleErRelationshipDeleted(JsonElement root)
    {
        if (_erDiagramModel == null) return;
        var index = root.GetProperty("index").GetInt32();
        if (index < 0 || index >= _erDiagramModel.Relationships.Count) return;

        PushUndo();
        _erDiagramModel.Relationships.RemoveAt(index);
        RaiseERDiagramModelChanged("er_relationshipDeleted");
    }

    private void RestoreERDiagramModelFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<ErDiagramDto>(json, JsonOptions);
        if (dto == null) return;

        _erDiagramModel = new ERDiagramModel
        {
            PreambleLines = dto.PreambleLines ?? new List<string>(),
            DeclarationLineIndex = dto.DeclarationLineIndex
        };

        if (dto.Entities != null)
        {
            foreach (var e in dto.Entities)
            {
                var entity = new EREntity
                {
                    Name = e.Name ?? string.Empty,
                    IsExplicit = e.IsExplicit
                };
                if (e.Attributes != null)
                {
                    foreach (var a in e.Attributes)
                    {
                        entity.Attributes.Add(new ERAttribute
                        {
                            Type = a.Type ?? "string",
                            Name = a.Name ?? "field",
                            Key = a.Key,
                            Comment = a.Comment
                        });
                    }
                }
                _erDiagramModel.Entities.Add(entity);
            }
        }

        if (dto.Relationships != null)
        {
            foreach (var r in dto.Relationships)
            {
                var leftCard = ERCardinality.ExactlyOne;
                var rightCard = ERCardinality.ExactlyOne;
                if (!string.IsNullOrEmpty(r.LeftCardinality))
                    Enum.TryParse(r.LeftCardinality, true, out leftCard);
                if (!string.IsNullOrEmpty(r.RightCardinality))
                    Enum.TryParse(r.RightCardinality, true, out rightCard);

                _erDiagramModel.Relationships.Add(new ERRelationship
                {
                    FromEntity = r.FromEntity ?? string.Empty,
                    ToEntity = r.ToEntity ?? string.Empty,
                    LeftCardinality = leftCard,
                    RightCardinality = rightCard,
                    IsIdentifying = r.IsIdentifying,
                    Label = r.Label
                });
            }
        }
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

        _sequenceModel.Elements.Add(note);
        RaiseSequenceModelChanged("seq_noteCreated");
    }

    private void HandleSeqFragmentCreated(JsonElement root)
    {
        if (_sequenceModel == null) return;

        var fragTypeStr = root.TryGetProperty("fragmentType", out var ftProp) ? ftProp.GetString() ?? "alt" : "alt";
        var condition = root.TryGetProperty("condition", out var condProp) ? condProp.GetString() ?? "" : "";

        PushUndo();
        var fragment = new SequenceFragment
        {
            Label = condition
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

        _sequenceModel.Elements.Add(fragment);
        RaiseSequenceModelChanged("seq_fragmentCreated");
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
                        Label = dto.Text ?? string.Empty
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

    // ========== DTOs for JSON Serialization ==========

    private class DiagramDto
    {
        public string? Direction { get; set; }
        public List<NodeDto>? Nodes { get; set; }
        public List<EdgeDto>? Edges { get; set; }
        public List<SubgraphDto>? Subgraphs { get; set; }
        public List<StyleDto>? Styles { get; set; }
        public List<CommentDto>? Comments { get; set; }
        public List<ClassAssignmentDto>? ClassAssignments { get; set; }
        public string? DiagramKeyword { get; set; }
        public int DeclarationLineIndex { get; set; }
        public List<string>? PreambleLines { get; set; }
    }

    private class NodeDto
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public string? Shape { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string? CssClass { get; set; }
        public bool HasManualPosition { get; set; }
    }

    private class EdgeDto
    {
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Label { get; set; }
        public string? Style { get; set; }
        public string? ArrowType { get; set; }
        public bool IsBidirectional { get; set; }
        public int LinkLength { get; set; } = 2;
    }

    private class SubgraphDto
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public List<string>? NodeIds { get; set; }
        public string? Direction { get; set; }
    }

    private class StyleDto
    {
        public bool IsClassDef { get; set; }
        public string? Target { get; set; }
        public string? StyleString { get; set; }
    }

    private class CommentDto
    {
        public string? Text { get; set; }
        public int OriginalLineIndex { get; set; }
    }

    private class ClassAssignmentDto
    {
        public string? NodeIds { get; set; }
        public string? ClassName { get; set; }
    }

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
        // Activation fields
        public string? ParticipantId { get; set; }
    }

    private class SeqFragmentSectionDto
    {
        public string? Label { get; set; }
        public List<SeqElementDto>? Elements { get; set; }
    }

    // ========== Class Diagram DTOs ==========

    private class ClsDiagramDto
    {
        public string? Direction { get; set; }
        public List<ClsDiagClassDto>? Classes { get; set; }
        public List<ClsDiagRelDto>? Relationships { get; set; }
        public List<ClsDiagNamespaceDto>? Namespaces { get; set; }
        public List<ClsDiagNoteDto>? Notes { get; set; }
        public List<string> PreambleLines { get; set; } = new();
        public int DeclarationLineIndex { get; set; }
    }

    private class ClsDiagClassDto
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public string? Annotation { get; set; }
        public string? GenericType { get; set; }
        public List<ClsDiagMemberDto>? Members { get; set; }
        public string? CssClass { get; set; }
        public bool IsExplicit { get; set; }
    }

    private class ClsDiagMemberDto
    {
        public string? RawText { get; set; }
        public string? Visibility { get; set; }
        public bool IsMethod { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Parameters { get; set; }
        public string? Classifier { get; set; }
    }

    private class ClsDiagRelDto
    {
        public string? FromId { get; set; }
        public string? ToId { get; set; }
        public string? LeftEnd { get; set; }
        public string? RightEnd { get; set; }
        public string? LinkStyle { get; set; }
        public string? Label { get; set; }
        public string? FromCardinality { get; set; }
        public string? ToCardinality { get; set; }
    }

    private class ClsDiagNamespaceDto
    {
        public string? Name { get; set; }
        public List<string>? ClassIds { get; set; }
    }

    private class ClsDiagNoteDto
    {
        public string? Text { get; set; }
        public string? ForClass { get; set; }
    }

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

    // ========== ER Diagram DTOs ==========

    private class ErDiagramDto
    {
        public List<ErDiagEntityDto>? Entities { get; set; }
        public List<ErDiagRelDto>? Relationships { get; set; }
        public List<string>? PreambleLines { get; set; }
        public int DeclarationLineIndex { get; set; }
    }

    private class ErDiagEntityDto
    {
        public string? Name { get; set; }
        public bool IsExplicit { get; set; }
        public List<ErDiagAttributeDto>? Attributes { get; set; }
    }

    private class ErDiagAttributeDto
    {
        public string? Type { get; set; }
        public string? Name { get; set; }
        public string? Key { get; set; }
        public string? Comment { get; set; }
    }

    private class ErDiagRelDto
    {
        public string? FromEntity { get; set; }
        public string? ToEntity { get; set; }
        public string? LeftCardinality { get; set; }
        public string? RightCardinality { get; set; }
        public bool IsIdentifying { get; set; }
        public string? Label { get; set; }
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
