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

    /// <summary>
    /// Optional: the participant ID at the left edge of the fragment.
    /// When null/empty, the fragment auto-sizes based on inner messages (or spans all participants).
    /// </summary>
    public string? OverParticipantStart { get; set; }

    /// <summary>
    /// Optional: the participant ID at the right edge of the fragment.
    /// When null/empty, the fragment auto-sizes based on inner messages (or spans all participants).
    /// </summary>
    public string? OverParticipantEnd { get; set; }
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

// =============================================
// Class Diagram Models (Phase 2.2)
// =============================================

/// <summary>
/// Represents a complete Mermaid class diagram.
/// </summary>
public class ClassDiagramModel
{
    /// <summary>
    /// Optional direction for the diagram layout (TB, BT, RL, LR).
    /// </summary>
    public string? Direction { get; set; }

    /// <summary>
    /// All class definitions in the diagram.
    /// </summary>
    public List<ClassDefinition> Classes { get; set; } = new();

    /// <summary>
    /// All relationships between classes.
    /// </summary>
    public List<ClassRelationship> Relationships { get; set; } = new();

    /// <summary>
    /// Namespace groupings.
    /// </summary>
    public List<ClassNamespace> Namespaces { get; set; } = new();

    /// <summary>
    /// Notes attached to the diagram or specific classes.
    /// </summary>
    public List<ClassNote> Notes { get; set; } = new();

    /// <summary>
    /// Preserved comments from the source text (%% lines).
    /// </summary>
    public List<CommentEntry> Comments { get; set; } = new();

    /// <summary>
    /// Lines that appear before the classDiagram declaration (e.g., config directives).
    /// </summary>
    public List<string> PreambleLines { get; set; } = new();

    /// <summary>
    /// The original line index of the classDiagram declaration.
    /// </summary>
    public int DeclarationLineIndex { get; set; }

    /// <summary>
    /// Style definitions (style className fill:#color and classDef className fill:#color).
    /// </summary>
    public List<StyleDefinition> Styles { get; set; } = new();

    /// <summary>
    /// CSS class assignments (cssClass "nodeId" className).
    /// </summary>
    public List<ClassDiagramCssClass> CssClassAssignments { get; set; } = new();
}

/// <summary>
/// Represents a class definition in a class diagram.
/// </summary>
public class ClassDefinition
{
    /// <summary>
    /// The unique identifier/name for this class.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Optional display label (for class Animal["Label"] syntax).
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Optional annotation (e.g., "interface", "abstract", "enumeration", "service").
    /// Stored without the &lt;&lt; &gt;&gt; delimiters.
    /// </summary>
    public string? Annotation { get; set; }

    /// <summary>
    /// Optional generic type parameter (e.g., "Shape" for class Square~Shape~).
    /// </summary>
    public string? GenericType { get; set; }

    /// <summary>
    /// All members (fields and methods) of this class.
    /// </summary>
    public List<ClassMember> Members { get; set; } = new();

    /// <summary>
    /// Optional CSS class name applied via ::: operator.
    /// </summary>
    public string? CssClass { get; set; }

    /// <summary>
    /// Whether this class was explicitly declared with the class keyword
    /// (vs. implicitly created from a relationship or member definition).
    /// </summary>
    public bool IsExplicit { get; set; }

    /// <summary>
    /// Position for the visual editor (x, y coordinates).
    /// </summary>
    public System.Windows.Point Position { get; set; }

    /// <summary>
    /// Whether this class has been manually positioned in the visual editor.
    /// When true, position data is stored as a special comment (%% @pos classId x,y).
    /// </summary>
    public bool HasManualPosition { get; set; }

    /// <summary>
    /// Returns the effective display label (Label if set, otherwise Id).
    /// </summary>
    public string DisplayLabel => Label ?? Id;
}

/// <summary>
/// Represents a member (field or method) of a class.
/// </summary>
public class ClassMember
{
    /// <summary>
    /// The raw text of the member as it appeared in the source.
    /// Used for round-trip fidelity when the member isn't modified.
    /// </summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>
    /// The visibility modifier of the member.
    /// </summary>
    public MemberVisibility Visibility { get; set; } = MemberVisibility.None;

    /// <summary>
    /// Whether this member is a method (has parentheses).
    /// </summary>
    public bool IsMethod { get; set; }

    /// <summary>
    /// The member name (without visibility prefix, type, or parameters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The data type for fields, or return type for methods.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// The parameter text inside parentheses for methods (without the parens).
    /// </summary>
    public string? Parameters { get; set; }

