using System.Windows;

namespace MermaidEditor;

public partial class NewDocumentDialog : Window
{
    public string? SelectedTemplate { get; private set; }
    public bool IsMermaid { get; private set; } = true;
    public bool OpenExistingFile { get; private set; } = false;

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

    private void Requirement_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateAndClose(@"---
config:
  theme: default
---
requirementDiagram

    requirement User_Authentication {
        id: REQ-001
        text: The system shall authenticate users
        risk: high
        verifymethod: test
    }

    requirement Password_Policy {
        id: REQ-002
        text: Passwords must be at least 8 characters
        risk: medium
        verifymethod: inspection
    }

    requirement Session_Management {
        id: REQ-003
        text: Sessions shall expire after 30 minutes
        risk: medium
        verifymethod: test
    }

    functionalRequirement Login_Page {
        id: REQ-004
        text: System shall provide a login page
        risk: low
        verifymethod: demonstration
    }

    performanceRequirement Response_Time {
        id: REQ-005
        text: Login shall complete within 2 seconds
        risk: medium
        verifymethod: test
    }

    interfaceRequirement API_Auth {
        id: REQ-006
        text: API shall support OAuth 2.0
        risk: high
        verifymethod: inspection
    }

    element Auth_Module {
        type: module
        docRef: AUTH-DOC-001
    }

    element Login_Component {
        type: component
        docRef: LOGIN-DOC-001
    }

    User_Authentication - traces -> Password_Policy
    User_Authentication - traces -> Session_Management
    User_Authentication - contains -> Login_Page
    Login_Page - derives -> Response_Time
    Auth_Module - satisfies -> User_Authentication
    Login_Component - satisfies -> Login_Page
    API_Auth - refines -> User_Authentication

    %% Requirement types:
    %% requirement - Generic requirement
    %% functionalRequirement - Functional requirement
    %% performanceRequirement - Performance requirement
    %% interfaceRequirement - Interface requirement
    %% physicalRequirement - Physical requirement
    %% designConstraint - Design constraint

    %% Relationship types:
    %% contains - Parent contains child
    %% copies - Copies another requirement
    %% derives - Derived from another
    %% satisfies - Element satisfies requirement
    %% verifies - Element verifies requirement
    %% refines - Refines another requirement
    %% traces - Traces to another requirement");
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
