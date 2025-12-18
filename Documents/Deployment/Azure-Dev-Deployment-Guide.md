# ADR Scheduler - Azure Dev Deployment Guide

## Azure Resources Summary

| Resource Type | Name | Details |
|--------------|------|---------|
| Web UI | `nuscetsadrschdevwebuiai` | IP: 10.222.24.52 |
| Web API | `nuscetsadrschdevwebapi` | IP: 10.222.24.53 |
| Managed Identity | `nuscetsadrschdevmi` | Used for SQL & Key Vault access |
| Key Vault | `nuscetsadrschdevkv` | Secrets storage |
| SQL Server | `nuscetsadrschdevsql` | Database: `ADRSchedulerDev` |
| VNet | `npduscevnet01/aseueges2wnsn04` | Shared network |
| App Insights (UI) | `nuscetsadrschdevwebuiai` | Telemetry |
| App Insights (API) | `nuscetsadrschdevwebapiai` | Telemetry |

---

## Part 1: Duende IdentityServer Client Registration

**Provide this section to your Duende administrator.**

### Required API Resource & Scopes

The following must be registered in the existing Duende instance:

```
API Resource Name: scheduler-api
Display Name: ADR Scheduler Platform API
Scopes: scheduler-api
User Claims: name, role, email, permission, is_system_admin, user_client_id
```

### Required Identity Resources

```
Identity Resource: role
  User Claims: role

Identity Resource: permissions
  Display Name: User Permissions
  User Claims: permission, is_system_admin
```

### Client 1: ADR Scheduler UI (Interactive Login)

```
Client ID: adr-scheduler-ui
Client Name: ADR Scheduler Platform UI
Grant Type: Authorization Code with PKCE
Requires Client Secret: Yes
Client Secret: [Generate secure secret - store in Key Vault]

Redirect URIs:
  - https://nuscetsadrschdevwebuiai.azurewebsites.net/signin-oidc

Post-Logout Redirect URIs:
  - https://nuscetsadrschdevwebuiai.azurewebsites.net/signout-callback-oidc

Allowed Scopes:
  - openid
  - profile
  - email
  - scheduler-api
  - role
  - permissions

Access Token Lifetime: 3600 seconds
Allow Offline Access: Yes
Require Consent: No
```

### Client 2: ADR Scheduler Swagger UI (API Documentation)

```
Client ID: adr-scheduler-swagger
Client Name: ADR Scheduler Swagger UI
Grant Type: Authorization Code with PKCE
Requires Client Secret: No (Public Client)

Redirect URIs:
  - https://nuscetsadrschdevwebapi.azurewebsites.net/swagger/oauth2-redirect.html

Allowed CORS Origins:
  - https://nuscetsadrschdevwebapi.azurewebsites.net

Allowed Scopes:
  - openid
  - profile
  - email
  - scheduler-api
  - role
  - permissions

Access Token Lifetime: 3600 seconds
Require Consent: No
```

### Client 3: ADR Scheduler Service Account (Background Jobs)

```
Client ID: svc-adrscheduler
Client Name: ADR Scheduler Service Account
Grant Type: Client Credentials
Requires Client Secret: Yes
Client Secret: [Generate secure secret - store in Key Vault]

Allowed Scopes:
  - scheduler-api

Client Claims (IMPORTANT - these must be included in tokens):
  - permission = scheduler:read
  - permission = schedules:read
  - permission = schedules:create
  - permission = schedules:update
  - permission = schedules:delete
  - permission = schedules:execute
  - permission = jobs:read

Always Send Client Claims: Yes
Client Claims Prefix: (empty string)
Access Token Lifetime: 3600 seconds
```

---

## Part 2: Key Vault Secrets

Add these secrets to Key Vault `nuscetsadrschdevkv`:

| Secret Name | Description | How to Get Value |
|-------------|-------------|------------------|
| `ConnectionStrings--DefaultConnection` | Main SQL connection string | See SQL Connection String section below |
| `ConnectionStrings--QuartzConnection` | Quartz scheduler SQL connection | Same as DefaultConnection |
| `ConnectionStrings--VendorCredential` | VendorCred database connection | Existing connection string |
| `Encryption--Key` | Data encryption key | Use existing: `IVtj6oce4lqq3Ujz+UP7vbkQzWYN7wnFkb6Vza0t07I=` |
| `Scheduler--InternalApiKey` | Internal API authentication | Generate new secure key |
| `IdentityServer--ClientSecret` | UI client secret | Get from Duende admin after registration |
| `ApplicationInsights--ConnectionString` | App Insights connection | Get from Azure portal |

### SQL Connection String (Managed Identity)

```
Server=tcp:nuscetsadrschdevsql.database.windows.net,1433;Initial Catalog=ADRSchedulerDev;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;
```

**Note:** The managed identity `nuscetsadrschdevmi` must have database access. Run this SQL as Azure AD admin:

