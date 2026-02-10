# Mermaid Editor

A visual IDE for editing Mermaid diagrams and Markdown files, built with WPF and .NET.

## Download

**Download [MermaidEditorSetup-2.0.0.exe](Installer/MermaidEditorSetup-2.0.0.exe?raw=true)** - (Latest Stable) Windows installer (self-contained, no .NET runtime required)

## Details

**Editor Capabilities**
- Live preview as you type with automatic refresh
- Syntax highlighting for Mermaid and Markdown
- IntelliSense code completion for Mermaid keywords
- Click-to-navigate between preview and source code
- Navigation dropdown for quick section jumping
- Undo/Redo support
- Drag and drop file support

**Mermaid Diagram Support**
- All Mermaid diagram types: flowchart, sequence, class, state, ER, gantt, pie, mindmap, timeline, gitGraph, journey, quadrantChart, requirementDiagram, C4 diagrams
- Pan and zoom in the preview pane
- Mermaid v11 with handDrawn look support
- Frontmatter config support for themes and styling

**Markdown Support**
- GitHub-flavored Markdown rendering
- Image embedding with relative path resolution
- Code syntax highlighting in preview

**Export Options**
- PNG export at 4x or 6x resolution
- SVG vector export
- EMF vector export for Office applications
- Word document export with embedded images

**User Interface**
- Visual Studio-style dark theme
- New Document dialog with templates for all diagram types
- File browser with preview on selection
- Purple accent styling on tabs and borders

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

### Project Structure

```
MermaidEditor/
├── MainWindow.xaml           # Main application window UI
├── MainWindow.xaml.cs        # Main window code-behind (3500+ lines)
├── NewDocumentDialog.xaml    # New document template dialog
├── NewDocumentDialog.xaml.cs # Template dialog code-behind
├── App.xaml                  # Application resources
├── App.xaml.cs               # Application startup
├── MermaidEditor.csproj      # Project file
├── app.ico                   # Application icon
├── LICENSE                   # GNU GPL v3 license
├── README.md                 # This file
├── Resources/
│   └── TemplateThumbnails/   # Template preview images (48x48 PNG)
└── Installer/
    └── MermaidEditorSetup.iss # Inno Setup installer script
```

### Key Components

**MainWindow.xaml.cs** contains the core application logic:

- `RenderMermaid()` - Renders Mermaid diagrams using WebView2 and Mermaid.js
- `RenderMarkdown()` - Renders Markdown using Markdig and custom HTML template
- `ExtractMermaidSections()` - Parses diagram content for navigation dropdown
- `FindAndHighlightInEditor()` - Click-to-navigate from preview to source
- `ExportMermaidToWord()` - Exports diagrams as PNG embedded in Word
- `ConvertMarkdownToWord()` - Converts Markdown to formatted Word document

**Rendering Pipeline**

1. User types in AvalonEdit code editor
2. Timer debounces input (300ms delay)
3. Content is sent to WebView2 via JavaScript
4. Mermaid.js renders the diagram in the browser
5. Click events in preview send messages back to C# via WebMessageReceived

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
| Ctrl+F | Fit to window |
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

5. The installer will be created at `bin\Installer\MermaidEditorSetup-1.7.exe`

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

See the [Mermaid documentation](https://mermaid.js.org/) for syntax reference.

## Contributing

This project is licensed under the GNU General Public License v3.0. Contributions are welcome!

### Development Guidelines

1. **Code Style** - Follow existing patterns in the codebase
2. **XAML** - Use the established dark theme colors (#1E1E1E background, #F1F1F1 foreground, #9184EE accent)
3. **Testing** - Test on Windows with various diagram types before submitting PRs
4. **Documentation** - Update this README for significant feature changes

### Adding New Diagram Type Support

To add navigation support for a new Mermaid diagram type:

1. Add the diagram type to the `diagramTypes` array in `ExtractMermaidSections()`
2. Add parsing logic to extract meaningful elements (look for existing patterns)
3. Test with sample diagrams to ensure navigation works correctly

### Adding New Templates

To add a new template to the New Document dialog:

1. Add a button in `NewDocumentDialog.xaml` with the appropriate thumbnail
2. Add a click handler in `NewDocumentDialog.xaml.cs` that calls `SetTemplateAndClose()`
3. Create a thumbnail image (48x48 PNG) in `Resources/TemplateThumbnails/`

### Key Files to Understand

- **MainWindow.xaml.cs** - Core application logic, rendering, export
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
