namespace MermaidEditor;

/// <summary>
/// Represents a complete Mermaid flowchart diagram.
/// This is the in-memory AST (Abstract Syntax Tree) that serves as the single
/// source of truth for both the text editor and visual editor.
/// </summary>
public class FlowchartModel
{
    /// <summary>
    /// The flowchart direction: TD (top-down), LR (left-right), BT (bottom-top), RL (right-left), TB (top-bottom).
    /// </summary>
    public string Direction { get; set; } = "TD";

    /// <summary>
    /// All nodes in the flowchart.
    /// </summary>
    public List<FlowchartNode> Nodes { get; set; } = new();

    /// <summary>
    /// All edges (connections) between nodes.
    /// </summary>
    public List<FlowchartEdge> Edges { get; set; } = new();

    /// <summary>
    /// Subgraph groupings.
    /// </summary>
    public List<FlowchartSubgraph> Subgraphs { get; set; } = new();

    /// <summary>
    /// Preserved comments from the source text (%% lines).
    /// </summary>
    public List<CommentEntry> Comments { get; set; } = new();

    /// <summary>
    /// Style definitions (style nodeId fill:#color and classDef className fill:#color).
    /// </summary>
    public List<StyleDefinition> Styles { get; set; } = new();

    /// <summary>
    /// Class assignments (class nodeId className).
    /// </summary>
    public List<ClassAssignment> ClassAssignments { get; set; } = new();

    /// <summary>
    /// The diagram keyword used in the source (e.g., "flowchart" or "graph").
    /// </summary>
    public string DiagramKeyword { get; set; } = "flowchart";

    /// <summary>
    /// The original line index of the flowchart declaration in the source text.
    /// Used by the serializer to correctly position comments relative to the declaration.
    /// </summary>
    public int DeclarationLineIndex { get; set; }

    /// <summary>
    /// Lines that appear before the flowchart declaration (e.g., config directives, frontmatter).
    /// These are preserved verbatim through round-trips.
    /// </summary>
    public List<string> PreambleLines { get; set; } = new();
}

/// <summary>
/// Represents a node in a flowchart diagram.
/// </summary>
public class FlowchartNode
{
    /// <summary>
    /// The unique identifier for this node (e.g., "A", "myNode").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The display label for this node. If null, the Id is used as the label.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// The visual shape of the node.
    /// </summary>
    public NodeShape Shape { get; set; } = NodeShape.Rectangle;

    /// <summary>
    /// Position for the visual editor (x, y coordinates).
    /// </summary>
    public System.Windows.Point Position { get; set; }

    /// <summary>
    /// Size for the visual editor (width, height).
    /// </summary>
    public System.Windows.Size Size { get; set; }

    /// <summary>
    /// Optional CSS class name applied via classDef.
    /// </summary>
    public string? CssClass { get; set; }

    /// <summary>
    /// Whether this node has been manually positioned in the visual editor.
    /// When true, position data is stored as a special comment (%% @pos nodeId x,y)
    /// during serialization, and restored on parse.
    /// </summary>
    public bool HasManualPosition { get; set; }

    /// <summary>
    /// Returns the effective display label (Label if set, otherwise Id).
    /// </summary>
    public string DisplayLabel => Label ?? Id;
}

/// <summary>
/// All supported Mermaid flowchart node shapes.
/// </summary>
public enum NodeShape
{
    /// <summary>Square brackets: A[text]</summary>
    Rectangle,

    /// <summary>Parentheses: A(text)</summary>
    Rounded,

    /// <summary>Stadium shape: A([text])</summary>
    Stadium,

    /// <summary>Subroutine shape: A[[text]]</summary>
    Subroutine,

    /// <summary>Cylindrical/database shape: A[(text)]</summary>
    Cylindrical,

    /// <summary>Circle shape: A((text))</summary>
    Circle,

    /// <summary>Asymmetric/flag shape: A>text]</summary>
    Asymmetric,

