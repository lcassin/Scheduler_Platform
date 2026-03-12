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

        // Write preamble lines (config directives, frontmatter, etc.)
        foreach (var preambleLine in model.PreambleLines)
        {
            sb.AppendLine(preambleLine);
        }

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

        var shapePart = node.Shape switch
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

        // Append :::className suffix if present
        if (!string.IsNullOrEmpty(node.CssClass))
            shapePart += $":::{node.CssClass}";

        return shapePart;
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
                // Open links need minimum 3 dashes (---), arrow links need 2 (--)
                var minLength = edge.ArrowType == ArrowType.Open ? 3 : 2;
                sb.Append(new string('-', Math.Max(edge.LinkLength, minLength)));
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
                if (edge.Style == EdgeStyle.Dotted)
                    sb.Append("-o");
                else
                    sb.Append('o');
                break;
            case ArrowType.Cross:
                if (edge.Style == EdgeStyle.Dotted)
                    sb.Append("-x");
                else
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

    // =============================================
    // Sequence Diagram Serializer (Phase 2.1)
    // =============================================

    /// <summary>
    /// Serializes a SequenceDiagramModel to valid Mermaid sequence diagram text.
    /// </summary>
    /// <param name="model">The sequence diagram model to serialize.</param>
    /// <returns>Valid Mermaid sequence diagram text.</returns>
    public static string SerializeSequenceDiagram(SequenceDiagramModel model)
    {
        if (model == null)
            return string.Empty;

        var sb = new StringBuilder();

        // Write preamble lines (config directives, frontmatter, etc.)
        foreach (var preambleLine in model.PreambleLines)
        {
            sb.AppendLine(preambleLine);
        }

        // Write comments that appeared before the declaration
        WriteSequenceCommentsBeforeLine(sb, model, model.DeclarationLineIndex);

        // Write the sequenceDiagram declaration
        sb.AppendLine("sequenceDiagram");

        // Write autonumber if enabled
        if (model.AutoNumber)
        {
            sb.AppendLine($"{Indent}autonumber");
        }

        // Write explicit participant/actor declarations
        foreach (var participant in model.Participants.Where(p => p.IsExplicit))
        {
            var keyword = participant.Type == SequenceParticipantType.Actor ? "actor" : "participant";
            if (!string.IsNullOrEmpty(participant.Alias))
            {
                sb.AppendLine($"{Indent}{keyword} {participant.Id} as {participant.Alias}");
            }
            else
            {
                sb.AppendLine($"{Indent}{keyword} {participant.Id}");
            }
        }

        // Blank line between declarations and elements
        if (model.Participants.Any(p => p.IsExplicit) && model.Elements.Count > 0)
        {
            sb.AppendLine();
        }

        // Write all elements in order
        WriteSequenceElements(sb, model.Elements, Indent);

        // Write trailing comments
        WriteSequenceTrailingComments(sb, model);

        return sb.ToString().TrimEnd('\r', '\n') + Environment.NewLine;
    }

    /// <summary>
    /// Writes a list of sequence elements with proper indentation. Handles recursive fragment serialization.
    /// </summary>
    private static void WriteSequenceElements(StringBuilder sb, List<SequenceElement> elements, string indent)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case SequenceMessage msg:
                    WriteSequenceMessage(sb, msg, indent);
                    break;
                case SequenceNote note:
                    WriteSequenceNote(sb, note, indent);
                    break;
                case SequenceFragment fragment:
                    WriteSequenceFragment(sb, fragment, indent);
                    break;
                case SequenceActivation activation:
                    var keyword = activation.IsActivate ? "activate" : "deactivate";
                    sb.AppendLine($"{indent}{keyword} {activation.ParticipantId}");
                    break;
                case SequenceCreate create:
                    var createKeyword = create.ParticipantType == SequenceParticipantType.Actor ? "actor" : "participant";
                    sb.AppendLine($"{indent}create {createKeyword} {create.ParticipantId}");
                    break;
                case SequenceDestroy destroy:
                    sb.AppendLine($"{indent}destroy {destroy.ParticipantId}");
                    break;
            }
        }
    }

    /// <summary>
    /// Writes a sequence message line.
    /// </summary>
    private static void WriteSequenceMessage(StringBuilder sb, SequenceMessage msg, string indent)
    {
        var arrowStr = msg.ArrowType switch
        {
            SequenceArrowType.SolidArrow => "->>",
            SequenceArrowType.DottedArrow => "-->>",
            SequenceArrowType.SolidOpen => "->",
            SequenceArrowType.DottedOpen => "-->",
            SequenceArrowType.SolidCross => "-x",
            SequenceArrowType.DottedCross => "--x",
            SequenceArrowType.SolidAsync => "-)",
            SequenceArrowType.DottedAsync => "--)",
            _ => "->>"
        };

        // Add activation/deactivation suffix
        var suffix = "";
        if (msg.ActivateTarget) suffix = "+";
        else if (msg.DeactivateSource) suffix = "-";

        sb.AppendLine($"{indent}{msg.FromId}{arrowStr}{suffix}{msg.ToId}: {msg.Text}");
    }

    /// <summary>
    /// Writes a sequence note (single-line or multi-line).
    /// </summary>
    private static void WriteSequenceNote(StringBuilder sb, SequenceNote note, string indent)
    {
        var positionStr = note.Position switch
        {
            SequenceNotePosition.RightOf => "right of",
            SequenceNotePosition.LeftOf => "left of",
            SequenceNotePosition.Over => "over",
            _ => "right of"
        };

        if (note.Text.Contains('\n'))
        {
            // Multi-line note
            sb.AppendLine($"{indent}Note {positionStr} {note.OverParticipants}");
            foreach (var line in note.Text.Split('\n'))
            {
                sb.AppendLine($"{indent}{Indent}{line}");
            }
            sb.AppendLine($"{indent}end note");
        }
        else
        {
            sb.AppendLine($"{indent}Note {positionStr} {note.OverParticipants}: {note.Text}");
        }
    }

    /// <summary>
    /// Writes a sequence fragment block with its sections. Handles alt/else, par/and, etc.
    /// </summary>
    private static void WriteSequenceFragment(StringBuilder sb, SequenceFragment fragment, string indent)
    {
        var typeStr = fragment.Type switch
        {
            SequenceFragmentType.Loop => "loop",
            SequenceFragmentType.Alt => "alt",
            SequenceFragmentType.Opt => "opt",
            SequenceFragmentType.Par => "par",
            SequenceFragmentType.Critical => "critical",
            SequenceFragmentType.Break => "break",
            SequenceFragmentType.Rect => "rect",
            _ => "loop"
        };

        sb.AppendLine($"{indent}{typeStr} {fragment.Label}");

        var innerIndent = indent + Indent;

        for (int i = 0; i < fragment.Sections.Count; i++)
        {
            if (i > 0)
            {
                // Determine the section divider keyword based on fragment type
                var dividerKeyword = fragment.Type switch
                {
                    SequenceFragmentType.Par => "and",
                    SequenceFragmentType.Critical => "option",
                    _ => "else"
                };

                var sectionLabel = fragment.Sections[i].Label;
                if (!string.IsNullOrEmpty(sectionLabel))
                {
                    sb.AppendLine($"{indent}{dividerKeyword} {sectionLabel}");
                }
                else
                {
                    sb.AppendLine($"{indent}{dividerKeyword}");
                }
            }

            // Recursively write section elements
            WriteSequenceElements(sb, fragment.Sections[i].Elements, innerIndent);
        }

        sb.AppendLine($"{indent}end");
    }

    /// <summary>
    /// Writes comments that appeared before a given line in the sequence diagram.
    /// </summary>
    private static void WriteSequenceCommentsBeforeLine(StringBuilder sb, SequenceDiagramModel model, int lineIndex)
    {
        foreach (var comment in model.Comments.Where(c => c.OriginalLineIndex < lineIndex))
        {
            sb.AppendLine($"%%{comment.Text}");
        }
    }

    /// <summary>
    /// Writes trailing comments for a sequence diagram.
    /// </summary>
    private static void WriteSequenceTrailingComments(StringBuilder sb, SequenceDiagramModel model)
    {
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

    // =============================================
    // Class Diagram Serializer (Phase 2.2)
    // =============================================

    /// <summary>
    /// Serializes a ClassDiagramModel to valid Mermaid class diagram text.
    /// </summary>
    /// <param name="model">The class diagram model to serialize.</param>
    /// <returns>Valid Mermaid class diagram text.</returns>
    public static string SerializeClassDiagram(ClassDiagramModel model)
    {
        if (model == null)
            return string.Empty;

        var sb = new StringBuilder();

        // Write preamble lines (config directives, frontmatter, etc.)
        foreach (var preambleLine in model.PreambleLines)
        {
            sb.AppendLine(preambleLine);
        }

        // Write comments that appeared before the declaration
        WriteClassDiagramCommentsBeforeLine(sb, model, model.DeclarationLineIndex);

        // Write the classDiagram declaration
        sb.AppendLine("classDiagram");

        // Write direction if specified
        if (!string.IsNullOrEmpty(model.Direction))
        {
            sb.AppendLine($"{Indent}direction {model.Direction}");
        }

        // Write notes that appear before class definitions
        foreach (var note in model.Notes)
        {
            if (note.ForClass != null)
            {
                sb.AppendLine($"{Indent}note for {note.ForClass} \"{note.Text}\"");
            }
            else
            {
                sb.AppendLine($"{Indent}note \"{note.Text}\"");
            }
        }

        // Collect classes that belong to namespaces
        var classesInNamespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ns in model.Namespaces)
        {
            foreach (var classId in ns.ClassIds)
            {
                classesInNamespaces.Add(classId);
            }
        }

        // Write namespace blocks with their classes
        foreach (var ns in model.Namespaces)
        {
            sb.AppendLine($"{Indent}namespace {ns.Name} {{");
            foreach (var classId in ns.ClassIds)
            {
                var cls = model.Classes.Find(c => c.Id == classId);
                if (cls != null)
                {
                    WriteClassDefinition(sb, cls, Indent + Indent);
                }
            }
            sb.AppendLine($"{Indent}}}");
        }

        // Write class definitions not in namespaces
        // First write classes with members (using body syntax)
        // Then write classes without members that were explicitly declared
        foreach (var cls in model.Classes.Where(c => !classesInNamespaces.Contains(c.Id)))
        {
            if (cls.Members.Count > 0 || cls.IsExplicit)
            {
                WriteClassDefinition(sb, cls, Indent);
            }
        }

        // Write relationships
        if (model.Relationships.Count > 0)
        {
            foreach (var rel in model.Relationships)
            {
                sb.AppendLine($"{Indent}{FormatClassRelationship(rel)}");
            }
        }

        // Write style definitions (classDef)
        foreach (var style in model.Styles.Where(s => s.IsClassDef))
        {
            sb.AppendLine($"{Indent}classDef {style.Target} {style.StyleString}");
        }

        // Write inline style definitions (style)
        foreach (var style in model.Styles.Where(s => !s.IsClassDef))
        {
            sb.AppendLine($"{Indent}style {style.Target} {style.StyleString}");
        }

        // Write cssClass assignments
        foreach (var cssClass in model.CssClassAssignments)
        {
            sb.AppendLine($"{Indent}cssClass \"{cssClass.NodeIds}\" {cssClass.ClassName}");
        }

        // Write trailing comments
        WriteClassDiagramTrailingComments(sb, model);

        return sb.ToString().TrimEnd('\r', '\n') + Environment.NewLine;
    }

    /// <summary>
    /// Writes a class definition block with its members.
    /// Uses body syntax { } when the class has members or an annotation.
    /// Uses inline syntax for simple declarations.
    /// </summary>
    private static void WriteClassDefinition(StringBuilder sb, ClassDefinition cls, string indent)
    {
        var classLine = new StringBuilder($"{indent}class {cls.Id}");

        // Append generic type
        if (!string.IsNullOrEmpty(cls.GenericType))
        {
            classLine.Append($"~{cls.GenericType}~");
        }

        // Append label
        if (!string.IsNullOrEmpty(cls.Label))
        {
            classLine.Append($"[\"{cls.Label}\"]");
        }

        // Append CSS class
        if (!string.IsNullOrEmpty(cls.CssClass))
        {
            classLine.Append($":::{cls.CssClass}");
        }

        // Inline annotation (only if no members and no body needed)
        if (!string.IsNullOrEmpty(cls.Annotation) && cls.Members.Count == 0)
        {
            classLine.Append($" <<{cls.Annotation}>>");
            sb.AppendLine(classLine.ToString());
            return;
        }

        // If class has members or annotation inside body, use { } syntax
        if (cls.Members.Count > 0 || !string.IsNullOrEmpty(cls.Annotation))
        {
            classLine.Append('{');
            sb.AppendLine(classLine.ToString());

            var innerIndent = indent + Indent;

            // Write annotation inside body if present
            if (!string.IsNullOrEmpty(cls.Annotation))
            {
                sb.AppendLine($"{innerIndent}<<{cls.Annotation}>>");
            }

            // Write members
            foreach (var member in cls.Members)
            {
                sb.AppendLine($"{innerIndent}{FormatClassMember(member)}");
            }

            sb.AppendLine($"{indent}}}");
        }
        else
        {
            // Simple class declaration
            sb.AppendLine(classLine.ToString());
        }
    }

    /// <summary>
    /// Formats a class member for serialization.
    /// Uses the raw text for round-trip fidelity when available.
    /// </summary>
    private static string FormatClassMember(ClassMember member)
    {
        // If we have raw text, use it for best round-trip fidelity
        if (!string.IsNullOrEmpty(member.RawText))
        {
            return member.RawText;
        }

        // Otherwise, reconstruct from parsed fields
        var sb = new StringBuilder();

        // Visibility prefix
        sb.Append(member.Visibility switch
        {
            MemberVisibility.Public => "+",
            MemberVisibility.Private => "-",
            MemberVisibility.Protected => "#",
            MemberVisibility.Package => "~",
            _ => ""
        });

        if (member.IsMethod)
        {
            sb.Append(member.Name);
            sb.Append('(');
            if (!string.IsNullOrEmpty(member.Parameters))
            {
                sb.Append(member.Parameters);
            }
            sb.Append(')');

            // Return type
            if (!string.IsNullOrEmpty(member.Type))
            {
                sb.Append($" {member.Type}");
            }
        }
        else
        {
            // Field: "Type name" or "name : type" format
            if (!string.IsNullOrEmpty(member.Type) && !string.IsNullOrEmpty(member.Name))
            {
                sb.Append($"{member.Type} {member.Name}");
            }
            else
            {
                sb.Append(member.Name);
            }
        }

        // Classifier suffix
        sb.Append(member.Classifier switch
        {
            MemberClassifier.Abstract => "*",
            MemberClassifier.Static => "$",
            _ => ""
        });

        return sb.ToString();
    }

    /// <summary>
    /// Formats a class relationship for serialization.
    /// </summary>
    private static string FormatClassRelationship(ClassRelationship rel)
    {
        var sb = new StringBuilder();

        sb.Append(rel.FromId);

        // From cardinality
        if (!string.IsNullOrEmpty(rel.FromCardinality))
        {
            sb.Append($" \"{rel.FromCardinality}\"");
        }

        sb.Append(' ');

        // Left end
        if (rel.LeftEnd == ClassRelationEnd.Lollipop)
        {
            sb.Append("()");
        }
        else
        {
            sb.Append(rel.LeftEnd switch
            {
                ClassRelationEnd.Inheritance => "<|",
                ClassRelationEnd.Composition => "*",
                ClassRelationEnd.Aggregation => "o",
                ClassRelationEnd.Arrow => "<",
                _ => ""
            });
        }

        // Link
        sb.Append(rel.LinkStyle == ClassLinkStyle.Dashed ? ".." : "--");

        // Right end
        if (rel.RightEnd == ClassRelationEnd.Lollipop)
        {
            sb.Append("()");
        }
        else
        {
            sb.Append(rel.RightEnd switch
            {
                ClassRelationEnd.Inheritance => "|>",
                ClassRelationEnd.Composition => "*",
                ClassRelationEnd.Aggregation => "o",
                ClassRelationEnd.Arrow => ">",
                _ => ""
            });
        }

        // To cardinality
        if (!string.IsNullOrEmpty(rel.ToCardinality))
        {
            sb.Append($" \"{rel.ToCardinality}\"");
        }

        sb.Append($" {rel.ToId}");

        // Label
        if (!string.IsNullOrEmpty(rel.Label))
        {
            sb.Append($" : {rel.Label}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes comments that appeared before a given line in the class diagram.
    /// </summary>
    private static void WriteClassDiagramCommentsBeforeLine(StringBuilder sb, ClassDiagramModel model, int lineIndex)
    {
        foreach (var comment in model.Comments.Where(c => c.OriginalLineIndex < lineIndex))
        {
            sb.AppendLine($"%%{comment.Text}");
        }
    }

    /// <summary>
    /// Writes trailing comments for a class diagram.
    /// </summary>
    private static void WriteClassDiagramTrailingComments(StringBuilder sb, ClassDiagramModel model)
    {
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
}
