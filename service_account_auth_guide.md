# Service Account Authentication Guide

## Overview
The Scheduler Platform supports service account authentication using OAuth 2.0 Client Credentials flow. This allows external processes to obtain bearer tokens and call the API without user interaction.

## Service Account Details
- **Client ID**: `svc-adrscheduler`
- **Client Secret**: `dev-secret-change-in-production` (⚠️ Change in production!)
- **Grant Type**: `client_credentials`
- **Token Endpoint**: `https://localhost:5001/connect/token`
- **API Base URL**: `https://localhost:7008/api`

## Permissions
The service account has Editor-level permissions:
- `scheduler:read`
- `schedules:read`, `schedules:create`, `schedules:update`, `schedules:delete`, `schedules:execute`
- `jobs:read`

## Step 1: Obtain Bearer Token

### Using PowerShell
```powershell
$tokenUrl = "https://localhost:5001/connect/token"
$body = @{
    grant_type = "client_credentials"
    client_id = "svc-adrscheduler"
    client_secret = "dev-secret-change-in-production"
    scope = "scheduler-api"
}

$response = Invoke-RestMethod -Uri $tokenUrl -Method Post -Body $body -ContentType "application/x-www-form-urlencoded"
$accessToken = $response.access_token

Write-Host "Access Token: $accessToken"
```

### Using cURL
```bash
curl -X POST https://localhost:5001/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=svc-adrscheduler" \
  -d "client_secret=dev-secret-change-in-production" \
  -d "scope=scheduler-api"
```

### Using C#
```csharp
using System.Net.Http;
using System.Text.Json;

public class TokenResponse
{
    public string access_token { get; set; }
    public int expires_in { get; set; }
    public string token_type { get; set; }
}

public async Task<string> GetAccessTokenAsync()
{
    using var client = new HttpClient();
    
    var tokenRequest = new Dictionary<string, string>
    {
        { "grant_type", "client_credentials" },
        { "client_id", "svc-adrscheduler" },
        { "client_secret", "dev-secret-change-in-production" },
        { "scope", "scheduler-api" }
    };
    
    var response = await client.PostAsync(
        "https://localhost:5001/connect/token",
        new FormUrlEncodedContent(tokenRequest));
    
    response.EnsureSuccessStatusCode();
    
    var content = await response.Content.ReadAsStringAsync();
    var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content);
    
    return tokenResponse.access_token;
}
```

### Response Example
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsImtpZCI6IjEyMzQ1Njc4OTAiLCJ0eXAiOiJKV1QifQ...",
  "expires_in": 3600,
  "token_type": "Bearer",
  "scope": "scheduler-api"
}
```

## Step 2: Use Bearer Token to Call API

### Create a Schedule (POST)
```powershell
$headers = @{
    "Authorization" = "Bearer $accessToken"
    "Content-Type" = "application/json"
}

$schedule = @{
    name = "Daily Report"
    description = "Generate daily sales report"
    clientId = 1
    jobType = 1
    frequency = 1
    cronExpression = "0 0 8 * * ?"
    isEnabled = $true
    maxRetries = 3
    retryDelayMinutes = 5
    timeZone = "Eastern Standard Time"
    jobDataJson = '{"reportType":"sales","format":"pdf"}'
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://localhost:7008/api/schedules" -Method Post -Headers $headers -Body $schedule
Write-Host "Schedule created with ID: $($response.id)"
```

### Get All Schedules (GET)
```powershell
$headers = @{ "Authorization" = "Bearer $accessToken" }
$schedules = Invoke-RestMethod -Uri "https://localhost:7008/api/schedules?paginated=false" -Headers $headers
```

### Trigger a Schedule (POST)
```powershell
$headers = @{ "Authorization" = "Bearer $accessToken" }
Invoke-RestMethod -Uri "https://localhost:7008/api/schedules/123/trigger" -Method Post -Headers $headers
```

### Complete PowerShell Example
```powershell
# Step 1: Get access token
$tokenUrl = "https://localhost:5001/connect/token"
$tokenBody = @{
    grant_type = "client_credentials"
    client_id = "svc-adrscheduler"
    client_secret = "dev-secret-change-in-production"
    scope = "scheduler-api"
}

$tokenResponse = Invoke-RestMethod -Uri $tokenUrl -Method Post -Body $tokenBody -ContentType "application/x-www-form-urlencoded"
$accessToken = $tokenResponse.access_token

# Step 2: Create a schedule
$apiUrl = "https://localhost:7008/api/schedules"
$headers = @{
    "Authorization" = "Bearer $accessToken"
    "Content-Type" = "application/json"
}

$schedule = @{
    name = "Automated Backup"
    description = "Daily database backup"
    clientId = 1
    jobType = 1
    frequency = 1
    cronExpression = "0 0 2 * * ?"
    isEnabled = $true
    maxRetries = 3
    retryDelayMinutes = 5
    timeZone = "Eastern Standard Time"
    jobDataJson = '{"backupType":"full","destination":"s3://backups"}'
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri $apiUrl -Method Post -Headers $headers -Body $schedule
Write-Host "Schedule created with ID: $($response.id)"
```

## Important Notes

### Token Lifetime
- Access tokens expire after **1 hour** (3600 seconds)
- Cache the token and reuse it until it expires
- Request a new token when the current one expires

### Production Considerations
1. **Change the client secret** in production! Current secret is for development only.
2. **Use HTTPS** in production (RequireHttpsMetadata should be true)
3. **Store secrets securely** (Azure Key Vault, environment variables, etc.)
4. **Implement token caching** to avoid requesting a new token for every API call
5. **Handle token expiration** gracefully with retry logic

### Updating Client Secret (Production)
To change the client secret for production, update `Config.cs` in IdentityServer:

```csharp
new Client
{
    ClientId = "svc-adrscheduler",
    ClientSecrets = { new Secret("YOUR_SECURE_SECRET_HERE".Sha256()) },
    // ... rest of config
}
```

Then restart IdentityServer and update your external processes with the new secret.

## Troubleshooting

### "invalid_client" Error
- Check that client_id and client_secret are correct
- Verify IdentityServer is running on port 5001

### "unauthorized_client" Error
- Verify the grant_type is "client_credentials"
- Check that the scope "scheduler-api" is requested

### 401 Unauthorized from API
- Verify the access token is included in the Authorization header
- Check that the token hasn't expired
- Ensure the API is configured to validate tokens from IdentityServer

### 403 Forbidden from API
- The service account doesn't have permission for the requested operation
- Check the permission claims in the access token (decode at jwt.io)
