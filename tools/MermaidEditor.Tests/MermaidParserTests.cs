using Xunit;

namespace MermaidEditor.Tests;

/// <summary>
/// Round-trip tests for MermaidParser and MermaidSerializer.
/// Each test parses Mermaid text, serializes the model back to text,
/// re-parses the serialized text, and compares the two models.
/// </summary>
public class MermaidParserTests
{
    /// <summary>
    /// Parses text, serializes, re-parses, and asserts the two models are structurally equal.
    /// </summary>
    private static void AssertRoundTrip(string input)
    {
        var model1 = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model1);

        var serialized = MermaidSerializer.Serialize(model1);
        Assert.False(string.IsNullOrWhiteSpace(serialized), "Serialized output should not be empty");

        var model2 = MermaidParser.ParseFlowchart(serialized);
        Assert.NotNull(model2);

        // Compare direction
        Assert.Equal(model1.Direction, model2.Direction);

        // Compare diagram keyword
        Assert.Equal(model1.DiagramKeyword, model2.DiagramKeyword);

        // Compare nodes
        Assert.Equal(model1.Nodes.Count, model2.Nodes.Count);
        for (int i = 0; i < model1.Nodes.Count; i++)
        {
            Assert.Equal(model1.Nodes[i].Id, model2.Nodes[i].Id);
            Assert.Equal(model1.Nodes[i].Label, model2.Nodes[i].Label);
            Assert.Equal(model1.Nodes[i].Shape, model2.Nodes[i].Shape);
        }

        // Compare edges
        Assert.Equal(model1.Edges.Count, model2.Edges.Count);
        for (int i = 0; i < model1.Edges.Count; i++)
        {
            Assert.Equal(model1.Edges[i].FromNodeId, model2.Edges[i].FromNodeId);
            Assert.Equal(model1.Edges[i].ToNodeId, model2.Edges[i].ToNodeId);
            Assert.Equal(model1.Edges[i].Label, model2.Edges[i].Label);
            Assert.Equal(model1.Edges[i].Style, model2.Edges[i].Style);
            Assert.Equal(model1.Edges[i].ArrowType, model2.Edges[i].ArrowType);
            Assert.Equal(model1.Edges[i].IsBidirectional, model2.Edges[i].IsBidirectional);
        }

        // Compare subgraphs
        Assert.Equal(model1.Subgraphs.Count, model2.Subgraphs.Count);
        for (int i = 0; i < model1.Subgraphs.Count; i++)
        {
            Assert.Equal(model1.Subgraphs[i].Id, model2.Subgraphs[i].Id);
            Assert.Equal(model1.Subgraphs[i].Label, model2.Subgraphs[i].Label);
            Assert.Equal(model1.Subgraphs[i].NodeIds.Count, model2.Subgraphs[i].NodeIds.Count);
            for (int j = 0; j < model1.Subgraphs[i].NodeIds.Count; j++)
            {
                Assert.Equal(model1.Subgraphs[i].NodeIds[j], model2.Subgraphs[i].NodeIds[j]);
            }
        }

        // Compare styles
        Assert.Equal(model1.Styles.Count, model2.Styles.Count);
        for (int i = 0; i < model1.Styles.Count; i++)
        {
            Assert.Equal(model1.Styles[i].IsClassDef, model2.Styles[i].IsClassDef);
            Assert.Equal(model1.Styles[i].Target, model2.Styles[i].Target);
            Assert.Equal(model1.Styles[i].StyleString, model2.Styles[i].StyleString);
        }

