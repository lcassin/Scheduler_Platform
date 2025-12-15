# Scheduler Platform Documentation

This folder contains centralized documentation for the Scheduler Platform.

## Folder Structure

### Business Documentation (`/Business/`)

Business-facing documentation in Word format (.docx) for non-technical stakeholders:

| Document | Description |
|----------|-------------|
| Root.README.docx | Platform overview and architecture |
| API.README.docx | API endpoints and usage guide |
| Core.README.docx | Domain entities and business rules |
| Infrastructure.README.docx | Data access and database documentation |
| Jobs.README.docx | Job execution engine documentation |
| UI.README.docx | User interface documentation |
| IdentityServer.README.docx | Authentication and security documentation |

The Business folder also contains architecture diagrams (diagram-*.png) that are embedded in the .docx files.

### Technical Documentation

Technical documentation for developers is maintained alongside the code in each project's README.md file:

| Project | Location |
|---------|----------|
| Root | [/README.md](/README.md) |
| API | [/src/SchedulerPlatform.API/README.md](/src/SchedulerPlatform.API/README.md) |
| Core | [/src/SchedulerPlatform.Core/README.md](/src/SchedulerPlatform.Core/README.md) |
| Infrastructure | [/src/SchedulerPlatform.Infrastructure/README.md](/src/SchedulerPlatform.Infrastructure/README.md) |
| Jobs | [/src/SchedulerPlatform.Jobs/README.md](/src/SchedulerPlatform.Jobs/README.md) |
| UI | [/src/SchedulerPlatform.UI/README.md](/src/SchedulerPlatform.UI/README.md) |
| IdentityServer | [/src/SchedulerPlatform.IdentityServer/README.md](/src/SchedulerPlatform.IdentityServer/README.md) |

Additional technical guides:
- [Audit Logging](/AUDIT_LOGGING.md) - Audit trail system documentation
- [Azure AD Setup](/AZURE_AD_SETUP.md) - Entra/Azure AD integration guide
- [Service Account Auth](/service_account_auth_guide.md) - OAuth2 Client Credentials flow guide

## Database Naming Conventions

The platform follows these database naming conventions:

- **Table names**: Singular (e.g., `Schedule`, `Client`, `User`)
- **Primary keys**: `<TableName>Id` format (e.g., `ScheduleId`, `ClientId`)
- **Audit fields**: 
  - `CreatedDateTime` - When the record was created
  - `CreatedBy` - Who created the record
  - `ModifiedDateTime` - When the record was last modified (non-nullable, defaults to CreatedDateTime)
  - `ModifiedBy` - Who last modified the record (non-nullable, defaults to CreatedBy)
- **DateTime fields**: Use `DateTime` suffix (e.g., `StartDateTime`, `EndDateTime`, `NextRunDateTime`)

## UI Branding

The platform uses Cass Information Systems branding:
- **Primary Color**: Cass Green (#006747)
- **Secondary Color**: Cass Blue (#3058A9)
- **Font**: Open Sans
- **App Name**: Cass Scheduler Platform

## Recent Updates (November 2025)

- Upgraded to .NET 10
- Added Entra (Azure AD) login integration
- Implemented comprehensive permission system
- Added missed schedules detection and bulk trigger feature
- Updated database naming conventions to company standards
- Made ModifiedBy/ModifiedDateTime non-nullable audit fields

## Recent Updates (December 2025)

- **ADR (Automated Data Retrieval) Process**: Complete implementation of automated invoice scraping from vendor portals
  - New entities: AdrAccount, AdrJob, AdrJobExecution, AdrOrchestrationRun
  - 4-step orchestration workflow: Sync Accounts → Create Jobs → Verify Credentials → Process Scraping → Check Statuses
  - Real-time Job Monitor page with progress tracking
  - Manual override support for billing dates/frequencies
  - Idempotency to prevent duplicate API calls to paid services
  - ADR-specific permissions (adr:view, adr:edit, adr:execute)

### ADR Documentation

Technical documentation for the ADR process is distributed across project READMEs:

| Topic | Location |
|-------|----------|
| ADR Overview | [Root README - ADR Section](/README.md#adr-automated-data-retrieval-process-december-2025) |
| ADR Domain Model | [Core README - ADR Domain Model](/src/SchedulerPlatform.Core/README.md#adr-domain-model) |
| ADR API Endpoints | [API README - AdrController](/src/SchedulerPlatform.API/README.md#adrcontroller) |
| ADR UI Pages | [UI README - ADR UI Pages](/src/SchedulerPlatform.UI/README.md#adr-ui-pages) |
| ADR Persistence | [Infrastructure README - ADR Persistence](/src/SchedulerPlatform.Infrastructure/README.md#adr-persistence-and-indexing) |

### ADR Diagrams

Technical diagrams for the ADR process are in `/Technical/diagrams/`:

| Diagram | Description |
|---------|-------------|
| [adr-er-diagram.png](/Documents/Technical/diagrams/adr-er-diagram.png) | Entity relationship diagram showing AdrAccount, AdrJob, AdrJobExecution, AdrOrchestrationRun |
| [adr-workflow.png](/Documents/Technical/diagrams/adr-workflow.png) | Flowchart showing the 4-step orchestration workflow |
| [adr-sequence-orchestration.png](/Documents/Technical/diagrams/adr-sequence-orchestration.png) | Sequence diagram showing API orchestration flow |

Source Mermaid files (.mmd) are also available for editing. See [Technical/diagrams/README.md](/Documents/Technical/diagrams/README.md) for regeneration instructions.
