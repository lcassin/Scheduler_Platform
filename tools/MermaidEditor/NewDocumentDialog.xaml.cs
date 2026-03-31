using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace MermaidEditor;

public partial class NewDocumentDialog : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    public string? SelectedTemplate { get; private set; }
    public bool IsMermaid { get; private set; } = true;
    public bool OpenExistingFile { get; private set; } = false;
    public string? SelectedRecentFilePath { get; private set; }

    private List<string> _recentFiles = new();

    public NewDocumentDialog()
    {
        InitializeComponent();
        SourceInitialized += NewDocumentDialog_SourceInitialized;
        LoadRecentFiles();
        PopulateRecentFilesList();
        PopulateTemplates();
    }

    private void NewDocumentDialog_SourceInitialized(object? sender, EventArgs e)
    {
        UpdateTitleBarTheme();
    }

    private void UpdateTitleBarTheme()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                var isDark = ThemeManager.IsDarkTheme;
                int darkModeValue = isDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, sizeof(int));

                var colors = ThemeManager.GetThemeColors(ThemeManager.CurrentTheme);
                int captionColor = ColorToInt(colors.Background);
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
            }
        }
        catch
        {
            // Silently fail if DWM API is not available
        }
    }

    private static int ColorToInt(System.Windows.Media.Color color)
    {
        // Color format is 0x00BBGGRR (BGR, not RGB)
        return (color.B << 16) | (color.G << 8) | color.R;
    }

    private static readonly string RecentFilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MermaidEditor", "recent.json");

    private void LoadRecentFiles()
    {
        try
        {
            if (File.Exists(RecentFilesPath))
            {
                var json = File.ReadAllText(RecentFilesPath);
                var allFiles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                
                // Check for missing files
                var existingFiles = allFiles.Where(f => !string.IsNullOrWhiteSpace(f) && File.Exists(f)).ToList();
                var missingFiles = allFiles.Where(f => !string.IsNullOrWhiteSpace(f) && !File.Exists(f)).ToList();
                
                if (missingFiles.Count > 0)
                {
                    var fileNames = string.Join("\n", missingFiles.Select(f => Path.GetFileName(f)));
                    var result = System.Windows.MessageBox.Show(
                        $"The following recent files no longer exist:\n\n{fileNames}\n\nRemove them from the recent files list?",
                        "Missing Files", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        // Save the cleaned list
                        SaveRecentFiles(existingFiles);
                    }
                }
                
                _recentFiles = existingFiles.Take(10).ToList();
            }
        }
        catch
        {
            // Ignore errors loading recent files
        }
    }

    private void SaveRecentFiles(List<string> files)
    {
        try
        {
            var directory = Path.GetDirectoryName(RecentFilesPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var json = System.Text.Json.JsonSerializer.Serialize(files);
            File.WriteAllText(RecentFilesPath, json);
        }
        catch
        {
            // Silently fail if we can't save recent files
        }
    }

    private void PopulateRecentFilesList()
    {
        if (_recentFiles.Count == 0)
        {
            NoRecentFilesText.Visibility = Visibility.Visible;
            return;
        }

        NoRecentFilesText.Visibility = Visibility.Collapsed;

        foreach (var filePath in _recentFiles)
        {
            var fileName = Path.GetFileName(filePath);
            var directory = Path.GetDirectoryName(filePath) ?? "";
            
            // Shorten the directory path if it's too long
            if (directory.Length > 35)
            {
                directory = "..." + directory.Substring(directory.Length - 32);
            }

            var button = new System.Windows.Controls.Button
            {
                Style = (Style)FindResource("RecentFileButtonStyle"),
                Tag = filePath,
                Content = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = fileName.EndsWith(".mmd") ? "\uE8A5" : "\uE8A5",
                            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                            FontSize = 16,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 8, 0),
                            Foreground = (System.Windows.Media.Brush)FindResource("ThemeDisabledForegroundBrush")
                        },
                        new StackPanel
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = fileName,
                                    FontWeight = FontWeights.SemiBold,
                                    Foreground = (System.Windows.Media.Brush)FindResource("ThemeForegroundBrush")
                                },
                                new TextBlock
                                {
                                    Text = directory,
                                    FontSize = 10,
                                    Foreground = (System.Windows.Media.Brush)FindResource("ThemeDisabledForegroundBrush")
                                }
                            }
                        }
                    }
                }
            };
            
            button.Click += RecentFile_Click;
            RecentFilesPanel.Children.Add(button);
        }
    }

    private void RecentFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string filePath)
        {
            SelectedRecentFilePath = filePath;
            DialogResult = true;
            Close();
        }
    }

    private void SetTemplateAndClose(string template, bool isMermaid = true)
    {
        SelectedTemplate = template;
        IsMermaid = isMermaid;
        DialogResult = true;
        Close();
    }

    private string _selectedBlankDiagramType = "Flowchart";

    private void DiagramTypeItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string type)
        {
            _selectedBlankDiagramType = type;

            // Update the label text
            if (BlankDiagramTypeLabel != null)
                BlankDiagramTypeLabel.Text = type;

            // Update the icon to match the selected diagram type
            if (BlankDiagramTypeIcon != null)
            {
                var iconName = type switch
                {
                    "Flowchart" => "flowchart",
                    "Sequence" => "sequence",
                    "Class" => "class",
                    "State" => "state",
                    "ER" => "erdiagram",
                    "Gantt" => "gantt",
                    "Pie" => "pie",
                    "MindMap" => "mindmap",
                    "Timeline" => "timeline",
                    "Git Graph" => "gitgraph",
                    "Journey" => "journey",
                    "Quadrant" => "quadrant",
                    "Requirement" => "requirement",
                    "C4" => "c4",
                    "Sankey" => "sankey",
                    "XY Chart" => "xychart",
                    "Block" => "block",
                    "Packet" => "packet",
                    "Kanban" => "kanban",
                    "Architecture" => "architecture",
                    "ZenUML" => "zenuml",
                    "Radar" => "radar",
                    "Treemap" => "treemap",
                    "Venn" => "venn",
                    _ => "blank"
                };
                try
                {
                    BlankDiagramTypeIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri($"Resources/TemplateThumbnails/{iconName}.png", UriKind.Relative));
                }
                catch { /* Ignore if icon not found */ }
            }

            // Close the popup
            if (DiagramTypeDropdownToggle != null)
                DiagramTypeDropdownToggle.IsChecked = false;
        }
    }

    private string GetBlankTemplateForSelectedType()
    {
        var selectedType = _selectedBlankDiagramType;

        return selectedType switch
        {
            "Flowchart" => @"flowchart TD
    A[Start] --> B[End]",
            "Sequence" => @"sequenceDiagram
    participant A as Alice
    participant B as Bob
    A->>B: Hello
    B-->>A: Hi there",
            "Class" => @"classDiagram
    class MyClass {
        +String name
        +doSomething() void
    }",
            "State" => @"stateDiagram-v2
    [*] --> Idle
    Idle --> Active : start
    Active --> Idle : stop",
            "ER" => @"erDiagram
    CUSTOMER ||--o{ ORDER : places
    CUSTOMER {
        int id PK
        string name
    }
    ORDER {
        int id PK
        date created
    }",
            "Gantt" => @"gantt
    title My Project
    dateFormat YYYY-MM-DD
    axisFormat %m/%d
    tickInterval 1day
    section Phase 1
        Task 1 :a1, 2024-01-01, 7d
        Task 2 :a2, after a1, 5d
        Finalization :crit, a3, after a2, 2d",
            "Pie" => @"pie showData
    title Distribution
    ""Category A"" : 50
    ""Category B"" : 50",
            "MindMap" => @"mindmap
    root(Central Topic)
        Branch A
        Branch B",
            "Timeline" => @"timeline
    title My Timeline
    section Phase 1
        Event A : Description A
        Event B : Description B
    section Phase 2
        Event C : Description C",
            "Git Graph" => @"gitGraph
    commit id: ""Initial""
    branch develop
    commit id: ""Feature""
    checkout main
    merge develop",
            "Journey" => @"journey
    title User Journey
    section Getting Started
        Sign up: 5: User
        First login: 4: User
    section Using App
        Create item: 3: User
        Share item: 4: User",
            "Quadrant" => @"quadrantChart
    title Priority Matrix
    x-axis Low Effort --> High Effort
    y-axis Low Impact --> High Impact
    quadrant-1 Do First
    quadrant-2 Schedule
    quadrant-3 Delegate
    quadrant-4 Eliminate
    Item A: [0.8, 0.9]
    Item B: [0.3, 0.7]",
            "Requirement" => "requirementDiagram\r\n\r\n    requirement my_req {\r\n    id: 1\r\n    text: Sample requirement\r\n    risk: medium\r\n    verifymethod: test\r\n    }",
            "C4" => @"C4Context
    title System Context
    Person(user, ""User"", ""A user of the system"")
    System(system, ""My System"", ""Main application"")
    Rel(user, system, ""Uses"")",
            "Sankey" => @"sankey-beta

Agricultural 'waste',Bio-conversion,124.729
Bio-conversion,Liquid,0.597
Bio-conversion,Losses,26.862
Bio-conversion,Solid,280.322
Bio-conversion,Gas,81.144",
            "XY Chart" => @"xychart-beta
    title ""Sales Revenue""
    x-axis [jan, feb, mar, apr, may, jun]
    y-axis ""Revenue (in $)"" 4000 --> 11000
    bar [5000, 6000, 7500, 8200, 9500, 10500]
    line [5000, 6000, 7500, 8200, 9500, 10500]",
            "Block" => @"block-beta
    columns 3
    a[""Frontend""] b[""Backend""] c[""Database""]",
            "Packet" => @"packet-beta
    0-15: ""Source Port""
    16-31: ""Destination Port""
    32-63: ""Sequence Number""
    64-95: ""Acknowledgment Number""
    96-99: ""Data Offset""
    100-105: ""Reserved""
    106-111: ""Flags""
    112-127: ""Window Size""
    128-143: ""Checksum""
    144-159: ""Urgent Pointer""",
            "Kanban" => @"kanban
    column1[""To Do""]
        task1[""Design UI""]
        task2[""Write tests""]
    column2[""In Progress""]
        task3[""Implement API""]
    column3[""Done""]
        task4[""Setup CI/CD""]",
            "Architecture" => @"architecture-beta
    group api(cloud)[""API Layer""]

    service db(database)[""Database""] in api
    service server(server)[""Server""] in api
    service disk(disk)[""Storage""] in api

    db:R -- L:server
    server:R -- L:disk",
            "ZenUML" => @"zenuml
    title Order Service
    @Actor Client
    @Boundary OrderController
    @Entity OrderService

    Client->OrderController.placeOrder() {
        OrderController->OrderService.create() {
            return id
        }
    }",
            "Radar" => @"radar-beta
    title Skills Assessment
    axis js[""JavaScript""], css[""CSS""], html[""HTML""], react[""React""], node[""Node.js""]
    curve a[""Current""]{80, 60, 90, 70, 50}
    curve b[""Target""]{90, 80, 95, 85, 75}
    max 100",
            "Treemap" => @"treemap-beta
    ""Project""
        ""Frontend""
            ""React"": 30
            ""CSS"": 20
        ""Backend""
            ""API"": 25
            ""Database"": 25",
            "Venn" => @"venn-beta
    title ""Team Skills""
    set Frontend
    set Backend
    set DevOps
    union Frontend,Backend[""Full-Stack""]
    union Backend,DevOps[""SRE""]",
            _ => @"flowchart TD
    A[Start] --> B[End]"
        };
    }

    private void BlankMermaid_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(GetBlankTemplateForSelectedType());
    }

    private void BlankMarkdown_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"# Title

Your content here...
", false);
    }

    private void MarkdownCheatSheet_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"# Markdown Cheat Sheet

A comprehensive reference for all Markdown syntax. Edit this document and watch the preview update in real time!

---

## 1. Headings

# Heading 1
## Heading 2
### Heading 3
#### Heading 4
##### Heading 5
###### Heading 6

---

## 2. Text Formatting

**Bold text** using double asterisks  
__Bold text__ using double underscores  
*Italic text* using single asterisks  
_Italic text_ using single underscores  
***Bold and italic*** using triple asterisks  
~~Strikethrough~~ using double tildes  
`Inline code` using backticks  
This is <sub>subscript</sub> and this is <sup>superscript</sup>

---

## 3. Paragraphs and Line Breaks

This is a paragraph. Leave a blank line between paragraphs to separate them.

This is a new paragraph. To create a line break within a paragraph,  
end a line with two spaces (or use `<br>`) before the next line.<br>
Like this.

---

## 4. Blockquotes

> This is a blockquote.
> It can span multiple lines.
>
> > Nested blockquotes are also supported.
> >
> > > And even deeper nesting.

> **Tip:** Blockquotes can contain other Markdown elements like **bold**, *italic*, and `code`.

---

## 5. Lists

### Unordered Lists

- Item 1
- Item 2
  - Nested item 2a
  - Nested item 2b
    - Deeply nested item
- Item 3

### Ordered Lists

1. First item
2. Second item
   1. Sub-item 2a
   2. Sub-item 2b
3. Third item

### Task Lists

- [x] Completed task
- [x] Another completed task
- [ ] Incomplete task
- [ ] Another incomplete task

---

## 6. Links

[Inline link](https://example.com)  
[Link with title](https://example.com ""Hover to see this title"")  
[Reference-style link][ref1]  
[Numbered reference link][1]  
<https://example.com> (auto-linked URL)  
<user@example.com> (auto-linked email)

[ref1]: https://example.com ""Reference Link""
[1]: https://example.com ""Numbered Reference""

---

## 7. Images

![Alt text for image](https://via.placeholder.com/400x100/4a90d9/ffffff?text=Sample+Image)

![Small image](https://via.placeholder.com/150x50/e74c3c/ffffff?text=150x50)

---

## 8. Code

### Inline Code

Use `console.log()` to print output. The `<div>` element is a block container.

### Fenced Code Blocks

```javascript
// JavaScript example
function greet(name) {
    console.log(`Hello, ${name}!`);
    return { message: `Welcome, ${name}` };
}

greet(""World"");
```

```python
# Python example
def fibonacci(n):
    a, b = 0, 1
    for _ in range(n):
        yield a
        a, b = b, a + b

for num in fibonacci(10):
    print(num)
```

```csharp
// C# example
public class Program
{
    public static void Main(string[] args)
    {
        var message = ""Hello, World!"";
        Console.WriteLine(message);
    }
}
```

```html
<!-- HTML example -->
<div class=""container"">
    <h1>Hello World</h1>
    <p>This is a <strong>paragraph</strong>.</p>
</div>
```

```css
/* CSS example */
.container {
    display: flex;
    justify-content: center;
    align-items: center;
    background-color: #f0f0f0;
    padding: 20px;
}
```

```sql
-- SQL example
SELECT users.name, COUNT(orders.id) AS order_count
FROM users
LEFT JOIN orders ON users.id = orders.user_id
WHERE users.active = 1
GROUP BY users.name
HAVING order_count > 5
ORDER BY order_count DESC;
```

```json
{
    ""name"": ""Markdown Cheat Sheet"",
    ""version"": ""1.0"",
    ""features"": [""headings"", ""lists"", ""tables"", ""code""],
    ""enabled"": true
}
```

---

## 9. Tables

### Basic Table

| Column 1 | Column 2 | Column 3 |
|----------|----------|----------|
| Row 1    | Data     | Data     |
| Row 2    | Data     | Data     |
| Row 3    | Data     | Data     |

### Aligned Table

| Left Aligned | Center Aligned | Right Aligned |
|:-------------|:--------------:|--------------:|
| Left         |    Center      |         Right |
| Text         |    Text        |          Text |
| Data         |    Data        |          Data |

---

## 10. Horizontal Rules

Three or more hyphens, asterisks, or underscores create a horizontal rule:

---

***

___

---

## 11. HTML in Markdown

Markdown supports inline HTML for advanced formatting:

<details>
<summary>Click to expand (collapsible section)</summary>

This content is hidden by default and revealed when the user clicks the summary.

- Works with lists
- **And formatting**
- `And code`

</details>

<div align=""center"">
    <strong>Centered content using HTML</strong><br>
    <em>With line breaks and formatting</em>
</div>

<kbd>Ctrl</kbd> + <kbd>C</kbd> to copy (keyboard keys)

Text with <mark>highlighted background</mark> using the mark tag.

---

## 12. Escaping Characters

Use a backslash to display literal characters that normally have special meaning:

\* Not italic \*  
\# Not a heading  
\- Not a list item  
\[Not a link\]  
\`Not inline code\`  
\| Not a table column \|

---

## 13. Footnotes

Here is a sentence with a footnote[^1] and another one[^2].

[^1]: This is the first footnote content.
[^2]: This is the second footnote — it can contain **formatting** and `code`.

---

## 14. Definition Lists

Term 1
: Definition for term 1

Term 2
: First definition for term 2
: Second definition for term 2

---

## 15. Emoji (if supported)

:smile: :rocket: :thumbsup: :heart: :warning: :star:

---

## 16. Comments

<!-- This is an HTML comment — it will NOT appear in the rendered output -->
<!-- Comments are useful for notes to yourself or temporarily hiding content -->

The text above this line contains hidden HTML comments (visible in the editor, hidden in preview).

---

## 17. Math (if supported by renderer)

Inline math: $E = mc^2$

Block math:

$$
\sum_{i=1}^{n} x_i = x_1 + x_2 + \cdots + x_n
$$

---

## 18. Abbreviations

The HTML specification is maintained by the W3C.

*[HTML]: Hyper Text Markup Language
*[W3C]: World Wide Web Consortium

---

> **Note:** Not all Markdown renderers support every feature listed here.  
> Standard features (headings, bold, italic, links, images, code, lists, tables, blockquotes)  
> are universally supported. Extended features (task lists, footnotes, math, abbreviations,  
> emoji) depend on the renderer.
", false);
    }

    private void Flowchart_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
flowchart TD
    A[Start] --> B{Decision}
    B -->|Yes| C[Action 1]
    B -->|No| D[Action 2]
    C --> E[End]
    D --> E

    %% Node shapes:
    %% [text] - Rectangle
    %% (text) - Rounded rectangle
    %% ([text]) - Stadium shape
    %% [[text]] - Subroutine
    %% [(text)] - Cylinder
    %% ((text)) - Circle
    %% {text} - Diamond
    %% {{text}} - Hexagon
    %% [/text/] - Parallelogram
    %% [\text\] - Parallelogram alt
    %% [/text\] - Trapezoid
    %% [\text/] - Trapezoid alt

    %% Arrow types:
    %% --> - Arrow
    %% --- - Line
    %% -.-> - Dotted arrow
    %% ==> - Thick arrow
    %% --text--> - Arrow with text
    %% -->|text| - Arrow with text alt");
    }

    private void FlowchartAdvanced_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  look: handDrawn
  theme: neutral
---
flowchart TB
    subgraph Frontend[""Frontend Layer""]
        direction LR
        UI[Web UI]
        Mobile[Mobile App]
    end

    subgraph Backend[""Backend Services""]
        direction TB
        API[API Gateway]
        Auth[Auth Service]
        Business[Business Logic]
    end

    subgraph Data[""Data Layer""]
        direction LR
        DB[(Database)]
        Cache[(Cache)]
    end

    Frontend --> API
    API --> Auth
    API --> Business
    Business --> Data

    %% Styling
    style Frontend fill:#e1f5fe
    style Backend fill:#fff3e0
    style Data fill:#e8f5e9

    classDef highlight fill:#ffeb3b,stroke:#f57f17
    class API highlight");
    }

    private void Sequence_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
sequenceDiagram
    autonumber
    participant U as User
    participant C as Client
    participant S as Server
    participant DB as Database

    U->>C: Request Action
    activate C
    C->>S: API Call
    activate S
    S->>DB: Query Data
    activate DB
    DB-->>S: Return Results
    deactivate DB
    S-->>C: Response
    deactivate S
    C-->>U: Display Results
    deactivate C

    Note over U,C: User interaction
    Note over S,DB: Server processing

    %% Message types:
    %% ->> Solid line with arrowhead
    %% -->> Dotted line with arrowhead
    %% -) Solid line with open arrow
    %% --) Dotted line with open arrow
    %% -x Solid line with cross
    %% --x Dotted line with cross

    alt Success
        S->>C: 200 OK
    else Error
        S->>C: 500 Error
    end

    loop Retry Logic
        C->>S: Retry Request
    end");
    }

    private void ClassDiagram_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
classDiagram
    class Animal {
        +String name
        +int age
        +makeSound() void
        +move() void
    }

    class Dog {
        +String breed
        +bark() void
        +fetch() void
    }

    class Cat {
        +bool isIndoor
        +meow() void
        +scratch() void
    }

    class Pet {
        <<interface>>
        +play() void
        +feed() void
    }

    Animal <|-- Dog : extends
    Animal <|-- Cat : extends
    Pet <|.. Dog : implements
    Pet <|.. Cat : implements

    %% Relationships:
    %% <|-- Inheritance
    %% *-- Composition
    %% o-- Aggregation
    %% --> Association
    %% -- Link (solid)
    %% ..> Dependency
    %% ..|> Realization
    %% .. Link (dashed)

    %% Cardinality:
    %% ""1"" Only 1
    %% ""0..1"" Zero or One
    %% ""1..*"" One or more
    %% ""*"" Many
    %% ""n"" n
    %% ""0..n"" zero to n
    %% ""1..n"" one to n");
    }

    private void StateDiagram_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
stateDiagram-v2
    [*] --> Idle

    Idle --> Processing : Start
    Processing --> Success : Complete
    Processing --> Error : Fail
    Success --> Idle : Reset
    Error --> Idle : Retry

    state Processing {
        [*] --> Validating
        Validating --> Executing : Valid
        Validating --> [*] : Invalid
        Executing --> [*]
    }

    state Error {
        [*] --> LogError
        LogError --> NotifyUser
        NotifyUser --> [*]
    }

    note right of Idle : System is ready
    note left of Error : Handle failures gracefully");
    }

    private void ERDiagram_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
erDiagram
    CUSTOMER ||--o{ ORDER : places
    CUSTOMER {
        int id PK
        string name
        string email UK
        date created_at
    }

    ORDER ||--|{ ORDER_ITEM : contains
    ORDER {
        int id PK
        int customer_id FK
        date order_date
        decimal total
        string status
    }

    ORDER_ITEM }|--|| PRODUCT : includes
    ORDER_ITEM {
        int id PK
        int order_id FK
        int product_id FK
        int quantity
        decimal price
    }

    PRODUCT {
        int id PK
        string name
        string description
        decimal price
        int stock
    }

    %% Relationship types:
    %% ||--|| One to one
    %% ||--o{ One to zero or more
    %% ||--|{ One to one or more
    %% }o--o{ Zero or more to zero or more");
    }

    private void Gantt_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
gantt
    title Project Timeline
    dateFormat YYYY-MM-DD
    axisFormat %m/%d
    tickInterval 1week
    excludes weekends

    section Planning
        Requirements gathering :a1, 2024-01-01, 7d
        Design phase          :a2, after a1, 10d
        Review                :milestone, m1, after a2, 0d

    section Development
        Backend development   :b1, after m1, 14d
        Frontend development  :b2, after m1, 14d
        Integration           :b3, after b1, 7d

    section Testing
        Unit testing          :c1, after b3, 5d
        Integration testing   :c2, after c1, 5d
        UAT                   :c3, after c2, 7d

    section Deployment
        Staging deployment    :d1, after c3, 2d
        Production deployment :milestone, m2, after d1, 0d

    %% Task status:
    %% done - Completed
    %% active - In progress
    %% crit - Critical path");
    }

    private void Pie_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
pie showData
    title Project Time Distribution
    ""Development"" : 45
    ""Testing"" : 25
    ""Documentation"" : 15
    ""Meetings"" : 10
    ""Other"" : 5");
    }

    private void Mindmap_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
mindmap
    root((Project))
        Planning
            Requirements
            Timeline
            Resources
        Development
            Frontend
                UI Design
                Components
            Backend
                API
                Database
        Testing
            Unit Tests
            Integration
            UAT
        Deployment
            Staging
            Production
            Monitoring");
    }

    private void Timeline_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
timeline
    title Project Milestones
    section Q1 2024
        January : Project kickoff
                : Team formation
        February : Requirements complete
        March : Design approved
    section Q2 2024
        April : Development starts
        May : Alpha release
        June : Beta release
    section Q3 2024
        July : Testing phase
        August : Bug fixes
        September : Release candidate
    section Q4 2024
        October : Production release
        November : Post-launch support
        December : Project review");
    }

    private void GitGraph_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
gitGraph
    commit id: ""Initial""
    branch develop
    checkout develop
    commit id: ""Feature A""
    commit id: ""Feature B""
    branch feature-x
    checkout feature-x
    commit id: ""Work on X""
    commit id: ""Complete X""
    checkout develop
    merge feature-x
    commit id: ""Bug fix""
    checkout main
    merge develop tag: ""v1.0""
    checkout develop
    commit id: ""New feature""
    checkout main
    merge develop tag: ""v1.1""");
    }

    private void Journey_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
journey
    title User Purchase Journey
    section Discovery
        Visit website: 5: User
        Browse products: 4: User
        Read reviews: 4: User
    section Selection
        Add to cart: 5: User
        View cart: 3: User
        Apply coupon: 4: User
    section Checkout
        Enter shipping: 3: User
        Enter payment: 2: User
        Confirm order: 5: User
    section Post-Purchase
        Receive confirmation: 5: User, System
        Track shipment: 4: User
        Receive product: 5: User

    %% Scores: 1 (frustrated) to 5 (happy)");
    }

    private void Quadrant_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
