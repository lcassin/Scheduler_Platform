using System.Text.RegularExpressions;

namespace MermaidEditor;

/// <summary>
/// Parses Mermaid flowchart text into a structured FlowchartModel.
/// Uses line-by-line regex parsing (Mermaid flowchart syntax is line-oriented).
/// </summary>
public static class MermaidParser
{
    // --- Node shape patterns ---
    // Order matters: more specific patterns must come before less specific ones.
    // Each pattern captures: nodeId, open bracket(s), label text, close bracket(s).

    // Double circle: (((text)))
    private static readonly Regex DoubleCircleNode = new(@"^([a-zA-Z_][\w]*)(\(\(\()(.+?)(\)\)\))\s*$", RegexOptions.Compiled);
    // Circle: ((text))
    private static readonly Regex CircleNode = new(@"^([a-zA-Z_][\w]*)(\(\()(.+?)(\)\))\s*$", RegexOptions.Compiled);
    // Stadium: ([text])
    private static readonly Regex StadiumNode = new(@"^([a-zA-Z_][\w]*)(\(\[)(.+?)(\]\))\s*$", RegexOptions.Compiled);
    // Cylindrical: [(text)]
    private static readonly Regex CylindricalNode = new(@"^([a-zA-Z_][\w]*)(\[\()(.+?)(\)\])\s*$", RegexOptions.Compiled);
    // Subroutine: [[text]]
    private static readonly Regex SubroutineNode = new(@"^([a-zA-Z_][\w]*)(\[\[)(.+?)(\]\])\s*$", RegexOptions.Compiled);
    // Hexagon: {{text}}
    private static readonly Regex HexagonNode = new(@"^([a-zA-Z_][\w]*)(\{\{)(.+?)(\}\})\s*$", RegexOptions.Compiled);
    // Trapezoid: [/text\]
    private static readonly Regex TrapezoidNode = new(@"^([a-zA-Z_][\w]*)(\[/)(.+?)(\\])\s*$", RegexOptions.Compiled);
    // Reverse trapezoid: [\text/]
    private static readonly Regex TrapezoidAltNode = new(@"^([a-zA-Z_][\w]*)(\[\\)(.+?)(/\])\s*$", RegexOptions.Compiled);
    // Parallelogram: [/text/]
    private static readonly Regex ParallelogramNode = new(@"^([a-zA-Z_][\w]*)(\[/)(.+?)(/\])\s*$", RegexOptions.Compiled);
    // Reverse parallelogram: [\text\]
    private static readonly Regex ParallelogramAltNode = new(@"^([a-zA-Z_][\w]*)(\[\\)(.+?)(\\])\s*$", RegexOptions.Compiled);
    // Asymmetric: >text]
    private static readonly Regex AsymmetricNode = new(@"^([a-zA-Z_][\w]*)(>)(.+?)(\])\s*$", RegexOptions.Compiled);
    // Rounded: (text)
    private static readonly Regex RoundedNode = new(@"^([a-zA-Z_][\w]*)(\()(.+?)(\))\s*$", RegexOptions.Compiled);
    // Rhombus/Diamond: {text}
    private static readonly Regex RhombusNode = new(@"^([a-zA-Z_][\w]*)(\{)(.+?)(\})\s*$", RegexOptions.Compiled);
    // Rectangle: [text]
    private static readonly Regex RectangleNode = new(@"^([a-zA-Z_][\w]*)(\[)(.+?)(\])\s*$", RegexOptions.Compiled);

    // --- Edge patterns ---
    // Matches edges like: A --> B, A -->|label| B, A -- text --> B, A -.-> B, A ==> B, etc.
    // Also handles: --o, --x, <-->, o--o, x--x variants
    private static readonly Regex EdgePattern = new(
        @"^([a-zA-Z_][\w]*)\s*" +                          // From node ID
        @"(<?)(-{2,}|-\.+->?|={2,}|~{2,})" +               // Optional left arrow + link start
        @"(?:\|([^|]*)\|)?" +                                // Optional pipe-delimited label |text|
        @"(?:\s+""([^""]*)""\s*)?" +                          // Optional quoted label "text"
        @"(>|o|x)?\s+" +                                     // Arrow end type
        @"([a-zA-Z_][\w]*)" +                                // To node ID
        @"(?:\s*\|\s*([^|]*)\s*\|)?" +                       // Optional trailing label |text|
        @"\s*$",
        RegexOptions.Compiled);

