# Audit Logging System

## Overview

The Scheduler Platform includes a comprehensive audit logging system that automatically tracks all changes to critical entities. This helps maintain compliance, track manual edits vs automated changes, and provide a complete audit trail for schedule management.

## What Gets Audited

The following entities are automatically audited:
- **Schedule** - All schedule creates, updates, and deletes
- **JobParameter** - Parameter changes for schedules
- **NotificationSetting** - Notification configuration changes
- **VendorCredential** - Vendor credential management
- **User** - User account changes
- **UserPermission** - Permission grants and revocations
- **Client** - Client account changes

## Audit Log Structure

Each audit log entry captures:

| Field | Description |
|-------|-------------|
| `EventType` | "Manual" (user action) or "Automated" (service/background job) |
| `EntityType` | Type of entity changed (e.g., "Schedule") |
| `EntityId` | ID of the changed entity |
| `Action` | "Added", "Modified", or "Deleted" |
| `UserName` | Email or name of the actor (or "System" for background jobs) |
| `ClientId` | Client ID if available |
| `IpAddress` | IP address of the request (if applicable) |
| `UserAgent` | Browser/client user agent (if applicable) |
| `TimestampDateTime` | UTC timestamp of the change |
| `OldValues` | JSON of property values before the change |
| `NewValues` | JSON of property values after the change |
| `AdditionalData` | Optional additional context |

## Manual vs Automated Changes

The system automatically distinguishes between:

### Manual Changes
- User authenticated via OIDC (has email claim)
- User authenticated via local password
- Any authenticated user that is NOT the service account

### Automated Changes
- Service account (`svc-adrscheduler`) making API calls
- Background jobs (no HTTP context)
- System operations (no authenticated user)

## Data Protection

### Sensitive Data Masking
The following fields are automatically masked in audit logs:
- `PasswordHash`
- `EncryptedPassword`
- `ConnectionString`
- `SourceConnectionString`
- `ApiKey`
- `Secret`
- `Token`

Masked values appear as `***MASKED***` in the audit log.

### Excluded Fields
To reduce noise, these auto-updated fields are excluded from audit logs:
- `ModifiedDateTime`
- `LastRunDateTime`
- `NextRunDateTime`
- `CreatedDateTime` (for Modified actions)
- `Id` (captured separately in EntityId)

## API Endpoints

### Get Audit Logs for a Schedule
```http
GET /api/auditlogs/schedules/{scheduleId}?pageNumber=1&pageSize=50&eventType=Manual&action=Modified&startDate=2025-01-01&endDate=2025-12-31
```

**Query Parameters:**
- `pageNumber` - Page number (default: 1)
- `pageSize` - Items per page (default: 50)
- `eventType` - Filter by "Manual" or "Automated"
- `action` - Filter by "Added", "Modified", or "Deleted"
- `startDate` - Filter by start date (UTC)
- `endDate` - Filter by end date (UTC)

**Response:**
```json
{
  "items": [
    {
      "id": 123,
      "eventType": "Manual",
      "entityType": "Schedule",
      "entityId": 456,
      "action": "Modified",
      "userName": "user@cassinfo.com",
      "clientId": 1,
      "ipAddress": "192.168.1.100",
      "userAgent": "Mozilla/5.0...",
      "timestamp": "2025-11-14T19:30:00Z",
      "oldValues": "{\"Name\":\"Old Name\",\"IsEnabled\":true}",
      "newValues": "{\"Name\":\"New Name\",\"IsEnabled\":false}",
      "additionalData": null
    }
  ],
  "totalCount": 150,
  "pageNumber": 1,
  "pageSize": 50,
  "totalPages": 3
}
```

### Get All Audit Logs (Admin Only)
```http
GET /api/auditlogs?entityType=Schedule&eventType=Manual&userName=user@cassinfo.com&pageNumber=1&pageSize=50
```

**Authorization:** Requires `Users.Manage` permission

**Query Parameters:**
- `entityType` - Filter by entity type
- `entityId` - Filter by specific entity ID
- `eventType` - Filter by "Manual" or "Automated"
- `action` - Filter by "Added", "Modified", or "Deleted"
- `userName` - Filter by user name (partial match)
- `startDate` - Filter by start date (UTC)
- `endDate` - Filter by end date (UTC)
- `pageNumber` - Page number (default: 1)
- `pageSize` - Items per page (default: 50)

## Implementation Details

### EF Core Interceptor
The `AuditLogInterceptor` automatically captures changes during `SaveChangesAsync()`:
1. Inspects the EF Core ChangeTracker for Added/Modified/Deleted entities
2. Filters to only audited entity types
3. Captures old and new property values
4. Masks sensitive data
5. Creates AuditLog entries in the same transaction

### Current Actor Service
The `CurrentActorService` extracts actor information from:
- **HttpContext** - For API requests (user principal, IP, user agent)
- **Claims** - Email, name, client_id
- **Service Account Detection** - Checks if client_id is `svc-adrscheduler`

### Registration
Services are registered in `Program.cs`:
```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, CurrentActorService>();
builder.Services.AddScoped<AuditLogInterceptor>();

builder.Services.AddDbContext<SchedulerDbContext>((serviceProvider, options) =>
{
    var auditLogInterceptor = serviceProvider.GetRequiredService<AuditLogInterceptor>();
    options.UseSqlServer(connectionString)
        .AddInterceptors(auditLogInterceptor);
});
```

## Database Schema

The `AuditLogs` table includes:
- Primary key on `Id`
- Index on `(EntityType, EntityId)` for entity lookups
- Index on `TimestampDateTime` for date range queries

## Use Cases

### Track Manual Schedule Edits
Find all manual changes to a specific schedule:
```http
GET /api/auditlogs/schedules/123?eventType=Manual
```

### Identify Automated Changes
Find all automated schedule creations:
```http
GET /api/auditlogs?entityType=Schedule&eventType=Automated&action=Added
```

### Compliance Reporting
Generate a report of all changes by a specific user:
```http
GET /api/auditlogs?userName=user@cassinfo.com&startDate=2025-01-01&endDate=2025-12-31
```

### Troubleshooting
Find who last modified a schedule:
```http
GET /api/auditlogs/schedules/123?pageSize=1
```

## Best Practices

1. **Regular Review** - Periodically review audit logs for unusual activity
2. **Retention Policy** - Implement a retention policy for old audit logs (e.g., 90 days)
3. **Monitoring** - Set up alerts for suspicious patterns (e.g., mass deletions)
4. **Compliance** - Use audit logs to demonstrate compliance with regulatory requirements
5. **Performance** - Use date range filters to limit query scope for large datasets

## Future Enhancements

Potential improvements:
- Add UI for viewing audit logs in the Blazor app
- Export audit logs to CSV/Excel
- Real-time audit log streaming
- Audit log archival to cold storage
- Advanced analytics and reporting