quadrantChart
    title Priority Matrix
    x-axis Low Effort --> High Effort
    y-axis Low Impact --> High Impact
    quadrant-1 Do First
    quadrant-2 Schedule
    quadrant-3 Delegate
    quadrant-4 Eliminate

    Task A: [0.8, 0.9]
    Task B: [0.3, 0.8]
    Task C: [0.7, 0.3]
    Task D: [0.2, 0.2]
    Task E: [0.5, 0.6]
    Task F: [0.9, 0.4]");
    }

    private void Requirement_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose("---\r\nconfig:\r\n    theme: default\r\n---\r\nrequirementDiagram\r\n\r\n    requirement test_req {\r\n    id: 1\r\n    text: the test text.\r\n    risk: high\r\n    verifymethod: test\r\n    }\r\n\r\n    functionalRequirement test_req2 {\r\n    id: 1.1\r\n    text: the second test text.\r\n    risk: low\r\n    verifymethod: inspection\r\n    }\r\n\r\n    performanceRequirement test_req3 {\r\n    id: 1.2\r\n    text: the third test text.\r\n    risk: medium\r\n    verifymethod: demonstration\r\n    }\r\n\r\n    element test_entity {\r\n    type: simulation\r\n    }\r\n\r\n    element test_entity2 {\r\n    type: word doc\r\n    docRef: reqs/test_entity\r\n    }\r\n\r\n    test_entity - satisfies -> test_req2\r\n    test_req - traces -> test_req2\r\n    test_req - contains -> test_req3\r\n\r\n    %% Requirement types:\r\n    %% requirement - Generic requirement\r\n    %% functionalRequirement - Functional requirement\r\n    %% performanceRequirement - Performance requirement\r\n    %% interfaceRequirement - Interface requirement\r\n    %% physicalRequirement - Physical requirement\r\n    %% designConstraint - Design constraint\r\n\r\n    %% Relationship types:\r\n    %% contains - Parent contains child\r\n    %% copies - Copies another requirement\r\n    %% derives - Derived from another\r\n    %% satisfies - Element satisfies requirement\r\n    %% verifies - Element verifies requirement\r\n    %% refines - Refines another requirement\r\n    %% traces - Traces to another requirement");
    }

    private void C4_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