    // Simpler edge pattern for text-on-link style: A -- text --> B
    private static readonly Regex TextEdgePattern = new(
        @"^([a-zA-Z_][\w]*)\s+" +                           // From node ID
        @"(<?)(-{2,}|-\.+-|={2,})\s+" +                      // Link start
        @"(.+?)\s+" +                                         // Link text
        @"(-{2,}>?|-\.+->?|={2,}>?)" +                       // Link end
        @"(>|o|x)?\s+" +                                      // Arrow end type
        @"([a-zA-Z_][\w]*)\s*$",
        RegexOptions.Compiled);

    // --- Direction pattern ---
    private static readonly Regex DirectionPattern = new(@"^\s*direction\s+(TB|TD|BT|RL|LR)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // --- Style patterns ---
    private static readonly Regex StylePattern = new(@"^\s*style\s+(.+?)\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex ClassDefPattern = new(@"^\s*classDef\s+(\S+)\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex ClassAssignPattern = new(@"^\s*class\s+(.+?)\s+(\S+)\s*$", RegexOptions.Compiled);

    // --- Subgraph patterns ---
    private static readonly Regex SubgraphStartPattern = new(@"^\s*subgraph\s+(?:(\S+)\s*\[([^\]]+)\]|(\S+)(?:\s+(.+))?)\s*$", RegexOptions.Compiled);
    private static readonly Regex SubgraphEndPattern = new(@"^\s*end\s*$", RegexOptions.Compiled);

    // --- Flowchart declaration ---
    private static readonly Regex FlowchartDeclaration = new(@"^\s*(flowchart|graph)\s+(TD|TB|BT|RL|LR)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // --- Comment pattern ---
    private static readonly Regex CommentPattern = new(@"^\s*%%(.*)$", RegexOptions.Compiled);

    // --- Position comment pattern: %% @pos nodeId x,y ---
    private static readonly Regex PosCommentPattern = new(@"^\s*%%\s*@pos\s+(\S+)\s+(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*$", RegexOptions.Compiled);

    // --- Node-only pattern (just a node ID on a line by itself, no shape) ---
    private static readonly Regex BareNodePattern = new(@"^\s*([a-zA-Z_][\w]*)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Parses Mermaid flowchart text into a FlowchartModel.
    /// </summary>
    /// <param name="text">The Mermaid flowchart source text.</param>
    /// <returns>A populated FlowchartModel, or null if the text is not a valid flowchart.</returns>
    public static FlowchartModel? ParseFlowchart(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = text.Split('\n');
        var model = new FlowchartModel();
        var knownNodes = new HashSet<string>(StringComparer.Ordinal);
        var subgraphStack = new Stack<FlowchartSubgraph>();
        var pendingPositions = new Dictionary<string, (double x, double y)>(StringComparer.Ordinal);
        bool foundDeclaration = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.TrimEnd('\r');

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Check for @pos position comments first (before general comments)
            var posMatch = PosCommentPattern.Match(line);
            if (posMatch.Success)
            {
                var posNodeId = posMatch.Groups[1].Value;
                var posX = double.Parse(posMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                var posY = double.Parse(posMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                pendingPositions[posNodeId] = (posX, posY);
                // Don't add @pos comments to the Comments list - they are metadata, not user comments
                continue;
            }

            // Check for comments
            var commentMatch = CommentPattern.Match(line);
            if (commentMatch.Success)
            {
                model.Comments.Add(new CommentEntry
                {
                    Text = commentMatch.Groups[1].Value,
                    OriginalLineIndex = i
                });
                continue;
            }

            // Check for flowchart/graph declaration
            if (!foundDeclaration)
            {
                var declMatch = FlowchartDeclaration.Match(line);
                if (declMatch.Success)
                {
                    model.DiagramKeyword = declMatch.Groups[1].Value;
                    model.Direction = declMatch.Groups[2].Value.ToUpperInvariant();
                    // Normalize TB to TD (they are equivalent)
                    if (model.Direction == "TB")
                        model.Direction = "TD";
                    model.DeclarationLineIndex = i;
                    foundDeclaration = true;
                    continue;
                }

                // If we haven't found a declaration yet and this isn't a comment/empty line,
                // preserve it as a preamble line (e.g., config directives, frontmatter)
                if (!line.TrimStart().StartsWith("%%"))
                {
                    model.PreambleLines.Add(line);
                    continue;
                }
            }

            var trimmedLine = line.Trim();

            // Check for direction directive inside subgraph
            var dirMatch = DirectionPattern.Match(trimmedLine);
            if (dirMatch.Success)
            {
                if (subgraphStack.Count > 0)
                {
                    subgraphStack.Peek().Direction = dirMatch.Groups[1].Value.ToUpperInvariant();
                }
                continue;
            }

            // Check for style directive
            var styleMatch = StylePattern.Match(trimmedLine);
            if (styleMatch.Success)
            {
                model.Styles.Add(new StyleDefinition
                {
                    IsClassDef = false,
                    Target = styleMatch.Groups[1].Value.Trim(),
                    StyleString = styleMatch.Groups[2].Value.Trim()
                });
                continue;
            }

            // Check for classDef directive
            var classDefMatch = ClassDefPattern.Match(trimmedLine);
            if (classDefMatch.Success)
            {
                model.Styles.Add(new StyleDefinition
                {
                    IsClassDef = true,
                    Target = classDefMatch.Groups[1].Value.Trim(),
                    StyleString = classDefMatch.Groups[2].Value.Trim()
                });
                continue;
            }

            // Check for class assignment
            var classAssignMatch = ClassAssignPattern.Match(trimmedLine);
            if (classAssignMatch.Success)
            {
                model.ClassAssignments.Add(new ClassAssignment
                {
                    NodeIds = classAssignMatch.Groups[1].Value.Trim(),
                    ClassName = classAssignMatch.Groups[2].Value.Trim()
                });
                continue;
            }

            // Check for subgraph start
            var subgraphMatch = SubgraphStartPattern.Match(trimmedLine);
            if (subgraphMatch.Success)
            {
                string sgId;
                string sgLabel;

                if (subgraphMatch.Groups[1].Success && subgraphMatch.Groups[2].Success)
                {
                    // subgraph id [Label Text]
                    sgId = subgraphMatch.Groups[1].Value;
                    sgLabel = subgraphMatch.Groups[2].Value;
                }
                else
                {
                    // subgraph Title or subgraph id
                    sgId = subgraphMatch.Groups[3].Value;
                    sgLabel = subgraphMatch.Groups[4].Success ? subgraphMatch.Groups[4].Value : sgId;
                }

                var subgraph = new FlowchartSubgraph
                {
                    Id = sgId,
                    Label = sgLabel.Trim()
                };
                subgraphStack.Push(subgraph);
                continue;
            }

            // Check for subgraph end
            if (SubgraphEndPattern.IsMatch(trimmedLine))
            {
                if (subgraphStack.Count > 0)
                {
                    var completedSubgraph = subgraphStack.Pop();
                    model.Subgraphs.Add(completedSubgraph);
                }
                continue;
            }

            // Try to parse as edge (with possible inline node definitions)
            if (TryParseEdgeLine(trimmedLine, model, knownNodes, subgraphStack))
                continue;

            // Try to parse as standalone node definition
            if (TryParseNodeDefinition(trimmedLine, model, knownNodes, subgraphStack))
                continue;

            // Try to parse as bare node reference (just a node ID)
            var bareMatch = BareNodePattern.Match(trimmedLine);
            if (bareMatch.Success)
            {
                var nodeId = bareMatch.Groups[1].Value;
                // Skip Mermaid keywords
                if (!IsMermaidKeyword(nodeId))
                {
                    EnsureNodeExists(nodeId, null, NodeShape.Rectangle, model, knownNodes);
                    AddNodeToCurrentSubgraph(nodeId, subgraphStack);
                }
                continue;
            }
        }

        // If no declaration was found, this isn't a flowchart
        if (!foundDeclaration)
            return null;

        // Apply any pending @pos position data to nodes
        foreach (var kvp in pendingPositions)
        {
            var node = model.Nodes.Find(n => n.Id == kvp.Key);
            if (node != null)
            {
                node.Position = new System.Windows.Point(kvp.Value.x, kvp.Value.y);
                node.HasManualPosition = true;
            }
        }

        return model;
    }

    /// <summary>
    /// Tries to parse a line as an edge with optional inline node definitions.
    /// </summary>
    private static bool TryParseEdgeLine(string line, FlowchartModel model, HashSet<string> knownNodes, Stack<FlowchartSubgraph> subgraphStack)
    {
        // First try to find an edge operator in the line
        // We need to handle chains like A --> B --> C
        var segments = SplitEdgeChain(line);
        if (segments == null || segments.Count < 2)
            return false;

        for (int i = 0; i < segments.Count - 1; i++)
        {
            var fromSegment = segments[i];
            var edgeInfo = segments[i].EdgeAfter;
            var toSegment = segments[i + 1];

            if (edgeInfo == null)
                continue;

            // Ensure both nodes exist
            var fromId = ParseAndEnsureNode(fromSegment.NodeText, model, knownNodes, subgraphStack);
            var toId = ParseAndEnsureNode(toSegment.NodeText, model, knownNodes, subgraphStack);

            if (fromId == null || toId == null)
                return false;

            model.Edges.Add(new FlowchartEdge
            {
                FromNodeId = fromId,
                ToNodeId = toId,
                Label = edgeInfo.Label,
                Style = edgeInfo.Style,
                ArrowType = edgeInfo.ArrowType,
                IsBidirectional = edgeInfo.IsBidirectional,
                LinkLength = edgeInfo.LinkLength
            });
        }

        return true;
    }

    /// <summary>
    /// Splits a line into chain segments connected by edge operators.
    /// Handles chains like: A --> B --> C and A -->|text| B.
    /// </summary>
    private static List<ChainSegment>? SplitEdgeChain(string line)
    {
        var segments = new List<ChainSegment>();
        var remaining = line;

        while (!string.IsNullOrWhiteSpace(remaining))
        {
            remaining = remaining.TrimStart();

            // Try to extract a node definition from the beginning
            var (nodeText, rest) = ExtractNodeText(remaining);
            if (nodeText == null)
                return null;

            var segment = new ChainSegment { NodeText = nodeText };

            remaining = rest.TrimStart();

            // Try to extract an edge operator
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                var edgeInfo = TryExtractEdge(remaining, out var afterEdge);
                if (edgeInfo != null)
                {
                    segment.EdgeAfter = edgeInfo;
                    remaining = afterEdge;
                }
                else if (segments.Count == 0)
                {
                    // No edge found and this is the first segment - not an edge line
                    return null;
                }
                else
                {
                    // Trailing text after chain - ignore
                    segments.Add(segment);
                    break;
                }
            }

            segments.Add(segment);
        }

        return segments.Count >= 2 ? segments : null;
    }

    /// <summary>
    /// Extracts a node definition (with possible shape brackets) from the beginning of the text.
    /// Returns the node text and the remaining string.
    /// </summary>
    private static (string? nodeText, string rest) ExtractNodeText(string text)
    {
        // First, get the node ID
        var idMatch = Regex.Match(text, @"^([a-zA-Z_][\w]*)");
        if (!idMatch.Success)
            return (null, text);

        var nodeId = idMatch.Groups[1].Value;
        var afterId = text[nodeId.Length..];

        // Check if the node has a shape definition following it
        if (afterId.Length > 0)
        {
            char firstChar = afterId[0];
            string? closingBrackets = null;
            int depth;

            switch (firstChar)
            {
                case '[':
                    closingBrackets = FindMatchingBracket(afterId, '[', ']');
                    break;
                case '(':
                    closingBrackets = FindMatchingBracket(afterId, '(', ')');
                    break;
                case '{':
                    closingBrackets = FindMatchingBracket(afterId, '{', '}');
                    break;
                case '>':
                    // Asymmetric shape: >text]
                    depth = afterId.IndexOf(']');
                    if (depth >= 0)
                        closingBrackets = afterId[..(depth + 1)];
                    break;
            }

            if (closingBrackets != null)
            {
                var rest = afterId[closingBrackets.Length..];
                // Consume optional :::className suffix
                if (rest.StartsWith(":::"))
                {
                    var classEnd = rest.IndexOfAny(new[] { ' ', '\t' }, 3);
                    rest = classEnd >= 0 ? rest[classEnd..] : string.Empty;
                }
                return (nodeId + closingBrackets, rest);
            }
        }

        // Just a bare node ID - also handle :::className on bare IDs
        var bareRest = afterId;
        if (bareRest.StartsWith(":::"))
        {
            var classEnd = bareRest.IndexOfAny(new[] { ' ', '\t' }, 3);
            bareRest = classEnd >= 0 ? bareRest[classEnd..] : string.Empty;
        }
        return (nodeId, bareRest);
    }

    /// <summary>
    /// Finds the matching closing bracket, handling nesting.
    /// </summary>
    private static string? FindMatchingBracket(string text, char open, char close)
    {
        if (text.Length == 0 || text[0] != open)
            return null;

        int depth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == open) depth++;
            else if (text[i] == close) depth--;

            if (depth == 0)
                return text[..(i + 1)];
        }

        return null;
    }

    /// <summary>
    /// Tries to extract an edge operator from the text.
    /// Returns edge info and the remaining text after the edge.
    /// </summary>
    private static EdgeInfo? TryExtractEdge(string text, out string remaining)
    {
        remaining = text;

        // Match various edge patterns:
        // Solid: -->, --->, ---->, --- (open), --o, --x
        // Dotted: -.->, -.-, -.->
        // Thick: ==>, ===>, ===
        // Bidirectional: <-->, <-.->. <==>, o--o, x--x
        // With label: -->|text|, -- text -->

        var edgeMatch = Regex.Match(text,
            @"^(<?)(-{2,}|-\.+-|={2,}|~{2,})" +      // optional left arrow + link body
            @"(?:\|([^|]*)\|)?" +                       // optional |label|
            @"(?:(\.)?(-{0,2})(>|o|x)?)" +              // optional dot + dashes + arrow end
            @"(?:\|([^|]*)\|)?" +                        // optional trailing |label|
            @"(?:\s|$)");

        if (!edgeMatch.Success)
        {
            // Try text-on-link pattern: -- text -->
            var textLinkMatch = Regex.Match(text,
                @"^(<?)(-{2,}|-\.+-|={2,})\s+" +        // link start
                @"(.+?)\s+" +                             // link text
                @"(-{2,}|-\.+-|={2,})(>|o|x)?" +         // link end
                @"(?:\s|$)");

            if (textLinkMatch.Success)
            {
                var info = new EdgeInfo();
                info.IsBidirectional = textLinkMatch.Groups[1].Value == "<";
                info.Label = textLinkMatch.Groups[3].Value.Trim();

                var linkStart = textLinkMatch.Groups[2].Value;
                var linkEnd = textLinkMatch.Groups[4].Value;
                var arrowEnd = textLinkMatch.Groups[5].Value;

                ClassifyEdgeStyle(linkStart + linkEnd, arrowEnd, info);
                info.LinkLength = Math.Max(linkStart.Length, linkEnd.Length);

                remaining = text[textLinkMatch.Length..].TrimStart();
                return info;
            }

            return null;
        }

        var edgeInfo = new EdgeInfo();
        edgeInfo.IsBidirectional = edgeMatch.Groups[1].Value == "<";

        // Label can be in group 3 (before arrow) or group 7 (after arrow)
        edgeInfo.Label = edgeMatch.Groups[3].Success && !string.IsNullOrEmpty(edgeMatch.Groups[3].Value)
            ? edgeMatch.Groups[3].Value.Trim()
            : edgeMatch.Groups[7].Success && !string.IsNullOrEmpty(edgeMatch.Groups[7].Value)
                ? edgeMatch.Groups[7].Value.Trim()
                : null;

        var linkBody = edgeMatch.Groups[2].Value;
        var hasDot = edgeMatch.Groups[4].Success && edgeMatch.Groups[4].Value == ".";
        var extraDashes = edgeMatch.Groups[5].Value;
        var arrowEndChar = edgeMatch.Groups[6].Value;

        // Classify the edge style
        var fullLink = linkBody + (hasDot ? "." : "") + extraDashes;
        ClassifyEdgeStyle(fullLink, arrowEndChar, edgeInfo);

        // Calculate link length (number of dashes)
        edgeInfo.LinkLength = CountLinkLength(linkBody);

        remaining = text[edgeMatch.Length..].TrimStart();
        return edgeInfo;
    }

    /// <summary>
    /// Classifies the edge style and arrow type from the link characters.
    /// </summary>
    private static void ClassifyEdgeStyle(string linkBody, string arrowEnd, EdgeInfo info)
    {
        // Determine style
        if (linkBody.Contains('.'))
            info.Style = EdgeStyle.Dotted;
        else if (linkBody.Contains('='))
            info.Style = EdgeStyle.Thick;
        else
            info.Style = EdgeStyle.Solid;

        // Determine arrow type
        info.ArrowType = arrowEnd switch
        {
            ">" => ArrowType.Arrow,
            "o" => ArrowType.Circle,
            "x" => ArrowType.Cross,
            _ => linkBody.EndsWith('>') ? ArrowType.Arrow : ArrowType.Open
        };
    }

    /// <summary>
    /// Counts the effective link length based on the number of repeating characters.
    /// </summary>
    private static int CountLinkLength(string linkBody)
    {
        // Count contiguous dashes or equals
        int count = 0;
        foreach (var c in linkBody)
        {
            if (c == '-' || c == '=')
                count++;
        }
        return Math.Max(count, 2);
    }

    /// <summary>
    /// Parses a node text and ensures it exists in the model.
    /// Returns the node ID.
    /// </summary>
    private static string? ParseAndEnsureNode(string nodeText, FlowchartModel model, HashSet<string> knownNodes, Stack<FlowchartSubgraph> subgraphStack)
    {
        nodeText = nodeText.Trim();

        // Try each shape pattern
        var (id, label, shape) = TryMatchNodeShape(nodeText);

        if (id == null)
        {
            // Try as bare node ID
            var bareMatch = Regex.Match(nodeText, @"^([a-zA-Z_][\w]*)$");
            if (bareMatch.Success && !IsMermaidKeyword(bareMatch.Groups[1].Value))
            {
                id = bareMatch.Groups[1].Value;
                label = null;
                shape = NodeShape.Rectangle;
            }
            else
            {
                return null;
            }
        }

        EnsureNodeExists(id, label, shape, model, knownNodes);
        AddNodeToCurrentSubgraph(id, subgraphStack);
        return id;
    }

    /// <summary>
    /// Tries to match a node text against all shape patterns.
    /// </summary>
    private static (string? id, string? label, NodeShape shape) TryMatchNodeShape(string text)
    {
        // Order: most specific first

        var m = DoubleCircleNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.DoubleCircle);

        m = CircleNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.Circle);

        m = StadiumNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.Stadium);

