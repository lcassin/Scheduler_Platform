# Mermaid Editor

A simple Windows-only .NET WPF application for editing Mermaid diagrams with live preview.

## Features

- Split-pane interface with code editor on the left and live preview on the right
- Live preview updates as you type (with 500ms debounce)
- Pan and zoom support for viewing large diagrams
- File operations: New, Open, Save, Save As
- Export diagrams as PNG or SVG
- Keyboard shortcuts for common operations
- Dark-themed code editor with monospace font

## Requirements

- Windows 10/11
- .NET 10.0 Runtime
- WebView2 Runtime (usually pre-installed on Windows 10/11)

## Building

```bash
cd tools/MermaidEditor
dotnet build
```

## Running

```bash
dotnet run
```

Or run the compiled executable from `bin/Debug/net10.0-windows/MermaidEditor.exe`

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+N | New file |
| Ctrl+O | Open file |
| Ctrl+S | Save file |
| Ctrl+Shift+S | Save As |
| Ctrl++ | Zoom in |
| Ctrl+- | Zoom out |
| Ctrl+0 | Reset zoom |
| Ctrl+F | Fit to window |

## Supported File Types

- `.mmd` - Mermaid files
- `.mermaid` - Mermaid files
- `.md` - Markdown files (for opening existing diagrams)

## Pan and Zoom

- Use mouse scroll wheel to zoom in/out
- Click and drag to pan the diagram
- Use toolbar buttons or View menu for zoom controls

## Diagram Types Supported

All Mermaid diagram types are supported, including:
- Flowcharts
- Sequence diagrams
- Class diagrams
- State diagrams
- Entity Relationship diagrams
- Gantt charts
- Pie charts
- And more...

See the [Mermaid documentation](https://mermaid.js.org/) for syntax reference.
