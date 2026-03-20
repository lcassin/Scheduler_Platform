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

    private void BlankDiagramTypeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (BlankDiagramTypeCombo?.SelectedItem is System.Windows.Controls.ComboBoxItem item)
        {
            var type = item.Content?.ToString() ?? "Flowchart";
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
                    "Mind Map" => "mindmap",
                    "Timeline" => "timeline",
                    "Git Graph" => "gitgraph",
                    "Journey" => "journey",
                    "Quadrant" => "quadrant",
                    "Requirement" => "requirement",
                    "C4" => "c4",
                    _ => "blank"
                };
                try
                {
                    BlankDiagramTypeIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri($"Resources/TemplateThumbnails/{iconName}.png", UriKind.Relative));
                }
                catch { /* Ignore if icon not found */ }
            }
        }
    }

    private string GetBlankTemplateForSelectedType()
    {
        var selectedType = "Flowchart";
        if (BlankDiagramTypeCombo?.SelectedItem is System.Windows.Controls.ComboBoxItem item)
        {
            selectedType = item.Content?.ToString() ?? "Flowchart";
        }

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
    section Phase 1
        Task 1 :a1, 2024-01-01, 7d
        Task 2 :a2, after a1, 5d",
            "Pie" => @"pie showData
    title Distribution
    ""Category A"" : 40
    ""Category B"" : 35
    ""Category C"" : 25",
            "Mind Map" => @"mindmap
    root((Central Topic))
        Branch A
            Leaf 1
            Leaf 2
        Branch B
            Leaf 3",
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
