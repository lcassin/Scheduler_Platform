# Mermaid Editor

A simple Windows-only .NET WPF application for editing Mermaid diagrams and Markdown files with live preview.

## Features

- Split-pane interface with code editor on the left and live preview on the right
- Live preview updates as you type (with 500ms debounce)
- Mermaid diagram rendering with pan and zoom support
- Markdown rendering with GitHub-style formatting and syntax highlighting
- Syntax highlighting and IntelliSense autocomplete for Mermaid keywords
- File operations: New, Open, Save, Save As
- Export diagrams as PNG or SVG
- Drag-and-drop file support
- Keyboard shortcuts for common operations
- Dark-themed code editor with line numbers

## Requirements

- Windows 10/11
- .NET 10.0 Runtime (not needed if using the installer with self-contained deployment)
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

5. The installer will be created at `bin\Installer\MermaidEditorSetup-1.2.exe`

### Installer Features

- Installs to Program Files (or user-selected location)
- Creates Start Menu shortcuts
- Optional desktop shortcut
- Optional file associations for .mmd and .mermaid files
- Includes uninstaller
- Self-contained (no .NET runtime required on target machine)

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
