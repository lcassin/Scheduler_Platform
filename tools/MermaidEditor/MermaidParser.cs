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

    // =============================================
    // Sequence Diagram Parser (Phase 2.1)
    // =============================================

    // --- Sequence diagram declaration ---
    private static readonly Regex SequenceDiagramDeclaration = new(@"^\s*sequenceDiagram\s*$", RegexOptions.Compiled);

    // --- Participant/actor declarations ---
    // participant Alice as Alice Label
    // actor Bob as Bob Label
    private static readonly Regex ParticipantDeclaration = new(
        @"^\s*(participant|actor)\s+(\S+?)(?:\s+as\s+(.+))?\s*$", RegexOptions.Compiled);

    // --- Message pattern ---
    // Alice->>Bob: Hello
    // Alice-->>Bob: Response
    // Alice->Bob: Open arrow
    // Alice-->Bob: Dotted open
    // Alice-xBob: Lost
    // Alice--xBob: Dotted lost
    // Alice-)Bob: Async
    // Alice--)Bob: Dotted async
    // Supports +/- suffixes for activate/deactivate
    private static readonly Regex MessagePattern = new(
        @"^\s*(.+?)\s*(->>|-->>|->|-->|-x|--x|-\)|--\))(\+|-)?(.+?):\s*(.*)$", RegexOptions.Compiled);

    // --- Note pattern ---
    // Note right of Alice: text
    // Note left of Bob: text
    // Note over Alice: text
    // Note over Alice,Bob: text
    private static readonly Regex NotePattern = new(
        @"^\s*[Nn]ote\s+(right of|left of|over)\s+(.+?):\s*(.*)$", RegexOptions.Compiled);

    // --- Multi-line note start pattern ---
    // Note right of Alice
    // Note over Alice,Bob
    private static readonly Regex NoteStartPattern = new(
        @"^\s*[Nn]ote\s+(right of|left of|over)\s+(.+?)\s*$", RegexOptions.Compiled);

    // --- Fragment patterns ---
    private static readonly Regex FragmentStartPattern = new(
        @"^\s*(loop|alt|opt|par|critical|break|rect)\s*(.*?)\s*$", RegexOptions.Compiled);
    private static readonly Regex ElsePattern = new(
        @"^\s*(else|and|option)\s*(.*?)\s*$", RegexOptions.Compiled);
    private static readonly Regex EndPattern = new(
        @"^\s*end\s*$", RegexOptions.Compiled);

    // --- Activate/deactivate ---
    private static readonly Regex ActivatePattern = new(
        @"^\s*(activate|deactivate)\s+(\S+)\s*$", RegexOptions.Compiled);

    // --- Autonumber ---
    private static readonly Regex AutonumberPattern = new(
        @"^\s*autonumber\s*$", RegexOptions.Compiled);

    // --- Create/destroy ---
    private static readonly Regex CreatePattern = new(
        @"^\s*create\s+(participant|actor)\s+(\S+?)(?:\s+as\s+(.+))?\s*$", RegexOptions.Compiled);
    private static readonly Regex DestroyPattern = new(
        @"^\s*destroy\s+(\S+)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Parses Mermaid sequence diagram text into a SequenceDiagramModel.
    /// </summary>
    /// <param name="text">The Mermaid sequence diagram source text.</param>
    /// <returns>A populated SequenceDiagramModel, or null if the text is not a valid sequence diagram.</returns>
    public static SequenceDiagramModel? ParseSequenceDiagram(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = text.Split('\n');
        var model = new SequenceDiagramModel();
        var knownParticipants = new HashSet<string>(StringComparer.Ordinal);
        bool foundDeclaration = false;

        // Stack for nested fragment parsing — each entry is the current section's element list
        var fragmentStack = new Stack<(SequenceFragment fragment, int sectionIndex)>();

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.TrimEnd('\r');

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Check for comments (before declaration check)
            var commentMatch = CommentPattern.Match(line);
            if (commentMatch.Success)
            {
                var commentText = commentMatch.Groups[1].Value;
                // Skip @pos comments (flowchart-specific metadata)
                if (!commentText.TrimStart().StartsWith("@pos"))
                {
                    model.Comments.Add(new CommentEntry
                    {
                        Text = commentText,
                        OriginalLineIndex = i
                    });
                }
                continue;
            }

            // Check for sequenceDiagram declaration
            if (!foundDeclaration)
            {
                var declMatch = SequenceDiagramDeclaration.Match(line);
                if (declMatch.Success)
                {
                    model.DeclarationLineIndex = i;
                    foundDeclaration = true;
                    continue;
                }

                // Preserve preamble lines (config directives, etc.)
                if (!line.TrimStart().StartsWith("%%"))
                {
                    model.PreambleLines.Add(line);
                    continue;
                }
            }

            if (!foundDeclaration)
                continue;

            var trimmedLine = line.Trim();

            // Get current element list (top-level or inside a fragment section)
            List<SequenceElement> currentElements = GetCurrentElementList(model, fragmentStack);

            // Check for autonumber
            if (AutonumberPattern.IsMatch(trimmedLine))
            {
                model.AutoNumber = true;
                continue;
            }

            // Check for create participant
            var createMatch = CreatePattern.Match(trimmedLine);
            if (createMatch.Success)
            {
                var type = createMatch.Groups[1].Value == "actor"
                    ? SequenceParticipantType.Actor
                    : SequenceParticipantType.Participant;
                var id = createMatch.Groups[2].Value;
                var alias = createMatch.Groups[3].Success ? createMatch.Groups[3].Value.Trim() : null;

                EnsureSequenceParticipant(model, knownParticipants, id, alias, type, isExplicit: true);

                currentElements.Add(new SequenceCreate
                {
                    ParticipantType = type,
                    ParticipantId = id
                });
                continue;
            }

            // Check for destroy
            var destroyMatch = DestroyPattern.Match(trimmedLine);
            if (destroyMatch.Success)
            {
                var id = destroyMatch.Groups[1].Value;
                currentElements.Add(new SequenceDestroy { ParticipantId = id });

                // Mark participant as destroyed
                var p = model.Participants.Find(p => p.Id == id);
                if (p != null) p.IsDestroyed = true;
                continue;
            }

            // Check for participant/actor declaration
            var participantMatch = ParticipantDeclaration.Match(trimmedLine);
            if (participantMatch.Success)
            {
                var type = participantMatch.Groups[1].Value == "actor"
                    ? SequenceParticipantType.Actor
                    : SequenceParticipantType.Participant;
                var id = participantMatch.Groups[2].Value;
                var alias = participantMatch.Groups[3].Success ? participantMatch.Groups[3].Value.Trim() : null;

                EnsureSequenceParticipant(model, knownParticipants, id, alias, type, isExplicit: true);
                continue;
            }

            // Check for activate/deactivate
            var activateMatch = ActivatePattern.Match(trimmedLine);
            if (activateMatch.Success)
            {
                var isActivate = activateMatch.Groups[1].Value == "activate";
                var participantId = activateMatch.Groups[2].Value;
                EnsureSequenceParticipant(model, knownParticipants, participantId);
                currentElements.Add(new SequenceActivation
                {
                    ParticipantId = participantId,
                    IsActivate = isActivate
                });
                continue;
            }

            // Check for fragment start (loop, alt, opt, par, critical, break, rect)
            var fragmentMatch = FragmentStartPattern.Match(trimmedLine);
            if (fragmentMatch.Success)
            {
                var typeStr = fragmentMatch.Groups[1].Value;
                var label = fragmentMatch.Groups[2].Value.Trim();

                var fragmentType = typeStr switch
                {
                    "loop" => SequenceFragmentType.Loop,
                    "alt" => SequenceFragmentType.Alt,
                    "opt" => SequenceFragmentType.Opt,
                    "par" => SequenceFragmentType.Par,
                    "critical" => SequenceFragmentType.Critical,
                    "break" => SequenceFragmentType.Break,
                    "rect" => SequenceFragmentType.Rect,
                    _ => SequenceFragmentType.Loop
                };

                var fragment = new SequenceFragment
                {
                    Type = fragmentType,
                    Label = label,
                    Sections = { new SequenceFragmentSection { Label = label, Elements = new() } }
                };

                currentElements.Add(fragment);
                fragmentStack.Push((fragment, 0));
                continue;
            }

            // Check for else/and/option (fragment section divider)
            var elseMatch = ElsePattern.Match(trimmedLine);
            if (elseMatch.Success && fragmentStack.Count > 0)
            {
                var label = elseMatch.Groups[2].Value.Trim();
                var (fragment, _) = fragmentStack.Pop();

                var newSection = new SequenceFragmentSection
                {
                    Label = string.IsNullOrEmpty(label) ? null : label,
                    Elements = new()
                };
                fragment.Sections.Add(newSection);
                fragmentStack.Push((fragment, fragment.Sections.Count - 1));
                continue;
            }

            // Check for end (closes fragment)
            if (EndPattern.IsMatch(trimmedLine))
            {
                if (fragmentStack.Count > 0)
                {
                    fragmentStack.Pop();
                }
                continue;
            }

            // Check for note (single-line)
            var noteMatch = NotePattern.Match(trimmedLine);
            if (noteMatch.Success)
            {
                var position = ParseNotePosition(noteMatch.Groups[1].Value);
                var participants = noteMatch.Groups[2].Value.Trim();
                var noteText = noteMatch.Groups[3].Value.Trim();

                // Ensure participants exist
                foreach (var pid in participants.Split(',').Select(p => p.Trim()))
                {
                    EnsureSequenceParticipant(model, knownParticipants, pid);
                }

                currentElements.Add(new SequenceNote
                {
                    Text = noteText,
                    Position = position,
                    OverParticipants = participants
                });
                continue;
            }

            // Check for multi-line note start
            var noteStartMatch = NoteStartPattern.Match(trimmedLine);
            if (noteStartMatch.Success)
            {
                var position = ParseNotePosition(noteStartMatch.Groups[1].Value);
                var participants = noteStartMatch.Groups[2].Value.Trim();

                // Collect lines until "end note"
                var noteLines = new List<string>();
                i++;
                while (i < lines.Length)
                {
                    var noteLine = lines[i].TrimEnd('\r').Trim();
                    if (noteLine.Equals("end note", StringComparison.OrdinalIgnoreCase))
                        break;
                    noteLines.Add(noteLine);
                    i++;
                }

                foreach (var pid in participants.Split(',').Select(p => p.Trim()))
                {
                    EnsureSequenceParticipant(model, knownParticipants, pid);
                }

                currentElements.Add(new SequenceNote
                {
                    Text = string.Join("\n", noteLines),
                    Position = position,
                    OverParticipants = participants
                });
                continue;
            }

            // Check for message
            var messageMatch = MessagePattern.Match(trimmedLine);
            if (messageMatch.Success)
            {
                var fromId = messageMatch.Groups[1].Value.Trim();
                var arrowStr = messageMatch.Groups[2].Value;
                var activationSuffix = messageMatch.Groups[3].Value;
                var toId = messageMatch.Groups[4].Value.Trim();
                var msgText = messageMatch.Groups[5].Value.Trim();

                EnsureSequenceParticipant(model, knownParticipants, fromId);
                EnsureSequenceParticipant(model, knownParticipants, toId);

                var arrowType = arrowStr switch
                {
                    "->>" => SequenceArrowType.SolidArrow,
                    "-->>" => SequenceArrowType.DottedArrow,
                    "->" => SequenceArrowType.SolidOpen,
                    "-->" => SequenceArrowType.DottedOpen,
                    "-x" => SequenceArrowType.SolidCross,
                    "--x" => SequenceArrowType.DottedCross,
                    "-)" => SequenceArrowType.SolidAsync,
                    "--)" => SequenceArrowType.DottedAsync,
                    _ => SequenceArrowType.SolidArrow
                };

                var message = new SequenceMessage
                {
                    FromId = fromId,
                    ToId = toId,
                    Text = msgText,
                    ArrowType = arrowType,
                    ActivateTarget = activationSuffix == "+",
                    DeactivateSource = activationSuffix == "-"
                };

                currentElements.Add(message);
                continue;
            }
        }

        if (!foundDeclaration)
            return null;

        return model;
    }

    /// <summary>
    /// Gets the current element list — either the top-level model elements or the current fragment section's elements.
    /// </summary>
    private static List<SequenceElement> GetCurrentElementList(
        SequenceDiagramModel model,
        Stack<(SequenceFragment fragment, int sectionIndex)> fragmentStack)
    {
        if (fragmentStack.Count > 0)
        {
            var (fragment, sectionIndex) = fragmentStack.Peek();
            return fragment.Sections[sectionIndex].Elements;
        }
        return model.Elements;
    }

    /// <summary>
    /// Ensures a participant exists in the model, creating it if not already known.
    /// </summary>
    private static void EnsureSequenceParticipant(
        SequenceDiagramModel model,
        HashSet<string> knownParticipants,
        string id,
        string? alias = null,
        SequenceParticipantType type = SequenceParticipantType.Participant,
        bool isExplicit = false)
    {
        if (knownParticipants.Contains(id))
        {
            // Update alias/type if explicitly declared later
            if (isExplicit)
            {
                var existing = model.Participants.Find(p => p.Id == id);
                if (existing != null)
                {
                    if (alias != null) existing.Alias = alias;
                    existing.Type = type;
                    existing.IsExplicit = true;
                }
            }
            return;
        }

        knownParticipants.Add(id);
        model.Participants.Add(new SequenceParticipant
        {
            Id = id,
            Alias = alias,
            Type = type,
            IsExplicit = isExplicit
        });
    }

    /// <summary>
    /// Parses a note position string to a SequenceNotePosition enum value.
    /// </summary>
    private static SequenceNotePosition ParseNotePosition(string positionStr)
    {
        return positionStr.ToLowerInvariant() switch
        {
            "right of" => SequenceNotePosition.RightOf,
            "left of" => SequenceNotePosition.LeftOf,
            "over" => SequenceNotePosition.Over,
            _ => SequenceNotePosition.RightOf
        };
    }

    // =============================================
    // Class Diagram Parser (Phase 2.2)
    // =============================================

    // --- Class diagram declaration ---
    private static readonly Regex ClassDiagramDeclaration = new(@"^\s*classDiagram\s*$", RegexOptions.Compiled);

    // --- Class declaration patterns ---
    // class Animal
    // class Animal["Label"]
    // class Animal~Shape~
    // class Animal:::cssClass
    // class Animal <<interface>>
    private static readonly Regex ClassDeclarationPattern = new(
        @"^\s*class\s+([\w-]+)(?:~([^~]+)~)?(?:\[""([^""]+)""\])?\s*(?::::([\w-]+))?\s*(?:<<([^>]+)>>)?\s*(\{)?\s*$",
        RegexOptions.Compiled);

    // --- Annotation on separate line: <<interface>> ClassName ---
    private static readonly Regex SeparateAnnotationPattern = new(
        @"^\s*<<([^>]+)>>\s+([\w-]+)\s*$", RegexOptions.Compiled);

    // --- Member defined via colon syntax: ClassName : +String owner ---
    private static readonly Regex ColonMemberPattern = new(
        @"^\s*([\w-]+)\s*:\s*(.+)$", RegexOptions.Compiled);

    // --- Relationship pattern ---
    // Handles all 8 relationship types with optional cardinality and labels
    // ClassA "1" <|-- "*" ClassB : implements
    // ClassA <|--|> ClassB
    // bar ()-- foo (lollipop)
    // foo --() bar (lollipop)
    private static readonly Regex ClassRelationPattern = new(
        @"^\s*([\w-]+)\s*(?:""([^""]*)""\s*)?" +    // FromId + optional cardinality
        @"(\(?)" +                                     // Optional ( for lollipop left
        @"(<\||\*|o|<|(?:\|>))?" +                     // Optional left end: <|, *, o, <, |>
        @"(\)?\s*)" +                                  // Optional ) for lollipop left
        @"(--|\.\.)" +                                 // Link: -- or ..
        @"(\s*\(?)" +                                  // Optional ( for lollipop right
        @"(\|>|\*|o|>|(?:<\|))?" +                     // Optional right end: |>, *, o, >, <|
        @"(\)?)" +                                     // Optional ) for lollipop right
        @"\s*(?:""([^""]*)""\s*)?" +                   // Optional cardinality
        @"([\w-]+)" +                                  // ToId
        @"(?:\s*:\s*(.+))?\s*$",                       // Optional label
        RegexOptions.Compiled);

    // --- Note patterns ---
    // note "text"
    // note for ClassName "text"
    private static readonly Regex ClassNotePattern = new(
        @"^\s*note\s+(?:for\s+([\w-]+)\s+)?""([^""]+)""\s*$", RegexOptions.Compiled);

    // --- Namespace pattern ---
    private static readonly Regex NamespaceStartPattern = new(
        @"^\s*namespace\s+([\w.-]+)\s*\{\s*$", RegexOptions.Compiled);

    // --- Direction pattern for class diagrams ---
    private static readonly Regex ClassDirectionPattern = new(
        @"^\s*direction\s+(TB|TD|BT|RL|LR)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // --- Style patterns (reuse from flowchart but keep local for clarity) ---
    private static readonly Regex ClassStylePattern = new(@"^\s*style\s+(.+?)\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex ClassClassDefPattern = new(@"^\s*classDef\s+(\S+)\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex CssClassPattern = new(
        @"^\s*cssClass\s+""([^""]+)""\s+(\S+)\s*;?\s*$", RegexOptions.Compiled);

    // --- Link/callback/click patterns (preserve but don't parse deeply) ---
    private static readonly Regex LinkCallbackPattern = new(
        @"^\s*(link|callback|click)\s+", RegexOptions.Compiled);

    /// <summary>
    /// Parses Mermaid class diagram text into a ClassDiagramModel.
    /// </summary>
    /// <param name="text">The Mermaid class diagram source text.</param>
    /// <returns>A populated ClassDiagramModel, or null if the text is not a valid class diagram.</returns>
    public static ClassDiagramModel? ParseClassDiagram(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = text.Split('\n');
        var model = new ClassDiagramModel();
        var knownClasses = new HashSet<string>(StringComparer.Ordinal);
        bool foundDeclaration = false;

        // Track namespace nesting
        var namespaceStack = new Stack<ClassNamespace>();
        // Track class body parsing (class Foo { ... })
        bool inClassBody = false;
        string? currentClassId = null;
        int braceDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.TrimEnd('\r');

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Check for comments (before declaration check)
            var commentMatch = CommentPattern.Match(line);
            if (commentMatch.Success)
            {
                var commentText = commentMatch.Groups[1].Value;
                if (!commentText.TrimStart().StartsWith("@pos"))
                {
                    model.Comments.Add(new CommentEntry
                    {
                        Text = commentText,
                        OriginalLineIndex = i
                    });
                }
                continue;
            }

            // Check for classDiagram declaration
            if (!foundDeclaration)
            {
                var declMatch = ClassDiagramDeclaration.Match(line);
                if (declMatch.Success)
                {
                    model.DeclarationLineIndex = i;
                    foundDeclaration = true;
                    continue;
                }

                // Preserve preamble lines
                if (!line.TrimStart().StartsWith("%%"))
                {
                    model.PreambleLines.Add(line);
                    continue;
                }
            }

            if (!foundDeclaration)
                continue;

            var trimmedLine = line.Trim();

            // If we're inside a class body { ... }, parse members
            if (inClassBody && currentClassId != null)
            {
                // Check for closing brace
                if (trimmedLine == "}" || trimmedLine.EndsWith("}"))
                {
                    braceDepth--;
                    if (braceDepth <= 0)
                    {
                        inClassBody = false;
                        currentClassId = null;
                        braceDepth = 0;
                    }
                    continue;
                }

                // Check for annotation inside class body: <<interface>>
                if (trimmedLine.StartsWith("<<") && trimmedLine.EndsWith(">>"))
                {
                    var annotation = trimmedLine[2..^2].Trim();
                    var cls = model.Classes.Find(c => c.Id == currentClassId);
                    if (cls != null) cls.Annotation = annotation;
                    continue;
                }

                // Parse as a class member
                var classDef = model.Classes.Find(c => c.Id == currentClassId);
                if (classDef != null)
                {
                    classDef.Members.Add(ParseClassMember(trimmedLine));
                }
                continue;
            }

            // Check for direction
            var dirMatch = ClassDirectionPattern.Match(trimmedLine);
            if (dirMatch.Success)
            {
                model.Direction = dirMatch.Groups[1].Value.ToUpperInvariant();
                continue;
            }

            // Check for namespace start
            var nsMatch = NamespaceStartPattern.Match(trimmedLine);
            if (nsMatch.Success)
            {
                var ns = new ClassNamespace { Name = nsMatch.Groups[1].Value };
                namespaceStack.Push(ns);
                continue;
            }

            // Check for closing brace (namespace end or class body end)
            if (trimmedLine == "}")
            {
                if (namespaceStack.Count > 0)
                {
                    var completedNs = namespaceStack.Pop();
                    model.Namespaces.Add(completedNs);
                }
                continue;
            }

            // Check for note
            var noteMatch = ClassNotePattern.Match(trimmedLine);
            if (noteMatch.Success)
            {
                var forClass = noteMatch.Groups[1].Success ? noteMatch.Groups[1].Value : null;
                var noteText = noteMatch.Groups[2].Value;
                model.Notes.Add(new ClassNote { Text = noteText, ForClass = forClass });
                continue;
            }

            // Check for style directive
            var styleMatch = ClassStylePattern.Match(trimmedLine);
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
            var classDefStyleMatch = ClassClassDefPattern.Match(trimmedLine);
            if (classDefStyleMatch.Success)
            {
                model.Styles.Add(new StyleDefinition
                {
                    IsClassDef = true,
                    Target = classDefStyleMatch.Groups[1].Value.Trim(),
                    StyleString = classDefStyleMatch.Groups[2].Value.Trim()
                });
                continue;
            }

            // Check for cssClass assignment
            var cssClassMatch = CssClassPattern.Match(trimmedLine);
            if (cssClassMatch.Success)
            {
                model.CssClassAssignments.Add(new ClassDiagramCssClass
                {
                    NodeIds = cssClassMatch.Groups[1].Value.Trim(),
                    ClassName = cssClassMatch.Groups[2].Value.Trim()
                });
                continue;
            }

            // Check for link/callback/click (preserve as-is, skip parsing)
            if (LinkCallbackPattern.IsMatch(trimmedLine))
                continue;

            // Check for separate annotation: <<interface>> ClassName
            var annotationMatch = SeparateAnnotationPattern.Match(trimmedLine);
            if (annotationMatch.Success)
            {
                var annotation = annotationMatch.Groups[1].Value.Trim();
                var classId = annotationMatch.Groups[2].Value;
                EnsureClassExists(model, knownClasses, classId, namespaceStack);
                var cls = model.Classes.Find(c => c.Id == classId);
                if (cls != null) cls.Annotation = annotation;
                continue;
            }

            // Check for class declaration: class ClassName { ... } or class ClassName
            var classMatch = ClassDeclarationPattern.Match(trimmedLine);
            if (classMatch.Success)
            {
                var classId = classMatch.Groups[1].Value;
                var genericType = classMatch.Groups[2].Success ? classMatch.Groups[2].Value : null;
                var label = classMatch.Groups[3].Success ? classMatch.Groups[3].Value : null;
                var cssClass = classMatch.Groups[4].Success ? classMatch.Groups[4].Value : null;
                var annotation = classMatch.Groups[5].Success ? classMatch.Groups[5].Value.Trim() : null;
                var hasOpenBrace = classMatch.Groups[6].Success;

                EnsureClassExists(model, knownClasses, classId, namespaceStack);
                var cls = model.Classes.Find(c => c.Id == classId);
                if (cls != null)
                {
                    cls.IsExplicit = true;
                    if (genericType != null) cls.GenericType = genericType;
                    if (label != null) cls.Label = label;
                    if (cssClass != null) cls.CssClass = cssClass;
                    if (annotation != null) cls.Annotation = annotation;
                }

                if (hasOpenBrace)
                {
                    inClassBody = true;
                    currentClassId = classId;
                    braceDepth = 1;
                }
                continue;
            }

            // Check for colon member syntax: ClassName : +memberDef
            var colonMatch = ColonMemberPattern.Match(trimmedLine);
            if (colonMatch.Success)
            {
                var classId = colonMatch.Groups[1].Value;
                var memberText = colonMatch.Groups[2].Value.Trim();

                // Make sure this isn't actually a relationship by checking for relationship operators
                if (!ContainsRelationshipOperator(memberText))
                {
                    EnsureClassExists(model, knownClasses, classId, namespaceStack);
                    var cls = model.Classes.Find(c => c.Id == classId);
                    if (cls != null)
                    {
                        cls.Members.Add(ParseClassMember(memberText));
                    }
                    continue;
                }
            }

            // Check for relationship
            if (TryParseClassRelationship(trimmedLine, model, knownClasses, namespaceStack))
                continue;
        }

        if (!foundDeclaration)
            return null;

        return model;
    }

    /// <summary>
    /// Checks if a member text contains a relationship operator (to distinguish from colon member syntax).
    /// </summary>
    private static bool ContainsRelationshipOperator(string text)
    {
        // Check for relationship operators within the text
        return text.Contains("<|") || text.Contains("|>") ||
               text.Contains("*--") || text.Contains("--*") ||
               text.Contains("o--") || text.Contains("--o") ||
               text.Contains("-->") || text.Contains("<--") ||
               text.Contains("..>") || text.Contains("<..") ||
               text.Contains("..|>") || text.Contains("<|..");
    }

    /// <summary>
    /// Attempts to parse a line as a class diagram relationship.
    /// </summary>
    private static bool TryParseClassRelationship(string line, ClassDiagramModel model,
        HashSet<string> knownClasses, Stack<ClassNamespace> namespaceStack)
    {
        // Try the general relationship regex
        var match = ClassRelationPattern.Match(line);
        if (!match.Success)
            return false;

        var fromId = match.Groups[1].Value;
        var fromCardinality = match.Groups[2].Success && match.Groups[2].Value.Length > 0
            ? match.Groups[2].Value : null;

        var leftParen = match.Groups[3].Value;
        var leftEndStr = match.Groups[4].Success ? match.Groups[4].Value : "";
        var leftParenClose = match.Groups[5].Value.Trim();

        var linkStr = match.Groups[6].Value; // -- or ..

        var rightParenOpen = match.Groups[7].Value.Trim();
        var rightEndStr = match.Groups[8].Success ? match.Groups[8].Value : "";
        var rightParen = match.Groups[9].Value;

        var toCardinality = match.Groups[10].Success && match.Groups[10].Value.Length > 0
            ? match.Groups[10].Value : null;
        var toId = match.Groups[11].Value;
        var label = match.Groups[12].Success ? match.Groups[12].Value.Trim() : null;

        // Determine link style
        var linkStyle = linkStr == ".." ? ClassLinkStyle.Dashed : ClassLinkStyle.Solid;

        // Parse left end
        var leftEnd = ParseRelationEnd(leftEndStr, leftParen.Contains("(") && leftParenClose.Contains(")"));

        // Parse right end
        var rightEnd = ParseRelationEnd(rightEndStr, rightParenOpen.Contains("(") && rightParen.Contains(")"));

        // Ensure classes exist
        EnsureClassExists(model, knownClasses, fromId, namespaceStack);
        EnsureClassExists(model, knownClasses, toId, namespaceStack);

        model.Relationships.Add(new ClassRelationship
        {
            FromId = fromId,
            ToId = toId,
            LeftEnd = leftEnd,
            RightEnd = rightEnd,
            LinkStyle = linkStyle,
            Label = string.IsNullOrEmpty(label) ? null : label,
            FromCardinality = fromCardinality,
            ToCardinality = toCardinality
        });

        return true;
    }

    /// <summary>
    /// Parses a relationship end marker string into a ClassRelationEnd enum value.
    /// </summary>
    private static ClassRelationEnd ParseRelationEnd(string endStr, bool isLollipop)
    {
        if (isLollipop) return ClassRelationEnd.Lollipop;

        return endStr switch
        {
            "<|" or "|>" => ClassRelationEnd.Inheritance,
            "*" => ClassRelationEnd.Composition,
            "o" => ClassRelationEnd.Aggregation,
            "<" or ">" => ClassRelationEnd.Arrow,
            _ => ClassRelationEnd.None
        };
    }

    /// <summary>
    /// Parses a class member text into a ClassMember object.
    /// Handles visibility (+, -, #, ~), method detection (parentheses), return types, and classifiers (* $).
    /// </summary>
    private static ClassMember ParseClassMember(string text)
    {
        var member = new ClassMember { RawText = text };
        var remaining = text.Trim();

        // Parse visibility prefix
        if (remaining.Length > 0)
        {
            switch (remaining[0])
            {
                case '+':
                    member.Visibility = MemberVisibility.Public;
                    remaining = remaining[1..];
                    break;
                case '-':
                    member.Visibility = MemberVisibility.Private;
                    remaining = remaining[1..];
                    break;
                case '#':
                    member.Visibility = MemberVisibility.Protected;
                    remaining = remaining[1..];
                    break;
                case '~':
                    member.Visibility = MemberVisibility.Package;
                    remaining = remaining[1..];
                    break;
            }
        }

        // Check for classifier suffix (* or $) at the very end
        if (remaining.EndsWith("*"))
        {
            member.Classifier = MemberClassifier.Abstract;
            remaining = remaining[..^1];
        }
        else if (remaining.EndsWith("$"))
        {
            member.Classifier = MemberClassifier.Static;
            remaining = remaining[..^1];
        }

        // Check if it's a method (contains parentheses)
        var parenOpen = remaining.IndexOf('(');
        if (parenOpen >= 0)
        {
            member.IsMethod = true;
            var parenClose = remaining.LastIndexOf(')');
            if (parenClose > parenOpen)
            {
                member.Parameters = remaining[(parenOpen + 1)..parenClose];
                // Method name is everything before the (
                var beforeParen = remaining[..parenOpen].Trim();
                member.Name = beforeParen;

                // Return type is everything after the closing )
                var afterParen = remaining[(parenClose + 1)..].Trim();
                if (!string.IsNullOrEmpty(afterParen))
                {
                    member.Type = afterParen;
                }
            }
            else
            {
                // Malformed - treat whole thing as name
                member.Name = remaining;
            }
        }
        else
        {
            // It's a field — could be "Type name" or just "name"
            member.IsMethod = false;
            // Split by space to separate type from name
            // Mermaid uses "Type name" format (e.g., "+String owner", "+int age")
            // Also handle "name : type" format
            var colonIdx = remaining.IndexOf(':');
            if (colonIdx >= 0)
            {
                // "name : type" format (e.g., "-idCard : IdCard")
                member.Name = remaining[..colonIdx].Trim();
                member.Type = remaining[(colonIdx + 1)..].Trim();
            }
            else
            {
                // "Type name" format or just "name"
                var parts = remaining.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    // Check if first part looks like a type (contains generic ~~ or starts with uppercase or is a known type)
                    member.Type = parts[0];
                    member.Name = parts[1];
                }
                else if (parts.Length == 1)
                {
                    member.Name = parts[0];
                }
            }
        }

        return member;
    }

    /// <summary>
    /// Ensures a class exists in the model, creating it if not already known.
    /// </summary>
    private static void EnsureClassExists(ClassDiagramModel model, HashSet<string> knownClasses,
        string classId, Stack<ClassNamespace> namespaceStack)
    {
        if (knownClasses.Contains(classId))
            return;

        knownClasses.Add(classId);
        model.Classes.Add(new ClassDefinition { Id = classId });

        // If we're inside a namespace, add the class to it
        if (namespaceStack.Count > 0)
        {
            namespaceStack.Peek().ClassIds.Add(classId);
        }
    }
}
