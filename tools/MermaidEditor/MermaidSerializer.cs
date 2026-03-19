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

        // Quote labels that contain Mermaid shape delimiter characters to prevent parse errors
        // e.g. "New Node (copy)" inside a rounded shape would produce node3(New Node (copy))
        // which the parser interprets (copy) as nested shape syntax.
        var label = node.Label;
        if (label.IndexOfAny(['(', ')', '[', ']', '{', '}']) >= 0)
        {
            label = $"\"{label}\"";
        }

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

        // Write @pos position comments at the very end
        WriteClassPositionComments(sb, model);

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

    // =============================================
    // State Diagram Serializer (Phase 2.3)
    // =============================================

    /// <summary>
    /// Serializes a StateDiagramModel to valid Mermaid state diagram text.
    /// </summary>
    /// <param name="model">The state diagram model to serialize.</param>
    /// <returns>Valid Mermaid state diagram text.</returns>
    public static string SerializeStateDiagram(StateDiagramModel model)
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
        WriteStateDiagramCommentsBeforeLine(sb, model, model.DeclarationLineIndex);

        // Write the stateDiagram declaration
        sb.AppendLine(model.IsV2 ? "stateDiagram-v2" : "stateDiagram");

        // Write direction if specified
        if (!string.IsNullOrEmpty(model.Direction))
        {
            sb.AppendLine($"{Indent}direction {model.Direction}");
        }

        // Write state definitions (explicit ones with labels, special types, composites)
        foreach (var state in model.States)
        {
            WriteStateDefinition(sb, state, Indent);
        }

        // Write transitions
        foreach (var transition in model.Transitions)
        {
            sb.AppendLine($"{Indent}{FormatStateTransition(transition)}");
        }

        // Write notes
        foreach (var note in model.Notes)
        {
            WriteStateNote(sb, note, Indent);
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

        // Write trailing comments
        WriteStateDiagramTrailingComments(sb, model);

        // Write @pos position comments at the very end
        WriteStatePositionComments(sb, model);

        return sb.ToString().TrimEnd('\r', '\n') + Environment.NewLine;
    }

    /// <summary>
    /// Writes a state definition, including composite states with nested content.
    /// </summary>
    private static void WriteStateDefinition(StringBuilder sb, StateDefinition state, string indent)
    {
        // Special state types (fork, join, choice)
        if (state.Type == StateType.Fork || state.Type == StateType.Join || state.Type == StateType.Choice)
        {
            var typeStr = state.Type switch
            {
                StateType.Fork => "fork",
                StateType.Join => "join",
                StateType.Choice => "choice",
                _ => "fork"
            };
            sb.AppendLine($"{indent}state {state.Id} <<{typeStr}>>");
            return;
        }

        // Composite state
        if (state.Type == StateType.Composite)
        {
            // Only emit composite block syntax if there are nested states or transitions.
            // An empty composite block (state X { }) causes Mermaid "roundedWithTitle" error.
            bool hasContent = state.NestedStates.Count > 0 || state.NestedTransitions.Count > 0;
            if (hasContent)
            {
                if (!string.IsNullOrEmpty(state.Label))
                {
                    sb.AppendLine($"{indent}state \"{state.Label}\" as {state.Id} {{");
                }
                else
                {
                    sb.AppendLine($"{indent}state {state.Id} {{");
                }

                var innerIndent = indent + Indent;

                // Write nested states
                foreach (var nested in state.NestedStates)
                {
                    WriteStateDefinition(sb, nested, innerIndent);
                }

                // Write nested transitions
                foreach (var transition in state.NestedTransitions)
                {
                    sb.AppendLine($"{innerIndent}{FormatStateTransition(transition)}");
                }

                sb.AppendLine($"{indent}}}");
                return;
            }

            // Empty composite — fall through to simple state declaration below
            // so it renders as a labeled state until content is added
        }

        // Simple state with label (using "as" syntax or colon syntax)
        if (!string.IsNullOrEmpty(state.Label) && state.IsExplicit)
        {
            sb.AppendLine($"{indent}state \"{state.Label}\" as {state.Id}");
            return;
        }

        // Simple state without label — only write if explicitly declared
        // (states inferred from transitions don't need explicit declarations)
        // Skip writing — the state will appear in transitions
    }

    /// <summary>
    /// Formats a state transition for serialization.
    /// </summary>
    private static string FormatStateTransition(StateTransition transition)
    {
        var sb = new StringBuilder();
        sb.Append(transition.FromId);
        sb.Append(" --> ");
        sb.Append(transition.ToId);

        if (!string.IsNullOrEmpty(transition.Label))
        {
            sb.Append($" : {transition.Label}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes a state note for serialization.
    /// Handles both single-line and multi-line notes.
    /// </summary>
    private static void WriteStateNote(StringBuilder sb, StateNote note, string indent)
    {
        var posStr = note.Position == StateNotePosition.LeftOf ? "left" : "right";

        if (note.Text.Contains('\n'))
        {
            // Multi-line note
            sb.AppendLine($"{indent}note {posStr} of {note.StateId}");
            foreach (var line in note.Text.Split('\n'))
            {
                sb.AppendLine($"{indent}{Indent}{line}");
            }
            sb.AppendLine($"{indent}end note");
        }
        else
        {
            // Single-line note
            sb.AppendLine($"{indent}note {posStr} of {note.StateId} : {note.Text}");
        }
    }

    /// <summary>
    /// Writes comments that appeared before a given line in the state diagram.
    /// </summary>
    private static void WriteStateDiagramCommentsBeforeLine(StringBuilder sb, StateDiagramModel model, int lineIndex)
    {
        foreach (var comment in model.Comments.Where(c => c.OriginalLineIndex < lineIndex))
        {
            sb.AppendLine($"%%{comment.Text}");
        }
    }

    /// <summary>
    /// Writes trailing comments for a state diagram.
    /// </summary>
    private static void WriteStateDiagramTrailingComments(StringBuilder sb, StateDiagramModel model)
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

    /// <summary>
    /// Writes %% @pos comments for states that have been manually positioned in the visual editor.
    /// Recursively collects from nested composite states.
    /// Format: %% @pos stateId x,y or %% @pos stateId x,y,w,h (when size is saved).
    /// </summary>
    private static void WriteStatePositionComments(StringBuilder sb, StateDiagramModel model)
    {
        var positioned = new List<StateDefinition>();
        foreach (var state in model.States)
        {
            CollectPositionedStates(state, positioned);
        }

        bool hasAny = positioned.Count > 0 || model.PseudoNodePositions.Count > 0 || model.NotePositions.Count > 0;
        if (!hasAny) return;

        sb.AppendLine();

        // Write real state positions
        foreach (var state in positioned)
        {
            var x = state.Position.X.ToString("F1", CultureInfo.InvariantCulture);
            var y = state.Position.Y.ToString("F1", CultureInfo.InvariantCulture);
            if (state.Size.Width > 0 && state.Size.Height > 0)
            {
                var w = state.Size.Width.ToString("F1", CultureInfo.InvariantCulture);
                var h = state.Size.Height.ToString("F1", CultureInfo.InvariantCulture);
                sb.AppendLine($"%% @pos {state.Id} {x},{y},{w},{h}");
            }
            else
            {
                sb.AppendLine($"%% @pos {state.Id} {x},{y}");
            }
        }

        // Write pseudo-node positions ([*]_start, [*]_end, [*]_start_ParentId, etc.)
        foreach (var (pseudoId, pos) in model.PseudoNodePositions.OrderBy(p => p.Key))
        {
            var x = pos.X.ToString("F1", CultureInfo.InvariantCulture);
            var y = pos.Y.ToString("F1", CultureInfo.InvariantCulture);
            sb.AppendLine($"%% @pos {pseudoId} {x},{y}");
        }

        // Write note positions (note_0, note_1, etc.)
        foreach (var (noteKey, pos) in model.NotePositions.OrderBy(p => p.Key))
        {
            var x = pos.X.ToString("F1", CultureInfo.InvariantCulture);
            var y = pos.Y.ToString("F1", CultureInfo.InvariantCulture);
            sb.AppendLine($"%% @pos {noteKey} {x},{y}");
        }
    }

    /// <summary>
    /// Recursively collects states with HasManualPosition set.
    /// </summary>
    private static void CollectPositionedStates(StateDefinition state, List<StateDefinition> result)
    {
        if (state.HasManualPosition)
            result.Add(state);
        foreach (var nested in state.NestedStates)
        {
            CollectPositionedStates(nested, result);
        }
    }

    // =============================================
    // ER Diagram Serializer (Phase 2.4)
    // =============================================

    /// <summary>
    /// Serializes an ERDiagramModel to valid Mermaid ER diagram text.
    /// </summary>
    /// <param name="model">The ER diagram model to serialize.</param>
    /// <returns>Valid Mermaid ER diagram text.</returns>
    public static string SerializeERDiagram(ERDiagramModel model)
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
        WriteERDiagramCommentsBeforeLine(sb, model, model.DeclarationLineIndex);

        // Write the erDiagram declaration
        sb.AppendLine("erDiagram");

        // Write entity definitions with attributes
        foreach (var entity in model.Entities.Where(e => e.Attributes.Count > 0))
        {
            sb.AppendLine($"{Indent}{entity.Name} {{");
            foreach (var attr in entity.Attributes)
            {
                sb.Append($"{Indent}{Indent}{attr.Type} {attr.Name}");
                if (!string.IsNullOrEmpty(attr.Key))
                {
                    sb.Append($" {attr.Key}");
                }
                if (!string.IsNullOrEmpty(attr.Comment))
                {
                    sb.Append($" \"{attr.Comment}\"");
                }
                sb.AppendLine();
            }
            sb.AppendLine($"{Indent}}}");
        }

        // Write relationships
        foreach (var rel in model.Relationships)
        {
            sb.AppendLine($"{Indent}{FormatERRelationship(rel)}");
        }

        // Write trailing comments
        WriteERDiagramTrailingComments(sb, model);

        // Write @pos position comments at the very end
        WriteERPositionComments(sb, model);

        return sb.ToString().TrimEnd('\r', '\n') + Environment.NewLine;
    }

    /// <summary>
    /// Formats an ER relationship for serialization.
    /// </summary>
    private static string FormatERRelationship(ERRelationship rel)
    {
        var sb = new StringBuilder();
        sb.Append(rel.FromEntity);
        sb.Append(' ');
        sb.Append(FormatERCardinality(rel.LeftCardinality, isRightSide: false));
        sb.Append(rel.IsIdentifying ? "--" : "..");
        sb.Append(FormatERCardinality(rel.RightCardinality, isRightSide: true));
        sb.Append(' ');
        sb.Append(rel.ToEntity);

        if (!string.IsNullOrEmpty(rel.Label))
        {
            sb.Append($" : {rel.Label}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a cardinality enum value to its Mermaid string representation.
    /// Left side uses: ||, |o, }o, }|  (read left-to-right toward the line)
    /// Right side uses mirrored forms: ||, o|, o{, |{  (read right-to-left toward the line)
    /// </summary>
    private static string FormatERCardinality(ERCardinality cardinality, bool isRightSide)
    {
        if (isRightSide)
        {
            return cardinality switch
            {
                ERCardinality.ExactlyOne => "||",
                ERCardinality.ZeroOrOne => "o|",
                ERCardinality.ZeroOrMore => "o{",
                ERCardinality.OneOrMore => "|{",
                _ => "||"
            };
        }
        return cardinality switch
        {
            ERCardinality.ExactlyOne => "||",
            ERCardinality.ZeroOrOne => "|o",
            ERCardinality.ZeroOrMore => "}o",
            ERCardinality.OneOrMore => "}|",
            _ => "||"
        };
    }

    /// <summary>
    /// Writes comments that appeared before a given line in the ER diagram.
    /// </summary>
    private static void WriteERDiagramCommentsBeforeLine(StringBuilder sb, ERDiagramModel model, int lineIndex)
    {
        foreach (var comment in model.Comments.Where(c => c.OriginalLineIndex < lineIndex))
        {
            sb.AppendLine($"%%{comment.Text}");
        }
    }

    /// <summary>
    /// Writes trailing comments for an ER diagram.
    /// </summary>
    private static void WriteERDiagramTrailingComments(StringBuilder sb, ERDiagramModel model)
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

    /// <summary>
    /// Writes %% @pos comments for classes that have been manually positioned in the visual editor.
    /// </summary>
    private static void WriteClassPositionComments(StringBuilder sb, ClassDiagramModel model)
    {
        var positioned = model.Classes.Where(c => c.HasManualPosition).ToList();
        if (positioned.Count == 0) return;

        sb.AppendLine();
        foreach (var cls in positioned)
        {
            var x = cls.Position.X.ToString("F1", CultureInfo.InvariantCulture);
            var y = cls.Position.Y.ToString("F1", CultureInfo.InvariantCulture);
            sb.AppendLine($"%% @pos {cls.Id} {x},{y}");
        }
    }

    /// <summary>
    /// Writes %% @pos comments for ER entities that have been manually positioned in the visual editor.
    /// </summary>
    private static void WriteERPositionComments(StringBuilder sb, ERDiagramModel model)
    {
        var positioned = model.Entities.Where(e => e.HasManualPosition).ToList();
        if (positioned.Count == 0) return;

        sb.AppendLine();
        foreach (var entity in positioned)
        {
            var x = entity.Position.X.ToString("F1", CultureInfo.InvariantCulture);
            var y = entity.Position.Y.ToString("F1", CultureInfo.InvariantCulture);
            sb.AppendLine($"%% @pos {entity.Name} {x},{y}");
        }
    }

    // =============================================
    // Gantt Chart Serializer (Phase 4)
    // =============================================

    /// <summary>
    /// Serializes a GanttModel to valid Mermaid gantt chart text.
    /// </summary>
    public static string SerializeGantt(GanttModel model)
    {
        if (model == null)
            return string.Empty;

        var sb = new StringBuilder();

        // Write preamble lines
        foreach (var preambleLine in model.PreambleLines)
        {
            sb.AppendLine(preambleLine);
        }

        // Write comments before declaration
        WriteGanttCommentsBeforeLine(sb, model, model.DeclarationLineIndex);

        // Write gantt declaration
        sb.AppendLine("gantt");

        // Write title
        if (!string.IsNullOrEmpty(model.Title))
        {
            sb.AppendLine($"{Indent}title {model.Title}");
        }

        // Write date format
        sb.AppendLine($"{Indent}dateFormat {model.DateFormat}");

        // Write axis format
        if (!string.IsNullOrEmpty(model.AxisFormat))
        {
            sb.AppendLine($"{Indent}axisFormat {model.AxisFormat}");
        }

        // Write excludes
        if (!string.IsNullOrEmpty(model.Excludes))
        {
            sb.AppendLine($"{Indent}excludes {model.Excludes}");
        }

        // Write top-level tasks (before any section), aligned
        if (model.Tasks.Count > 0)
        {
            WriteAlignedGanttTasks(sb, model.Tasks);
        }

        // Write sections with their tasks, aligned within each section
        foreach (var section in model.Sections)
        {
            sb.AppendLine();
            sb.AppendLine($"{Indent}section {section.Name}");
            WriteAlignedGanttTasks(sb, section.Tasks);
        }

        // Write trailing comments
        WriteGanttTrailingComments(sb, model);

        return sb.ToString().TrimEnd('\r', '\n') + Environment.NewLine;
    }

    /// <summary>
    /// Formats a gantt task as a Mermaid string.
    /// Format: label :metadata (e.g., "Task 1 :done, task1, 2024-01-01, 2024-01-15")
    /// </summary>
    private static string FormatGanttTask(GanttTask task)
    {
        var parts = new List<string>();

        // Add tags (done, active, crit, milestone)
        parts.AddRange(task.Tags);

        // Add ID if present
        if (!string.IsNullOrEmpty(task.Id))
            parts.Add(task.Id);

        // Add start date
        if (!string.IsNullOrEmpty(task.StartDate))
            parts.Add(task.StartDate);

        // Add end date/duration
        if (!string.IsNullOrEmpty(task.EndDate))
            parts.Add(task.EndDate);

        return $"{task.Label} :{string.Join(", ", parts)}";
    }

    /// <summary>
    /// Writes a list of gantt tasks with aligned colon separators for readability.
    /// E.g., "Requirements gathering :a1, 2024-01-01, 7d"
    ///        "Design phase           :a2, after a1, 10d"
    /// </summary>
    private static void WriteAlignedGanttTasks(StringBuilder sb, List<GanttTask> tasks)
    {
        if (tasks.Count == 0) return;

        // Build the metadata part for each task (everything after the colon)
        var taskParts = tasks.Select(t =>
        {
            var parts = new List<string>();
            parts.AddRange(t.Tags);
            if (!string.IsNullOrEmpty(t.Id)) parts.Add(t.Id);
            if (!string.IsNullOrEmpty(t.StartDate)) parts.Add(t.StartDate);
            if (!string.IsNullOrEmpty(t.EndDate)) parts.Add(t.EndDate);
            return new { Label = t.Label, Meta = string.Join(", ", parts) };
        }).ToList();

        // Find the longest label to pad the others
        var maxLabelLen = taskParts.Max(tp => tp.Label.Length);

        foreach (var tp in taskParts)
        {
            var paddedLabel = tp.Label.PadRight(maxLabelLen);
            sb.AppendLine($"{Indent}{paddedLabel} :{tp.Meta}");
        }
    }

    private static void WriteGanttCommentsBeforeLine(StringBuilder sb, GanttModel model, int lineIndex)
    {
        foreach (var comment in model.Comments.Where(c => c.OriginalLineIndex < lineIndex))
        {
            sb.AppendLine($"%%{comment.Text}");
        }
    }

    private static void WriteGanttTrailingComments(StringBuilder sb, GanttModel model)
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
    // Mind Map Serializer (Phase 4)
    // =============================================

    /// <summary>
    /// Serializes a MindMapModel to valid Mermaid mind map text.
    /// </summary>
    public static string SerializeMindMap(MindMapModel model)
    {
        if (model == null)
            return string.Empty;

        var sb = new StringBuilder();

        // Write preamble lines
        foreach (var preambleLine in model.PreambleLines)
        {
            sb.AppendLine(preambleLine);
        }

        // Write comments before declaration
        WriteMindMapCommentsBeforeLine(sb, model, model.DeclarationLineIndex);

        // Write mindmap declaration
        sb.AppendLine("mindmap");

        // Write tree recursively with indentation
        WriteMindMapNode(sb, model.Root, Indent);

        // Write trailing comments
        WriteMindMapTrailingComments(sb, model);

        return sb.ToString().TrimEnd('\r', '\n') + Environment.NewLine;
    }

    /// <summary>
    /// Recursively writes a mind map node and its children.
    /// </summary>
    private static void WriteMindMapNode(StringBuilder sb, MindMapNode node, string indent)
    {
        // Format node with shape
        var formattedText = node.Shape switch
        {
            MindMapNodeShape.Square => $"[{node.Label}]",
            MindMapNodeShape.Rounded => $"({node.Label})",
            MindMapNodeShape.Circle => $"(({node.Label}))",
            MindMapNodeShape.Bang => $")){node.Label}((",
            MindMapNodeShape.Cloud => $"){node.Label}(",
            MindMapNodeShape.Hexagon => $"{{{{{node.Label}}}}}",
            _ => node.Label
        };

        sb.AppendLine($"{indent}{formattedText}");

        // Write icon if present
        if (!string.IsNullOrEmpty(node.Icon))
        {
            sb.AppendLine($"{indent}{Indent}::icon({node.Icon})");
        }

        // Write CSS class if present
        if (!string.IsNullOrEmpty(node.CssClass))
        {
            sb.AppendLine($"{indent}{Indent}:::{node.CssClass}");
        }

        // Write children recursively with increased indentation
        foreach (var child in node.Children)
        {
            WriteMindMapNode(sb, child, indent + Indent);
        }
    }

    private static void WriteMindMapCommentsBeforeLine(StringBuilder sb, MindMapModel model, int lineIndex)
    {
        foreach (var comment in model.Comments.Where(c => c.OriginalLineIndex < lineIndex))
        {
            sb.AppendLine($"%%{comment.Text}");
        }
    }

    private static void WriteMindMapTrailingComments(StringBuilder sb, MindMapModel model)
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
    // Pie Chart Serializer (Phase 4)
    // =============================================

    /// <summary>
    /// Serializes a PieChartModel to valid Mermaid pie chart text.
    /// </summary>
    public static string SerializePieChart(PieChartModel model)
    {
        if (model == null)
            return string.Empty;

        var sb = new StringBuilder();

        // Write preamble lines
        foreach (var preambleLine in model.PreambleLines)
        {
            sb.AppendLine(preambleLine);
        }

        // Write comments before declaration
        WritePieChartCommentsBeforeLine(sb, model, model.DeclarationLineIndex);

        // Write pie declaration
        if (model.ShowData)
            sb.AppendLine("pie showData");
        else
            sb.AppendLine("pie");

        // Write title
        if (!string.IsNullOrEmpty(model.Title))
        {
            sb.AppendLine($"{Indent}title {model.Title}");
        }

        // Write slices
        foreach (var slice in model.Slices)
        {
            var valueStr = slice.Value.ToString("G", CultureInfo.InvariantCulture);
            sb.AppendLine($"{Indent}\"{slice.Label}\" : {valueStr}");
        }

        // Write trailing comments
        WritePieChartTrailingComments(sb, model);

        return sb.ToString().TrimEnd('\r', '\n') + Environment.NewLine;
    }

    private static void WritePieChartCommentsBeforeLine(StringBuilder sb, PieChartModel model, int lineIndex)
    {
        foreach (var comment in model.Comments.Where(c => c.OriginalLineIndex < lineIndex))
        {
            sb.AppendLine($"%%{comment.Text}");
        }
    }

    private static void WritePieChartTrailingComments(StringBuilder sb, PieChartModel model)
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
