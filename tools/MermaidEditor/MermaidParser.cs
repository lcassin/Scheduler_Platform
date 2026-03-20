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

    // --- Position comment pattern: %% @pos nodeId x,y[,w,h] ---
    // Groups: 1=nodeId, 2=x, 3=y, 4=optional ",w,h" suffix, 5=w, 6=h
    private static readonly Regex PosCommentPattern = new(@"^\s*%%\s*@pos\s+(\S+)\s+(-?[\d.]+)\s*,\s*(-?[\d.]+)(\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+))?\s*$", RegexOptions.Compiled);

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
        // Strip surrounding double quotes from labels (used to escape special characters)
        // e.g. "New Node (copy)" → New Node (copy)
        if (label != null && label.Length >= 2 && label.StartsWith('"') && label.EndsWith('"'))
        {
            label = label[1..^1];
        }

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
        var pendingPositions = new Dictionary<string, (double x, double y)>(StringComparer.Ordinal);
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

            // Check for @pos position comments first (before general comments)
            var posMatch = PosCommentPattern.Match(line);
            if (posMatch.Success)
            {
                var posId = posMatch.Groups[1].Value;
                var posX = double.Parse(posMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                var posY = double.Parse(posMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                pendingPositions[posId] = (posX, posY);
                continue;
            }

            // Check for comments (before declaration check)
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

        // Apply any pending @pos position data to classes
        foreach (var (posId, (px, py)) in pendingPositions)
        {
            var cls = model.Classes.Find(c => c.Id == posId);
            if (cls != null)
            {
                cls.Position = new System.Windows.Point(px, py);
                cls.HasManualPosition = true;
            }
        }

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

    // =============================================
    // State Diagram Parser (Phase 2.3)
    // =============================================

    // --- State diagram declaration ---
    // stateDiagram-v2 or stateDiagram
    private static readonly Regex StateDiagramDeclaration = new(
        @"^\s*stateDiagram(-v2)?\s*$", RegexOptions.Compiled);

    // --- State declaration with label ---
    // state "Label" as s1
    private static readonly Regex StateAsPattern = new(
        @"^\s*state\s+""([^""]+)""\s+as\s+([\w-]+)\s*$", RegexOptions.Compiled);

    // --- State with colon label ---
    // s1 : Label text
    private static readonly Regex StateColonLabelPattern = new(
        @"^\s*([\w-]+)\s*:\s*(.+)$", RegexOptions.Compiled);

    // --- Composite state start ---
    // state StateName {
    // state "Label" as StateName {
    private static readonly Regex CompositeStatePattern = new(
        @"^\s*state\s+(?:""([^""]+)""\s+as\s+)?([\w-]+)\s*\{\s*$", RegexOptions.Compiled);

    // --- Special state types ---
    // state fork_state <<fork>>
    // state join_state <<join>>
    // state choice_state <<choice>>
    private static readonly Regex SpecialStatePattern = new(
        @"^\s*state\s+([\w-]+)\s+<<(fork|join|choice)>>\s*$", RegexOptions.Compiled);

    // --- Transition pattern ---
    // [*] --> s1
    // s1 --> s2 : label
    // s1 --> [*]
    private static readonly Regex StateTransitionPattern = new(
        @"^\s*(\[\*\]|[\w-]+)\s*-->\s*(\[\*\]|[\w-]+)(?:\s*:\s*(.+))?\s*$", RegexOptions.Compiled);

    // --- Note patterns for state diagrams ---
    // note right of s1 : single line text
    // note right of s1
    //   multi-line text
    // end note
    private static readonly Regex StateNoteInlinePattern = new(
        @"^\s*note\s+(right|left)\s+of\s+([\w-]+)\s*:\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex StateNoteStartPattern = new(
        @"^\s*note\s+(right|left)\s+of\s+([\w-]+)\s*$", RegexOptions.Compiled);
    private static readonly Regex StateNoteEndPattern = new(
        @"^\s*end\s+note\s*$", RegexOptions.Compiled);

    // --- Direction pattern for state diagrams ---
    private static readonly Regex StateDirectionPattern = new(
        @"^\s*direction\s+(TB|TD|BT|RL|LR)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // --- Style patterns for state diagrams ---
    private static readonly Regex StateStylePattern = new(@"^\s*style\s+(.+?)\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex StateClassDefPattern = new(@"^\s*classDef\s+(\S+)\s+(.+)$", RegexOptions.Compiled);

    // --- Close brace for composite states ---
    private static readonly Regex CloseBracePattern = new(@"^\s*\}\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Parses Mermaid state diagram text into a StateDiagramModel.
    /// </summary>
    /// <param name="text">The Mermaid state diagram source text.</param>
    /// <returns>A populated StateDiagramModel, or null if the text is not a valid state diagram.</returns>
    public static StateDiagramModel? ParseStateDiagram(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = text.Split('\n');
        var model = new StateDiagramModel();
        var knownStates = new HashSet<string>(StringComparer.Ordinal);
        var pendingPositions = new Dictionary<string, (double x, double y, double w, double h)>(StringComparer.Ordinal);
        bool foundDeclaration = false;

        // Stack for nested composite state parsing
        var compositeStack = new Stack<StateDefinition>();

        // Multi-line note tracking
        bool inMultiLineNote = false;
        string noteStateId = string.Empty;
        StateNotePosition notePosition = StateNotePosition.RightOf;
        var noteTextLines = new List<string>();

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
                var posId = posMatch.Groups[1].Value;
                var posX = double.Parse(posMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                var posY = double.Parse(posMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                double posW = 0, posH = 0;
                if (posMatch.Groups[5].Success && posMatch.Groups[6].Success)
                {
                    posW = double.Parse(posMatch.Groups[5].Value, System.Globalization.CultureInfo.InvariantCulture);
                    posH = double.Parse(posMatch.Groups[6].Value, System.Globalization.CultureInfo.InvariantCulture);
                }
                pendingPositions[posId] = (posX, posY, posW, posH);
                continue;
            }

            // Check for comments (before declaration check)
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

            // Handle multi-line note content
            if (inMultiLineNote)
            {
                var endNoteMatch = StateNoteEndPattern.Match(line);
                if (endNoteMatch.Success)
                {
                    // Finish the multi-line note
                    var note = new StateNote
                    {
                        StateId = noteStateId,
                        Position = notePosition,
                        Text = string.Join("\n", noteTextLines)
                    };
                    if (compositeStack.Count > 0)
                    {
                        // Note inside a composite state — not directly supported in model,
                        // add to top-level notes with state context
                        model.Notes.Add(note);
                    }
                    else
                    {
                        model.Notes.Add(note);
                    }
                    inMultiLineNote = false;
                    noteTextLines.Clear();
                    continue;
                }
                noteTextLines.Add(line.Trim());
                continue;
            }

            // Look for the stateDiagram declaration
            if (!foundDeclaration)
            {
                // Preamble lines (frontmatter, config directives, etc.)
                var declMatch = StateDiagramDeclaration.Match(line);
                if (declMatch.Success)
                {
                    foundDeclaration = true;
                    model.DeclarationLineIndex = i;
                    model.IsV2 = declMatch.Groups[1].Success; // -v2 suffix present
                    continue;
                }

                // Store as preamble
                model.PreambleLines.Add(line);
                continue;
            }

            // --- After declaration: parse state diagram content ---

            // Direction
            var dirMatch = StateDirectionPattern.Match(line);
            if (dirMatch.Success)
            {
                if (compositeStack.Count == 0)
                {
                    model.Direction = dirMatch.Groups[1].Value.ToUpperInvariant();
                }
                continue;
            }

            // Close brace (end of composite state)
            var closeBraceMatch = CloseBracePattern.Match(line);
            if (closeBraceMatch.Success)
            {
                if (compositeStack.Count > 0)
                {
                    compositeStack.Pop();
                }
                continue;
            }

            // Special state types: <<fork>>, <<join>>, <<choice>>
            var specialMatch = SpecialStatePattern.Match(line);
            if (specialMatch.Success)
            {
                var stateId = specialMatch.Groups[1].Value;
                var typeStr = specialMatch.Groups[2].Value.ToLowerInvariant();
                var stateType = typeStr switch
                {
                    "fork" => StateType.Fork,
                    "join" => StateType.Join,
                    "choice" => StateType.Choice,
                    _ => StateType.Simple
                };

                var stateDef = EnsureStateExists(model, knownStates, stateId, compositeStack);
                stateDef.Type = stateType;
                stateDef.IsExplicit = true;
                continue;
            }

            // Composite state start: state StateName {
            var compositeMatch = CompositeStatePattern.Match(line);
            if (compositeMatch.Success)
            {
                var label = compositeMatch.Groups[1].Success ? compositeMatch.Groups[1].Value : null;
                var stateId = compositeMatch.Groups[2].Value;

                var stateDef = EnsureStateExists(model, knownStates, stateId, compositeStack);
                stateDef.Type = StateType.Composite;
                stateDef.IsExplicit = true;
                if (label != null)
                {
                    stateDef.Label = label;
                }

                compositeStack.Push(stateDef);
                continue;
            }

            // State with "as" label: state "Label" as s1
            var asMatch = StateAsPattern.Match(line);
            if (asMatch.Success)
            {
                var label = asMatch.Groups[1].Value;
                var stateId = asMatch.Groups[2].Value;

                var stateDef = EnsureStateExists(model, knownStates, stateId, compositeStack);
                stateDef.Label = label;
                stateDef.IsExplicit = true;
                continue;
            }

            // Inline note: note right of s1 : text
            var noteInlineMatch = StateNoteInlinePattern.Match(line);
            if (noteInlineMatch.Success)
            {
                var posStr = noteInlineMatch.Groups[1].Value.ToLowerInvariant();
                var stateId = noteInlineMatch.Groups[2].Value;
                var noteText = noteInlineMatch.Groups[3].Value.Trim();
                var pos = posStr == "left" ? StateNotePosition.LeftOf : StateNotePosition.RightOf;

                model.Notes.Add(new StateNote
                {
                    StateId = stateId,
                    Position = pos,
                    Text = noteText
                });

                // Ensure state exists
                EnsureStateExists(model, knownStates, stateId, compositeStack);
                continue;
            }

            // Multi-line note start: note right of s1
            var noteStartMatch = StateNoteStartPattern.Match(line);
            if (noteStartMatch.Success)
            {
                var posStr = noteStartMatch.Groups[1].Value.ToLowerInvariant();
                noteStateId = noteStartMatch.Groups[2].Value;
                notePosition = posStr == "left" ? StateNotePosition.LeftOf : StateNotePosition.RightOf;
                inMultiLineNote = true;
                noteTextLines.Clear();

                // Ensure state exists
                EnsureStateExists(model, knownStates, noteStateId, compositeStack);
                continue;
            }

            // Style: classDef
            var classDefMatch = StateClassDefPattern.Match(line);
            if (classDefMatch.Success)
            {
                model.Styles.Add(new StyleDefinition
                {
                    Target = classDefMatch.Groups[1].Value,
                    StyleString = classDefMatch.Groups[2].Value,
                    IsClassDef = true
                });
                continue;
            }

            // Style: style
            var styleMatch = StateStylePattern.Match(line);
            if (styleMatch.Success)
            {
                model.Styles.Add(new StyleDefinition
                {
                    Target = styleMatch.Groups[1].Value,
                    StyleString = styleMatch.Groups[2].Value,
                    IsClassDef = false
                });
                continue;
            }

            // Transition: s1 --> s2 : label
            var transMatch = StateTransitionPattern.Match(line);
            if (transMatch.Success)
            {
                var fromId = transMatch.Groups[1].Value;
                var toId = transMatch.Groups[2].Value;
                var label = transMatch.Groups[3].Success ? transMatch.Groups[3].Value.Trim() : null;

                var transition = new StateTransition
                {
                    FromId = fromId,
                    ToId = toId,
                    Label = label
                };

                if (compositeStack.Count > 0)
                {
                    compositeStack.Peek().NestedTransitions.Add(transition);
                }
                else
                {
                    model.Transitions.Add(transition);
                }

                // Ensure states exist (unless [*])
                if (fromId != "[*]")
                    EnsureStateExists(model, knownStates, fromId, compositeStack);
                if (toId != "[*]")
                    EnsureStateExists(model, knownStates, toId, compositeStack);

                continue;
            }

            // State with colon label: s1 : Label text
            var colonMatch = StateColonLabelPattern.Match(line);
            if (colonMatch.Success)
            {
                var stateId = colonMatch.Groups[1].Value;
                var label = colonMatch.Groups[2].Value.Trim();

                // Skip keywords that look like state:label
                if (stateId == "state" || stateId == "note" || stateId == "direction" ||
                    stateId == "classDef" || stateId == "style")
                    continue;

                var stateDef = EnsureStateExists(model, knownStates, stateId, compositeStack);
                stateDef.Label = label;
                stateDef.IsExplicit = true;
                continue;
            }
        }

        // Apply any pending @pos position data to states
        if (foundDeclaration)
        {
            foreach (var (posId, (px, py, pw, ph)) in pendingPositions)
            {
                // Pseudo-node positions ([*]_start, [*]_end, etc.) and note positions (note_0, note_1, etc.)
                // go into separate dictionaries since they don't have StateDefinition objects.
                if (posId.StartsWith("[*]_"))
                {
                    model.PseudoNodePositions[posId] = new System.Windows.Point(px, py);
                    continue;
                }
                if (posId.StartsWith("note_"))
                {
                    model.NotePositions[posId] = new System.Windows.Point(px, py);
                    continue;
                }

                // Search all states recursively
                StateDefinition? target = null;
                foreach (var s in model.States)
                {
                    if (s.Id == posId) { target = s; break; }
                    target = FindStateRecursive(s, posId);
                    if (target != null) break;
                }
                if (target != null)
                {
                    target.Position = new System.Windows.Point(px, py);
                    if (pw > 0 && ph > 0)
                        target.Size = new System.Windows.Size(pw, ph);
                    target.HasManualPosition = true;
                }
            }
        }

        return foundDeclaration ? model : null;
    }

    /// <summary>
    /// Ensures a state exists in the model (or in a composite parent), creating it if not already known.
    /// Returns the existing or newly created state definition.
    /// </summary>
    private static StateDefinition EnsureStateExists(StateDiagramModel model, HashSet<string> knownStates,
        string stateId, Stack<StateDefinition> compositeStack)
    {
        // Check if state already exists
        if (knownStates.Contains(stateId))
        {
            // Find and return existing state
            if (compositeStack.Count > 0)
            {
                var parent = compositeStack.Peek();
                var existing = parent.NestedStates.Find(s => s.Id == stateId);
                if (existing != null) return existing;
            }
            var topLevel = model.States.Find(s => s.Id == stateId);
            if (topLevel != null) return topLevel;

            // Search nested states recursively
            foreach (var state in model.States)
            {
                var found = FindStateRecursive(state, stateId);
                if (found != null) return found;
            }

            // Shouldn't happen, but create a new one as fallback
            var fallback = new StateDefinition { Id = stateId };
            model.States.Add(fallback);
            return fallback;
        }

        knownStates.Add(stateId);
        var newState = new StateDefinition { Id = stateId };

        if (compositeStack.Count > 0)
        {
            compositeStack.Peek().NestedStates.Add(newState);
        }
        else
        {
            model.States.Add(newState);
        }

        return newState;
    }

    /// <summary>
    /// Recursively finds a state by ID within nested states.
    /// </summary>
    private static StateDefinition? FindStateRecursive(StateDefinition parent, string stateId)
    {
        foreach (var nested in parent.NestedStates)
        {
            if (nested.Id == stateId) return nested;
            var found = FindStateRecursive(nested, stateId);
            if (found != null) return found;
        }
        return null;
    }

    // =============================================
    // ER Diagram Parser (Phase 2.4)
    // =============================================

    // --- ER diagram declaration ---
    private static readonly Regex ERDiagramDeclaration = new(
        @"^\s*erDiagram\s*$", RegexOptions.Compiled);

    // --- ER relationship pattern ---
    // CUSTOMER ||--o{ ORDER : places
    // CUSTOMER }|..|{ DELIVERY-ADDRESS : uses
    // Left entity, left cardinality, link style, right cardinality, right entity, label
    private static readonly Regex ERRelationshipPattern = new(
        @"^\s*([\w-]+)\s+(\|\||[|}][|o]|[|o][|{]|\|\{|\{[|o]|[|}]\|)\s*(--|\.\.)\s*(\|\||[|}][|o]|[|o][|{]|\|\{|\{[|o]|[|}]\|)\s*([\w-]+)\s*:\s*(.+?)\s*$",
        RegexOptions.Compiled);

    // --- ER entity block start ---
    // CUSTOMER {
    private static readonly Regex EREntityBlockStart = new(
        @"^\s*([\w-]+)\s*\{\s*$", RegexOptions.Compiled);

    // --- ER entity block end ---
    private static readonly Regex EREntityBlockEnd = new(
        @"^\s*\}\s*$", RegexOptions.Compiled);

    // --- ER attribute line ---
    // string name PK "The customer name"
    // int age
    // date created FK
    private static readonly Regex ERAttributePattern = new(
        @"^\s*(\S+)\s+(\S+)(?:\s+(PK|FK|UK))?(?:\s+""([^""]+)"")?\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Parses Mermaid ER diagram text into an ERDiagramModel.
    /// </summary>
    /// <param name="text">The Mermaid ER diagram source text.</param>
    /// <returns>A populated ERDiagramModel, or null if the text is not a valid ER diagram.</returns>
    public static ERDiagramModel? ParseERDiagram(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = text.Split('\n');
        var model = new ERDiagramModel();
        var knownEntities = new HashSet<string>(StringComparer.Ordinal);
        var pendingPositions = new Dictionary<string, (double x, double y)>(StringComparer.Ordinal);
        bool foundDeclaration = false;

        // Track entity body parsing
        bool inEntityBody = false;
        string? currentEntityName = null;

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
                var posId = posMatch.Groups[1].Value;
                var posX = double.Parse(posMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                var posY = double.Parse(posMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                pendingPositions[posId] = (posX, posY);
                continue;
            }

            // Check for comments (before declaration check)
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

            // Handle entity body content
            if (inEntityBody)
            {
                var endMatch = EREntityBlockEnd.Match(line);
                if (endMatch.Success)
                {
                    inEntityBody = false;
                    currentEntityName = null;
                    continue;
                }

                // Parse attribute line
                var attrMatch = ERAttributePattern.Match(line);
                if (attrMatch.Success && currentEntityName != null)
                {
                    var entity = model.Entities.Find(e => e.Name == currentEntityName);
                    if (entity != null)
                    {
                        entity.Attributes.Add(new ERAttribute
                        {
                            Type = attrMatch.Groups[1].Value,
                            Name = attrMatch.Groups[2].Value,
                            Key = attrMatch.Groups[3].Success ? attrMatch.Groups[3].Value : null,
                            Comment = attrMatch.Groups[4].Success ? attrMatch.Groups[4].Value : null
                        });
                    }
                }
                continue;
            }

            // Look for the erDiagram declaration
            if (!foundDeclaration)
            {
                var declMatch = ERDiagramDeclaration.Match(line);
                if (declMatch.Success)
                {
                    foundDeclaration = true;
                    model.DeclarationLineIndex = i;
                    continue;
                }

                // Store as preamble
                model.PreambleLines.Add(line);
                continue;
            }

            // --- After declaration: parse ER diagram content ---

            // Entity block start: ENTITY_NAME {
            var entityBlockMatch = EREntityBlockStart.Match(line);
            if (entityBlockMatch.Success)
            {
                var entityName = entityBlockMatch.Groups[1].Value;
                EnsureEREntityExists(model, knownEntities, entityName);
                var entity = model.Entities.Find(e => e.Name == entityName);
                if (entity != null)
                {
                    entity.IsExplicit = true;
                }
                inEntityBody = true;
                currentEntityName = entityName;
                continue;
            }

            // Relationship: ENTITY1 ||--o{ ENTITY2 : label
            var relMatch = ERRelationshipPattern.Match(line);
            if (relMatch.Success)
            {
                var fromEntity = relMatch.Groups[1].Value;
                var leftCardStr = relMatch.Groups[2].Value;
                var linkStyle = relMatch.Groups[3].Value;
                var rightCardStr = relMatch.Groups[4].Value;
                var toEntity = relMatch.Groups[5].Value;
                var label = relMatch.Groups[6].Value.Trim();

                model.Relationships.Add(new ERRelationship
                {
                    FromEntity = fromEntity,
                    ToEntity = toEntity,
                    LeftCardinality = ParseERCardinality(leftCardStr),
                    RightCardinality = ParseERCardinality(rightCardStr),
                    IsIdentifying = linkStyle == "--",
                    Label = label
                });

                // Ensure entities exist
                EnsureEREntityExists(model, knownEntities, fromEntity);
                EnsureEREntityExists(model, knownEntities, toEntity);
                continue;
            }
        }

        // Apply any pending @pos position data to entities
        if (foundDeclaration)
        {
            foreach (var (posId, (px, py)) in pendingPositions)
            {
                var entity = model.Entities.Find(e => e.Name == posId);
                if (entity != null)
                {
                    entity.Position = new System.Windows.Point(px, py);
                    entity.HasManualPosition = true;
                }
            }
        }

        return foundDeclaration ? model : null;
    }

    /// <summary>
    /// Parses a cardinality string to an ERCardinality enum value.
    /// Mermaid ER cardinality markers:
    /// || = exactly one, |o or o| = zero or one, }o or o{ = zero or more, }| or |{ = one or more
    /// </summary>
    private static ERCardinality ParseERCardinality(string cardStr)
    {
        return cardStr switch
        {
            "||" => ERCardinality.ExactlyOne,
            "|o" or "o|" => ERCardinality.ZeroOrOne,
            "}o" or "o{" => ERCardinality.ZeroOrMore,
            "}|" or "|{" => ERCardinality.OneOrMore,
            _ => ERCardinality.ExactlyOne
        };
    }

    /// <summary>
    /// Ensures an ER entity exists in the model, creating it if not already known.
    /// </summary>
    private static void EnsureEREntityExists(ERDiagramModel model, HashSet<string> knownEntities, string entityName)
    {
        if (knownEntities.Contains(entityName))
            return;

        knownEntities.Add(entityName);
        model.Entities.Add(new EREntity { Name = entityName });
    }

    // =============================================
    // Gantt Chart Parser (Phase 4)
    // =============================================

    // --- Gantt patterns ---
    private static readonly Regex GanttDeclaration = new(@"^\s*gantt\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GanttTitlePattern = new(@"^\s*title\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex GanttDateFormatPattern = new(@"^\s*dateFormat\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex GanttAxisFormatPattern = new(@"^\s*axisFormat\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex GanttExcludesPattern = new(@"^\s*excludes\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex GanttSectionPattern = new(@"^\s*section\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex GanttTickIntervalPattern = new(@"^\s*tickInterval\s+(.+)$", RegexOptions.Compiled);
    // Task pattern: label :metadata
    private static readonly Regex GanttTaskPattern = new(@"^\s*(.+?)\s*:\s*(.+)$", RegexOptions.Compiled);

    /// <summary>
    /// Parses Mermaid gantt chart text into a GanttModel.
    /// </summary>
    public static GanttModel? ParseGantt(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = text.Split('\n');
        var model = new GanttModel();
        bool foundDeclaration = false;
        GanttSection? currentSection = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.TrimEnd('\r');

            if (string.IsNullOrWhiteSpace(line))
                continue;

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

            // Look for gantt declaration
            if (!foundDeclaration)
            {
                var declMatch = GanttDeclaration.Match(line);
                if (declMatch.Success)
                {
                    foundDeclaration = true;
                    model.DeclarationLineIndex = i;
                    continue;
                }

                model.PreambleLines.Add(line);
                continue;
            }

            var trimmed = line.Trim();

            // Title
            var titleMatch = GanttTitlePattern.Match(trimmed);
            if (titleMatch.Success)
            {
                model.Title = titleMatch.Groups[1].Value.Trim();
                continue;
            }

            // Date format
            var dfMatch = GanttDateFormatPattern.Match(trimmed);
            if (dfMatch.Success)
            {
                model.DateFormat = dfMatch.Groups[1].Value.Trim();
                continue;
            }

            // Axis format
            var afMatch = GanttAxisFormatPattern.Match(trimmed);
            if (afMatch.Success)
            {
                model.AxisFormat = afMatch.Groups[1].Value.Trim();
                continue;
            }

            // Excludes
            var exMatch = GanttExcludesPattern.Match(trimmed);
            if (exMatch.Success)
            {
                var excludesStr = exMatch.Groups[1].Value.Trim();
                model.Excludes = excludesStr;
                if (excludesStr.Contains("weekends", StringComparison.OrdinalIgnoreCase))
                    model.ExcludesWeekends = true;
                continue;
            }

            // Tick interval (just skip, we preserve it but don't model it specially)
            if (GanttTickIntervalPattern.IsMatch(trimmed))
                continue;

            // Section
            var sectionMatch = GanttSectionPattern.Match(trimmed);
            if (sectionMatch.Success)
            {
                currentSection = new GanttSection { Name = sectionMatch.Groups[1].Value.Trim() };
                model.Sections.Add(currentSection);
                continue;
            }

            // Task: label :metadata (comma-separated parts)
            var taskMatch = GanttTaskPattern.Match(trimmed);
            if (taskMatch.Success)
            {
                var task = ParseGanttTask(taskMatch.Groups[1].Value.Trim(), taskMatch.Groups[2].Value.Trim());
                if (currentSection != null)
                    currentSection.Tasks.Add(task);
                else
                    model.Tasks.Add(task);
                continue;
            }
        }

        return foundDeclaration ? model : null;
    }

    /// <summary>
    /// Parses a gantt task from its label and metadata string.
    /// Metadata format: [id,] [done|active|crit|milestone,]... startDate, endDateOrDuration
    /// </summary>
    private static GanttTask ParseGanttTask(string label, string metadata)
    {
        var task = new GanttTask { Label = label };
        var parts = metadata.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();

        // Parse metadata parts in order
        // Known tags: done, active, crit, milestone
        // If a part looks like an ID (single word, not a tag, not a date): it's the ID
        // The last 1-2 parts are startDate and endDate/duration
        var tags = new List<string>();
        var dateParts = new List<string>();
        string? taskId = null;

        foreach (var part in parts)
        {
            var lowerPart = part.ToLowerInvariant();
            if (lowerPart == "done" || lowerPart == "active" || lowerPart == "crit" || lowerPart == "milestone")
            {
                tags.Add(lowerPart);
            }
            else if (lowerPart.StartsWith("after ") || lowerPart.Contains('-') || lowerPart.EndsWith('d') || lowerPart.EndsWith('h') || lowerPart.EndsWith('w'))
            {
                // Looks like a date, duration, or dependency reference
                dateParts.Add(part);
            }
            else if (taskId == null && !part.Contains(' ') && dateParts.Count == 0)
            {
                // Likely a task ID
                taskId = part;
            }
            else
            {
                dateParts.Add(part);
            }
        }

        task.Tags = tags;
        task.Id = taskId;
        task.IsMilestone = tags.Contains("milestone");

        if (dateParts.Count >= 2)
        {
            task.StartDate = dateParts[0];
            task.EndDate = dateParts[1];
        }
        else if (dateParts.Count == 1)
        {
            task.EndDate = dateParts[0];
        }

        return task;
    }

    // =============================================
    // Mind Map Parser (Phase 4)
    // =============================================

    private static readonly Regex MindMapDeclaration = new(@"^\s*mindmap\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses Mermaid mind map text into a MindMapModel.
    /// Mind maps use indentation-based hierarchy.
    /// </summary>
    public static MindMapModel? ParseMindMap(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = text.Split('\n');
        var model = new MindMapModel();
        bool foundDeclaration = false;
        var nodeStack = new List<(int indent, MindMapNode node)>();

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.TrimEnd('\r');

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Check for @pos position comments first
            var posMatch = PosCommentPattern.Match(line);
            if (posMatch.Success)
            {
                var posNodeId = posMatch.Groups[1].Value;
                var px = double.Parse(posMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                var py = double.Parse(posMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                model.NodePositions[posNodeId] = new System.Windows.Point(px, py);
                model.HasPositionData = true;
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

            // Look for mindmap declaration
            if (!foundDeclaration)
            {
                var declMatch = MindMapDeclaration.Match(line);
                if (declMatch.Success)
                {
                    foundDeclaration = true;
                    model.DeclarationLineIndex = i;
                    continue;
                }

                model.PreambleLines.Add(line);
                continue;
            }

            // Skip icon/class directives (::icon(...) or :::className)
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("::icon(") || (trimmed.StartsWith(":::") && !trimmed.Contains('[')))
            {
                // Apply icon/class to the most recent node
                if (nodeStack.Count > 0)
                {
                    var lastNode = nodeStack[nodeStack.Count - 1].node;
                    if (trimmed.StartsWith("::icon("))
                    {
                        var iconMatch = Regex.Match(trimmed, @"::icon\((.+?)\)");
                        if (iconMatch.Success)
                            lastNode.Icon = iconMatch.Groups[1].Value;
                    }
                    else if (trimmed.StartsWith(":::"))
                    {
                        lastNode.CssClass = trimmed[3..].Trim();
                    }
                }
                continue;
            }

            // Calculate indentation level
            int indent = 0;
            foreach (char c in line)
            {
                if (c == ' ') indent++;
                else if (c == '\t') indent += 4;
                else break;
            }

            // Parse node text and shape
            var nodeText = trimmed;
            var node = ParseMindMapNodeText(nodeText);

            // Build tree based on indentation
            // Pop stack until we find a parent with less indent
            while (nodeStack.Count > 0 && nodeStack[nodeStack.Count - 1].indent >= indent)
            {
                nodeStack.RemoveAt(nodeStack.Count - 1);
            }

            if (nodeStack.Count == 0)
            {
                // This is the root node
                model.Root = node;
            }
            else
            {
                // Add as child of the last node in the stack
                nodeStack[nodeStack.Count - 1].node.Children.Add(node);
            }

            nodeStack.Add((indent, node));
        }

        return foundDeclaration ? model : null;
    }

    /// <summary>
    /// Parses a mind map node's text and determines its shape.
    /// </summary>
    private static MindMapNode ParseMindMapNodeText(string text)
    {
        var node = new MindMapNode();

        // In Mermaid mindmap syntax, nodes can have an optional ID prefix before shape brackets.
        // e.g., "root((Central Topic))" has ID "root" with circle shape containing "Central Topic".
        // First, try to extract an ID prefix (alphanumeric text before the first shape bracket).
        string? idPrefix = null;
        var shapeText = text;

        // Find the first occurrence of a shape-opening bracket sequence
        int shapeStart = -1;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '(' || c == '[' || c == '{' || c == ')')
            {
                shapeStart = i;
                break;
            }
        }

        // If there's text before the first bracket, it's an ID prefix
        if (shapeStart > 0)
        {
            idPrefix = text[..shapeStart];
            shapeText = text[shapeStart..];
        }

        // Try to match shapes on the remaining text:
        // ((text)) = circle
        // (text) = rounded
        // [text] = square
        // ))text(( = bang
        // )text( = cloud
        // {{text}} = hexagon
        if (shapeText.StartsWith("((") && shapeText.EndsWith("))"))
        {
            node.Label = shapeText[2..^2];
            node.Shape = MindMapNodeShape.Circle;
        }
        else if (shapeText.StartsWith("{{") && shapeText.EndsWith("}}"))
        {
            node.Label = shapeText[2..^2];
            node.Shape = MindMapNodeShape.Hexagon;
        }
        else if (shapeText.StartsWith("))") && shapeText.EndsWith("(("))
        {
            node.Label = shapeText[2..^2];
            node.Shape = MindMapNodeShape.Bang;
        }
        else if (shapeText.StartsWith(")") && shapeText.EndsWith("("))
        {
            node.Label = shapeText[1..^1];
            node.Shape = MindMapNodeShape.Cloud;
        }
        else if (shapeText.StartsWith("(") && shapeText.EndsWith(")"))
        {
            node.Label = shapeText[1..^1];
            node.Shape = MindMapNodeShape.Rounded;
        }
        else if (shapeText.StartsWith("[") && shapeText.EndsWith("]"))
        {
            node.Label = shapeText[1..^1];
            node.Shape = MindMapNodeShape.Square;
        }
        else
        {
            // No shape brackets found — entire text is the label
            node.Label = text;
            node.Shape = MindMapNodeShape.Default;
            // No ID prefix when there's no shape
            idPrefix = null;
        }

        node.Id = idPrefix;
        return node;
    }

    // =============================================
    // Pie Chart Parser (Phase 4)
    // =============================================

    private static readonly Regex PieDeclaration = new(@"^\s*pie(?:\s+showData)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PieTitlePattern = new(@"^\s*title\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex PieSlicePattern = new(@"^\s*""([^""]+)""\s*:\s*([\d.]+)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Parses Mermaid pie chart text into a PieChartModel.
    /// </summary>
    public static PieChartModel? ParsePieChart(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = text.Split('\n');
        var model = new PieChartModel();
        bool foundDeclaration = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.TrimEnd('\r');

            if (string.IsNullOrWhiteSpace(line))
                continue;

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

            // Look for pie declaration
            if (!foundDeclaration)
            {
                var declMatch = PieDeclaration.Match(line);
                if (declMatch.Success)
                {
                    foundDeclaration = true;
                    model.DeclarationLineIndex = i;
                    if (line.Contains("showData", StringComparison.OrdinalIgnoreCase))
                        model.ShowData = true;
                    continue;
                }

                model.PreambleLines.Add(line);
                continue;
            }

            var trimmed = line.Trim();

            // Title
            var titleMatch = PieTitlePattern.Match(trimmed);
            if (titleMatch.Success)
            {
                model.Title = titleMatch.Groups[1].Value.Trim();
                continue;
            }

            // Slice: "Label" : value
            var sliceMatch = PieSlicePattern.Match(trimmed);
            if (sliceMatch.Success)
            {
                var value = double.TryParse(sliceMatch.Groups[2].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
                model.Slices.Add(new PieSlice
                {
                    Label = sliceMatch.Groups[1].Value,
                    Value = value
                });
                continue;
            }
        }

        return foundDeclaration ? model : null;
    }
}