    /// <summary>
    /// Optional classifier suffix (* for abstract, $ for static).
    /// </summary>
    public MemberClassifier Classifier { get; set; } = MemberClassifier.None;
}

/// <summary>
/// Visibility modifiers for class members.
/// </summary>
public enum MemberVisibility
{
    /// <summary>No visibility modifier specified.</summary>
    None,

    /// <summary>Public: +</summary>
    Public,

    /// <summary>Private: -</summary>
    Private,

    /// <summary>Protected: #</summary>
    Protected,

    /// <summary>Package/Internal: ~</summary>
    Package
}

/// <summary>
/// Classifier suffixes for class members.
/// </summary>
public enum MemberClassifier
{
    /// <summary>No classifier.</summary>
    None,

    /// <summary>Abstract method: someMethod()*</summary>
    Abstract,

    /// <summary>Static member: someMethod()$ or someField$</summary>
    Static
}

/// <summary>
/// Represents a relationship between two classes.
/// </summary>
public class ClassRelationship
{
    /// <summary>
    /// The left-side class ID.
    /// </summary>
    public string FromId { get; set; } = string.Empty;

    /// <summary>
    /// The right-side class ID.
    /// </summary>
    public string ToId { get; set; } = string.Empty;

    /// <summary>
    /// The relation end type on the left (From) side.
    /// </summary>
    public ClassRelationEnd LeftEnd { get; set; } = ClassRelationEnd.None;

    /// <summary>
    /// The relation end type on the right (To) side.
    /// </summary>
    public ClassRelationEnd RightEnd { get; set; } = ClassRelationEnd.None;

    /// <summary>
    /// The link style (solid or dashed).
    /// </summary>
    public ClassLinkStyle LinkStyle { get; set; } = ClassLinkStyle.Solid;

    /// <summary>
    /// Optional label describing the relationship.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Optional cardinality on the left (From) side (e.g., "1", "0..*").
    /// </summary>
    public string? FromCardinality { get; set; }

    /// <summary>
    /// Optional cardinality on the right (To) side (e.g., "*", "1..*").
    /// </summary>
    public string? ToCardinality { get; set; }
}

/// <summary>
/// Relation end types for class diagram relationships.
/// </summary>
public enum ClassRelationEnd
{
    /// <summary>No marker (plain line end).</summary>
    None,

    /// <summary>Inheritance arrow: &lt;| or |&gt;</summary>
    Inheritance,

    /// <summary>Composition diamond: *</summary>
    Composition,

    /// <summary>Aggregation diamond: o</summary>
    Aggregation,

    /// <summary>Association arrow: &lt; or &gt;</summary>
    Arrow,

    /// <summary>Lollipop interface: ()</summary>
    Lollipop
}

/// <summary>
/// Link line styles for class diagram relationships.
/// </summary>
public enum ClassLinkStyle
{
    /// <summary>Solid line: --</summary>
    Solid,

    /// <summary>Dashed line: ..</summary>
    Dashed
}

/// <summary>
/// Represents a namespace grouping in a class diagram.
/// </summary>
public class ClassNamespace
{
    /// <summary>
    /// The namespace name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// IDs of classes that belong to this namespace.
    /// </summary>
    public List<string> ClassIds { get; set; } = new();
}

/// <summary>
/// Represents a note in a class diagram.
/// </summary>
public class ClassNote
{
    /// <summary>
    /// The note text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The class ID this note is for (null for general notes).
    /// </summary>
    public string? ForClass { get; set; }
}

/// <summary>
/// Represents a cssClass assignment in a class diagram.
/// </summary>
public class ClassDiagramCssClass
{
    /// <summary>
    /// The node ID(s) to assign the class to (comma-separated for multiple).
    /// </summary>
    public string NodeIds { get; set; } = string.Empty;

    /// <summary>
    /// The CSS class name to assign.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;
}

// =============================================
// State Diagram Models (Phase 2.3)
// =============================================

/// <summary>
/// Represents a complete Mermaid state diagram (stateDiagram-v2).
/// Contains states, transitions, notes, and composite states.
/// </summary>
public class StateDiagramModel
{
    /// <summary>
    /// Optional direction for the state diagram (e.g., "LR", "TB").
    /// </summary>
    public string? Direction { get; set; }

    /// <summary>
    /// All state definitions in the diagram.
    /// </summary>
    public List<StateDefinition> States { get; set; } = new();