    /// <summary>Diamond/rhombus shape: A{text}</summary>
    Rhombus,

    /// <summary>Hexagon shape: A{{text}}</summary>
    Hexagon,

    /// <summary>Parallelogram shape: A[/text/]</summary>
    Parallelogram,

    /// <summary>Reverse parallelogram shape: A[\text\]</summary>
    ParallelogramAlt,

    /// <summary>Trapezoid shape: A[/text\]</summary>
    Trapezoid,

    /// <summary>Reverse trapezoid shape: A[\text/]</summary>
    TrapezoidAlt,

    /// <summary>Double circle shape: A(((text)))</summary>
    DoubleCircle
}

/// <summary>
/// Edge line styles.
/// </summary>
public enum EdgeStyle
{
    /// <summary>Solid line: --></summary>
    Solid,

    /// <summary>Dotted line: -.-></summary>
    Dotted,

    /// <summary>Thick line: ==></summary>
    Thick
}

/// <summary>
/// Arrow head types for edges.
/// </summary>
public enum ArrowType
{
    /// <summary>Standard arrow: --></summary>
    Arrow,

    /// <summary>Open (no arrowhead): ---</summary>
    Open,

    /// <summary>Circle end: --o</summary>
    Circle,

    /// <summary>Cross end: --x</summary>
    Cross
}

/// <summary>
/// Represents an edge (connection) between two nodes.
/// </summary>
public class FlowchartEdge
{
    /// <summary>
    /// The source node ID.
    /// </summary>
    public string FromNodeId { get; set; } = string.Empty;

    /// <summary>
    /// The target node ID.
    /// </summary>
    public string ToNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Optional label displayed on the edge.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// The visual style of the edge line.
    /// </summary>
    public EdgeStyle Style { get; set; } = EdgeStyle.Solid;

    /// <summary>
    /// The type of arrow at the target end.
    /// </summary>
    public ArrowType ArrowType { get; set; } = ArrowType.Arrow;

    /// <summary>
    /// Whether the edge has an arrow at the source end (bidirectional).
    /// </summary>
    public bool IsBidirectional { get; set; }

    /// <summary>
    /// The minimum length of the edge (number of dashes, e.g., ---> is length 3).
    /// Default is 2 for standard edges.
    /// </summary>
    public int LinkLength { get; set; } = 2;
}

/// <summary>
/// Represents a subgraph grouping in a flowchart.
/// </summary>
public class FlowchartSubgraph
{
    /// <summary>
    /// The subgraph identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The display label/title for the subgraph.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// IDs of nodes that belong to this subgraph.
    /// </summary>
    public List<string> NodeIds { get; set; } = new();

    /// <summary>
    /// Optional direction override within the subgraph.
    /// </summary>
    public string? Direction { get; set; }
}

/// <summary>
/// Represents a style or classDef directive.
/// </summary>
public class StyleDefinition
{
    /// <summary>
    /// Whether this is a classDef (true) or inline style (false).
    /// </summary>
    public bool IsClassDef { get; set; }

    /// <summary>
    /// For classDef: the class name. For style: the node ID(s).
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// The raw CSS-like style string (e.g., "fill:#f9f,stroke:#333,stroke-width:4px").
    /// </summary>
    public string StyleString { get; set; } = string.Empty;
}

/// <summary>
/// Represents a class assignment (class nodeId className).
/// </summary>
public class ClassAssignment
{
    /// <summary>
    /// The node ID(s) to assign the class to (comma-separated for multiple).
    /// </summary>
    public string NodeIds { get; set; } = string.Empty;

    /// <summary>
    /// The class name to assign.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;
}

/// <summary>
/// Represents a preserved comment from the source text.
/// </summary>
public class CommentEntry
{
    /// <summary>
    /// The comment text (without the %% prefix).
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The original line index in the source text (for preserving position).
    /// </summary>
    public int OriginalLineIndex { get; set; }
}

// =============================================
// Sequence Diagram Models (Phase 2.1)
// =============================================

