using System.Globalization;
using System.Text;

namespace MermaidEditor;

/// <summary>
/// Converts a FlowchartModel back to valid Mermaid flowchart text.
/// Produces clean, readable output with proper indentation.
/// Preserves comments and formatting where possible.
/// </summary>
public static class MermaidSerializer
{
    private const string Indent = "    ";

    /// <summary>
    /// Serializes a FlowchartModel to Mermaid flowchart text.
    /// </summary>
    /// <param name="model">The flowchart model to serialize.</param>
    /// <returns>Valid Mermaid flowchart text.</returns>
    public static string Serialize(FlowchartModel model)
    {
        if (model == null)
            return string.Empty;

        var sb = new StringBuilder();

        // Write any comments that appeared before the declaration
        WriteCommentsBeforeLine(sb, model, model.DeclarationLineIndex);

        // Write the flowchart declaration
        var direction = model.Direction;
        // Normalize: TD is the canonical form (TB is equivalent)
        sb.AppendLine($"{model.DiagramKeyword} {direction}");

        // Write classDef definitions first (convention)
        foreach (var style in model.Styles.Where(s => s.IsClassDef))
        {
            sb.AppendLine($"{Indent}classDef {style.Target} {style.StyleString}");
        }

        if (model.Styles.Any(s => s.IsClassDef))
            sb.AppendLine();

        // Collect nodes that belong to subgraphs
        var nodesInSubgraphs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sg in model.Subgraphs)
        {
            foreach (var nodeId in sg.NodeIds)
            {
                nodesInSubgraphs.Add(nodeId);
            }
        }

