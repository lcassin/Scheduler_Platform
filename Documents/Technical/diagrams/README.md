# Scheduler Platform Diagrams

This folder contains the source files and generated images for all architecture diagrams.

## Files

| File | Description |
|------|-------------|
| `core-er-diagram.mmd` | Entity Relationship diagram showing all database entities and relationships |
| `core-er-diagram.png` | PNG export of the ER diagram |
| `core-er-diagram.svg` | SVG export of the ER diagram (for Visio import) |
| `core-class-diagram.mmd` | UML Class diagram showing entity classes and inheritance |
| `core-class-diagram.png` | PNG export of the class diagram |
| `core-class-diagram.svg` | SVG export of the class diagram (for Visio import) |

## Regenerating Diagrams

To regenerate diagrams after editing the `.mmd` files, use the Mermaid CLI:

```bash
# Install mermaid-cli (if not already installed)
npm install -g @mermaid-js/mermaid-cli

# Generate PNG
mmdc -i core-er-diagram.mmd -o core-er-diagram.png -b white

# Generate SVG (for Visio import)
mmdc -i core-er-diagram.mmd -o core-er-diagram.svg -b white
```

## Creating Visio Files from SVG

1. Open Microsoft Visio
2. Create a new blank diagram
3. Go to **Insert** > **Pictures** > **This Device...**
4. Select the `.svg` file you want to import
5. Right-click the imported image and select **Convert to Shape** (Visio 2016+)
6. Edit text and shapes as needed
7. Save as `.vsdx`

Note: After converting to shapes, you may need to adjust connectors and layout for optimal appearance.

## Alternative: Edit in draw.io First

If you want to edit the diagram before importing to Visio:

1. Open [draw.io](https://app.diagrams.net/) (web or desktop)
2. Go to **Arrange** (or **Insert**) → **Advanced** → **Mermaid**
3. Paste the contents of any `.mmd` file
4. Click Insert to render the diagram
5. Edit the diagram as needed in draw.io
6. Export as SVG: **File** → **Export As** → **SVG**
7. Import the SVG into Visio using the steps above

Note: draw.io can import Visio files but does not have a native "Export to Visio" option. Use SVG as the intermediate format.

## Mermaid Syntax Reference

- [Mermaid Documentation](https://mermaid.js.org/intro/)
- [Entity Relationship Diagrams](https://mermaid.js.org/syntax/entityRelationshipDiagram.html)
- [Class Diagrams](https://mermaid.js.org/syntax/classDiagram.html)
- [Sequence Diagrams](https://mermaid.js.org/syntax/sequenceDiagram.html)

## Database Naming Conventions

All diagrams follow these naming conventions:

- **Table names**: Singular (e.g., `Schedule`, `Client`, `User`)
- **Primary keys**: `Id` (e.g., `int Id PK`)
- **Foreign keys**: `<RelatedTable>Id` (e.g., `ClientId FK`)
- **Audit fields**:
  - `CreatedDateTime` - When the record was created
  - `CreatedBy` - Who created the record
  - `ModifiedDateTime` - When the record was last modified
  - `ModifiedBy` - Who last modified the record
- **DateTime fields**: Use `DateTime` suffix (e.g., `StartDateTime`, `EndDateTime`, `NextRunDateTime`)