/// <summary>
/// Represents a complete Mermaid sequence diagram.
/// </summary>
public class SequenceDiagramModel
{
    /// <summary>
    /// All participants/actors in the diagram, in order of appearance (left to right).
    /// </summary>
    public List<SequenceParticipant> Participants { get; set; } = new();

    /// <summary>
    /// All messages and events in the diagram, in order of appearance (top to bottom).
    /// </summary>
    public List<SequenceElement> Elements { get; set; } = new();

    /// <summary>
    /// Preserved comments from the source text (%% lines).
    /// </summary>
    public List<CommentEntry> Comments { get; set; } = new();

    /// <summary>
    /// Lines that appear before the sequenceDiagram declaration (e.g., config directives).
    /// </summary>
    public List<string> PreambleLines { get; set; } = new();

    /// <summary>
    /// The original line index of the sequenceDiagram declaration.
    /// </summary>
    public int DeclarationLineIndex { get; set; }

    /// <summary>
    /// Whether to automatically number messages (autonumber directive).
    /// </summary>
    public bool AutoNumber { get; set; }
}

/// <summary>
/// Represents a participant or actor in a sequence diagram.
/// </summary>
public class SequenceParticipant
{
    /// <summary>
    /// The unique identifier for this participant.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The display alias/label. If null, Id is used.
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>
    /// Whether this is an actor (stick figure) or participant (box).
    /// </summary>
    public SequenceParticipantType Type { get; set; } = SequenceParticipantType.Participant;

    /// <summary>
    /// Returns the effective display label.
    /// </summary>
    public string DisplayLabel => Alias ?? Id;

    /// <summary>
    /// Whether this participant was explicitly declared (vs. implicitly created from a message).
    /// </summary>
    public bool IsExplicit { get; set; }

    /// <summary>
    /// Whether this participant has been destroyed (via destroy keyword).
    /// </summary>
    public bool IsDestroyed { get; set; }
}

/// <summary>
/// Type of sequence diagram participant.
/// </summary>
public enum SequenceParticipantType
{
    /// <summary>Rendered as a box: participant Alice</summary>
    Participant,

    /// <summary>Rendered as a stick figure: actor Alice</summary>
    Actor
}

/// <summary>
/// Base class for all elements in a sequence diagram (messages, notes, fragments, etc.).
/// Elements are ordered top-to-bottom in the diagram.
/// </summary>
public abstract class SequenceElement
{
}

/// <summary>
/// Represents a message (arrow) between two participants.
/// </summary>
public class SequenceMessage : SequenceElement
{
    /// <summary>
    /// The source participant ID.
    /// </summary>
    public string FromId { get; set; } = string.Empty;

    /// <summary>
    /// The target participant ID.
    /// </summary>
    public string ToId { get; set; } = string.Empty;

    /// <summary>
    /// The message text displayed on the arrow.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The arrow/line style of this message.
    /// </summary>
    public SequenceArrowType ArrowType { get; set; } = SequenceArrowType.SolidArrow;

    /// <summary>
    /// Whether this message activates the target participant's lifeline.
    /// Corresponds to the + suffix on arrow types.
    /// </summary>
    public bool ActivateTarget { get; set; }

    /// <summary>
    /// Whether this message deactivates the source participant's lifeline.
    /// Corresponds to the - suffix on arrow types.
    /// </summary>
    public bool DeactivateSource { get; set; }
}

/// <summary>
/// Arrow types for sequence diagram messages.
/// </summary>
public enum SequenceArrowType
{
    /// <summary>Solid line with filled arrowhead: ->></summary>
    SolidArrow,

    /// <summary>Dotted line with filled arrowhead: -->> (return/response)</summary>
    DottedArrow,

    /// <summary>Solid line with open arrowhead: ->   (async)</summary>
    SolidOpen,

    /// <summary>Dotted line with open arrowhead: --> (async return)</summary>
    DottedOpen,

    /// <summary>Solid line with cross end: -x (lost message)</summary>
    SolidCross,