C4Context
    title System Context Diagram for E-Commerce Platform

    Person(customer, ""Customer"", ""A user who browses and purchases products"")
    Person(admin, ""Administrator"", ""Manages products and orders"")

    System(ecommerce, ""E-Commerce Platform"", ""Allows customers to browse and purchase products"")

    System_Ext(payment, ""Payment Gateway"", ""Handles payment processing"")
    System_Ext(shipping, ""Shipping Service"", ""Manages order delivery"")
    System_Ext(email, ""Email Service"", ""Sends notifications"")

    Rel(customer, ecommerce, ""Browses products, places orders"")
    Rel(admin, ecommerce, ""Manages inventory, processes orders"")
    Rel(ecommerce, payment, ""Processes payments"", ""HTTPS"")
    Rel(ecommerce, shipping, ""Creates shipments"", ""REST API"")
    Rel(ecommerce, email, ""Sends notifications"", ""SMTP"")

    UpdateLayoutConfig($c4ShapeInRow=""3"", $c4BoundaryInRow=""1"")

    %% C4 Elements:
    %% Person(alias, label, description) - A person/user
    %% Person_Ext(alias, label, description) - External person
    %% System(alias, label, description) - Your system
    %% System_Ext(alias, label, description) - External system
    %% SystemDb(alias, label, description) - Database system
    %% SystemDb_Ext(alias, label, description) - External database
    %% Container(alias, label, technology, description) - Container
    %% ContainerDb(alias, label, technology, description) - Database container
    %% Component(alias, label, technology, description) - Component
    %% Boundary(alias, label) - Boundary grouping

    %% Relationships:
    %% Rel(from, to, label) - Relationship
    %% Rel(from, to, label, technology) - Relationship with tech
    %% BiRel(from, to, label) - Bidirectional relationship");
    }

    private void Sankey_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"sankey-beta