    /// <summary>
    /// All transitions in the diagram.
    /// </summary>
    public List<StateTransition> Transitions { get; set; } = new();

    /// <summary>
    /// Notes attached to states.
    /// </summary>
    public List<StateNote> Notes { get; set; } = new();

    /// <summary>
    /// Comments preserved from the original text.
    /// </summary>
    public List<CommentEntry> Comments { get; set; } = new();

    /// <summary>
    /// Lines that appeared before the stateDiagram declaration (config directives, frontmatter, etc.).
    /// </summary>
    public List<string> PreambleLines { get; set; } = new();

    /// <summary>
    /// The line index of the stateDiagram declaration in the original text.
    /// Used for comment placement during serialization.
    /// </summary>
    public int DeclarationLineIndex { get; set; }

    /// <summary>
    /// Style definitions (classDef and style directives).
    /// </summary>
    public List<StyleDefinition> Styles { get; set; } = new();

    /// <summary>
    /// Whether the diagram uses stateDiagram-v2 (true) or stateDiagram (false).
    /// </summary>
    public bool IsV2 { get; set; } = true;

    /// <summary>
    /// Positions of pseudo-state nodes ([*]_start, [*]_end, [*]_start_ParentId, etc.)
    /// that don't have corresponding StateDefinition objects. Keyed by pseudo-node ID.
    /// </summary>
    public Dictionary<string, System.Windows.Point> PseudoNodePositions { get; set; } = new();

    /// <summary>
    /// Positions of notes that have been manually dragged.
    /// Keyed by note index (as string), value is the (x,y) position.
    /// </summary>
    public Dictionary<string, System.Windows.Point> NotePositions { get; set; } = new();
}

/// <summary>
/// Represents a state in a state diagram.
/// Can be a simple state, composite state (with nested states), fork/join, or choice.
/// </summary>
public class StateDefinition
{
    /// <summary>
    /// The state identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Optional display label for the state.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// The type of state (simple, composite, fork, join, choice).
    /// </summary>
    public StateType Type { get; set; } = StateType.Simple;

    /// <summary>
    /// Nested states for composite states.
    /// </summary>
    public List<StateDefinition> NestedStates { get; set; } = new();

    /// <summary>
    /// Nested transitions for composite states.
    /// </summary>
    public List<StateTransition> NestedTransitions { get; set; } = new();

    /// <summary>
    /// Optional CSS class assignment.
    /// </summary>
    public string? CssClass { get; set; }

    /// <summary>
    /// Whether this state was explicitly declared (vs. inferred from transitions).
    /// </summary>
    public bool IsExplicit { get; set; }

    /// <summary>
    /// Position for the visual editor (x, y coordinates).
    /// </summary>
    public System.Windows.Point Position { get; set; }

    /// <summary>
    /// Size for the visual editor (width, height). Primarily used for composite states
    /// so their container dimensions are preserved when switching documents.
    /// </summary>
    public System.Windows.Size Size { get; set; }

    /// <summary>
    /// Whether this state has been manually positioned in the visual editor.
    /// When true, position data is stored as a special comment (%% @pos stateId x,y,w,h).
    /// </summary>
    public bool HasManualPosition { get; set; }

    /// <summary>
    /// Display label: uses Label if set, otherwise Id.
    /// </summary>
    public string DisplayLabel => Label ?? Id;
}

/// <summary>
/// Types of states in a state diagram.
/// </summary>
public enum StateType
{
    /// <summary>A regular state.</summary>
    Simple,

    /// <summary>A composite state containing nested states and transitions.</summary>
    Composite,

    /// <summary>A fork pseudo-state (horizontal bar splitting into parallel paths).</summary>
    Fork,

    /// <summary>A join pseudo-state (horizontal bar merging parallel paths).</summary>
    Join,

    /// <summary>A choice pseudo-state (diamond decision point).</summary>
    Choice
}

/// <summary>
/// Represents a transition between states in a state diagram.
/// </summary>
public class StateTransition
{
    /// <summary>
    /// The source state ID. Use "[*]" for start pseudo-state.
    /// </summary>
    public string FromId { get; set; } = string.Empty;

    /// <summary>
    /// The target state ID. Use "[*]" for end pseudo-state.
    /// </summary>
    public string ToId { get; set; } = string.Empty;

    /// <summary>
    /// Optional transition label (event [guard] / action).
    /// </summary>
    public string? Label { get; set; }
}

