# What's New in Mermaid Editor

---

## Version 4.0.0 — Visual Editor for All 5 Diagram Types
*March 2026*

### Visual Editor
- **Flowchart Visual Editor** — Drag-and-drop nodes, edges, and subgraphs with 14 shape types, connection points, inline label editing, resize handles, and snap-to-grid
- **Sequence Diagram Visual Editor** — Visual editing of participants, messages, fragments (loop/alt/opt/par/break/critical), and notes with drag reordering and property panels
- **Class Diagram Visual Editor** — Visual class boxes with members, relationships (inheritance, composition, aggregation, etc.), and structured member editing
- **State Diagram Visual Editor** — States, transitions, composite/nested states, pseudo-states (fork/join/choice/start/end), curved Bezier transitions, and notes
- **ER Diagram Visual Editor** — Entities with typed attributes, relationships with cardinality markers, and attribute-level editing
- **Text / Visual / Split modes** — Switch between code editor, visual editor, or side-by-side split view
- **Copy & Paste** — Copy/paste nodes, classes, entities, members, attributes, fragments, and notes across all diagram types (Ctrl+C / Ctrl+V)
- **Undo / Redo** — Full undo/redo history (up to 100 steps) with layout preservation across all diagram types
- **Auto-Layout** — Dagre-based automatic layout for all 5 diagram types
- **Node Position Persistence** — Drag positions saved as `@pos` comments in Mermaid code, surviving round-trips between text and visual editing
- **Curved Edges** — Toggle curved Bezier edges for Flowchart, Class, State, and ER diagrams
- **Context Menus** — Right-click context menus with diagram-specific actions (add, edit, delete, copy, paste, move)
- **Property Panels** — Floating property panels for editing labels, shapes, colors, types, and relationships
- **Toolbar** — Diagram-specific toolbar buttons (Add Node, Add Edge, Add Subgraph, Delete, Undo, Redo, Auto-Layout, Zoom)
- **Minimap** — Minimap navigation for large diagrams with click-to-navigate and viewport indicator

### AI Integration
- **Ask AI** (Ctrl+Shift+A) — Chat with OpenAI or Anthropic models to generate, explain, or modify your diagrams and documents
- **Visual Editor AI Mode** — Diagram-type-aware prompting with "Replace Diagram" button to generate complete diagrams from natural language descriptions
- **File Attachments** — Attach images to AI conversations for multi-modal input
- **Streaming Responses** — Real-time streaming of AI responses with stop button

### Code Intelligence
- **Partial Class Architecture** — VisualEditorBridge split into 6 focused files by diagram type for better maintainability

---

## Version 3.0.0 — Settings, AI, Spell Check, and Print
*March 2026*

### Settings & Configuration
- **Settings Dialog** (View > Settings) — Centralized configuration for editor, appearance, auto-save, and AI settings
- **Font Preview** — Live font preview in Settings dialog
- **Editor Defaults** — Configure word wrap, line numbers, minimap, bracket matching, and default file type

### Spell Check
- **Spell Check** — Real-time spell checking with red squiggly underlines using Hunspell dictionaries
- **Right-Click Suggestions** — Right-click misspelled words for correction suggestions

### Print & Export
- **Print Preview** — Full print preview with fit-to-page, fit-to-width scaling, printer selection, and margin controls
- **Code Print Preview** — Print code with syntax highlighting, line numbers, and word wrap options
- **PDF Export** — Save diagrams and documents as PDF from Print Preview (Ctrl+Shift+D)
- **Page Range** — Print specific page ranges (All or From-To)

### Table Generator
- **Table Generator Dialog** — Create markdown tables with configurable rows and columns

### Auto-Save
- **Auto-Save with Session Restore** — Automatic saving with configurable interval (default 30s) and session restore on startup

### Ask AI
- **Ask AI Dialog** — Streaming chat with OpenAI/Anthropic, file attachments, code block extraction, and "Insert at Cursor"

### UI Improvements
- **Custom SVG Icons** — Replaced toolbar and menu icons with custom SVGs for a polished look
- **Toggle Comment Indicator** — Green icon when cursor is inside a comment
- **Markdown Cheat Sheet Template** — New template with all markdown syntax and live preview examples

---