    /// <summary>Dotted line with cross end: --x</summary>
    DottedCross,

    /// <summary>Solid line with open arrow (async): -)</summary>
    SolidAsync,

    /// <summary>Dotted line with open arrow (async): --)</summary>
    DottedAsync
}

/// <summary>
/// Represents a note in a sequence diagram.
/// </summary>
public class SequenceNote : SequenceElement
{
    /// <summary>
    /// The note text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The position of the note relative to participant(s).
    /// </summary>
    public SequenceNotePosition Position { get; set; } = SequenceNotePosition.RightOf;

    /// <summary>
    /// The participant ID(s) this note is attached to.
    /// For "over" notes spanning multiple participants, comma-separated.
    /// </summary>
    public string OverParticipants { get; set; } = string.Empty;
}

/// <summary>
/// Note positioning in a sequence diagram.
/// </summary>
public enum SequenceNotePosition
{
    /// <summary>Note right of Alice</summary>
    RightOf,

    /// <summary>Note left of Alice</summary>
    LeftOf,

    /// <summary>Note over Alice or Note over Alice,Bob</summary>
    Over
}

/// <summary>
/// Represents a fragment (loop, alt, opt, par, critical, break, rect) in a sequence diagram.
/// Fragments can contain nested elements and have multiple sections (e.g., alt/else).
/// </summary>
public class SequenceFragment : SequenceElement
{
    /// <summary>
    /// The fragment type (loop, alt, opt, par, critical, break, rect).
    /// </summary>
    public SequenceFragmentType Type { get; set; }

    /// <summary>
    /// The label/condition text for the fragment.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The sections within the fragment.
    /// For simple fragments (loop, opt), there is one section.
    /// For alt, there is the main section plus else sections.
    /// For par, there is the main section plus "and" sections.
    /// </summary>
    public List<SequenceFragmentSection> Sections { get; set; } = new();
}

/// <summary>
/// A section within a fragment (e.g., the "if" part or an "else" part of an alt block).
/// </summary>
public class SequenceFragmentSection
{
    /// <summary>
    /// The label for this section (e.g., condition text for alt/else, or empty for the main section of a loop).
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// The elements contained within this section.
    /// </summary>
    public List<SequenceElement> Elements { get; set; } = new();
}

/// <summary>
/// Types of sequence diagram fragments.
/// </summary>
public enum SequenceFragmentType
{
    /// <summary>loop [condition]...end</summary>
    Loop,

    /// <summary>alt [condition]...else [condition]...end</summary>
    Alt,

    /// <summary>opt [condition]...end</summary>
    Opt,

    /// <summary>par [label]...and [label]...end</summary>
    Par,

    /// <summary>critical [label]...option [label]...end</summary>
    Critical,

    /// <summary>break [condition]...end</summary>
    Break,

    /// <summary>rect rgb(...)...end</summary>
    Rect
}

/// <summary>
/// Represents an activate directive in a sequence diagram.
/// </summary>
public class SequenceActivation : SequenceElement
{
    /// <summary>
    /// The participant ID to activate.
    /// </summary>
    public string ParticipantId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is an activate (true) or deactivate (false) directive.
    /// </summary>
    public bool IsActivate { get; set; }
}

/// <summary>
/// Represents a participant creation event (create participant Alice).
/// </summary>
public class SequenceCreate : SequenceElement
{
    /// <summary>
    /// The type of the created participant (participant or actor).
    /// </summary>
    public SequenceParticipantType ParticipantType { get; set; } = SequenceParticipantType.Participant;

    /// <summary>
    /// The participant ID being created.
    /// </summary>
    public string ParticipantId { get; set; } = string.Empty;
}

/// <summary>
/// Represents a participant destruction event (destroy Alice).
/// </summary>
public class SequenceDestroy : SequenceElement
{
    /// <summary>
    /// The participant ID being destroyed.
    /// </summary>
    public string ParticipantId { get; set; } = string.Empty;
}
