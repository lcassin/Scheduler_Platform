# Mermaid Visual Editor - Architecture & Implementation Plan

## Executive Summary

Add a **visual drag-and-drop diagram editor** to the existing MermaidEditor tool that allows users to create and modify Mermaid diagrams graphically, with real-time bidirectional sync to the text-based code editor. The visual editor would appear as an alternative editing mode for `.mmd` (Mermaid) files, alongside the existing text editor + preview pane.

---

## Current Architecture Overview

### What We Have Today

```
+-------------------+     WebView2      +-------------------+
|   Code Editor     | ---- render ----> |   Preview Pane    |
|   (AvalonEdit)    | <-- click-to --   |   (WebView2)      |
|                   |     highlight     |   Mermaid.js      |
+-------------------+                   +-------------------+
```

**Key components:**
- **AvalonEdit** text editor with syntax highlighting, IntelliSense, auto-complete
- **WebView2 Preview** renders Mermaid diagrams via `mermaid.js` with pan/zoom (panzoom.js)
- **Click-to-Highlight**: JS click handlers in preview send `nodeClick`/`elementClick` messages to C# via `postMessage`, which calls `FindAndHighlightInEditor()` to select corresponding source code
- **Bidirectional communication**: C# <-> WebView2 via `ExecuteScriptAsync` (C# to JS) and `WebMessageReceived` (JS to C#)
- **Document model**: `_openDocuments` list with tab system, session save/restore
- **Rendering pipeline**: Text changes -> 500ms debounce timer -> `RenderPreview()` -> `RenderMermaid()` generates full HTML with embedded JS and navigates WebView2

### Supported Diagram Types
Flowcharts, state diagrams, sequence diagrams, class diagrams, ER diagrams, Gantt charts, pie charts, mindmaps, timelines, gitGraph, quadrant charts, requirement diagrams, C4 diagrams, and more.

### Technology Stack
- **Framework**: WPF (.NET 10, `net10.0-windows`)
- **Editor**: AvalonEdit 6.3.1
- **Preview**: WebView2 (Microsoft.Web.WebView2 1.0.3719)
- **Diagram Rendering**: Mermaid.js v11 (loaded from CDN)
- **Pan/Zoom**: panzoom.js v9.4.3
- **Markdown**: Markdig 0.45.0
- **SVG Processing**: Svg 3.4.7
- **Word Export**: DocumentFormat.OpenXml 3.4.1

---

## Proposed Architecture

### High-Level Design

```
+-------------------+     +-------------------+     +-------------------+
|   Code Editor     | <-->|   Diagram Model   |<--> |  Visual Editor    |
|   (AvalonEdit)    |     |   (C# classes)    |     |  (WebView2 +      |
|   Text Mode       |     |   In-memory AST   |     |   JS Canvas)      |
+-------------------+     +-------------------+     +-------------------+
                                  |
                          +-------+-------+
                          |               |
                    +-----------+   +-----------+
                    |  Mermaid  |   |  Preview   |
                    |  Parser   |   |  Renderer  |
                    |  (Text->  |   |  (Model->  |
                    |   Model)  |   |   Text->   |
                    +-----------+   |  Mermaid)  |
                                    +-----------+
```

### Core Concept: Three-Way Sync

1. **Text -> Model**: Parse Mermaid text into an in-memory diagram model (AST)
2. **Model -> Text**: Serialize the diagram model back to Mermaid syntax
3. **Visual Editor -> Model**: User drag/drop/edit actions update the model
4. **Model -> Visual Editor**: Model changes update the visual canvas
5. **Model -> Preview**: Model serializes to text, which renders via Mermaid.js (existing pipeline)

The **model** is the single source of truth. Both the text editor and visual editor are views into the model.

---

## Phased Implementation Plan

### Phase 1: Foundation - Flowchart Visual Editor (MVP)
**Estimated effort: Large - 8-12 prompting sessions**

Focus on **flowcharts only** (the most common diagram type) to prove the architecture before expanding.

#### 1.1 Mermaid Flowchart Parser
Build a C# parser that converts flowchart Mermaid text into a structured model.