        m = CylindricalNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.Cylindrical);

        m = SubroutineNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.Subroutine);

        m = HexagonNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.Hexagon);

        // Trapezoid [/text\] must come before Parallelogram [/text/]
        m = TrapezoidNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.Trapezoid);

        // TrapezoidAlt [\text/] must come before ParallelogramAlt [\text\]
        m = TrapezoidAltNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.TrapezoidAlt);

        m = ParallelogramNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.Parallelogram);

        m = ParallelogramAltNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.ParallelogramAlt);

        m = AsymmetricNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.Asymmetric);

        m = RoundedNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.Rounded);

        m = RhombusNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.Rhombus);

        m = RectangleNode.Match(text);
        if (m.Success) return (m.Groups[1].Value, m.Groups[3].Value, NodeShape.Rectangle);

        return (null, null, NodeShape.Rectangle);
    }

    /// <summary>
    /// Ensures a node with the given ID exists in the model.
    /// If the node already exists, updates its label and shape if they are being defined for the first time.
    /// </summary>
    private static void EnsureNodeExists(string id, string? label, NodeShape shape, FlowchartModel model, HashSet<string> knownNodes)
    {
        if (knownNodes.Contains(id))
        {
            // Update label/shape if this is a more specific definition
            if (label != null)
            {
                var existing = model.Nodes.Find(n => n.Id == id);
                if (existing != null && existing.Label == null)
                {
                    existing.Label = label;
                    existing.Shape = shape;
                }
            }
            return;
        }

        knownNodes.Add(id);
        model.Nodes.Add(new FlowchartNode
        {
            Id = id,
            Label = label,
            Shape = shape
        });
    }

    /// <summary>
    /// Adds a node to the current subgraph being parsed, if any.
    /// </summary>
    private static void AddNodeToCurrentSubgraph(string nodeId, Stack<FlowchartSubgraph> subgraphStack)
    {
        if (subgraphStack.Count > 0)
        {
            var current = subgraphStack.Peek();
            if (!current.NodeIds.Contains(nodeId))
            {
                current.NodeIds.Add(nodeId);
            }
        }
    }

    /// <summary>
    /// Checks if a string is a Mermaid keyword that shouldn't be treated as a node ID.
    /// </summary>
    private static bool IsMermaidKeyword(string text)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "flowchart", "graph", "subgraph", "end", "direction",
            "style", "classDef", "class", "click", "callback",
            "linkStyle", "TB", "TD", "BT", "RL", "LR"
        };
        return keywords.Contains(text);
    }

    /// <summary>
    /// Attempts to parse a standalone node definition line.
    /// </summary>
    private static bool TryParseNodeDefinition(string line, FlowchartModel model, HashSet<string> knownNodes, Stack<FlowchartSubgraph> subgraphStack)
    {
        // Strip optional :::className suffix before shape matching
        string? cssClass = null;
        var classIdx = line.IndexOf(":::");
        if (classIdx >= 0)
        {
            cssClass = line[(classIdx + 3)..].Trim();
            line = line[..classIdx];
        }

        var (id, label, shape) = TryMatchNodeShape(line);
        if (id != null && !IsMermaidKeyword(id))
        {
            EnsureNodeExists(id, label, shape, model, knownNodes);
            if (!string.IsNullOrEmpty(cssClass))
            {
                var node = model.Nodes.Find(n => n.Id == id);
                if (node != null) node.CssClass = cssClass;
            }
            AddNodeToCurrentSubgraph(id, subgraphStack);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Internal class to hold edge information during parsing.
    /// </summary>
    private class EdgeInfo
    {
        public string? Label { get; set; }
        public EdgeStyle Style { get; set; } = EdgeStyle.Solid;
        public ArrowType ArrowType { get; set; } = ArrowType.Arrow;
        public bool IsBidirectional { get; set; }
        public int LinkLength { get; set; } = 2;
    }

    /// <summary>
    /// Internal class to hold a segment in an edge chain.
    /// </summary>
    private class ChainSegment
    {
        public string NodeText { get; set; } = string.Empty;
        public EdgeInfo? EdgeAfter { get; set; }
    }
}