/// <summary>
/// Represents a note attached to a state.
/// </summary>
public class StateNote
{
    /// <summary>
    /// The note text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The state ID this note is attached to.
    /// </summary>
    public string StateId { get; set; } = string.Empty;

    /// <summary>
    /// Position of the note relative to the state.
    /// </summary>
    public StateNotePosition Position { get; set; } = StateNotePosition.RightOf;
}

/// <summary>
/// Position of a note relative to a state.
/// </summary>
public enum StateNotePosition
{
    /// <summary>Note appears to the right of the state.</summary>
    RightOf,

    /// <summary>Note appears to the left of the state.</summary>
    LeftOf
}

// =============================================
// ER Diagram Models (Phase 2.4)
// =============================================

/// <summary>
/// Represents a complete Mermaid ER (Entity-Relationship) diagram.
/// Contains entities with attributes and relationships with cardinality markers.
/// </summary>
public class ERDiagramModel
{
    /// <summary>
    /// All entity definitions in the diagram.
    /// </summary>
    public List<EREntity> Entities { get; set; } = new();

    /// <summary>
    /// All relationships between entities.
    /// </summary>
    public List<ERRelationship> Relationships { get; set; } = new();

    /// <summary>
    /// Comments preserved from the original text.
    /// </summary>
    public List<CommentEntry> Comments { get; set; } = new();

    /// <summary>
    /// Lines that appeared before the erDiagram declaration (config directives, frontmatter, etc.).
    /// </summary>
    public List<string> PreambleLines { get; set; } = new();

    /// <summary>
    /// The line index of the erDiagram declaration in the original text.
    /// Used for comment placement during serialization.
    /// </summary>
    public int DeclarationLineIndex { get; set; }
}

/// <summary>
/// Represents an entity in an ER diagram.
/// An entity has a name and optional attributes with types and keys.
/// </summary>
public class EREntity
{
    /// <summary>
    /// The entity name (e.g., "CUSTOMER", "ORDER").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Attributes of the entity.
    /// </summary>
    public List<ERAttribute> Attributes { get; set; } = new();

    /// <summary>
    /// Whether this entity was explicitly declared (vs. inferred from relationships).
    /// </summary>
    public bool IsExplicit { get; set; }

    /// <summary>
    /// Position for the visual editor (x, y coordinates).
    /// </summary>
    public System.Windows.Point Position { get; set; }

    /// <summary>
    /// Whether this entity has been manually positioned in the visual editor.
    /// When true, position data is stored as a special comment (%% @pos entityName x,y).
    /// </summary>
    public bool HasManualPosition { get; set; }
}

/// <summary>
/// Represents an attribute of an entity in an ER diagram.
/// Attributes have a type, name, and optional key/comment.
/// </summary>
public class ERAttribute
{
    /// <summary>
    /// The data type of the attribute (e.g., "string", "int", "date").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The attribute name (e.g., "name", "id", "address").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The key/constraint type (e.g., "PK", "FK", "UK").
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Optional comment/description for the attribute (in quotes).
    /// </summary>
    public string? Comment { get; set; }
}

/// <summary>
/// Represents a relationship between two entities in an ER diagram.
/// Includes cardinality markers on both sides.
/// </summary>
public class ERRelationship
{
    /// <summary>
    /// The first (left) entity name.
    /// </summary>
    public string FromEntity { get; set; } = string.Empty;

    /// <summary>
    /// The second (right) entity name.
    /// </summary>
    public string ToEntity { get; set; } = string.Empty;

    /// <summary>
    /// Cardinality on the left side of the relationship.
    /// </summary>
    public ERCardinality LeftCardinality { get; set; } = ERCardinality.ExactlyOne;

    /// <summary>
    /// Cardinality on the right side of the relationship.
    /// </summary>
    public ERCardinality RightCardinality { get; set; } = ERCardinality.ExactlyOne;

    /// <summary>
    /// Whether the relationship is identifying (solid line) or non-identifying (dashed line).
    /// Identifying uses "--", non-identifying uses "..".
    /// </summary>
    public bool IsIdentifying { get; set; } = true;

    /// <summary>
    /// The relationship label (e.g., "places", "contains", "has").
    /// </summary>
    public string? Label { get; set; }
}

/// <summary>
/// Cardinality markers for ER diagram relationships.
/// These define how many instances of one entity relate to another.
/// </summary>
public enum ERCardinality
{
    /// <summary>Exactly one: || (one and only one)</summary>
    ExactlyOne,