## Version 2.1.0 — Editor Polish and Multi-Format
*February 2026*

### Editor Features
- **Find & Replace** (Ctrl+F / Ctrl+H) — Find and Find & Replace dialogs with F3 for Find Next
- **Minimap** — Toggle minimap with viewport indicator and click-to-navigate
- **Edit Toolbar** — Indent, outdent, toggle comment, move lines up/down, and word wrap toggle
- **View Toolbar** — Split view, line numbers, bracket matching, and minimap toggles
- **Bracket Matching** — Highlight matching brackets with gold/orange indicators
- **Markdown Formatting Toolbar** — Bold, italic, headers, code blocks, lists, links, images, and tables
- **Drag-and-Drop Tab Reordering** — Drag tabs to reorder open documents
- **Scroll Position Preservation** — Editor and preview scroll positions preserved when switching tabs

### Print
- **Print Preview** — Initial print preview with fit-to-page scaling
- **Code Print Preview** — Print code with line numbers

### Zoom
- **Status Bar Zoom Slider** — Word-style zoom slider in the status bar

### File Handling
- **Combined File Filter** — "Mermaid & Markdown" as default filter in Open File dialog

---

## Version 2.0.0 — Themes and File Detection
*February 2026*

### Themes
- **Dark / Light / Twilight** — Three complete themes applied to editor, preview, menus, toolbars, tabs, dialogs, and status bar
- **Theme-Aware Preview** — Mermaid and Markdown preview backgrounds match the selected theme

### File Detection
- **External File Change Detection** — Detects when files are modified externally (cloud drives, other editors) and prompts to reload

### UI
- **New Document Dialog on Startup** — Opens on startup with template gallery and recent files
- **Recent Files in New Document** — Side-by-side layout with recent files list
- **Mermaid Icon** — Custom mermaid tail icon for the application

---

## Version 1.6 — Exports, Navigation, and Templates
*February 2026*

### Exports
- **PNG Export** — High-resolution PNG export with 4x/6x scale selection
- **SVG Export** — Clean SVG export
- **EMF Export** — Windows Enhanced Metafile export for Office integration
- **Word Export** — Export Mermaid diagrams and Markdown documents to Word (.docx) with embedded images
- **Keyboard Shortcuts** — Ctrl+Shift+P (PNG), Ctrl+Shift+S (SVG), Ctrl+Shift+E (EMF), Ctrl+Shift+W (Word)

### Navigation
- **Navigation Dropdown** — Jump to diagram elements (nodes, participants, classes, states, entities) from a dropdown below the tabs
- **Click-to-Highlight** — Click elements in the preview pane to highlight them in the code editor

### Templates
- **New Document Dialog** — Template gallery with Blank, Markdown, Flowchart, Sequence, Class, State, ER, Gantt, Pie, Requirement, and C4 templates
- **Thumbnail Previews** — Template thumbnails in the New Document dialog

### Editor
- **IntelliSense** — Autocomplete for Mermaid keywords, diagram types, and frontmatter config options
- **Mermaid v11** — Upgraded to Mermaid v11 with handDrawn look support
- **Frontmatter Config** — Support for YAML frontmatter configuration blocks

### Multi-Document
- **Tabbed Interface** — Open multiple documents in tabs
- **Tab Context Menu** — Close, Close All, Close All But This
- **Single Instance** — Opening files routes to existing instance

### General
- **Visual Studio-Style Dark Theme** — Modern dark theme matching Visual Studio aesthetics
- **Undo/Redo** — Full undo/redo for the code editor
- **Recent Files** — Recent files menu with quick access
- **Back Button** — Navigate back in the preview pane

---

## Version 1.0 — Initial Release
*February 2026*

- **Mermaid Code Editor** — Syntax-highlighted code editor powered by AvalonEdit
- **Live Preview** — Real-time Mermaid diagram rendering via WebView2
- **Markdown Support** — Edit and preview Markdown documents
- **Drag & Drop** — Open .mmd, .mermaid, and .md files by dragging onto the editor
- **File Browser** — Built-in file browser for .mmd and .md files
- **Pan & Zoom** — Pan and zoom the preview pane
- **Inno Setup Installer** — Windows installer with automatic uninstall of previous versions