        // Write standalone node definitions (nodes not in any subgraph that have labels/shapes)
        var writtenNodeDefs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in model.Nodes.Where(n => !nodesInSubgraphs.Contains(n.Id)))
        {
            if (node.Label != null || node.Shape != NodeShape.Rectangle)
            {
                sb.AppendLine($"{Indent}{FormatNode(node)}");
                writtenNodeDefs.Add(node.Id);
            }
        }

        // Write subgraphs with their contained nodes
        foreach (var subgraph in model.Subgraphs)
        {
            sb.AppendLine();
            WriteSubgraph(sb, subgraph, model, writtenNodeDefs, Indent);
        }

        if (model.Nodes.Any(n => !nodesInSubgraphs.Contains(n.Id) && writtenNodeDefs.Contains(n.Id)) && model.Edges.Count > 0)
            sb.AppendLine();

        // Write edges
        foreach (var edge in model.Edges)
        {
            sb.AppendLine($"{Indent}{FormatEdge(edge)}");
        }

        // Write inline style definitions
        var inlineStyles = model.Styles.Where(s => !s.IsClassDef).ToList();
        if (inlineStyles.Count > 0)
        {
            sb.AppendLine();
            foreach (var style in inlineStyles)
            {
                sb.AppendLine($"{Indent}style {style.Target} {style.StyleString}");
            }
        }

        // Write class assignments
        if (model.ClassAssignments.Count > 0)
        {
            sb.AppendLine();
            foreach (var ca in model.ClassAssignments)
            {
                sb.AppendLine($"{Indent}class {ca.NodeIds} {ca.ClassName}");
            }
        }

        // Write trailing comments
        WriteTrailingComments(sb, model);

        // Write @pos comments for manually-positioned nodes
        WritePositionComments(sb, model);

        return sb.ToString().TrimEnd('\r', '\n') + Environment.NewLine;
    }

    /// <summary>
    /// Writes a subgraph block with its contained node definitions and direction.
    /// </summary>
    private static void WriteSubgraph(StringBuilder sb, FlowchartSubgraph subgraph, FlowchartModel model,
        HashSet<string> writtenNodeDefs, string indent)
    {
        // Write subgraph header
        if (subgraph.Label != subgraph.Id && !string.IsNullOrEmpty(subgraph.Label))
        {
            sb.AppendLine($"{indent}subgraph {subgraph.Id} [{subgraph.Label}]");
        }
        else
        {
            sb.AppendLine($"{indent}subgraph {subgraph.Id}");
        }

        var innerIndent = indent + Indent;

        // Write direction if specified
        if (!string.IsNullOrEmpty(subgraph.Direction))
        {
            sb.AppendLine($"{innerIndent}direction {subgraph.Direction}");
        }

        // Write node definitions within the subgraph
        foreach (var nodeId in subgraph.NodeIds)
        {
            var node = model.Nodes.Find(n => n.Id == nodeId);
            if (node != null && !writtenNodeDefs.Contains(nodeId))
            {
                sb.AppendLine($"{innerIndent}{FormatNode(node)}");
                writtenNodeDefs.Add(nodeId);
            }
            else if (node != null && writtenNodeDefs.Contains(nodeId))
            {
                // Node was already defined, just reference it
                sb.AppendLine($"{innerIndent}{nodeId}");
            }
        }

        sb.AppendLine($"{indent}end");
    }

    /// <summary>
    /// Formats a node definition as a Mermaid string.
    /// </summary>
    private static string FormatNode(FlowchartNode node)
    {
        if (node.Label == null)
            return node.Id;

        var label = node.Label;

        return node.Shape switch
        {
            NodeShape.Rectangle => $"{node.Id}[{label}]",
            NodeShape.Rounded => $"{node.Id}({label})",
            NodeShape.Stadium => $"{node.Id}([{label}])",
            NodeShape.Subroutine => $"{node.Id}[[{label}]]",
            NodeShape.Cylindrical => $"{node.Id}[({label})]",
            NodeShape.Circle => $"{node.Id}(({label}))",
            NodeShape.Asymmetric => $"{node.Id}>{label}]",
            NodeShape.Rhombus => $"{node.Id}{{{label}}}",
            NodeShape.Hexagon => $"{node.Id}{{{{{label}}}}}",
            NodeShape.Parallelogram => $"{node.Id}[/{label}/]",
            NodeShape.ParallelogramAlt => $"{node.Id}[\\{label}\\]",
            NodeShape.Trapezoid => $"{node.Id}[/{label}\\]",
            NodeShape.TrapezoidAlt => $"{node.Id}[\\{label}/]",
            NodeShape.DoubleCircle => $"{node.Id}((({label})))",
            _ => $"{node.Id}[{label}]"
        };
    }

    /// <summary>
    /// Formats an edge definition as a Mermaid string.
    /// </summary>
    private static string FormatEdge(FlowchartEdge edge)
    {
        var linkStr = BuildLinkString(edge);

        if (!string.IsNullOrEmpty(edge.Label))
        {
            // Use pipe-delimited label format: A -->|label| B
            return $"{edge.FromNodeId} {linkStr}|{edge.Label}| {edge.ToNodeId}";
        }

        return $"{edge.FromNodeId} {linkStr} {edge.ToNodeId}";
    }

    /// <summary>
    /// Builds the link/arrow string for an edge based on its style and arrow type.
    /// </summary>
    private static string BuildLinkString(FlowchartEdge edge)
    {
        var sb = new StringBuilder();

        // Bidirectional left arrow
        if (edge.IsBidirectional)
        {
            sb.Append(edge.ArrowType switch
            {
                ArrowType.Arrow => "<",
                ArrowType.Circle => "o",
                ArrowType.Cross => "x",
                _ => "<"
            });
        }

        // Link body
        switch (edge.Style)
        {
            case EdgeStyle.Solid:
                sb.Append(new string('-', edge.LinkLength));
                break;
            case EdgeStyle.Dotted:
                sb.Append("-.");
                sb.Append(new string('-', Math.Max(edge.LinkLength - 2, 0)));
                break;
            case EdgeStyle.Thick:
                sb.Append(new string('=', edge.LinkLength));
                break;
        }

        // Arrow end
        switch (edge.ArrowType)
        {
            case ArrowType.Arrow:
                if (edge.Style == EdgeStyle.Dotted)
                    sb.Append("->");
                else
                    sb.Append('>');
                break;
            case ArrowType.Open:
                // No arrow head
                if (edge.Style == EdgeStyle.Dotted)
                    sb.Append('-');
                break;
            case ArrowType.Circle:
                sb.Append('o');
                break;
            case ArrowType.Cross:
                sb.Append('x');
                break;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes comments that appeared before a given line index.
    /// </summary>
    private static void WriteCommentsBeforeLine(StringBuilder sb, FlowchartModel model, int lineIndex)
    {
        foreach (var comment in model.Comments.Where(c => c.OriginalLineIndex < lineIndex))
        {
            sb.AppendLine($"%%{comment.Text}");
        }
    }

    /// <summary>
    /// Writes any remaining comments that appeared after the main content.
    /// </summary>
    private static void WriteTrailingComments(StringBuilder sb, FlowchartModel model)
    {
        // Simple heuristic: comments with high line indices are trailing
        if (model.Comments.Count > 0)
        {
            var trailingComments = model.Comments
                .Where(c => c.OriginalLineIndex > model.DeclarationLineIndex)
                .OrderBy(c => c.OriginalLineIndex)
                .ToList();

            if (trailingComments.Count > 0)
            {
                sb.AppendLine();
                foreach (var comment in trailingComments)
                {
                    sb.AppendLine($"%%{comment.Text}");
                }
            }
        }
    }

    /// <summary>
    /// Writes %% @pos comments for nodes that have been manually positioned in the visual editor.
    /// These special comments store node positions so they survive round-trips.
    /// </summary>
    private static void WritePositionComments(StringBuilder sb, FlowchartModel model)
    {
        var positionedNodes = model.Nodes.Where(n => n.HasManualPosition).ToList();
        if (positionedNodes.Count == 0) return;

        sb.AppendLine();
        foreach (var node in positionedNodes)
        {
            var x = node.Position.X.ToString("F1", CultureInfo.InvariantCulture);
            var y = node.Position.Y.ToString("F1", CultureInfo.InvariantCulture);
            sb.AppendLine($"%% @pos {node.Id} {x},{y}");
        }
    }
}