    /// <summary>Zero or one: |o (zero or one)</summary>
    ZeroOrOne,

    /// <summary>Zero or more: }o (zero or more)</summary>
    ZeroOrMore,

    /// <summary>One or more: }| (one or more)</summary>
    OneOrMore
}

// =============================================
// Gantt Chart Models (Phase 4)
// =============================================

/// <summary>
/// Represents a complete Mermaid gantt chart diagram.
/// Contains sections with tasks, date format, and axis formatting.
/// </summary>
public class GanttModel
{
    /// <summary>
    /// The chart title (optional).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The date format string (e.g., "YYYY-MM-DD").
    /// </summary>
    public string DateFormat { get; set; } = "YYYY-MM-DD";

    /// <summary>
    /// The axis format string for timeline display (e.g., "%Y-%m-%d").
    /// </summary>
    public string? AxisFormat { get; set; }

    /// <summary>
    /// Whether to exclude weekends from the timeline.
    /// </summary>
    public bool ExcludesWeekends { get; set; }

    /// <summary>
    /// Custom excludes string (e.g., "weekends", "2024-01-01").
    /// </summary>
    public string? Excludes { get; set; }

    /// <summary>
    /// All sections in the gantt chart.
    /// </summary>
    public List<GanttSection> Sections { get; set; } = new();

    /// <summary>
    /// Tasks not in any section (top-level tasks).
    /// </summary>
    public List<GanttTask> Tasks { get; set; } = new();

    /// <summary>
    /// Comments preserved from the original text.
    /// </summary>
    public List<CommentEntry> Comments { get; set; } = new();

    /// <summary>
    /// Lines before the gantt declaration.
    /// </summary>
    public List<string> PreambleLines { get; set; } = new();

    /// <summary>
    /// The line index of the gantt declaration.
    /// </summary>
    public int DeclarationLineIndex { get; set; }
}

/// <summary>
/// Represents a section in a gantt chart that groups tasks.
/// </summary>
public class GanttSection
{
    /// <summary>
    /// The section name/label.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tasks within this section.
    /// </summary>
    public List<GanttTask> Tasks { get; set; } = new();
}

/// <summary>
/// Represents a task in a gantt chart.
/// </summary>
public class GanttTask
{
    /// <summary>
    /// The task display label.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Optional task ID for referencing in dependencies.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Task status tags (e.g., "done", "active", "crit").
    /// Multiple tags can be combined.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Start date or reference (e.g., "2024-01-01", "after task1").
    /// </summary>
    public string? StartDate { get; set; }

    /// <summary>
    /// End date or duration (e.g., "2024-01-15", "30d", "5d").
    /// </summary>
    public string? EndDate { get; set; }

    /// <summary>
    /// Whether this is a milestone (zero-duration marker).
    /// </summary>
    public bool IsMilestone { get; set; }
}

/// <summary>
/// Status tags that can appear on gantt tasks.
/// </summary>
public enum GanttTaskStatus
{
    /// <summary>No special status</summary>
    Normal,

    /// <summary>Task is completed: done</summary>
    Done,

    /// <summary>Task is in progress: active</summary>
    Active,

    /// <summary>Task is critical: crit</summary>
    Critical,

    /// <summary>Task is a milestone: milestone</summary>
    Milestone
}

// =============================================
// Mind Map Models (Phase 4)
// =============================================

/// <summary>
/// Represents a complete Mermaid mind map diagram.
/// A mind map is a tree structure with a central root node.
/// </summary>
public class MindMapModel
{
    /// <summary>
    /// The root node of the mind map.
    /// </summary>
    public MindMapNode Root { get; set; } = new();

    /// <summary>
    /// Comments preserved from the original text.
    /// </summary>
    public List<CommentEntry> Comments { get; set; } = new();

    /// <summary>
    /// Lines before the mindmap declaration.
    /// </summary>
    public List<string> PreambleLines { get; set; } = new();

    /// <summary>
    /// The line index of the mindmap declaration.
    /// </summary>
    public int DeclarationLineIndex { get; set; }

    /// <summary>
    /// When true, position data is stored as a special comment (%% @pos nodeId x,y).
    /// </summary>
    public bool HasPositionData { get; set; }

    /// <summary>
    /// Stored positions keyed by path-based node ID (e.g., "mm_root", "mm_0", "mm_1_2").
    /// These are persisted as %% @pos comments and used to restore visual editor positions.
    /// </summary>
    public Dictionary<string, System.Windows.Point> NodePositions { get; set; } = new();
}

