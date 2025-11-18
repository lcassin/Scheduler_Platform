# SQL Server Integration Guide

This guide explains how to call the Scheduler API from SQL Server Agent jobs or stored procedures.

## Option 1: SQL Server Agent Job (Recommended)

This is the simplest approach - call the compiled console application from a SQL Server Agent job.

### Step 1: Build and Deploy the Console Application

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

This creates a self-contained executable that doesn't require .NET to be installed on the SQL Server.

Copy the published files to a location accessible by SQL Server, e.g.:
```
C:\SchedulerTools\SchedulerApiClient\
```

### Step 2: Create SQL Server Agent Job

```sql
USE msdb;
GO

-- Create a new job
EXEC dbo.sp_add_job
    @job_name = N'Create Schedule via API',
    @enabled = 1,
    @description = N'Creates a schedule using the Scheduler Platform API';

-- Add a job step
EXEC dbo.sp_add_jobstep
    @job_name = N'Create Schedule via API',
    @step_name = N'Call API Client',
    @subsystem = N'CmdExec',
    @command = N'C:\SchedulerTools\SchedulerApiClient\SchedulerApiClient.exe',
    @retry_attempts = 3,
    @retry_interval = 5;

-- Add a schedule (optional - run on demand or scheduled)
EXEC dbo.sp_add_jobschedule
    @job_name = N'Create Schedule via API',
    @name = N'Daily at 6 AM',
    @freq_type = 4, -- Daily
    @freq_interval = 1,
    @active_start_time = 060000; -- 6:00 AM

-- Attach the job to the local server
EXEC dbo.sp_add_jobserver
    @job_name = N'Create Schedule via API',
    @server_name = N'(local)';
GO
```

### Step 3: Test the Job

```sql
-- Execute the job manually
EXEC dbo.sp_start_job @job_name = N'Create Schedule via API';

-- Check job history
EXEC dbo.sp_help_jobhistory @job_name = N'Create Schedule via API';
```

### Passing Parameters to the Console App

Modify the console application to accept command-line arguments:

```csharp
static async Task<int> Main(string[] args)
{
    if (args.Length > 0)
    {
        var scheduleName = args[0];
        var cronExpression = args.Length > 1 ? args[1] : "0 0 8 * * ?";
        
        // Use these parameters to create a specific schedule
        // ...
    }
    // ...
}
```

Then update the job step command:
```sql
@command = N'C:\SchedulerTools\SchedulerApiClient\SchedulerApiClient.exe "My Schedule" "0 0 12 * * ?"'
```

## Option 2: PowerShell from SQL Server

SQL Server Agent can execute PowerShell scripts directly.

### Step 1: Create PowerShell Script

Save as `C:\SchedulerTools\CreateSchedule.ps1`:

```powershell
param(
    [string]$ScheduleName = "Default Schedule",
    [string]$CronExpression = "0 0 8 * * ?"
)

# Configuration
$tokenUrl = "https://localhost:5001/connect/token"
$apiUrl = "https://localhost:7008/api/schedules"
$clientId = "svc-adrscheduler"
$clientSecret = "dev-secret-change-in-production"

try {
    # Get access token
    $tokenBody = @{
        grant_type = "client_credentials"
        client_id = $clientId
        client_secret = $clientSecret
        scope = "scheduler-api"
    }
    
    $tokenResponse = Invoke-RestMethod -Uri $tokenUrl -Method Post -Body $tokenBody -ContentType "application/x-www-form-urlencoded"
    $accessToken = $tokenResponse.access_token
    
    # Create schedule
    $headers = @{
        "Authorization" = "Bearer $accessToken"
        "Content-Type" = "application/json"
    }
    
    $schedule = @{
        name = $ScheduleName
        description = "Created from SQL Server job"
        clientId = 1
        jobType = 1
        frequency = 1
        cronExpression = $CronExpression
        isEnabled = $true
        maxRetries = 3
        retryDelayMinutes = 5
        timeZone = "Eastern Standard Time"
        jobDataJson = '{}'
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Headers $headers -Body $schedule
    
    Write-Output "Schedule created successfully with ID: $($response.id)"
    exit 0
}
catch {
    Write-Error "Failed to create schedule: $_"
    exit 1
}
```

### Step 2: Create SQL Server Agent Job

```sql
USE msdb;
GO

EXEC dbo.sp_add_job
    @job_name = N'Create Schedule via PowerShell';

EXEC dbo.sp_add_jobstep
    @job_name = N'Create Schedule via PowerShell',
    @step_name = N'Execute PowerShell Script',
    @subsystem = N'PowerShell',
    @command = N'C:\SchedulerTools\CreateSchedule.ps1 -ScheduleName "Daily Report" -CronExpression "0 0 8 * * ?"';

EXEC dbo.sp_add_jobserver
    @job_name = N'Create Schedule via PowerShell';
GO
```

## Option 3: CLR Stored Procedure (Advanced)