%% Sankey Diagram - Energy Flow
%% Format: Source,Target,Value

Agricultural 'waste',Bio-conversion,124.729
Bio-conversion,Liquid,0.597
Bio-conversion,Losses,26.862
Bio-conversion,Solid,280.322
Bio-conversion,Gas,81.144
Liquid,Losses,1.401
Liquid,Thermal generation,10.064
Solid,Losses,4.394
Solid,Thermal generation,21.735
Gas,Losses,2.812
Gas,Thermal generation,78.332
Thermal generation,Electricity grid,52.803
Thermal generation,District heating,46.184

%% Sankey diagrams show flow/energy distribution
%% Each line: Source,Target,Value
%% Values determine the width of the flow");
    }

    private void XYChart_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"xychart-beta
    title ""Monthly Sales Performance""
    x-axis [Jan, Feb, Mar, Apr, May, Jun, Jul, Aug, Sep, Oct, Nov, Dec]
    y-axis ""Revenue (in $)"" 4000 --> 15000
    bar [5000, 6000, 7500, 8200, 9500, 10500, 9800, 11000, 12500, 13000, 14000, 14500]
    line [5000, 6000, 7500, 8200, 9500, 10500, 9800, 11000, 12500, 13000, 14000, 14500]

    %% XY Chart Elements:
    %% title ""Chart Title"" - Chart title
    %% x-axis [label1, label2, ...] - X axis categories
    %% x-axis ""Label"" min --> max - X axis range
    %% y-axis ""Label"" min --> max - Y axis range
    %% bar [val1, val2, ...] - Bar series
    %% line [val1, val2, ...] - Line series");
    }

    private void BlockDiagram_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"block-beta
    columns 3

    doc(""Document""):3
    blockArrowDown1<[""&nbsp;&nbsp;&nbsp;""]>(down):3

    block:services:3
        columns 3
        a[""Frontend""] b[""API Gateway""] c[""Auth Service""]
    end

    blockArrowDown2<[""&nbsp;&nbsp;&nbsp;""]>(down):3

    block:data:3
        columns 2
        d[""Database""] e[""Cache""]
    end

    %% Block Diagram Elements:
    %% columns N - Set number of columns
    %% id[""Label""] - Block with label
    %% id[""Label""]:N - Block spanning N columns
    %% block:id:N ... end - Nested block group
    %% space - Empty space placeholder
    %% blockArrowId<[""Label""]>(direction) - Arrow block (down/up/left/right)");
    }

    private void Packet_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"packet-beta
    title TCP Packet Structure

    0-15: ""Source Port""
    16-31: ""Destination Port""
    32-63: ""Sequence Number""
    64-95: ""Acknowledgment Number""
    96-99: ""Data Offset""
    100-105: ""Reserved""
    106-111: ""Flags""
    112-127: ""Window Size""
    128-143: ""Checksum""
    144-159: ""Urgent Pointer""
    160-191: ""Options (if Data Offset > 5)""
    192-255: ""Data (variable length)""

    %% Packet Diagram Elements:
    %% title - Packet title
    %% start-end: ""Label"" - Bit range with label
    %% Bit ranges define the structure of network packets");
    }

    private void Kanban_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"kanban
    column1[""To Do""]
        task1[""Research competitors""]
        task2[""Define requirements""]
        task3[""Create wireframes""]
    column2[""In Progress""]
        task4[""Design UI mockups""]
        task5[""Implement backend API""]
    column3[""Review""]
        task6[""Code review: auth module""]
    column4[""Done""]
        task7[""Setup CI/CD pipeline""]
        task8[""Configure database""]

    %% Kanban Board Elements:
    %% column[""Title""] - Define a column
    %%     task[""Description""] - Task within column
    %% Tasks are listed under their parent column");
    }

    private void Architecture_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"architecture-beta
    group api(cloud)[""Cloud Infrastructure""]
    group backend(server)[""Backend Services""] in api
    group storage(database)[""Data Layer""] in api

    service gateway(internet)[""API Gateway""] in api
    service web(server)[""Web Server""] in backend
    service auth(server)[""Auth Service""] in backend
    service db(database)[""PostgreSQL""] in storage
    service cache(database)[""Redis Cache""] in storage
    service files(disk)[""File Storage""] in storage

    gateway:R -- L:web
    gateway:R -- L:auth
    web:B -- T:db
    web:B -- T:cache
    auth:B -- T:db
    web:R -- L:files

    %% Architecture Elements:
    %% group alias(icon)[""Label""] - Group/boundary
    %% service alias(icon)[""Label""] in group - Service node
    %% Icons: cloud, database, disk, internet, server
    %% from:R -- L:to - Connection (T/B/L/R = Top/Bottom/Left/Right)");
    }

    private void ZenUML_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"zenuml
    title Order Processing Workflow

    @Actor Customer
    @Boundary WebApp
    @Control OrderService
    @Entity Database
    @Entity PaymentGateway

    // Customer places order
    Customer->WebApp.placeOrder(items) {
        WebApp->OrderService.createOrder(items) {
            OrderService->Database.saveOrder(order) {
                return orderId
            }
            OrderService->PaymentGateway.processPayment(amount) {
                return paymentConfirmation
            }
            return orderConfirmation
        }
        return ""Order placed successfully""
    }

    %% ZenUML Elements:
    %% @Actor Name - Actor participant
    %% @Boundary Name - Boundary participant
    %% @Control Name - Control participant
    %% @Entity Name - Entity participant
    %% A->B.method() { } - Sync call with nested interactions
    %% A->B.method() - Simple sync call
    %% return value - Return from call");
    }

    private void Radar_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"radar-beta
    title Technology Assessment
    axis perf[""Performance""], scale[""Scalability""], sec[""Security""]
    axis usab[""Usability""], cost[""Cost""], maint[""Maintainability""]
    curve a[""Solution A""]{80, 90, 70, 85, 60, 75}
    curve b[""Solution B""]{65, 75, 90, 70, 80, 85}
    curve c[""Solution C""]{90, 60, 80, 65, 70, 80}
    max 100

    %% Radar Chart Elements:
    %% title - Chart title
    %% axis id[""Label""], id2[""Label""] - Define axes (min 3)
    %% curve id[""Name""]{val1, val2, ...} - Data series
    %% max N - Set maximum axis value
    %% graticule polygon|circle - Grid shape");
    }

    private void Treemap_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"treemap-beta
    ""Company Budget""
        ""Engineering""
            ""Frontend Team"": 40
            ""Backend Team"": 35
            ""DevOps"": 15
            ""QA"": 10
        ""Marketing""
            ""Digital"": 25
            ""Content"": 15
            ""Events"": 10
        ""Sales""
            ""Enterprise"": 30
            ""SMB"": 20
        ""Operations""
            ""HR"": 15
            ""Finance"": 20
            ""Legal"": 10

    %% Treemap Elements:
    %% ""Section"" - Parent/section node
    %%     ""Leaf"": value - Leaf node with size value
    %% Hierarchy created by indentation
    %% Leaf sizes are proportional within their parent");
    }

    private void Venn_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"venn-beta
    title ""Development Team Skills""

    set Frontend[""Frontend""]
        text f1[""React""]
        text f2[""CSS""]
    set Backend[""Backend""]
        text b1[""API""]
        text b2[""SQL""]
    set DevOps[""DevOps""]
        text d1[""Docker""]
        text d2[""AWS""]
    union Frontend,Backend[""Full-Stack""]
        text fb1[""Node.js""]
    union Backend,DevOps[""SRE""]
        text bd1[""Monitoring""]

    %% Venn Diagram Elements:
    %% title ""Title"" - Diagram title
    %% set Name[""Label""] - Define a set
    %%     text id[""Label""] - Text inside set
    %% union A,B[""Label""] - Overlap of two sets
    %% style Name fill:#color - Custom styling");
    }

    private void PopulateTemplates()
    {
        var categories = new (string Category, (string Name, string Description, string Icon, RoutedEventHandler Click)[] Templates)[]
        {
            ("Markdown", new[]
            {
                ("Markdown Cheat Sheet", "All markdown syntax with live preview examples", "markdown.png", (RoutedEventHandler)MarkdownCheatSheet_Click),
            }),
            ("Flowcharts & Process", new[]
            {
                ("Flowchart", "Basic flowchart with nodes and connections", "flowchart.png", (RoutedEventHandler)Flowchart_Click),
                ("Flowchart (Advanced)", "Flowchart with subgraphs and styling", "flowchart.png", (RoutedEventHandler)FlowchartAdvanced_Click),
                ("State Diagram", "State machine with transitions", "state.png", (RoutedEventHandler)StateDiagram_Click),
                ("Block Diagram", "Block-based system layout", "block.png", (RoutedEventHandler)BlockDiagram_Click),
            }),
            ("Sequence & Interaction", new[]
            {
                ("Sequence Diagram", "Interactions between participants", "sequence.png", (RoutedEventHandler)Sequence_Click),
                ("ZenUML Sequence Diagram", "Code-like sequence diagram syntax", "zenuml.png", (RoutedEventHandler)ZenUML_Click),
                ("User Journey", "User experience journey map", "journey.png", (RoutedEventHandler)Journey_Click),
                ("Git Graph", "Git branch and commit visualization", "gitgraph.png", (RoutedEventHandler)GitGraph_Click),
            }),
            ("Charts & Data", new[]
            {
                ("Pie Chart", "Simple pie chart with percentages", "pie.png", (RoutedEventHandler)Pie_Click),
                ("XY Chart", "Bar and line charts with axes", "xychart.png", (RoutedEventHandler)XYChart_Click),
                ("Quadrant Chart", "Four-quadrant analysis chart", "quadrant.png", (RoutedEventHandler)Quadrant_Click),
                ("Radar Chart", "Multi-axis spider/radar comparison chart", "radar.png", (RoutedEventHandler)Radar_Click),
                ("Sankey Diagram", "Flow and energy distribution visualization", "sankey.png", (RoutedEventHandler)Sankey_Click),
                ("Venn Diagram", "Overlapping set relationships", "venn.png", (RoutedEventHandler)Venn_Click),
                ("Treemap", "Hierarchical data as nested rectangles", "treemap.png", (RoutedEventHandler)Treemap_Click),
            }),
            ("Project & Timeline", new[]
            {
                ("Gantt Chart", "Project timeline with tasks", "gantt.png", (RoutedEventHandler)Gantt_Click),
                ("Timeline", "Chronological timeline of events", "timeline.png", (RoutedEventHandler)Timeline_Click),
                ("Kanban Board", "Task board with columns and cards", "kanban.png", (RoutedEventHandler)Kanban_Click),
            }),
            ("Structure & Architecture", new[]
            {
                ("Class Diagram", "UML class diagram with relationships", "class.png", (RoutedEventHandler)ClassDiagram_Click),
                ("ER Diagram", "Database entity relationship diagram", "erdiagram.png", (RoutedEventHandler)ERDiagram_Click),
                ("Mind Map", "Hierarchical mind map", "mindmap.png", (RoutedEventHandler)Mindmap_Click),
                ("C4 Context Diagram", "System context with people and systems", "c4.png", (RoutedEventHandler)C4_Click),
                ("Architecture Diagram", "System architecture with services and connections", "architecture.png", (RoutedEventHandler)Architecture_Click),
                ("Requirement Diagram", "Requirements traceability", "requirement.png", (RoutedEventHandler)Requirement_Click),
                ("Packet Diagram", "Network packet structure visualization", "packet.png", (RoutedEventHandler)Packet_Click),
            }),
        };

        foreach (var (category, templates) in categories)
        {
            var categoryPanel = new StackPanel();

            var header = new TextBlock
            {
                Text = category,
                Foreground = (System.Windows.Media.Brush)FindResource("ThemeDisabledForegroundBrush"),
                FontSize = 12,
                Margin = new Thickness(4, 12, 0, 4)
            };
            categoryPanel.Children.Add(header);

            foreach (var (name, description, icon, click) in templates)
            {
                var btn = new System.Windows.Controls.Button
                {
                    Style = (Style)FindResource("TemplateButtonStyle"),
                    Tag = $"{name}|{description}|{category}".ToLowerInvariant()
                };

                var outerStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

                var img = new System.Windows.Controls.Image
                {
                    Width = 36,
                    Height = 36,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                try
                {
                    img.Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri($"Resources/TemplateThumbnails/{icon}", UriKind.Relative));
                }
                catch { /* Ignore if icon not found */ }
                outerStack.Children.Add(img);

                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textStack.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold });
                textStack.Children.Add(new TextBlock
                {
                    Text = description,
                    Foreground = (System.Windows.Media.Brush)FindResource("ThemeDisabledForegroundBrush"),
                    FontSize = 11
                });
                outerStack.Children.Add(textStack);

                btn.Content = outerStack;
                btn.Click += click;
                categoryPanel.Children.Add(btn);
            }

            DynamicTemplatesPanel.Children.Add(categoryPanel);
        }
    }

    private void TemplateSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = TemplateSearchBox?.Text?.Trim().ToLowerInvariant() ?? "";

        // Show/hide search placeholder
        if (SearchPlaceholder != null)
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(searchText) ? Visibility.Visible : Visibility.Collapsed;

        // Filter blank section items
        if (BlankSectionHeader != null)
        {
            bool blankMermaidMatch = string.IsNullOrEmpty(searchText)
                || "blank mermaid diagram flowchart".Contains(searchText, StringComparison.OrdinalIgnoreCase);
            bool blankMarkdownMatch = string.IsNullOrEmpty(searchText)
                || "blank markdown document".Contains(searchText, StringComparison.OrdinalIgnoreCase);

            BlankSectionHeader.Visibility = (blankMermaidMatch || blankMarkdownMatch) ? Visibility.Visible : Visibility.Collapsed;
            BlankMermaidGrid.Visibility = blankMermaidMatch ? Visibility.Visible : Visibility.Collapsed;
            BlankMarkdownButton.Visibility = blankMarkdownMatch ? Visibility.Visible : Visibility.Collapsed;
        }

        // Filter dynamic templates
        if (DynamicTemplatesPanel == null) return;

        foreach (var child in DynamicTemplatesPanel.Children)
        {
            if (child is StackPanel categoryPanel)
            {
                bool anyVisible = false;

                foreach (var item in categoryPanel.Children)
                {
                    if (item is System.Windows.Controls.Button btn)
                    {
                        bool matches = string.IsNullOrEmpty(searchText)
                            || (btn.Tag is string tag && tag.Contains(searchText));
                        btn.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                        if (matches) anyVisible = true;
                    }
                }

                // Show/hide category header
                if (categoryPanel.Children.Count > 0 && categoryPanel.Children[0] is TextBlock catHeader)
                {
                    catHeader.Visibility = anyVisible ? Visibility.Visible : Visibility.Collapsed;
                }

                categoryPanel.Visibility = anyVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void OpenExistingFile_Click(object sender, RoutedEventArgs e)
    {
        OpenExistingFile = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