**Model classes:**
```csharp
public class FlowchartModel
{
    public string Direction { get; set; } = "TD"; // TD, LR, BT, RL
    public List<FlowchartNode> Nodes { get; set; } = new();
    public List<FlowchartEdge> Edges { get; set; } = new();
    public List<FlowchartSubgraph> Subgraphs { get; set; } = new();
    public List<string> Comments { get; set; } = new();
    public List<StyleDefinition> Styles { get; set; } = new();
}

public class FlowchartNode
{
    public string Id { get; set; }
    public string Label { get; set; }
    public NodeShape Shape { get; set; } // Rectangle, Rounded, Diamond, Circle, etc.
    public Point Position { get; set; } // For visual editor placement
    public Size Size { get; set; }
    public string? CssClass { get; set; }
}

public enum NodeShape
{
    Rectangle,      // [text]
    Rounded,        // (text)
    Stadium,        // ([text])
    Subroutine,     // [[text]]
    Cylindrical,    // [(text)]
    Circle,         // ((text))
    Asymmetric,     // >text]
    Rhombus,        // {text}
    Hexagon,        // {{text}}
    Parallelogram,  // [/text/]
    Trapezoid,      // [/text\]
    DoubleCircle,   // (((text)))
}

public class FlowchartEdge
{
    public string FromNodeId { get; set; }
    public string ToNodeId { get; set; }
    public string? Label { get; set; }
    public EdgeStyle Style { get; set; } // Solid, Dotted, Thick
    public ArrowType ArrowType { get; set; } // Arrow, Open, Circle, Cross
}

public class FlowchartSubgraph
{
    public string Id { get; set; }
    public string Label { get; set; }
    public List<string> NodeIds { get; set; } = new();
    public string? Direction { get; set; }
}
```

**Parser approach:**
- Line-by-line regex parsing (Mermaid syntax is line-oriented for flowcharts)
- Handle node definitions: `A[Label]`, `B(Rounded)`, `C{Diamond}`, etc.
- Handle edges: `A --> B`, `A -.-> B`, `A ==> B`, `A -->|label| B`
- Handle subgraphs: `subgraph Title ... end`
- Handle styles: `style A fill:#f00`, `classDef className fill:#f00`
- Preserve comments (`%%`) and whitespace for round-trip fidelity

**Files to create:**
- `MermaidParser.cs` - Main parser with `ParseFlowchart(string text)` method
- `MermaidModels.cs` - All model classes
- `MermaidSerializer.cs` - Model back to Mermaid text

**Prompts for Phase 1.1:**
```
Session 1: "Create MermaidModels.cs in tools/MermaidEditor/ with model classes for 
flowcharts (FlowchartModel, FlowchartNode, FlowchartEdge, FlowchartSubgraph, 
NodeShape enum, EdgeStyle enum, ArrowType enum). Include all Mermaid flowchart 
node shapes. Also create MermaidParser.cs with a ParseFlowchart(string text) method 
that parses Mermaid flowchart syntax into the model. Handle: node definitions with 
all bracket types, edges with labels and styles, subgraphs, comments (%%),  style/
classDef directives, and direction declarations."

Session 2: "Create MermaidSerializer.cs that converts a FlowchartModel back to 
valid Mermaid text. The serializer should produce clean, readable output with 
proper indentation. Test round-trip: parse existing Mermaid text, serialize back, 
and verify the output renders the same diagram. Preserve comments and formatting 
where possible."
```

#### 1.2 Visual Editor Canvas (WebView2-based)
Build the visual editor as a WebView2 page using HTML5 Canvas or SVG, with a JS library for graph editing.