```sql
-- Run in ADRSchedulerDev database
CREATE USER [nuscetsadrschdevmi] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [nuscetsadrschdevmi];
ALTER ROLE db_datawriter ADD MEMBER [nuscetsadrschdevmi];
ALTER ROLE db_ddladmin ADD MEMBER [nuscetsadrschdevmi];  -- For migrations
```

---

## Part 3: App Service Configuration

### Web API (`nuscetsadrschdevwebapi`) - Application Settings

| Setting | Value |
|---------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Development` or `AzureDev` |
| `KeyVault__VaultUri` | `https://nuscetsadrschdevkv.vault.azure.net/` |
| `Authentication__Authority` | `https://[your-duende-url]` |
| `Authentication__Audience` | `scheduler-api` |
| `Authentication__RequireHttpsMetadata` | `true` |
| `Cors__AllowedOrigins__0` | `https://nuscetsadrschdevwebuiai.azurewebsites.net` |
| `SchedulerSettings__AdrApi__BaseUrl` | `https://nuse2etsadrdevfn01.azurewebsites.net/api/` |
| `SchedulerSettings__AdrApi__RecipientEmail` | `[appropriate-email]` |

### Web UI (`nuscetsadrschdevwebuiai`) - Application Settings

| Setting | Value |
|---------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Development` or `AzureDev` |
| `KeyVault__VaultUri` | `https://nuscetsadrschdevkv.vault.azure.net/` |
| `API__BaseUrl` | `https://nuscetsadrschdevwebapi.azurewebsites.net/api/` |
| `IdentityServer__Authority` | `https://[your-duende-url]` |
| `IdentityServer__ClientId` | `adr-scheduler-ui` |

**Note:** `IdentityServer__ClientSecret` should come from Key Vault, not App Settings.

---

## Part 4: Deployment Checklist

### Pre-Deployment

- [ ] Duende admin has registered all 3 clients
- [ ] Duende admin has confirmed API resource `scheduler-api` exists
- [ ] Duende admin has confirmed identity resources `role` and `permissions` exist
- [ ] Received client secrets from Duende admin
- [ ] Received Duende Authority URL

### Key Vault Setup

- [ ] Added `ConnectionStrings--DefaultConnection` with Managed Identity auth
- [ ] Added `ConnectionStrings--QuartzConnection` (same as above)
- [ ] Added `ConnectionStrings--VendorCredential`
- [ ] Added `Encryption--Key`
- [ ] Added `Scheduler--InternalApiKey`
- [ ] Added `IdentityServer--ClientSecret`
- [ ] Added `ApplicationInsights--ConnectionString`
- [ ] Granted managed identity `nuscetsadrschdevmi` access to Key Vault (Get, List secrets)

### SQL Server Setup

- [ ] Created contained user for managed identity
- [ ] Granted db_datareader role
- [ ] Granted db_datawriter role
- [ ] Granted db_ddladmin role (for migrations)
- [ ] Ran EF migrations to create schema

### App Service Configuration

- [ ] Set all API application settings
- [ ] Set all UI application settings
- [ ] Verified managed identity is enabled on both App Services
- [ ] Verified managed identity is assigned to Key Vault

### Post-Deployment Verification

- [ ] API health endpoint responds: `https://nuscetsadrschdevwebapi.azurewebsites.net/health`
- [ ] UI loads login page
- [ ] Login flow completes successfully
- [ ] API calls from UI work (check browser console)
- [ ] Swagger UI loads and can authenticate

---

## Part 5: Troubleshooting

### Common Issues

**"Invalid redirect URI" during login**
- Verify redirect URIs in Duende match exactly (including trailing slashes)
- Check `IdentityServer:Authority` points to correct Duende URL

**"Unauthorized" errors from API**
- Verify `Authentication:Authority` matches Duende URL
- Verify `Authentication:Audience` matches API resource name in Duende
- Check that tokens include required claims (`permission`, `role`)

**SQL connection failures**
- Verify managed identity has database access
- Check connection string uses `Active Directory Managed Identity`
- Ensure SQL Server has Azure AD admin configured

**Key Vault access denied**
- Verify managed identity has Get/List permissions on secrets
- Check `KeyVault:VaultUri` is correct
- Ensure App Service is using the correct managed identity

---

## Appendix: Claim Requirements

The ADR Scheduler API authorization system expects these claims in access tokens:

| Claim Type | Description | Example Values |
|------------|-------------|----------------|
| `permission` | Feature-specific permissions | `adr:read`, `adr:update`, `schedules:execute` |
| `role` | User role | `Admin`, `Editor`, `Viewer` |
| `is_system_admin` | Super Admin flag (bypasses all checks) | `True` |
| `user_client_id` | Associated client ID | `1` |
| `name` | User display name | `John Doe` |
| `email` | User email | `john@example.com` |

If the existing Duende instance uses different claim names, the API's `PermissionAuthorizationHandler` will need to be updated to map the claims appropriately.
