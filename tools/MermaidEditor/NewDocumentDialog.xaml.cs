using System.Windows;

namespace MermaidEditor;

public partial class NewDocumentDialog : Window
{
    public string? SelectedTemplate { get; private set; }
    public bool IsMermaid { get; private set; } = true;

    public NewDocumentDialog()
    {
        InitializeComponent();
    }

    private void SetTemplateAndClose(string template, bool isMermaid = true)
    {
        SelectedTemplate = template;
        IsMermaid = isMermaid;
        DialogResult = true;
        Close();
    }

    private void BlankMermaid_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"flowchart TD
    A[Start] --> B[End]");
    }

    private void BlankMarkdown_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"# Title

Your content here...
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

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