**Recommended JS library: [rete.js](https://rete.js.org/) or custom SVG-based approach**

After research, a **custom SVG-based approach** is recommended because:
- Rete.js and similar libraries are designed for node-based programming, not diagram editors
- We already have WebView2 + JS infrastructure
- Mermaid.js renders to SVG, so we can potentially re-use rendered SVG as the visual canvas
- Full control over the UX

**Visual Editor Features (MVP):**
- Canvas with grid background
- Nodes rendered as shapes (rectangle, rounded, diamond, etc.) with labels
- Edges rendered as lines/arrows between nodes with optional labels
- **Drag nodes** to reposition
- **Click node** to select (highlight in blue, show resize handles)
- **Double-click node** to edit label inline
- **Right-click** context menu: Add Node, Delete Node, Add Edge, Edit Properties
- **Toolbar**: Add Node, Add Edge, Delete, Undo/Redo, Auto-Layout, Zoom controls
- **Property panel** (right side): Edit selected node/edge properties (shape, label, style)

**Layout Engine:**
- Use [dagre.js](https://github.com/dagrejs/dagre) for automatic graph layout (same library Mermaid uses internally)
- User can manually drag nodes to override auto-layout positions
- "Auto Layout" button re-applies dagre layout

**Files to create:**
- `VisualEditor.html` (embedded resource) - The visual editor HTML/CSS/JS
- `VisualEditorBridge.cs` - C# <-> JS communication for the visual editor WebView2

**Prompts for Phase 1.2:**
```
Session 3: "Create VisualEditor.html as an embedded resource in tools/MermaidEditor/
Resources/. This is an SVG-based visual diagram editor with:
- SVG canvas with grid background and pan/zoom (reuse panzoom.js)
- Ability to render nodes as SVG shapes (rect, rounded-rect, diamond, circle, 
  hexagon, etc.) with text labels inside
- Ability to render edges as SVG paths/lines with arrowheads and optional labels
- Drag-to-move nodes (update edge paths automatically when nodes move)
- Click to select (blue highlight border)
- Double-click to inline-edit label text
- All interactions send messages to C# via window.chrome.webview.postMessage()
- Accept diagram data from C# via window.loadDiagram(jsonData) function
- Include dagre.js (from CDN) for auto-layout
Include full CSS styling matching the dark/light theme system."

Session 4: "Create VisualEditorBridge.cs that handles communication between the 
visual editor WebView2 and the C# diagram model. It should:
- Convert FlowchartModel to JSON for the JS visual editor
- Handle messages from JS (node moved, node edited, edge created, etc.)
- Update the FlowchartModel when the visual editor changes
- Trigger text regeneration via MermaidSerializer when model changes
- Support undo/redo by maintaining a model history stack"
```

#### 1.3 UI Integration
Add the visual editor as a new editing mode in the existing MainWindow.

**Layout concept:**
```
+------------------------------------------------------------------+
| File  Edit  Format  View  Help                                   |
|------------------------------------------------------------------|
| [toolbar buttons...]  [Text Mode] [Visual Mode] [Split Mode]    |
|------------------------------------------------------------------|
| Tab1 | Tab2* | Tab3 |                                           |
|------------------------------------------------------------------|
|                    |                    |                         |
|   Code Editor      |   Visual Editor   |   Preview               |
|   (AvalonEdit)     |   (WebView2 #2)   |   (WebView2 #1)        |
|                    |                    |                         |
|                    |                    |                         |
+------------------------------------------------------------------+
| Line 42, Col 15  |  Mermaid rendered                             |
+------------------------------------------------------------------+
```

**Three modes:**
1. **Text Mode** (current): Code Editor + Preview (existing layout)
2. **Visual Mode**: Visual Editor + Preview (no text editor visible)
3. **Split Mode**: Code Editor + Visual Editor + Preview (three-pane)

**Mode switching:**
- Toggle buttons in toolbar (or View menu)
- Only available for Mermaid files (disabled for Markdown)
- Visual mode parses current text into model on activation
- Switching back to text mode serializes model back to text

**Prompts for Phase 1.3:**
```
Session 5: "Add a second WebView2 control to MainWindow.xaml for the visual editor. 
Add three mode toggle buttons to the toolbar: Text Mode, Visual Mode, Split Mode 
(with SVG icons). Only show these for Mermaid files. In Text Mode, show CodeEditor + 
PreviewWebView (existing). In Visual Mode, show VisualEditorWebView + PreviewWebView. 
In Split Mode, show all three in a Grid with GridSplitters. Add mode switching 
logic to MainWindow.xaml.cs that:
- On switch to Visual Mode: parse current text via MermaidParser, send model to 
  visual editor via VisualEditorBridge
- On switch to Text Mode: serialize model to text, update CodeEditor.Text
- On visual editor changes: update model, serialize to text, re-render preview
- Wire the visual editor WebView2 initialization in MainWindow_Loaded"

Session 6: "Add a context menu to the visual editor (right-click):
- Add Node (submenu with shape types)
- Add Edge (starts edge-drawing mode: click source, then click target)
- Edit Label (opens inline text editor)
- Delete Selected
- Auto Layout
Also add a floating property panel that appears when a node/edge is selected,
showing editable fields for: label, shape (dropdown), style (color picker)."
```

#### 1.4 Round-Trip Fidelity & Polish
Ensure text<->visual<->text doesn't lose information.

**Critical requirements:**
- Comments (`%%`) must be preserved through round-trips
- Style definitions (`style`, `classDef`) must be preserved
- Node ordering should be stable (don't randomly reorder)
- Subgraph membership must be preserved
- Manual position data can be stored as comments: `%% @pos A 100,200`

**Prompts for Phase 1.4:**
```
Session 7: "Add round-trip fidelity to the Mermaid parser/serializer:
- Preserve %% comments in their original positions
- Preserve style/classDef directives
- Store visual editor node positions as special comments (%% @pos nodeId x,y)
- When parsing, read @pos comments to restore node positions in visual editor
- When serializing, write @pos comments for manually-positioned nodes
- Maintain stable node ordering (don't shuffle nodes on serialize)
- Add unit tests for round-trip: parse -> serialize -> parse -> compare"

Session 8: "Polish the visual editor UX:
- Add keyboard shortcuts: Delete (delete selected), Ctrl+Z (undo), Ctrl+Y (redo),
  Ctrl+A (select all), Escape (deselect)
- Add edge label editing (double-click edge label)
- Add snap-to-grid when dragging nodes
- Add minimap in visual editor (small overview in corner)
- Improve edge routing to avoid overlapping nodes
- Add visual feedback: hover highlights, drag ghost, connection points on nodes
- Theme-aware: respect dark/light mode"
```

---

### Phase 2: Extended Diagram Types
**Estimated effort: Medium - 4-6 prompting sessions per diagram type**

Extend the parser/model/visual-editor to support additional diagram types. Each follows the same pattern: Model classes -> Parser -> Serializer -> Visual editor support.

#### 2.1 Sequence Diagrams
```
Participants (horizontal boxes at top)
   |          |          |
   |--message->|         |
   |          |--message->|
   |          |          |
```

**Visual editor additions:**
- Vertical lifeline lanes
- Drag to create messages between participants
- Activation boxes on lifelines
- Alt/opt/loop/par fragments as overlay rectangles

**Prompts:**
```
Session 9: "Add sequence diagram support to the Mermaid parser/serializer:
- SequenceDiagramModel with Participant, Message, Note, Fragment classes
- Parse: participant/actor declarations, messages (->>, -->>), notes, 
  activate/deactivate, loop/alt/opt/par blocks
- Serialize back to valid Mermaid sequence diagram syntax"

Session 10: "Add sequence diagram visual editing to VisualEditor.html:
- Render participants as boxes at top with vertical lifelines
- Render messages as horizontal arrows between lifelines
- Drag participants to reorder
- Click between lifelines to add a message
- Double-click message to edit text
- Support activation boxes and fragments (loop, alt, opt)
- Auto-space messages vertically"
```

#### 2.2 Class Diagrams
```
+-------------------+
|    ClassName       |
|-------------------|
| - field: type     |
| + field: type     |
|-------------------|
| + method(): type  |
| - method(): type  |
+-------------------+
```

**Prompts:**
```
Session 11: "Add class diagram support: ClassDiagramModel with ClassDefinition 
(name, members, methods, annotations), Relationship (inheritance, composition, 
aggregation, association with cardinality). Parse classDiagram syntax including 
class declarations, members with visibility (+, -, #, ~), relationships with 
labels and cardinality."

Session 12: "Add class diagram visual editing: render classes as UML boxes with 
sections for name, fields, methods. Drag to position. Click to add members. 
Draw relationships by dragging from one class to another. Right-click to set 
relationship type and cardinality."
```

#### 2.3 State Diagrams

**Prompts:**
```
Session 13: "Add state diagram support: StateDiagramModel with State (including 
composite states), Transition (with event/guard/action labels). Parse 
stateDiagram-v2 syntax including states, transitions, [*] start/end, composite 
states with nested content, notes."

Session 14: "Add state diagram visual editing: render states as rounded rectangles, 
transitions as arrows. Support composite states as containers. Drag states to 
reposition. Draw transitions between states."
```

#### 2.4 ER Diagrams

**Prompts:**
```
Session 15: "Add ER diagram support: ERDiagramModel with Entity (name, attributes 
with type/key), Relationship (with cardinality markers ||--o{, etc.). Parse 
erDiagram syntax."

Session 16: "Add ER diagram visual editing: render entities as tables, relationships 
as lines with cardinality symbols. Drag entities to position. Click to add 
attributes. Draw relationships between entities."
```

---

### Phase 3: Advanced Features
**Estimated effort: Large - 6-10 prompting sessions**

#### 3.1 Smart Code Generation
- **AI-Assisted**: "Describe your diagram in natural language" -> generate Mermaid code
- Leverage existing Ask AI dialog infrastructure
- Prompt: "Generate a flowchart for [user description]" -> insert into editor

#### 3.2 Template Gallery for Visual Editor
- Pre-built diagram templates with visual layout
- Extend existing NewDocumentDialog with visual editor templates
- Save/load visual layouts

#### 3.3 Collaborative Annotations
- Add sticky notes / annotations to diagrams
- Color-coded labels
- Export annotations with diagram

#### 3.4 Import/Export
- Import from draw.io XML format
- Import from Visio (basic shapes)
- Export visual layout as SVG with embedded position data
- Copy diagram to clipboard as image

#### 3.5 Advanced Visual Features
- **Minimap** for large diagrams in visual editor
- **Find in diagram** - search nodes by label
- **Undo/redo** with visual diff (show what changed)
- **Multi-select** with rubber-band selection
- **Copy/paste** nodes and subgraphs
- **Alignment tools** (align left, center, distribute evenly)

---

## Technical Decisions & Trade-offs

### Why WebView2 for the Visual Editor (not WPF Canvas)?

| Factor | WebView2 (HTML/SVG/JS) | WPF Canvas |
|--------|----------------------|------------|
| **Rendering quality** | Excellent SVG rendering | Good but more complex |
| **Library ecosystem** | Rich (dagre, d3, panzoom) | Limited graph libraries |
| **Developer velocity** | Fast iteration (HTML/CSS/JS) | Slower (XAML/C#) |
| **Consistency** | Same tech as preview pane | Different tech stack |
| **Existing expertise** | Already using WebView2 | New patterns needed |
| **Performance** | Good for most diagrams | Better for very large diagrams |
| **Interop overhead** | postMessage serialization | Direct C# access |

**Decision**: Use WebView2. The existing preview pane already proves this approach works. The JS ecosystem for graph editing is much richer than WPF.

### Why a C# Model Layer (not JS-only)?

The model layer in C# provides:
1. **Type safety** for the diagram structure
2. **Serialization control** for Mermaid text generation
3. **Undo/redo** at the model level (works across both editors)
4. **Session persistence** (save/restore visual layouts)
5. **Future extensibility** (AI integration, validation, etc.)

### Position Storage Strategy

Node positions from the visual editor need to survive text editing. Options:

1. **Special comments**: `%% @pos A 100,200` (chosen approach)
   - Pro: Visible in text editor, survives copy/paste
   - Pro: Doesn't break Mermaid rendering
   - Con: Clutters the text slightly

2. **Sidecar file**: `diagram.mmd.layout`
   - Pro: Clean text
   - Con: Easy to lose, doesn't survive copy/paste

3. **Auto-layout only** (no position persistence)
   - Pro: Simplest
   - Con: Lose manual positioning on every switch

**Decision**: Use special comments (`%% @pos`) with an option to hide them in the text editor via a code folding region.

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Parser doesn't handle all Mermaid syntax | High | Start with flowcharts only; add types incrementally |
| Round-trip text loss | High | Extensive testing; preserve original text structure |
| Performance with large diagrams | Medium | Virtualize rendering; lazy load nodes |
| Two WebView2 instances slow startup | Medium | Lazy-init visual editor WebView2 only when entering visual mode |
| Edge routing complexity | Medium | Use dagre.js for initial layout; allow manual override |
| Mermaid syntax changes upstream | Low | Pin mermaid.js version; update parser when upgrading |

---

## File Structure

### Planned (Original)
```
tools/MermaidEditor/
  Models/
    FlowchartModel.cs          # Flowchart data model
    SequenceDiagramModel.cs     # Phase 2
    ...
  Parsing/
    MermaidParser.cs            # Text -> Model
    MermaidSerializer.cs        # Model -> Text
    ...
  VisualEditor/
    VisualEditorBridge.cs       # C# <-> JS communication
    ...
  Resources/
    VisualEditor.html           # The visual editor page
    visual-editor.css           # Styles
    visual-editor.js            # Editor logic
```

### Actual (Phase 1 — Flat Structure)
```
tools/MermaidEditor/
  MermaidModels.cs              # All model classes (FlowchartModel, nodes, edges, subgraphs, enums)
  MermaidParser.cs              # ParseFlowchart() — add ParseSequenceDiagram() etc. for Phase 2
  MermaidSerializer.cs          # Serialize() — flowchart serialization with round-trip fidelity
  VisualEditorBridge.cs         # C# <-> JS bridge, undo/redo history, model JSON conversion
  MainWindow.xaml               # Updated with visual editor WebView2, mode toggle buttons
  MainWindow.xaml.cs            # Mode switching, visual editor initialization
  MermaidEditor.csproj          # Updated with embedded resource reference
  Resources/
    VisualEditor.html           # All-in-one: SVG canvas + CSS + JS (embedded resource)
  MermaidEditor-Visual-Editor-Architecture.md  # This document

tools/MermaidEditor.Tests/
  MermaidEditor.Tests.csproj    # xUnit test project
  MermaidParserTests.cs         # 28 round-trip tests (compile on Linux, run on Windows)
```

---

## Session-by-Session Prompting Guide

Below is a suggested order for prompting sessions. Each session should be a single focused task.

### Phase 1 Sessions (Foundation)

| # | Session Goal | Key Deliverables | Dependencies |
|---|-------------|-----------------|--------------|
| 1 | Flowchart model + parser | `MermaidModels.cs`, `MermaidParser.cs` | None |
| 2 | Flowchart serializer + round-trip | `MermaidSerializer.cs` | Session 1 |
| 3 | Visual editor HTML/SVG canvas | `VisualEditor.html` | None (parallel with 1-2) |
| 4 | C# <-> JS bridge | `VisualEditorBridge.cs` | Sessions 1-3 |
| 5 | MainWindow UI integration | Modified `MainWindow.xaml/.cs` | Sessions 1-4 |
| 6 | Context menu + property panel | Updated `VisualEditor.html` | Session 5 |
| 7 | Round-trip fidelity + position storage | Updated parser/serializer | Sessions 1-2, 5 |
| 8 | Polish: keyboard shortcuts, UX | Updated visual editor | Sessions 3-7 |

### Phase 2 Sessions (Diagram Types)

| # | Session Goal | Dependencies |
|---|-------------|--------------|
| 9-10 | Sequence diagrams | Phase 1 complete |
| 11-12 | Class diagrams | Phase 1 complete |
| 13-14 | State diagrams | Phase 1 complete |
| 15-16 | ER diagrams | Phase 1 complete |

### Phase 3 Sessions (Advanced)

| # | Session Goal | Dependencies |
|---|-------------|--------------|
| 17 | AI-assisted diagram generation | Phase 1 + existing AI integration |
| 18 | Template gallery | Phase 1 |
| 19 | Import/export (draw.io, Visio) | Phase 1 |
| 20 | Advanced: multi-select, alignment, minimap | Phase 1 |

---

## Sample Prompt Templates

### For Model/Parser Work
```
"In the MermaidEditor tool (tools/MermaidEditor/ in lcassin/Scheduler_Platform, 
branch Net-10-Upgrade), create [filename] that [description]. 

The MermaidEditor is a WPF desktop app using .NET 10, AvalonEdit, and WebView2. 
It currently supports text editing of Mermaid diagrams with a WebView2 preview 
pane. We are adding a visual drag-and-drop editor.

Requirements:
- [specific requirements]
- Follow existing code conventions (nullable enabled, implicit usings)
- Add the new file to the project (no manual .csproj changes needed for .cs files)
- Build with: dotnet build tools/MermaidEditor/MermaidEditor.csproj --no-restore"
```

### For Visual Editor JS Work
```
"In the MermaidEditor tool, create/update Resources/VisualEditor.html - an 
SVG-based visual diagram editor embedded in a WebView2 control. 

Communication with C# is via:
- C# to JS: ExecuteScriptAsync calling window.loadDiagram(jsonData)
- JS to C#: window.chrome.webview.postMessage({ type: '...', ... })

The editor must support dark/light themes (will receive theme via 
window.setTheme('dark'|'light')).

Requirements:
- [specific requirements]
- Load dagre.js from CDN for auto-layout
- Load panzoom.js from CDN for pan/zoom (already used in preview pane)
- All node shapes from Mermaid flowcharts must be rendered
- Edge routing should use dagre layout with manual override support"
```

### For UI Integration Work
```
"In the MermaidEditor tool, modify MainWindow.xaml and MainWindow.xaml.cs to add 
visual editor mode. The existing layout has a code editor (AvalonEdit) on the 
left and a WebView2 preview on the right with a GridSplitter.

Add:
- A second WebView2 control (VisualEditorWebView) for the visual editor
- Three mode buttons in the toolbar (Text/Visual/Split)
- Mode switching logic that shows/hides the appropriate panels
- Only enable visual mode for Mermaid files (not Markdown)
- Lazy-initialize the visual editor WebView2 (don't load until first use)
- Theme the visual editor to match the current app theme

The visual editor communicates with C# via VisualEditorBridge.cs (already created).
The existing preview WebView2 continues to render the Mermaid preview as before."
```

---

## Success Criteria

### Phase 1 (MVP) — COMPLETED
- [x] User can switch to Visual Mode for a flowchart `.mmd` file
- [x] All existing flowchart nodes and edges appear in the visual editor
- [x] User can drag nodes to new positions
- [x] User can add new nodes and edges via context menu and toolbar
- [x] User can edit node labels by double-clicking
- [x] Changes in visual editor update the text and re-render the preview
- [x] Changes in text editor update the visual editor (when switching modes)
- [x] Node positions are preserved across sessions (via `%% @pos` comments)
- [x] No information is lost during text <-> visual round-trips (config directives, comments, styles preserved)
- [x] Existing text-only editing workflow is completely unaffected
- [x] In-editor toolbar with Add Node, Add Edge, Delete, Undo/Redo, Auto-Layout, Zoom controls
- [x] Resize handles on selected nodes (8-handle pattern)
- [x] Subgraph management: create, assign nodes, edit label, delete (context menu + keyboard + toolbar)
- [x] Empty subgraph visibility (placeholder boxes with dashed borders)
- [x] Compound graph layout via dagre (hierarchical subgraph positioning)
- [x] Shape previews in property panel dropdown (inline SVG)
- [x] Keyboard shortcuts: Delete, Ctrl+Z/Y, Ctrl+A, Escape
- [x] Snap-to-grid, minimap, edge routing, drag ghost, hover highlights
- [x] 28 xUnit round-trip tests

### Phase 2
- [ ] Support for sequence, class, state, and ER diagrams in visual editor
- [ ] Each diagram type has appropriate visual editing affordances
- [ ] Round-trip fidelity for all supported diagram types

### Phase 3
- [ ] AI can generate diagrams from natural language descriptions
- [ ] Import from common formats (draw.io)
- [ ] Advanced editing features (multi-select, alignment, etc.)

---

## Phase 1 Lessons Learned

The following lessons were discovered during the Phase 1 flowchart implementation. These should be applied when building Phase 2+ diagram types.

### Architecture & File Structure

| Planned | Actual | Notes |
|---------|--------|-------|
| Subdirectories (`Models/`, `Parsing/`, `VisualEditor/`) | Flat — all `.cs` files in `tools/MermaidEditor/` root | Simpler for a single-tool project. No need to change. |
| Separate `visual-editor.css` + `visual-editor.js` | All-in-one `VisualEditor.html` with embedded CSS/JS | Simplifies embedded resource loading. Single file is easier for WebView2. |
| Separate parser per diagram type (`FlowchartParser.cs`) | Single `MermaidParser.cs` with `ParseFlowchart()` method | For Phase 2, add `ParseSequenceDiagram()`, `ParseClassDiagram()`, etc. to the same file — or split only if it gets too large. |

### Subgraph / Container Handling (Critical for Phase 2)

Subgraphs were the most complex part of Phase 1. Every diagram type with containers (sequence diagram fragments, class diagram packages, state diagram composite states) will face similar issues:

1. **Proxy Nodes**: When a subgraph ID is used as an edge endpoint (e.g., `Frontend --> API`), the ID collides between the container and a logical node. Solution: detect "proxy nodes" (nodes whose ID matches a container ID), filter them from layout/rendering/measurement, and resolve edges to the first contained node instead.

2. **Empty Containers**: dagre won't lay out containers with no children. Must render empty containers as placeholder boxes with stored positions (`_emptyX`/`_emptyY`) and dashed borders. Without this, newly created containers are invisible.

3. **Compound Graph Layout**: dagre's `setParent()` is essential for hierarchical container positioning. Without it, containers are positioned independently and overlap. Use `compound: true` in dagre graph options and call `g.setParent(nodeId, containerId)` for each child.

4. **Container Lifecycle UX**: Users need the full lifecycle: create → assign nodes → edit properties → delete. Each operation should be available via **both** context menu (right-click) **and** keyboard/toolbar. Discoverability was a recurring issue — if an action only exists in one place, users can't find it.

5. **Container Selection**: Clicking on a container's border/background should select it, showing editable properties in the property panel. This requires hit-testing on the SVG rect elements for containers, separate from node hit-testing.

6. **Container Deletion**: Deleting a container should release its children (not delete them). Children become top-level elements. Undo must restore the container and re-parent the children.

### Parser & Round-Trip Fidelity

1. **Preamble / Config Directives**: Mermaid files often start with config directives like `%%{init: {"theme": "dark", "look": "handdrawn"}}%%`. These are NOT comments — they must be captured as "preamble lines" and re-emitted verbatim at the top of serialized output. Losing them silently breaks diagram appearance.

2. **Class Name Suffixes**: Nodes can have `:::className` suffixes (e.g., `A[Label]:::highlight`). The parser must strip these during node parsing and store them separately. The serializer must re-append them.

3. **Style Preservation on Visual Edits**: When the user changes a node's fill color in the visual editor, the corresponding `style` directive must be updated (not duplicated). If no `style` directive exists, create one.

4. **Stable Node Ordering**: The serializer must emit nodes in the same order they were parsed. Shuffling node order makes text diffs noisy and confuses users who care about their text layout.

5. **@pos Comment Positioning**: `%% @pos nodeId x,y` comments should be emitted at the end of the file, after all diagram content. Interleaving them with node definitions clutters the text.

### Visual Editor UX

1. **Shape Previews in Dropdowns**: Shape names alone ("Stadium", "Subroutine", "Asymmetric") are not self-explanatory. Inline SVG previews next to each name in the shape picker dramatically improve usability. Apply this pattern to any future property dropdowns (e.g., relationship types, arrow styles).

2. **Context Menu Completeness**: Every action available via toolbar/keyboard should also be in the right-click context menu. Users have different mental models — some reach for right-click first, others look for buttons. The context menu should show/hide items dynamically based on what's selected (node, edge, container, nothing).

3. **Node Measurement Before Layout**: dagre needs accurate node sizes. Measure text by creating a temporary SVG `<text>` element, calling `getBBox()`, then adding padding. Without this, nodes overlap or have excessive whitespace.

4. **Single Atomic Messages for Bulk Operations**: Auto-layout moves many nodes at once. Sending individual `nodeMoved` messages per node creates N undo entries. Instead, send a single `autoLayoutComplete` message with all positions, creating one undo entry for the whole operation.

5. **Resize Handles**: 8-handle pattern (4 corners + 4 edge midpoints) works well. Enforce minimum size (40×24) and snap-to-grid (20px increments) during resize.

6. **Edge Routing**: Edges should connect to the nearest connection point on the node boundary, not the center. Calculate intersection of the edge line with the node shape for clean arrow placement.

### C# Bridge & Undo/Redo

1. **History Stack Size**: Cap at 100 entries to prevent memory bloat on long editing sessions.

2. **Model Snapshot Strategy**: Store full model JSON snapshots (not diffs). Simpler to implement and restore. The model is small enough that full snapshots are fine.

3. **Null-Check on Parse**: `ParseAndSendToVisualEditor()` must null-check the parser result before assigning to the current model. Non-flowchart content or invalid syntax returns null — without the check, the model is silently cleared.

4. **Message Types**: Each visual editor action needs a distinct message type. Phase 1 uses: `nodeMoved`, `nodeEdited`, `nodeAdded`, `edgeAdded`, `edgeEdited`, `deleteNode`, `deleteEdge`, `autoLayoutComplete`, `nodeResized`, `nodeShapeChanged`, `nodeStyleChanged`, `subgraphCreated`, `subgraphDeleted`, `subgraphLabelChanged`, `nodeMovedToSubgraph`, `nodeRemovedFromSubgraph`. Future diagram types will need their own message types.

### Build & Testing

1. **No CI on this repo**: All verification is manual `dotnet build --no-restore`. Always check for 0 errors before committing.

2. **Unit tests require Windows**: Tests reference WPF types (`System.Windows.Point`, `System.Windows.Size`). They compile on Linux but must be executed on a Windows machine with `dotnet test`.

3. **WPF Binding Warnings**: `System.Windows.Data Error: 4` warnings about `HorizontalContentAlignment` / `VerticalContentAlignment` on ComboBoxItem/MenuItem are a known WPF theming issue. They are harmless and do not affect functionality — safe to ignore.

### Patterns to Reuse for Phase 2 Diagram Types

Each new diagram type (sequence, class, state, ER) should follow this pattern:

1. **Model classes**: Add to `MermaidModels.cs` (e.g., `SequenceDiagramModel`, `SequenceParticipant`, `SequenceMessage`)
2. **Parser method**: Add `ParseSequenceDiagram()` to `MermaidParser.cs` (or new file if parser grows too large)
3. **Serializer method**: Add `SerializeSequenceDiagram()` to `MermaidSerializer.cs`
4. **Visual editor rendering**: Add `renderSequenceDiagram()` to `VisualEditor.html` — different diagram types need fundamentally different rendering (lifelines vs. nodes vs. tables)
5. **Bridge handlers**: Add message types to `VisualEditorBridge.cs` for diagram-specific interactions
6. **Round-trip tests**: Add to `MermaidEditor.Tests` project

The visual editor should detect diagram type from the parsed model and switch rendering modes accordingly. The property panel should adapt its fields based on what's selected (e.g., participant properties vs. node properties vs. entity attributes).

---

## Questions for Discussion

1. **Priority**: Should we start with Phase 1 (flowcharts only) or is there a different diagram type you use most?

2. **Visual editor position**: The document shows it between Code Editor and Preview. Would you prefer it to **replace** the preview pane in visual mode (since you can see the diagram in the visual editor itself)?

3. **Node position storage**: Are you comfortable with `%% @pos` comments in the text, or would you prefer a sidecar file approach?

4. **JS library**: Should we go fully custom SVG, or evaluate a library like [JointJS](https://www.jointjs.com/) (has a free tier) or [Cytoscape.js](https://js.cytoscape.org/) for the graph rendering?

5. **Scope**: How many diagram types do you actively use? This helps prioritize Phase 2.