For tighter integration, you can create a CLR stored procedure that calls the API directly from T-SQL.

### Pros:
- Call API directly from T-SQL
- Can be used in triggers, stored procedures, etc.
- Better error handling and transaction support

### Cons:
- More complex to set up
- Requires CLR to be enabled on SQL Server
- Security considerations (EXTERNAL_ACCESS or UNSAFE assembly)
- Maintenance overhead

### Step 1: Enable CLR

```sql
sp_configure 'clr enabled', 1;
RECONFIGURE;
GO

sp_configure 'clr strict security', 0;
RECONFIGURE;
GO
```

### Step 2: Create CLR Project

Create a new C# Class Library project targeting .NET Framework 4.8 (SQL Server CLR requirement):

```csharp
using System;
using System.Data.SqlTypes;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Server;

public class SchedulerApiCLR
{
    [SqlProcedure]
    public static void CreateSchedule(
        SqlString scheduleName,
        SqlString cronExpression,
        SqlString description)
    {
        try
        {
            var result = CreateScheduleAsync(
                scheduleName.Value,
                cronExpression.Value,
                description.Value).Result;
            
            SqlContext.Pipe.Send($"Schedule created with ID: {result}");
        }
        catch (Exception ex)
        {
            SqlContext.Pipe.Send($"Error: {ex.Message}");
        }
    }
    
    private static async Task<int> CreateScheduleAsync(
        string name,
        string cronExpression,
        string description)
    {
        using (var client = new HttpClient())
        {
            // Get token
            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", "svc-adrscheduler"),
                new KeyValuePair<string, string>("client_secret", "dev-secret-change-in-production"),
                new KeyValuePair<string, string>("scope", "scheduler-api")
            });
            
            var tokenResponse = await client.PostAsync(
                "https://localhost:5001/connect/token",
                tokenRequest);
            
            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
            // Parse token (simplified - use JSON parser in real implementation)
            var token = ExtractToken(tokenContent);
            
            // Create schedule
            client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            var scheduleJson = $@"{{
                ""name"": ""{name}"",
                ""description"": ""{description}"",
                ""clientId"": 1,
                ""jobType"": 1,
                ""frequency"": 1,
                ""cronExpression"": ""{cronExpression}"",
                ""isEnabled"": true,
                ""maxRetries"": 3,
                ""retryDelayMinutes"": 5,
                ""timeZone"": ""Eastern Standard Time"",
                ""jobDataJson"": ""{{}}""
            }}";
            
            var content = new StringContent(scheduleJson, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(
                "https://localhost:7008/api/schedules",
                content);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            // Parse response to get ID (simplified)
            return 1; // Return actual ID from response
        }
    }
    
    private static string ExtractToken(string json)
    {
        // Simplified token extraction - use proper JSON parser in production
        var start = json.IndexOf("\"access_token\":\"") + 16;
        var end = json.IndexOf("\"", start);
        return json.Substring(start, end - start);
    }
}
```

### Step 3: Deploy CLR Assembly

```sql
-- Create assembly from DLL
CREATE ASSEMBLY SchedulerApiCLR
FROM 'C:\SchedulerTools\SchedulerApiCLR.dll'
WITH PERMISSION_SET = UNSAFE; -- Required for HTTP calls
GO

-- Create stored procedure
CREATE PROCEDURE dbo.usp_CreateScheduleViaAPI
    @ScheduleName NVARCHAR(255),
    @CronExpression NVARCHAR(100),
    @Description NVARCHAR(MAX)
AS EXTERNAL NAME SchedulerApiCLR.[SchedulerApiCLR].CreateSchedule;
GO
```

### Step 4: Use from T-SQL

```sql
EXEC dbo.usp_CreateScheduleViaAPI
    @ScheduleName = 'Daily Report',
    @CronExpression = '0 0 8 * * ?',
    @Description = 'Created from CLR stored procedure';
```

## Recommendation

For most use cases, **Option 1 (SQL Server Agent Job with Console App)** is recommended because:

1. ✅ Simple to implement and maintain
2. ✅ Easy to debug (can run console app manually)
3. ✅ No CLR security concerns
4. ✅ Can be updated without SQL Server changes
5. ✅ Better error logging and handling

Use **Option 2 (PowerShell)** if you need to pass dynamic parameters from SQL queries.

Use **Option 3 (CLR)** only if you need tight integration with T-SQL transactions or need to call the API from within stored procedures/triggers.

## Security Considerations

1. **Store credentials securely**: Don't hardcode the client secret in scripts
   - Use SQL Server credentials store
   - Use Windows Credential Manager
   - Use Azure Key Vault

2. **Use SQL Server proxy accounts**: Don't run jobs as sa or high-privilege accounts

3. **Validate inputs**: If accepting parameters, validate and sanitize them

4. **Log operations**: Keep audit trail of API calls made from SQL Server

5. **Handle errors gracefully**: Don't expose sensitive information in error messages
