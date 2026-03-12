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