        // Compare class assignments
        Assert.Equal(model1.ClassAssignments.Count, model2.ClassAssignments.Count);
        for (int i = 0; i < model1.ClassAssignments.Count; i++)
        {
            Assert.Equal(model1.ClassAssignments[i].NodeIds, model2.ClassAssignments[i].NodeIds);
            Assert.Equal(model1.ClassAssignments[i].ClassName, model2.ClassAssignments[i].ClassName);
        }
    }

    [Fact]
    public void RoundTrip_BasicFlowchart()
    {
        var input = @"flowchart TD
    A[Start] --> B[Process]
    B --> C[End]";
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_GraphKeyword()
    {
        var input = @"graph LR
    A[Start] --> B[End]";
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_AllDirections()
    {
        foreach (var dir in new[] { "TD", "LR", "BT", "RL" })
        {
            var input = $@"flowchart {dir}
    A[Node A] --> B[Node B]";
            AssertRoundTrip(input);
        }
    }

    [Fact]
    public void RoundTrip_RectangleShape()
    {
        var input = @"flowchart TD
    A[Rectangle Node]";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Single(model.Nodes);
        Assert.Equal(NodeShape.Rectangle, model.Nodes[0].Shape);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_RoundedShape()
    {
        var input = @"flowchart TD
    A(Rounded Node)";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(NodeShape.Rounded, model.Nodes[0].Shape);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_StadiumShape()
    {
        var input = @"flowchart TD
    A([Stadium Node])";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(NodeShape.Stadium, model.Nodes[0].Shape);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_SubroutineShape()
    {
        var input = @"flowchart TD
    A[[Subroutine Node]]";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(NodeShape.Subroutine, model.Nodes[0].Shape);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_CylindricalShape()
    {
        var input = @"flowchart TD
    A[(Database)]";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(NodeShape.Cylindrical, model.Nodes[0].Shape);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_CircleShape()
    {
        var input = @"flowchart TD
    A((Circle Node))";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(NodeShape.Circle, model.Nodes[0].Shape);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_AsymmetricShape()
    {
        var input = @"flowchart TD
    A>Flag Shape]";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(NodeShape.Asymmetric, model.Nodes[0].Shape);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_RhombusShape()
    {
        var input = @"flowchart TD
    A{Diamond}";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(NodeShape.Rhombus, model.Nodes[0].Shape);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_HexagonShape()
    {
        var input = @"flowchart TD
    A{{Hexagon}}";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(NodeShape.Hexagon, model.Nodes[0].Shape);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_ParallelogramShape()
    {
        var input = @"flowchart TD
    A[/Parallelogram/]";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(NodeShape.Parallelogram, model.Nodes[0].Shape);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_TrapezoidShape()
    {
        var input = @"flowchart TD
    A[/Trapezoid\]";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(NodeShape.Trapezoid, model.Nodes[0].Shape);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_DoubleCircleShape()
    {
        var input = @"flowchart TD
    A(((Double Circle)))";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(NodeShape.DoubleCircle, model.Nodes[0].Shape);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_SolidEdge()
    {
        var input = @"flowchart TD
    A --> B";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Single(model.Edges);
        Assert.Equal(EdgeStyle.Solid, model.Edges[0].Style);
        Assert.Equal(ArrowType.Arrow, model.Edges[0].ArrowType);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_DottedEdge()
    {
        var input = @"flowchart TD
    A -.-> B";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Single(model.Edges);
        Assert.Equal(EdgeStyle.Dotted, model.Edges[0].Style);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_ThickEdge()
    {
        var input = @"flowchart TD
    A ==> B";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Single(model.Edges);
        Assert.Equal(EdgeStyle.Thick, model.Edges[0].Style);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_EdgeWithLabel()
    {
        var input = @"flowchart TD
    A -->|Yes| B
    A -->|No| C";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(2, model.Edges.Count);
        Assert.Equal("Yes", model.Edges[0].Label);
        Assert.Equal("No", model.Edges[1].Label);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_OpenEdge()
    {
        var input = @"flowchart TD
    A --- B";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Single(model.Edges);
        Assert.Equal(ArrowType.Open, model.Edges[0].ArrowType);
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_Subgraph()
    {
        var input = @"flowchart TD
    subgraph sg1 [My Subgraph]
        A[Node A]
        B[Node B]
    end
    A --> B";
        AssertRoundTrip(input);

        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Single(model.Subgraphs);
        Assert.Equal("sg1", model.Subgraphs[0].Id);
        Assert.Equal("My Subgraph", model.Subgraphs[0].Label);
        Assert.Equal(2, model.Subgraphs[0].NodeIds.Count);
    }

    [Fact]
    public void RoundTrip_MultipleSubgraphs()
    {
        var input = @"flowchart TD
    subgraph Frontend
        A[Web UI]
        B[Mobile App]
    end
    subgraph Backend
        C[API Gateway]
        D[Auth Service]
    end
    A --> C
    B --> C
    C --> D";
        AssertRoundTrip(input);

        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal(2, model.Subgraphs.Count);
    }

    [Fact]
    public void RoundTrip_StyleDirective()
    {
        var input = @"flowchart TD
    A[Styled Node] --> B[Other]
    style A fill:#f9f,stroke:#333,stroke-width:4px";
        AssertRoundTrip(input);

        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Single(model.Styles);
        Assert.False(model.Styles[0].IsClassDef);
        Assert.Equal("A", model.Styles[0].Target);
    }

    [Fact]
    public void RoundTrip_ClassDefDirective()
    {
        var input = @"flowchart TD
    classDef highlight fill:#ff0,stroke:#333

    A[Node A]:::highlight --> B[Node B]";
        AssertRoundTrip(input);

        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        var classDef = Assert.Single(model.Styles, s => s.IsClassDef);
        Assert.Equal("highlight", classDef.Target);
    }

    [Fact]
    public void RoundTrip_ClassAssignment()
    {
        var input = @"flowchart TD
    classDef important fill:#f00

    A[Node A] --> B[Node B]
    class A important";
        AssertRoundTrip(input);

        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Single(model.ClassAssignments);
        Assert.Equal("important", model.ClassAssignments[0].ClassName);
    }

    [Fact]
    public void RoundTrip_CommentsPreserved()
    {
        var input = @"flowchart TD
    A[Start] --> B[End]

%% This is a trailing comment";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.True(model.Comments.Count > 0, "Comments should be preserved");

        // Verify round-trip preserves comments
        var serialized = MermaidSerializer.Serialize(model);
        Assert.Contains("%%", serialized);

        var model2 = MermaidParser.ParseFlowchart(serialized);
        Assert.NotNull(model2);
        Assert.Equal(model.Comments.Count, model2.Comments.Count);
    }

    [Fact]
    public void RoundTrip_PosDirectives()
    {
        var input = @"flowchart TD
    A[Start] --> B[End]

%% @pos A 100.0,200.0
%% @pos B 300.0,200.0";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);

        var nodeA = model.Nodes.Find(n => n.Id == "A");
        Assert.NotNull(nodeA);
        Assert.True(nodeA.HasManualPosition, "Node A should have manual position set");
        Assert.Equal(100.0, nodeA.Position.X, 1);
        Assert.Equal(200.0, nodeA.Position.Y, 1);

        var nodeB = model.Nodes.Find(n => n.Id == "B");
        Assert.NotNull(nodeB);
        Assert.True(nodeB.HasManualPosition, "Node B should have manual position set");
        Assert.Equal(300.0, nodeB.Position.X, 1);
        Assert.Equal(200.0, nodeB.Position.Y, 1);

        // Verify positions survive round-trip
        var serialized = MermaidSerializer.Serialize(model);
        Assert.Contains("@pos A 100.0,200.0", serialized);
        Assert.Contains("@pos B 300.0,200.0", serialized);

        var model2 = MermaidParser.ParseFlowchart(serialized);
        Assert.NotNull(model2);
        var nodeA2 = model2.Nodes.Find(n => n.Id == "A");
        Assert.NotNull(nodeA2);
        Assert.True(nodeA2.HasManualPosition);
        Assert.Equal(100.0, nodeA2.Position.X, 1);
        Assert.Equal(200.0, nodeA2.Position.Y, 1);
    }

    [Fact]
    public void RoundTrip_StableNodeOrdering()
    {
        var input = @"flowchart TD
    A[First] --> B[Second]
    B --> C[Third]
    C --> D[Fourth]";
        var model1 = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model1);

        var serialized = MermaidSerializer.Serialize(model1);
        var model2 = MermaidParser.ParseFlowchart(serialized);
        Assert.NotNull(model2);

        // Verify node order is preserved
        for (int i = 0; i < model1.Nodes.Count; i++)
        {
            Assert.Equal(model1.Nodes[i].Id, model2.Nodes[i].Id);
        }
    }

    [Fact]
    public void RoundTrip_ComplexDiagram()
    {
        var input = @"flowchart TD
    classDef important fill:#f9f,stroke:#333

    subgraph Frontend [Frontend Layer]
        A[Web UI]
        B([Mobile App])
    end
    subgraph Backend [Backend Services]
        C{API Gateway}
        D[[Auth Service]]
        E((Cache))
    end
    A --> C
    B --> C
    C -->|auth| D
    C -.->|cache| E
    D ==> F[(Database)]
    class C important";
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_SubgraphWithDirection()
    {
        var input = @"flowchart TD
    subgraph sg1 [My Group]
        direction LR
        A[Left] --> B[Right]
    end";
        AssertRoundTrip(input);

        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);
        Assert.Equal("LR", model.Subgraphs[0].Direction);
    }

    [Fact]
    public void Parse_ReturnsNull_ForNonFlowchart()
    {
        var input = "sequenceDiagram\n    Alice->>Bob: Hello";
        var model = MermaidParser.ParseFlowchart(input);
        Assert.Null(model);
    }

    [Fact]
    public void Parse_ReturnsNull_ForEmptyInput()
    {
        Assert.Null(MermaidParser.ParseFlowchart(""));
        Assert.Null(MermaidParser.ParseFlowchart("   "));
    }

    [Fact]
    public void Serialize_ReturnsEmpty_ForNullModel()
    {
        Assert.Equal(string.Empty, MermaidSerializer.Serialize(null!));
    }

    [Fact]
    public void RoundTrip_MixedShapesAndEdges()
    {
        var input = @"flowchart LR
    A[Rectangle] --> B(Rounded)
    B --> C{Diamond}
    C -->|yes| D((Circle))
    C -->|no| E>Flag]
    D --> F[(Database)]";
        AssertRoundTrip(input);
    }

    [Fact]
    public void RoundTrip_SubgraphMembershipPreserved()
    {
        var input = @"flowchart TD
    subgraph sg1 [Group 1]
        A[Node A]
        B[Node B]
    end
    subgraph sg2 [Group 2]
        C[Node C]
    end
    A --> C
    B --> C";
        AssertRoundTrip(input);

        var model = MermaidParser.ParseFlowchart(input);
        Assert.NotNull(model);

        Assert.Contains("A", model.Subgraphs[0].NodeIds);
        Assert.Contains("B", model.Subgraphs[0].NodeIds);
        Assert.Contains("C", model.Subgraphs[1].NodeIds);

        // Verify C is NOT in sg1
        Assert.DoesNotContain("C", model.Subgraphs[0].NodeIds);
    }
}
