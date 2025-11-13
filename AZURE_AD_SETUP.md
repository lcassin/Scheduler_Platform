# Azure AD Integration Setup Guide

This guide explains how to complete the Azure AD integration for the Scheduler Platform.

## Prerequisites

- Azure AD Tenant ID: `08717c9a-7042-4ddf-b86a-e0a500d32cde`
- Access to Azure Portal with permissions to create App Registrations

## Step 1: Create Azure AD App Registration

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** > **App registrations**
3. Click **New registration**
4. Configure the app:
   - **Name**: `Scheduler Platform - IdentityServer`
   - **Supported account types**: `Accounts in this organizational directory only (Single tenant)`
   - **Redirect URI**: 
     - Platform: `Web`
     - URI: `https://localhost:5001/signin-entra` (for development)
     - Add production URI when deploying

5. Click **Register**

## Step 2: Configure Authentication

1. In your app registration, go to **Authentication**
2. Under **Implicit grant and hybrid flows**, enable:
   - ✅ ID tokens (used for implicit and hybrid flows)
3. Under **Advanced settings**:
   - Allow public client flows: **No**
4. Click **Save**

## Step 3: Create Client Secret

1. Go to **Certificates & secrets**
2. Click **New client secret**
3. Add description: `Development Secret`
4. Set expiration (recommend 6 months for dev, shorter for production)
5. Click **Add**
6. **IMPORTANT**: Copy the secret value immediately (you won't be able to see it again)

## Step 4: Configure API Permissions

1. Go to **API permissions**
2. The following permissions should already be present:
   - `User.Read` (Microsoft Graph)
3. Add additional permissions if needed:
   - Click **Add a permission**
   - Select **Microsoft Graph**
   - Select **Delegated permissions**
   - Add: `email`, `openid`, `profile`
4. Click **Grant admin consent** (if you have admin rights)

## Step 5: Update Configuration

Update the `appsettings.json` file in `SchedulerPlatform.IdentityServer`:

```json
{
  "AzureAd": {
    "TenantId": "08717c9a-7042-4ddf-b86a-e0a500d32cde",
    "ClientId": "YOUR_APPLICATION_CLIENT_ID_HERE",
    "ClientSecret": "YOUR_CLIENT_SECRET_HERE"
  }
}
```

**For Production**: Use Azure Key Vault or environment variables instead of storing secrets in appsettings.json

## Step 6: Update Redirect URIs for Production

When deploying to production, add the production redirect URI:
1. Go to your app registration > **Authentication**
2. Add redirect URI: `https://your-production-domain.com/signin-entra`
3. Update the `appsettings.json` in production environment

## Step 7: Test the Integration

1. Run the IdentityServer: `dotnet run --project src/SchedulerPlatform.IdentityServer`
2. Navigate to the login page
3. You should see a "Sign in with Microsoft" button
4. Click it and authenticate with your Azure AD credentials
5. On first login, a local user account will be created automatically (JIT provisioning)
6. Default permissions will be assigned (scheduler:read only)
7. An admin can then grant additional permissions via the admin UI

## Service Account Authentication

The service account `svc-adrscheduler` is configured for machine-to-machine authentication using OAuth2 Client Credentials flow.

### Getting a Token

```bash
curl -X POST https://localhost:5001/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=svc-adrscheduler" \
  -d "client_secret=dev-secret-change-in-production" \
  -d "scope=scheduler-api"
```

### Using the Token

```bash
curl -X GET https://localhost:7008/api/schedules \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN_HERE"
```

### Service Account Permissions

The `svc-adrscheduler` service account has the following permissions:
- `scheduler:read`
- `schedules:read`
- `schedules:create`
- `schedules:update`
- `schedules:delete`
- `schedules:execute`
- `jobs:read`

### Production Configuration

For production, replace the client secret in `Config.cs`:

```csharp
ClientSecrets = { new Secret("YOUR_PRODUCTION_SECRET_HERE".Sha256()) }
```

**Recommended**: Use certificate-based authentication (private_key_jwt) instead of shared secrets for production.

## Troubleshooting

### "Sign in with Microsoft" button not appearing
- Verify ClientId and ClientSecret are configured in appsettings.json
- Check IdentityServer logs for authentication provider registration errors

### Authentication fails with "invalid_client"
- Verify the Client Secret hasn't expired
- Ensure the Client ID matches the Azure AD app registration
- Check that the redirect URI matches exactly (including https/http and trailing slashes)

### User created but has no permissions
- Check that the JIT provisioning code is executing (check logs)
- Verify the default client (ClientId = 1) exists in the database
- Run the DATABASE_SEED.sql script if the client doesn't exist

### Service account authentication fails
- Verify the client_id and client_secret match Config.cs
- Ensure the scope "scheduler-api" is included in the token request
- Check that AlwaysSendClientClaims is set to true in the client configuration

## Security Recommendations

1. **Never commit secrets to source control**
2. Use Azure Key Vault for production secrets
3. Rotate client secrets regularly (every 3-6 months)
4. Use certificate-based authentication for service accounts in production
5. Enable MFA for all Azure AD users
6. Monitor authentication logs for suspicious activity
7. Set appropriate token lifetimes (shorter for sensitive operations)

## Permission Templates

### Viewer (Read-Only)
- `scheduler:read`
- `schedules:read`
- `jobs:read`

### Editor
- `scheduler:read`
- `schedules:read`, `schedules:create`, `schedules:update`, `schedules:delete`, `schedules:execute`
- `jobs:read`

### Admin
- All Editor permissions
- `users:manage` (can manage users and permissions)

### Super Admin
- All Admin permissions
- `IsSystemAdmin = true` (cannot be modified via UI)
