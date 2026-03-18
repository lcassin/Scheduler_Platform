# Mermaid Editor

A visual IDE for editing Mermaid diagrams and Markdown files, built with WPF and .NET.

## Download

**Download [MermaidEditorSetup-4.0.0.exe](Installer/MermaidEditorSetup-4.0.0.exe?raw=true)** - (Latest Stable) Windows installer (self-contained, no .NET runtime required)

## Details

**Visual Editor**
- Drag-and-drop visual editors for 5 diagram types: Flowchart, Sequence, Class, State, and ER
- Text / Visual / Split editing modes with seamless code-to-visual synchronization
- Copy/paste, undo/redo, auto-layout, and minimap
- Node resize, snap-to-grid, and curved edges
- Context menus and property panels for editing nodes, edges, and attributes
- Add participants, messages, notes, and fragments visually (Sequence)
- Add entities and attributes visually (ER)
- Add states and transitions visually (State)
- Add classes, methods, and relationships visually (Class)

**Code Editor**
- Live preview as you type with automatic refresh
- Syntax highlighting for Mermaid and Markdown
- IntelliSense code completion for Mermaid keywords
- Click-to-navigate between preview and source code
- Navigation dropdown for quick section jumping
- Find & Replace with regex support
- Bracket matching with highlight
- Minimap with viewport indicator and word wrap sync
- Undo/Redo support
- Drag and drop file support
- Auto-save with session restore (reopens tabs on startup, including untitled documents)
- Spell check with squiggly underlines and right-click suggestions (markdown only)

**AI Integration**
- Ask AI to generate, explain, or modify diagrams
- OpenAI and Anthropic (Claude) support with streaming responses
- Visual editor mode with "Replace Diagram" to apply AI-generated changes directly
- File attachments for multi-modal input
- Editor context awareness for smarter suggestions

**Mermaid Diagram Support**
- All Mermaid diagram types: flowchart, sequence, class, state, ER, gantt, pie, mindmap, timeline, gitGraph, journey, quadrantChart, requirementDiagram, C4 diagrams
- Pan and zoom in the preview pane
- Mermaid v11 with handDrawn look support
- Frontmatter config support for themes and styling

**Markdown Support**
- GitHub-flavored Markdown rendering
- Image embedding with relative path resolution
- Code syntax highlighting in preview
- Markdown formatting toolbar (bold, italic, headers, lists, links, etc.)

**Export & Print**
- PNG export at 4x or 6x resolution
- SVG vector export
- EMF vector export for Office applications
- Word document export with embedded images
- Print Preview with PDF export
- Code Print with syntax highlighting

**User Interface**
- Dark / Light / Twilight themes with title bar theming
- Multi-document tabbed interface
- New Document dialog with templates for all diagram types
- File browser with preview on selection
- Custom SVG toolbar icons with toggle state indicators
- Settings dialog for editor defaults, theme, and AI integration
- Table generator dialog for markdown files
- What's New dialog on version updates (with opt-out)
- Toggle preview panel on/off in any editing mode

## Requirements

- Windows 10/11
- .NET 10.0 Runtime (not needed if using the installer with self-contained deployment)
- WebView2 Runtime (usually pre-installed on Windows 10/11)

## Architecture

The application is built using the following technologies and patterns:

### Technology Stack

- **.NET 10 / WPF** - Windows desktop application framework
- **AvalonEdit** - Code editor with syntax highlighting
- **WebView2** - Chromium-based browser control for rendering
- **Mermaid.js v11** - JavaScript library for diagram rendering
- **Markdig** - Markdown parsing library
- **OpenXML SDK** - Word document generation
- **Svg.NET** - SVG parsing for EMF conversion
- **WeCantSpell.Hunspell** - Spell checking with Hunspell dictionaries

### Project Structure

