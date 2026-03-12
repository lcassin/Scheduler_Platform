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

    // =============================================
    // Class Diagram Tests (Phase 2.2)
    // =============================================

    /// <summary>
    /// Round-trip helper for class diagrams: parse → serialize → re-parse → compare models.
    /// </summary>
    private static void AssertClassDiagramRoundTrip(string input)
    {
        var model1 = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model1);

        var serialized = MermaidSerializer.SerializeClassDiagram(model1);
        Assert.False(string.IsNullOrWhiteSpace(serialized), "Serialized output should not be empty");

        var model2 = MermaidParser.ParseClassDiagram(serialized);
        Assert.NotNull(model2);

        // Compare direction
        Assert.Equal(model1.Direction, model2.Direction);

        // Compare classes
        Assert.Equal(model1.Classes.Count, model2.Classes.Count);
        for (int i = 0; i < model1.Classes.Count; i++)
        {
            Assert.Equal(model1.Classes[i].Id, model2.Classes[i].Id);
            Assert.Equal(model1.Classes[i].Annotation, model2.Classes[i].Annotation);
            Assert.Equal(model1.Classes[i].GenericType, model2.Classes[i].GenericType);
            Assert.Equal(model1.Classes[i].Members.Count, model2.Classes[i].Members.Count);
            for (int j = 0; j < model1.Classes[i].Members.Count; j++)
            {
                Assert.Equal(model1.Classes[i].Members[j].RawText, model2.Classes[i].Members[j].RawText);
                Assert.Equal(model1.Classes[i].Members[j].IsMethod, model2.Classes[i].Members[j].IsMethod);
                Assert.Equal(model1.Classes[i].Members[j].Visibility, model2.Classes[i].Members[j].Visibility);
            }
        }

        // Compare relationships
        Assert.Equal(model1.Relationships.Count, model2.Relationships.Count);
        for (int i = 0; i < model1.Relationships.Count; i++)
        {
            Assert.Equal(model1.Relationships[i].FromId, model2.Relationships[i].FromId);
            Assert.Equal(model1.Relationships[i].ToId, model2.Relationships[i].ToId);
            Assert.Equal(model1.Relationships[i].LeftEnd, model2.Relationships[i].LeftEnd);
            Assert.Equal(model1.Relationships[i].RightEnd, model2.Relationships[i].RightEnd);
            Assert.Equal(model1.Relationships[i].LinkStyle, model2.Relationships[i].LinkStyle);
            Assert.Equal(model1.Relationships[i].Label, model2.Relationships[i].Label);
            Assert.Equal(model1.Relationships[i].FromCardinality, model2.Relationships[i].FromCardinality);
            Assert.Equal(model1.Relationships[i].ToCardinality, model2.Relationships[i].ToCardinality);
        }

        // Compare namespaces
        Assert.Equal(model1.Namespaces.Count, model2.Namespaces.Count);
        for (int i = 0; i < model1.Namespaces.Count; i++)
        {
            Assert.Equal(model1.Namespaces[i].Name, model2.Namespaces[i].Name);
            Assert.Equal(model1.Namespaces[i].ClassIds.Count, model2.Namespaces[i].ClassIds.Count);
        }

        // Compare notes
        Assert.Equal(model1.Notes.Count, model2.Notes.Count);
        for (int i = 0; i < model1.Notes.Count; i++)
        {
            Assert.Equal(model1.Notes[i].Text, model2.Notes[i].Text);
            Assert.Equal(model1.Notes[i].ForClass, model2.Notes[i].ForClass);
        }

        // Compare styles
        Assert.Equal(model1.Styles.Count, model2.Styles.Count);
        for (int i = 0; i < model1.Styles.Count; i++)
        {
            Assert.Equal(model1.Styles[i].IsClassDef, model2.Styles[i].IsClassDef);
            Assert.Equal(model1.Styles[i].Target, model2.Styles[i].Target);
            Assert.Equal(model1.Styles[i].StyleString, model2.Styles[i].StyleString);
        }
    }

    [Fact]
    public void ClassDiagram_BasicParsing()
    {
        var input = @"classDiagram
    class Animal
    Animal : +int age
    Animal : +String gender
    Animal: +isMammal()
    Animal: +mate()";
        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Single(model.Classes);
        Assert.Equal("Animal", model.Classes[0].Id);
        Assert.Equal(4, model.Classes[0].Members.Count);

        // Check visibility
        Assert.Equal(MemberVisibility.Public, model.Classes[0].Members[0].Visibility);

        // Check method detection
        Assert.False(model.Classes[0].Members[0].IsMethod); // +int age
        Assert.False(model.Classes[0].Members[1].IsMethod); // +String gender
        Assert.True(model.Classes[0].Members[2].IsMethod);  // +isMammal()
        Assert.True(model.Classes[0].Members[3].IsMethod);  // +mate()
    }

    [Fact]
    public void ClassDiagram_RoundTrip_BasicClasses()
    {
        var input = @"classDiagram
    class Animal
    class Duck{
        +String beakColor
        +swim()
        +quack()
    }
    class Fish{
        -int sizeInFeet
        -canEat()
    }";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Equal(3, model.Classes.Count);
        Assert.Equal("Animal", model.Classes[0].Id);
        Assert.Equal("Duck", model.Classes[1].Id);
        Assert.Equal(3, model.Classes[1].Members.Count);
        Assert.Equal(MemberVisibility.Public, model.Classes[1].Members[0].Visibility);
        Assert.Equal(MemberVisibility.Private, model.Classes[2].Members[0].Visibility);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_AllRelationshipTypes()
    {
        var input = @"classDiagram
    classA <|-- classB
    classC *-- classD
    classE o-- classF
    classG <-- classH
    classI -- classJ
    classK <.. classL
    classM <|.. classN
    classO .. classP";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Equal(8, model.Relationships.Count);

        // Inheritance: <|--
        Assert.Equal(ClassRelationEnd.Inheritance, model.Relationships[0].LeftEnd);
        Assert.Equal(ClassRelationEnd.None, model.Relationships[0].RightEnd);
        Assert.Equal(ClassLinkStyle.Solid, model.Relationships[0].LinkStyle);

        // Composition: *--
        Assert.Equal(ClassRelationEnd.Composition, model.Relationships[1].LeftEnd);
        Assert.Equal(ClassLinkStyle.Solid, model.Relationships[1].LinkStyle);

        // Aggregation: o--
        Assert.Equal(ClassRelationEnd.Aggregation, model.Relationships[2].LeftEnd);
        Assert.Equal(ClassLinkStyle.Solid, model.Relationships[2].LinkStyle);

        // Association: <--
        Assert.Equal(ClassRelationEnd.Arrow, model.Relationships[3].LeftEnd);
        Assert.Equal(ClassLinkStyle.Solid, model.Relationships[3].LinkStyle);

        // Solid link: --
        Assert.Equal(ClassRelationEnd.None, model.Relationships[4].LeftEnd);
        Assert.Equal(ClassRelationEnd.None, model.Relationships[4].RightEnd);
        Assert.Equal(ClassLinkStyle.Solid, model.Relationships[4].LinkStyle);

        // Dashed dependency: <..
        Assert.Equal(ClassRelationEnd.Arrow, model.Relationships[5].LeftEnd);
        Assert.Equal(ClassLinkStyle.Dashed, model.Relationships[5].LinkStyle);

        // Dashed realization: <|..
        Assert.Equal(ClassRelationEnd.Inheritance, model.Relationships[6].LeftEnd);
        Assert.Equal(ClassLinkStyle.Dashed, model.Relationships[6].LinkStyle);

        // Dashed link: ..
        Assert.Equal(ClassRelationEnd.None, model.Relationships[7].LeftEnd);
        Assert.Equal(ClassRelationEnd.None, model.Relationships[7].RightEnd);
        Assert.Equal(ClassLinkStyle.Dashed, model.Relationships[7].LinkStyle);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_RelationshipsWithLabels()
    {
        var input = @"classDiagram
    classA --|> classB : Inheritance
    classC --* classD : Composition
    classE --o classF : Aggregation";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Equal("Inheritance", model.Relationships[0].Label);
        Assert.Equal("Composition", model.Relationships[1].Label);
        Assert.Equal("Aggregation", model.Relationships[2].Label);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_Cardinality()
    {
        var input = @"classDiagram
    Customer ""1"" --> ""*"" Ticket
    Student ""1"" --> ""1..*"" Course";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Equal(2, model.Relationships.Count);
        Assert.Equal("1", model.Relationships[0].FromCardinality);
        Assert.Equal("*", model.Relationships[0].ToCardinality);
        Assert.Equal("1", model.Relationships[1].FromCardinality);
        Assert.Equal("1..*", model.Relationships[1].ToCardinality);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_Annotations()
    {
        var input = @"classDiagram
    class Shape{
        <<interface>>
        noOfVertices
        draw()
    }
    class Color{
        <<enumeration>>
        RED
        BLUE
        GREEN
    }";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Equal("interface", model.Classes[0].Annotation);
        Assert.Equal("enumeration", model.Classes[1].Annotation);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_VisibilityModifiers()
    {
        var input = @"classDiagram
    class MyClass{
        +publicField
        -privateField
        #protectedField
        ~packageField
        +publicMethod()
        -privateMethod()
        #protectedMethod()
        ~packageMethod()
    }";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        var members = model.Classes[0].Members;
        Assert.Equal(8, members.Count);
        Assert.Equal(MemberVisibility.Public, members[0].Visibility);
        Assert.Equal(MemberVisibility.Private, members[1].Visibility);
        Assert.Equal(MemberVisibility.Protected, members[2].Visibility);
        Assert.Equal(MemberVisibility.Package, members[3].Visibility);
        Assert.False(members[0].IsMethod);
        Assert.True(members[4].IsMethod);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_ReturnTypes()
    {
        var input = @"classDiagram
    class BankAccount{
        +String owner
        +BigDecimal balance
        +deposit(amount) bool
        +withdrawal(amount) int
    }";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        var members = model.Classes[0].Members;
        Assert.True(members[2].IsMethod);
        Assert.Equal("bool", members[2].Type);
        Assert.True(members[3].IsMethod);
        Assert.Equal("int", members[3].Type);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_Direction()
    {
        var input = @"classDiagram
    direction RL
    class Student{
        -idCard : IdCard
    }
    class IdCard{
        -id : int
        -name : string
    }
    Student ""1"" --o ""1"" IdCard : carries";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Equal("RL", model.Direction);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_Namespace()
    {
        var input = @"classDiagram
    namespace BaseShapes {
        class Triangle
        class Rectangle{
            double width
            double height
        }
    }";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Single(model.Namespaces);
        Assert.Equal("BaseShapes", model.Namespaces[0].Name);
        Assert.Equal(2, model.Namespaces[0].ClassIds.Count);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_Notes()
    {
        var input = @"classDiagram
    note ""This is a general note""
    note for MyClass ""This is a note for a class""
    class MyClass{
    }";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Equal(2, model.Notes.Count);
        Assert.Null(model.Notes[0].ForClass);
        Assert.Equal("This is a general note", model.Notes[0].Text);
        Assert.Equal("MyClass", model.Notes[1].ForClass);
        Assert.Equal("This is a note for a class", model.Notes[1].Text);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_Styling()
    {
        var input = @"classDiagram
    class Animal
    class Mineral
    style Animal fill:#f9f,stroke:#333,stroke-width:4px
    classDef important fill:#f00";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Equal(2, model.Styles.Count);
        Assert.False(model.Styles[0].IsClassDef);
        Assert.Equal("Animal", model.Styles[0].Target);
        Assert.True(model.Styles[1].IsClassDef);
        Assert.Equal("important", model.Styles[1].Target);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_ComplexDiagram()
    {
        var input = @"classDiagram
    note ""From Duck till Zebra""
    Animal <|-- Duck
    note for Duck ""can fly""
    Animal <|-- Fish
    Animal <|-- Zebra
    Animal : +int age
    Animal : +String gender
    Animal: +isMammal()
    Animal: +mate()
    class Duck{
        +String beakColor
        +swim()
        +quack()
    }
    class Fish{
        -int sizeInFeet
        -canEat()
    }
    class Zebra{
        +bool is_wild
        +run()
    }";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Equal(4, model.Classes.Count); // Animal, Duck, Fish, Zebra
        Assert.Equal(3, model.Relationships.Count); // 3 inheritance relationships
        Assert.Equal(2, model.Notes.Count);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_PreamblePreserved()
    {
        var input = @"---
title: Animal example
---
classDiagram
    class Animal
    class Duck";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.True(model.PreambleLines.Count > 0, "Preamble should be preserved");
    }

    [Fact]
    public void ClassDiagram_RoundTrip_Classifiers()
    {
        var input = @"classDiagram
    class MyClass{
        +someAbstractMethod()*
        +someStaticMethod()$
        String someField$
    }";
        AssertClassDiagramRoundTrip(input);

        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        var members = model.Classes[0].Members;
        Assert.Equal(MemberClassifier.Abstract, members[0].Classifier);
        Assert.Equal(MemberClassifier.Static, members[1].Classifier);
        Assert.Equal(MemberClassifier.Static, members[2].Classifier);
    }

    [Fact]
    public void ClassDiagram_Parse_ReturnsNull_ForNonClassDiagram()
    {
        var input = "flowchart TD\n    A --> B";
        var model = MermaidParser.ParseClassDiagram(input);
        Assert.Null(model);
    }

    [Fact]
    public void ClassDiagram_Parse_ReturnsNull_ForEmptyInput()
    {
        Assert.Null(MermaidParser.ParseClassDiagram(""));
        Assert.Null(MermaidParser.ParseClassDiagram("   "));
    }

    [Fact]
    public void ClassDiagram_Serialize_ReturnsEmpty_ForNullModel()
    {
        Assert.Equal(string.Empty, MermaidSerializer.SerializeClassDiagram(null!));
    }

    [Fact]
    public void ClassDiagram_RoundTrip_SeparateAnnotation()
    {
        var input = @"classDiagram
    class Shape
    <<interface>> Shape
    Shape : noOfVertices
    Shape : draw()";
        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Equal("interface", model.Classes[0].Annotation);
        Assert.Equal(2, model.Classes[0].Members.Count);
    }

    [Fact]
    public void ClassDiagram_RoundTrip_CssClass()
    {
        var input = @"classDiagram
    class Animal:::someclass
    classDef someclass fill:#f96";
        var model = MermaidParser.ParseClassDiagram(input);
        Assert.NotNull(model);
        Assert.Equal("someclass", model.Classes[0].CssClass);
        Assert.Single(model.Styles);
    }

    // =============================================
    // State Diagram Tests (Phase 2.3)
    // =============================================

    /// <summary>
    /// Helper method to verify state diagram round-trip fidelity.
    /// Parses text -> serializes -> re-parses -> compares models.
    /// </summary>
    private static void AssertStateDiagramRoundTrip(string input)
    {
        var model1 = MermaidParser.ParseStateDiagram(input);
        Assert.NotNull(model1);

        var serialized = MermaidSerializer.SerializeStateDiagram(model1);
        Assert.False(string.IsNullOrWhiteSpace(serialized), "Serialized output should not be empty");

        var model2 = MermaidParser.ParseStateDiagram(serialized);
        Assert.NotNull(model2);

        // Compare direction
        Assert.Equal(model1.Direction, model2.Direction);

        // Compare IsV2
        Assert.Equal(model1.IsV2, model2.IsV2);

        // Compare states
        Assert.Equal(model1.States.Count, model2.States.Count);
        for (int i = 0; i < model1.States.Count; i++)
        {
            AssertStateDefinitionEqual(model1.States[i], model2.States[i]);
        }

        // Compare transitions
        Assert.Equal(model1.Transitions.Count, model2.Transitions.Count);
        for (int i = 0; i < model1.Transitions.Count; i++)
        {
            Assert.Equal(model1.Transitions[i].FromId, model2.Transitions[i].FromId);
            Assert.Equal(model1.Transitions[i].ToId, model2.Transitions[i].ToId);
            Assert.Equal(model1.Transitions[i].Label, model2.Transitions[i].Label);
        }

        // Compare notes
        Assert.Equal(model1.Notes.Count, model2.Notes.Count);
        for (int i = 0; i < model1.Notes.Count; i++)
        {
            Assert.Equal(model1.Notes[i].StateId, model2.Notes[i].StateId);
            Assert.Equal(model1.Notes[i].Position, model2.Notes[i].Position);
            Assert.Equal(model1.Notes[i].Text, model2.Notes[i].Text);
        }

        // Compare styles
        Assert.Equal(model1.Styles.Count, model2.Styles.Count);
        for (int i = 0; i < model1.Styles.Count; i++)
        {
            Assert.Equal(model1.Styles[i].Target, model2.Styles[i].Target);
            Assert.Equal(model1.Styles[i].StyleString, model2.Styles[i].StyleString);
            Assert.Equal(model1.Styles[i].IsClassDef, model2.Styles[i].IsClassDef);
        }

        // Compare preamble
        Assert.Equal(model1.PreambleLines.Count, model2.PreambleLines.Count);
        for (int i = 0; i < model1.PreambleLines.Count; i++)
        {
            Assert.Equal(model1.PreambleLines[i], model2.PreambleLines[i]);
        }
    }

    /// <summary>
    /// Recursively compares two state definitions for equality.
    /// </summary>
    private static void AssertStateDefinitionEqual(StateDefinition expected, StateDefinition actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Label, actual.Label);
        Assert.Equal(expected.Type, actual.Type);

        // Compare nested states
        Assert.Equal(expected.NestedStates.Count, actual.NestedStates.Count);
        for (int i = 0; i < expected.NestedStates.Count; i++)
        {
            AssertStateDefinitionEqual(expected.NestedStates[i], actual.NestedStates[i]);
        }

        // Compare nested transitions
        Assert.Equal(expected.NestedTransitions.Count, actual.NestedTransitions.Count);
        for (int i = 0; i < expected.NestedTransitions.Count; i++)
        {
            Assert.Equal(expected.NestedTransitions[i].FromId, actual.NestedTransitions[i].FromId);
            Assert.Equal(expected.NestedTransitions[i].ToId, actual.NestedTransitions[i].ToId);
            Assert.Equal(expected.NestedTransitions[i].Label, actual.NestedTransitions[i].Label);
        }
    }

    [Fact]
    public void StateDiagram_BasicParsing()
    {
        var input = @"stateDiagram-v2
    [*] --> Still
    Still --> [*]
    Still --> Moving
    Moving --> Still
    Moving --> Crash
    Crash --> [*]";
        var model = MermaidParser.ParseStateDiagram(input);
        Assert.NotNull(model);
        Assert.True(model.IsV2);
        Assert.Equal(3, model.States.Count);
        Assert.Equal(6, model.Transitions.Count);
        Assert.Equal("Still", model.States[0].Id);
        Assert.Equal("Moving", model.States[1].Id);
        Assert.Equal("Crash", model.States[2].Id);
    }

    [Fact]
    public void StateDiagram_RoundTrip_BasicTransitions()
    {
        var input = @"stateDiagram-v2
    [*] --> Still
    Still --> [*]
    Still --> Moving
    Moving --> Still
    Moving --> Crash
    Crash --> [*]";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_TransitionLabels()
    {
        var input = @"stateDiagram-v2
    [*] --> Still
    Still --> Moving : A transition
    Moving --> Still : Another transition
    Moving --> Crash : Oh no!
    Crash --> [*]";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_StateLabels()
    {
        var input = @"stateDiagram-v2
    state ""This is a state description"" as s1
    state ""Another state"" as s2
    [*] --> s1
    s1 --> s2
    s2 --> [*]";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_CompositeState()
    {
        var input = @"stateDiagram-v2
    [*] --> First
    state First {
        [*] --> second
        second --> [*]
    }";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_NestedComposite()
    {
        var input = @"stateDiagram-v2
    [*] --> First
    state First {
        [*] --> Second
        state Second {
            [*] --> second
            second --> Third
            state Third {
                [*] --> third
                third --> [*]
            }
        }
    }";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_ForkJoin()
    {
        var input = @"stateDiagram-v2
    state fork_state <<fork>>
    [*] --> fork_state
    fork_state --> State2
    fork_state --> State3
    state join_state <<join>>
    State2 --> join_state
    State3 --> join_state
    join_state --> State4
    State4 --> [*]";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_Choice()
    {
        var input = @"stateDiagram-v2
    state if_state <<choice>>
    [*] --> IsPositive
    IsPositive --> if_state
    if_state --> False : if n < 0
    if_state --> True : if n >= 0";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_Notes()
    {
        var input = @"stateDiagram-v2
    [*] --> Active
    Active --> Inactive
    note right of Active : This is a note
    note left of Inactive : Another note";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_Direction()
    {
        var input = @"stateDiagram-v2
    direction LR
    [*] --> A
    A --> B
    B --> [*]";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_Styling()
    {
        var input = @"stateDiagram-v2
    [*] --> Active
    Active --> Inactive
    classDef notMoving fill:white
    classDef movement font-style:italic";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_V1Syntax()
    {
        var input = @"stateDiagram
    [*] --> Still
    Still --> Moving
    Moving --> [*]";
        var model = MermaidParser.ParseStateDiagram(input);
        Assert.NotNull(model);
        Assert.False(model.IsV2);
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_PreamblePreserved()
    {
        var input = @"---
title: My State Diagram
---
stateDiagram-v2
    [*] --> Active
    Active --> [*]";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_ComplexDiagram()
    {
        var input = @"stateDiagram-v2
    direction LR
    state ""Ready State"" as Ready
    state fork_state <<fork>>
    state join_state <<join>>
    [*] --> Ready
    Ready --> fork_state
    fork_state --> Processing
    fork_state --> Validating
    state Processing {
        [*] --> Parsing
        Parsing --> Transforming
        Transforming --> [*]
    }
    Processing --> join_state
    Validating --> join_state
    join_state --> Done
    Done --> [*]
    note right of Ready : Waiting for input";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_ColonLabels()
    {
        var input = @"stateDiagram-v2
    s1 : The first state
    s2 : The second state
    [*] --> s1
    s1 --> s2
    s2 --> [*]";
        var model = MermaidParser.ParseStateDiagram(input);
        Assert.NotNull(model);
        Assert.Equal("The first state", model.States[0].Label);
        Assert.Equal("The second state", model.States[1].Label);
    }

    [Fact]
    public void StateDiagram_RoundTrip_CompositeWithLabel()
    {
        var input = @"stateDiagram-v2
    state ""Not Moving"" as Still {
        [*] --> idle
        idle --> [*]
    }
    [*] --> Still
    Still --> [*]";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_Parse_ReturnsNull_ForNonStateDiagram()
    {
        var input = @"flowchart TD
    A --> B";
        var model = MermaidParser.ParseStateDiagram(input);
        Assert.Null(model);
    }

    [Fact]
    public void StateDiagram_Parse_ReturnsNull_ForEmptyInput()
    {
        var model = MermaidParser.ParseStateDiagram("");
        Assert.Null(model);
    }

    [Fact]
    public void StateDiagram_Serialize_ReturnsEmpty_ForNullModel()
    {
        var result = MermaidSerializer.SerializeStateDiagram(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void StateDiagram_RoundTrip_MultiLineNote()
    {
        var input = @"stateDiagram-v2
    [*] --> Active
    note right of Active
        This is line 1
        This is line 2
    end note
    Active --> [*]";
        AssertStateDiagramRoundTrip(input);
    }

    [Fact]
    public void StateDiagram_RoundTrip_AllSpecialTypes()
    {
        var input = @"stateDiagram-v2
    state fork1 <<fork>>
    state join1 <<join>>
    state choice1 <<choice>>
    [*] --> fork1
    fork1 --> A
    fork1 --> B
    A --> choice1
    choice1 --> C : yes
    choice1 --> D : no
    B --> join1
    C --> join1
    D --> join1
    join1 --> [*]";
        AssertStateDiagramRoundTrip(input);
    }

    // =============================================
    // ER Diagram Tests (Phase 2.4)
    // =============================================

    /// <summary>
    /// Helper method to verify ER diagram round-trip fidelity.
    /// Parses text -> serializes -> re-parses -> compares models.
    /// </summary>
    private static void AssertERDiagramRoundTrip(string input)
    {
        var model1 = MermaidParser.ParseERDiagram(input);
        Assert.NotNull(model1);

        var serialized = MermaidSerializer.SerializeERDiagram(model1);
        Assert.False(string.IsNullOrWhiteSpace(serialized), "Serialized output should not be empty");

        var model2 = MermaidParser.ParseERDiagram(serialized);
        Assert.NotNull(model2);

        // Compare entities
        Assert.Equal(model1.Entities.Count, model2.Entities.Count);
        for (int i = 0; i < model1.Entities.Count; i++)
        {
            Assert.Equal(model1.Entities[i].Name, model2.Entities[i].Name);
            Assert.Equal(model1.Entities[i].Attributes.Count, model2.Entities[i].Attributes.Count);
            for (int j = 0; j < model1.Entities[i].Attributes.Count; j++)
            {
                Assert.Equal(model1.Entities[i].Attributes[j].Type, model2.Entities[i].Attributes[j].Type);
                Assert.Equal(model1.Entities[i].Attributes[j].Name, model2.Entities[i].Attributes[j].Name);
                Assert.Equal(model1.Entities[i].Attributes[j].Key, model2.Entities[i].Attributes[j].Key);
                Assert.Equal(model1.Entities[i].Attributes[j].Comment, model2.Entities[i].Attributes[j].Comment);
            }
        }

        // Compare relationships
        Assert.Equal(model1.Relationships.Count, model2.Relationships.Count);
        for (int i = 0; i < model1.Relationships.Count; i++)
        {
            Assert.Equal(model1.Relationships[i].FromEntity, model2.Relationships[i].FromEntity);
            Assert.Equal(model1.Relationships[i].ToEntity, model2.Relationships[i].ToEntity);
            Assert.Equal(model1.Relationships[i].LeftCardinality, model2.Relationships[i].LeftCardinality);
            Assert.Equal(model1.Relationships[i].RightCardinality, model2.Relationships[i].RightCardinality);
            Assert.Equal(model1.Relationships[i].IsIdentifying, model2.Relationships[i].IsIdentifying);
            Assert.Equal(model1.Relationships[i].Label, model2.Relationships[i].Label);
        }

        // Compare preamble
        Assert.Equal(model1.PreambleLines.Count, model2.PreambleLines.Count);
        for (int i = 0; i < model1.PreambleLines.Count; i++)
        {
            Assert.Equal(model1.PreambleLines[i], model2.PreambleLines[i]);
        }
    }

    [Fact]
    public void ERDiagram_BasicParsing()
    {
        var input = @"erDiagram
    CUSTOMER ||--o{ ORDER : places
    ORDER ||--|{ LINE-ITEM : contains";
        var model = MermaidParser.ParseERDiagram(input);
        Assert.NotNull(model);
        Assert.Equal(3, model.Entities.Count);
        Assert.Equal(2, model.Relationships.Count);
        Assert.Equal("CUSTOMER", model.Entities[0].Name);
        Assert.Equal("ORDER", model.Entities[1].Name);
        Assert.Equal("LINE-ITEM", model.Entities[2].Name);
    }

    [Fact]
    public void ERDiagram_RoundTrip_BasicRelationships()
    {
        var input = @"erDiagram
    CUSTOMER ||--o{ ORDER : places
    ORDER ||--|{ LINE-ITEM : contains";
        AssertERDiagramRoundTrip(input);
    }

    [Fact]
    public void ERDiagram_RoundTrip_AllCardinalityTypes()
    {
        var input = @"erDiagram
    A ||--|| B : one-to-one
    C ||--o{ D : one-to-zero-or-more
    E ||--|{ F : one-to-one-or-more
    G |o--o| H : zero-or-one-to-zero-or-one";
        AssertERDiagramRoundTrip(input);
    }

    [Fact]
    public void ERDiagram_RoundTrip_NonIdentifyingRelationship()
    {
        var input = @"erDiagram
    CUSTOMER ||..o{ ORDER : places
    ORDER ||..|{ LINE-ITEM : contains";
        AssertERDiagramRoundTrip(input);
    }

    [Fact]
    public void ERDiagram_RoundTrip_EntityAttributes()
    {
        var input = @"erDiagram
    CUSTOMER {
        string name
        int age
        date created
    }
    CUSTOMER ||--o{ ORDER : places";
        AssertERDiagramRoundTrip(input);
    }

    [Fact]
    public void ERDiagram_RoundTrip_AttributesWithKeys()
    {
        var input = @"erDiagram
    CUSTOMER {
        int id PK
        string name
        string email UK
    }
    ORDER {
        int id PK
        int customerId FK
        date orderDate
    }
    CUSTOMER ||--o{ ORDER : places";
        AssertERDiagramRoundTrip(input);
    }

    [Fact]
    public void ERDiagram_RoundTrip_AttributesWithComments()
    {
        var input = @"erDiagram
    CUSTOMER {
        int id PK ""The customer ID""
        string name ""Full name""
        string email UK ""Must be unique""
    }
    CUSTOMER ||--o{ ORDER : places";
        AssertERDiagramRoundTrip(input);
    }

    [Fact]
    public void ERDiagram_RoundTrip_ComplexDiagram()
    {
        var input = @"erDiagram
    CUSTOMER {
        int id PK
        string name
        string email UK
    }
    ORDER {
        int id PK
        int customerId FK
        date orderDate
    }
    LINE-ITEM {
        int id PK
        int orderId FK
        int productId FK
        int quantity
    }
    PRODUCT {
        int id PK
        string name
        float price
    }
    CUSTOMER ||--o{ ORDER : places
    ORDER ||--|{ LINE-ITEM : contains
    PRODUCT ||--o{ LINE-ITEM : includes";
        AssertERDiagramRoundTrip(input);
    }

    [Fact]
    public void ERDiagram_RoundTrip_PreamblePreserved()
    {
        var input = @"---
title: My ER Diagram
---
erDiagram
    CUSTOMER ||--o{ ORDER : places";
        AssertERDiagramRoundTrip(input);
    }

    [Fact]
    public void ERDiagram_RoundTrip_MixedIdentifyingNonIdentifying()
    {
        var input = @"erDiagram
    CUSTOMER ||--o{ ORDER : places
    ORDER }|..|{ DELIVERY-ADDRESS : uses
    CUSTOMER }|..|{ DELIVERY-ADDRESS : lives-at";
        AssertERDiagramRoundTrip(input);
    }

    [Fact]
    public void ERDiagram_Parse_ReturnsNull_ForNonERDiagram()
    {
        var input = @"flowchart TD
    A --> B";
        var model = MermaidParser.ParseERDiagram(input);
        Assert.Null(model);
    }

    [Fact]
    public void ERDiagram_Parse_ReturnsNull_ForEmptyInput()
    {
        var model = MermaidParser.ParseERDiagram("");
        Assert.Null(model);
    }

    [Fact]
    public void ERDiagram_Serialize_ReturnsEmpty_ForNullModel()
    {
        var result = MermaidSerializer.SerializeERDiagram(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ERDiagram_RoundTrip_EntitiesFromRelationshipsOnly()
    {
        var input = @"erDiagram
    PERSON ||--o{ CAR : owns
    CAR }o--|| MANUFACTURER : made-by";
        var model = MermaidParser.ParseERDiagram(input);
        Assert.NotNull(model);
        Assert.Equal(3, model.Entities.Count);
        Assert.Equal("PERSON", model.Entities[0].Name);
        Assert.Equal("CAR", model.Entities[1].Name);
        Assert.Equal("MANUFACTURER", model.Entities[2].Name);
        AssertERDiagramRoundTrip(input);
    }
}
