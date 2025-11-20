# Scheduler Platform API Client

This is a sample console application that demonstrates how to authenticate with the Scheduler Platform API and perform operations like creating and retrieving schedules.

## Prerequisites

- .NET 10 SDK
- Access to the Scheduler Platform API
- Client credentials (Client ID and Client Secret)

## Configuration

Before running the application, update the following constants in `Program.cs`:

```csharp
private const string API_BASE_URL = "https://your-api-url.com";  // Your API URL (e.g., https://localhost:5001)
private const string CLIENT_ID = "your-client-id";                // Your client ID from the database
private const string CLIENT_SECRET = "your-client-secret";        // Your client secret from the database
```

## Getting Client Credentials

To get your client credentials, run this SQL query against your Scheduler Platform database:

```sql
SELECT ClientId, ClientSecret 
FROM ServiceAccounts 
WHERE IsActive = 1;
```

Or create a new service account:

```sql
INSERT INTO ServiceAccounts (ClientId, ClientSecret, Name, IsActive, CreatedAt)
VALUES ('console-app-client', 'your-secure-secret-here', 'Console App Client', 1, GETUTCDATE());
```

## Building and Running

1. Open a terminal in the `SchedulerApiClient` directory
2. Restore dependencies:
   ```bash
   dotnet restore
   ```
3. Build the project:
   ```bash
   dotnet build
   ```
4. Run the application:
   ```bash
   dotnet run
   ```

## What This Application Does

1. **Authenticates** with the Scheduler Platform API using client credentials (OAuth 2.0 client credentials flow)
2. **Creates a new schedule** with an API call configuration
3. **Retrieves all schedules** from the API and displays them

## API Endpoints Used

- `POST /connect/token` - Get access token using client credentials
- `POST /api/schedules` - Create a new schedule
- `GET /api/schedules` - Get all schedules

## Example Output

```
Scheduler Platform API Client
==============================

Authenticating with API...
✓ Successfully authenticated

Creating a new schedule...
✓ Schedule created successfully
Response: {"id":123,"name":"Test Schedule from Console App",...}

Fetching all schedules...
✓ Found 5 schedule(s)
  - Daily Report (ID: 1, Active: True)
  - Hourly Sync (ID: 2, Active: True)
  - Test Schedule from Console App (ID: 123, Active: True)
  ...

Press any key to exit...
```

## Customizing Schedule Creation

To create different types of schedules, modify the `CreateScheduleExample` method:

### API Call Schedule
```csharp
var schedule = new
{
    name = "My API Call Schedule",
    scheduleType = "ApiCall",
    cronExpression = "0 0 * * *",  // Daily at midnight
    isActive = true,
    apiCallConfig = new
    {
        url = "https://api.example.com/webhook",
        method = "POST",
        headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
        body = "{\"data\": \"value\"}"
    }
};
```

### Stored Procedure Schedule
```csharp
var schedule = new
{
    name = "My Stored Procedure Schedule",
    scheduleType = "StoredProcedure",
    cronExpression = "0 */6 * * *",  // Every 6 hours
    isActive = true,
    storedProcedureConfig = new
    {
        connectionString = "Server=...;Database=...;",
        procedureName = "sp_MyProcedure",
        parameters = new Dictionary<string, object>
        {
            { "@Param1", "value1" },
            { "@Param2", 123 }
        }
    }
};
```

### Process Schedule
```csharp
var schedule = new
{
    name = "My Process Schedule",
    scheduleType = "Process",
    cronExpression = "0 0 * * 0",  // Weekly on Sunday at midnight
    isActive = true,
    processConfig = new
    {
        fileName = "C:\\Scripts\\backup.bat",
        arguments = "--full",
        workingDirectory = "C:\\Scripts"
    }
};
```

## Cron Expression Examples

- `0 0 * * *` - Daily at midnight
- `0 */6 * * *` - Every 6 hours
- `0 0 * * 0` - Weekly on Sunday at midnight
- `0 0 1 * *` - Monthly on the 1st at midnight
- `*/15 * * * *` - Every 15 minutes

## Troubleshooting

### Authentication Fails
- Verify your CLIENT_ID and CLIENT_SECRET are correct
- Ensure the service account is active in the database
- Check that the API_BASE_URL is correct and accessible

### Schedule Creation Fails
- Verify the access token has the correct permissions
- Check that the schedule configuration is valid
- Ensure the cron expression is valid

### Connection Errors
- Verify the API is running and accessible
- Check firewall settings
- Ensure HTTPS certificates are valid (or use HTTP for local development)

## Security Notes

- **Never commit client secrets to source control**
- Store credentials in environment variables or secure configuration
- Use HTTPS in production
- Rotate client secrets regularly
- Limit service account permissions to only what's needed