```
MermaidEditor/
├── MainWindow.xaml              # Main application window UI
├── MainWindow.xaml.cs           # Main window code-behind
├── MermaidParser.cs             # Mermaid text-to-model parser
├── MermaidSerializer.cs         # Model-to-Mermaid text serializer
├── MermaidModels.cs             # Data models for all diagram types
├── VisualEditorBridge.cs        # C#/JS bridge for visual editor
├── VisualEditorBridge.Flowchart.cs  # Flowchart visual editor logic
├── NewDocumentDialog.xaml       # New document template dialog
├── NewDocumentDialog.xaml.cs    # Template dialog code-behind
├── SettingsDialog.xaml          # Settings/Configuration dialog
├── SettingsDialog.xaml.cs       # Settings dialog code-behind
├── SettingsManager.cs           # Persistent settings management
├── AskAiDialog.xaml             # Ask AI chat dialog
├── AskAiDialog.xaml.cs          # AI dialog code-behind
├── AiService.cs                 # AI provider integration (OpenAI, Claude)
├── WhatsNewDialog.xaml          # What's New release notes dialog
├── WhatsNewDialog.xaml.cs       # What's New dialog code-behind
├── PrintCodePreviewDialog.xaml  # Code print preview dialog
├── PrintPreviewDialog.xaml      # Diagram print preview dialog
├── SpellCheckService.cs         # Spell checking with Hunspell dictionaries
├── SpellCheckBackgroundRenderer.cs # Squiggly underline renderer
├── TableGeneratorDialog.xaml    # Table generator dialog
├── ThemeManager.cs              # Theme management (dark/light/twilight)
├── SvgIconHelper.cs             # SVG icon loading and theming
├── App.xaml                     # Application resources
├── App.xaml.cs                  # Application startup
├── MermaidEditor.csproj         # Project file
├── app.ico                      # Application icon
├── LICENSE                      # GNU GPL v3 license
├── README.md                    # This file
├── Icons/                       # Custom SVG toolbar icons
├── Dictionaries/                # Hunspell spell check dictionaries
├── Resources/
│   ├── VisualEditor.html        # Visual editor HTML/JS application
│   ├── WhatsNew.md              # What's New release notes content
│   └── TemplateThumbnails/      # Template preview images (48x48 PNG)
└── Installer/
    └── MermaidEditorSetup.iss   # Inno Setup installer script
```

### Key Components

**MainWindow.xaml.cs** contains the core application logic:

- `RenderMermaid()` - Renders Mermaid diagrams using WebView2 and Mermaid.js
- `RenderMarkdown()` - Renders Markdown using Markdig and custom HTML template
- `ExtractMermaidSections()` - Parses diagram content for navigation dropdown
- `FindAndHighlightInEditor()` - Click-to-navigate from preview to source
- `ExportMermaidToWord()` - Exports diagrams as PNG embedded in Word
- `ConvertMarkdownToWord()` - Converts Markdown to formatted Word document
- `SwitchToVisualMode()` / `SwitchToSplitMode()` / `SwitchToTextMode()` - Visual editor mode switching
- `ParseAndSendToVisualEditor()` - Parses text and sends model to visual editor
- `ShowWhatsNewIfNeeded()` - Version-based What's New dialog display

**MermaidParser.cs** - Parses Mermaid text into typed models:
- `ParseFlowchart()` - Flowchart nodes, edges, subgraphs
- `ParseSequenceDiagram()` - Participants, messages, notes, fragments
- `ParseClassDiagram()` - Classes, methods, relationships
- `ParseStateDiagram()` - States, transitions, composite states
- `ParseERDiagram()` - Entities, attributes, relationships

**MermaidSerializer.cs** - Converts models back to Mermaid text (round-trip support)

**VisualEditorBridge.cs** - Manages communication between C# and the JavaScript visual editor via WebView2 messages

**Rendering Pipeline**

1. User types in AvalonEdit code editor
2. Timer debounces input (300ms delay)
3. Content is sent to WebView2 via JavaScript
4. Mermaid.js renders the diagram in the browser
5. Click events in preview send messages back to C# via WebMessageReceived

**Visual Editor Pipeline**

1. User switches to Visual or Split mode
2. MermaidParser parses the current text into a typed model
3. Model is sent to the JavaScript visual editor via VisualEditorBridge
4. User edits visually (drag nodes, add elements, etc.)
5. JavaScript sends updated model back to C#
6. MermaidSerializer converts the model back to Mermaid text
7. Code editor and preview are updated

**Navigation System**

The navigation dropdown extracts meaningful elements from each diagram type:
- Flowchart: subgraphs
- Sequence: participants, actors, notes, loops, alt blocks
- Class: class definitions
- Gantt/Journey/Timeline: sections
- GitGraph: branch, checkout, merge commands
- C4: Person, System, Container, Component, Boundary
- RequirementDiagram: requirements, elements
- And more...

## Building from Source

```bash
# Restore packages
dotnet restore tools/MermaidEditor/MermaidEditor.csproj

# Build
dotnet build tools/MermaidEditor/MermaidEditor.csproj

# Run
dotnet run --project tools/MermaidEditor/MermaidEditor.csproj
```

Or run the compiled executable from `bin/Debug/net10.0-windows/MermaidEditor.exe`

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+N | New file |
| Ctrl+O | Open file |
| Ctrl+S | Save file |
| Ctrl+Shift+S | Save As |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl++ | Zoom in |
| Ctrl+- | Zoom out |
| Ctrl+0 | Reset zoom |
| Ctrl+F | Find / Fit to window |
| Ctrl+H | Find & Replace |
| Ctrl+Shift+1 | Export PNG |
| Ctrl+Shift+2 | Export SVG |
| Ctrl+Shift+3 | Export EMF |
| Ctrl+Shift+4 | Export Word |