/// <summary>
/// Represents a node in a mind map tree.
/// </summary>
public class MindMapNode
{
    /// <summary>
    /// The text label of this node.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The shape of this node.
    /// </summary>
    public MindMapNodeShape Shape { get; set; } = MindMapNodeShape.Default;

    /// <summary>
    /// Child nodes branching from this node.
    /// </summary>
    public List<MindMapNode> Children { get; set; } = new();

    /// <summary>
    /// Optional icon for this node (Font Awesome class).
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Optional CSS class for this node.
    /// </summary>
    public string? CssClass { get; set; }
}

/// <summary>
/// Node shapes for mind map nodes.
/// </summary>
public enum MindMapNodeShape
{
    /// <summary>Default shape (auto based on level)</summary>
    Default,

    /// <summary>Square: [text]</summary>
    Square,

    /// <summary>Rounded: (text)</summary>
    Rounded,

    /// <summary>Circle: ((text))</summary>
    Circle,

    /// <summary>Bang/explosion: ))text((</summary>
    Bang,

    /// <summary>Cloud: )text(</summary>
    Cloud,

    /// <summary>Hexagon: {{text}}</summary>
    Hexagon
}

// =============================================
// Pie Chart Models (Phase 4)
// =============================================

/// <summary>
/// Represents a complete Mermaid pie chart diagram.
/// Contains slices with labels and values.
/// </summary>
public class PieChartModel
{
    /// <summary>
    /// The chart title (optional).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Whether to show data values on the chart.
    /// </summary>
    public bool ShowData { get; set; }

    /// <summary>
    /// The slices of the pie chart.
    /// </summary>
    public List<PieSlice> Slices { get; set; } = new();

    /// <summary>
    /// Comments preserved from the original text.
    /// </summary>
    public List<CommentEntry> Comments { get; set; } = new();

    /// <summary>
    /// Lines before the pie declaration.
    /// </summary>
    public List<string> PreambleLines { get; set; } = new();

    /// <summary>
    /// The line index of the pie declaration.
    /// </summary>
    public int DeclarationLineIndex { get; set; }
}

/// <summary>
/// Represents a slice in a pie chart.
/// </summary>
public class PieSlice
{
    /// <summary>
    /// The label for this slice (in quotes in Mermaid syntax).
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The numeric value for this slice.
    /// </summary>
    public double Value { get; set; }
}

// =============================================
// Timeline Diagram Models
// =============================================

/// <summary>
/// Represents a Mermaid timeline diagram.
/// Timeline syntax:
///   timeline
///       title My Timeline
///       section Section Name
///       2024 : Event A : Event B
///       2025 : Event C
/// </summary>
public class TimelineModel
{
    /// <summary>
    /// The diagram title (optional).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Sections in the timeline. Events not in any section go into a default section with an empty name.
    /// </summary>
    public List<TimelineSection> Sections { get; set; } = new();

    /// <summary>
    /// Top-level events that appear before any section declaration.
    /// </summary>
    public List<TimelineEvent> Events { get; set; } = new();

    /// <summary>
    /// Comments preserved from the original text.
    /// </summary>
    public List<CommentEntry> Comments { get; set; } = new();

    /// <summary>
    /// Lines before the timeline declaration.
    /// </summary>
    public List<string> PreambleLines { get; set; } = new();

    /// <summary>
    /// The line index of the timeline declaration.
    /// </summary>
    public int DeclarationLineIndex { get; set; }
}

/// <summary>
/// Represents a section in a timeline diagram.
/// </summary>
public class TimelineSection
{
    /// <summary>
    /// The section name/label.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Events within this section.
    /// </summary>
    public List<TimelineEvent> Events { get; set; } = new();
}

/// <summary>
/// Represents a single time period entry in a timeline.
/// Each entry has a time period (date/label) and one or more events that occurred at that time.
/// Mermaid syntax: 2024 : Event A : Event B
/// </summary>
public class TimelineEvent
{
    /// <summary>
    /// The time period label (e.g., "2024", "January", "Phase 1").
    /// </summary>
    public string TimePeriod { get; set; } = string.Empty;

    /// <summary>
    /// The events/descriptions that occurred at this time period.
    /// Multiple events are separated by colons in Mermaid syntax.
    /// </summary>
    public List<string> Events { get; set; } = new();
}