## Supported File Types

- `.mmd` - Mermaid diagram files
- `.mermaid` - Mermaid diagram files
- `.md` - Markdown files (rendered with GitHub styling)

## Creating an Installer

The project includes an Inno Setup script to create a standalone Windows installer.

### Prerequisites

1. Install [Inno Setup 6](https://jrsoftware.org/isdl.php) (free)

### Build Steps

1. Open a PowerShell or Command Prompt in the `tools/MermaidEditor` directory

2. Publish the application as self-contained:
   ```powershell
   # Using the build script (recommended)
   .\Installer\Build-Installer.bat
   
   # Or manually
   dotnet publish -c Release -r win-x64 --self-contained true
   ```

3. Open `Installer\MermaidEditorSetup.iss` in Inno Setup

4. Click Build > Compile (or press Ctrl+F9)

5. The installer will be created at `bin\Installer\MermaidEditorSetup-4.0.0.exe`

### Installer Features

- Installs to Program Files (or user-selected location)
- Creates Start Menu shortcuts
- Optional desktop shortcut
- Optional file associations for .mmd, .mermaid, and .md files
- Automatic uninstall of previous versions
- Includes uninstaller
- Self-contained (no .NET runtime required on target machine)

## Pan and Zoom

- Use mouse scroll wheel to zoom in/out
- Click and drag to pan the diagram
- Use toolbar buttons or View menu for zoom controls

## Diagram Types Supported

All Mermaid diagram types are supported, including:
- Flowcharts (basic and advanced with subgraphs)
- Sequence diagrams
- Class diagrams
- State diagrams
- Entity Relationship diagrams
- Gantt charts
- Pie charts
- Mind maps
- Timelines
- Git graphs
- User journeys
- Quadrant charts
- Requirement diagrams
- C4 diagrams (Context, Container, Component, Dynamic, Deployment)

Visual editing is available for: **Flowchart, Sequence, Class, State, and ER** diagrams.

See the [Mermaid documentation](https://mermaid.js.org/) for syntax reference.

## Contributing

This project is licensed under the GNU General Public License v3.0. Contributions are welcome!

### Development Guidelines

1. **Code Style** - Follow existing patterns in the codebase
2. **XAML** - Use the established theme system via ThemeManager (Dark, Light, Twilight)
3. **Testing** - Test on Windows with various diagram types before submitting PRs
4. **Documentation** - Update this README for significant feature changes

### Adding New Diagram Type Support

To add navigation support for a new Mermaid diagram type:

1. Add the diagram type to the `diagramTypes` array in `ExtractMermaidSections()`
2. Add parsing logic to extract meaningful elements (look for existing patterns)
3. Test with sample diagrams to ensure navigation works correctly

### Adding Visual Editor Support for a New Diagram Type

1. Add a model class in `MermaidModels.cs`
2. Add parser logic in `MermaidParser.cs`
3. Add serializer logic in `MermaidSerializer.cs`
4. Add a new partial class file `VisualEditorBridge.<DiagramType>.cs`
5. Add JavaScript rendering in `Resources/VisualEditor.html`
6. Wire up the mode switching in `MainWindow.xaml.cs`

### Adding New Templates

To add a new template to the New Document dialog:

1. Add a button in `NewDocumentDialog.xaml` with the appropriate thumbnail
2. Add a click handler in `NewDocumentDialog.xaml.cs` that calls `SetTemplateAndClose()`
3. Create a thumbnail image (48x48 PNG) in `Resources/TemplateThumbnails/`

### Key Files to Understand

- **MainWindow.xaml.cs** - Core application logic, rendering, export, mode switching
- **MermaidParser.cs** / **MermaidSerializer.cs** - Text-to-model and model-to-text conversion
- **MermaidModels.cs** - Data models for all supported diagram types
- **VisualEditorBridge.cs** - C#/JS interop for visual editing
- **Resources/VisualEditor.html** - JavaScript visual editor application
- **NewDocumentDialog.xaml.cs** - Template definitions for all diagram types
- **MainWindow.xaml** - UI layout, styling, toolbar configuration

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

## Credits

- [Mermaid.js](https://mermaid.js.org/) - Diagram rendering engine
- [AvalonEdit](http://avalonedit.net/) - WPF text editor component
- [Markdig](https://github.com/xoofx/markdig) - Markdown processor
- [OpenXML SDK](https://github.com/OfficeDev/Open-XML-SDK) - Office document generation

## Contact

Created by Lee Cassin - feel free to open issues or submit pull requests!
