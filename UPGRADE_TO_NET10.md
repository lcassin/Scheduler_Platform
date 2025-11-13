# Upgrade to .NET 10 Guide

This document outlines the changes made to upgrade the Scheduler Platform from .NET 9 to .NET 10.

## Prerequisites

Before building this project, you need:

1. **.NET 10 SDK** - Download from https://dotnet.microsoft.com/download/dotnet/10.0
2. **Visual Studio 2022 (17.12+)** or **Visual Studio 2026** - Both support .NET 10
   - Alternatively, use VS Code with the latest C# Dev Kit extension
3. **SQL Server** - For database operations

## Changes Made

### 1. Target Framework Updates

All project files have been updated from `net9.0` to `net10.0`:

- SchedulerPlatform.API
- SchedulerPlatform.IdentityServer
- SchedulerPlatform.UI
- SchedulerPlatform.Core
- SchedulerPlatform.Infrastructure
- SchedulerPlatform.Jobs
- SchedulerPlatform.LogCleanup
- SchedulerPlatform.ScheduleSync

### 2. NuGet Package Updates

**Microsoft packages updated to version 10.0.0:**

- Microsoft.AspNetCore.Authentication.JwtBearer: 9.0.10 → 10.0.0
- Microsoft.AspNetCore.Authentication.OpenIdConnect: 9.0.10 → 10.0.0
- Microsoft.EntityFrameworkCore.*: 9.0.10 → 10.0.0
- Microsoft.Extensions.*: 9.0.10 → 10.0.0

**Important Package Changes:**
- **Removed**: `Microsoft.AspNetCore.OpenApi` - Not needed when using Swashbuckle
- **Added**: `Microsoft.OpenApi` 3.0.0 - Required for `Microsoft.OpenApi.Models` namespace

### 3. Third-Party Package Updates

All third-party packages have been updated to their latest versions:

- **Swashbuckle.AspNetCore**: 9.0.6 → 10.0.1
- **Microsoft.OpenApi**: Added at 3.0.0 (compatible with Swashbuckle 10.x)
- **Microsoft.Data.SqlClient**: 6.1.2 → 6.1.3
- **Quartz** (all packages): 3.15.0 → 3.15.1
- **MudBlazor**: 8.13.0 → 8.14.0
- **Heron.MudCalendar**: 3.2.0 → 3.3.0
- **Duende.IdentityServer**: 7.3.2 (latest in 7.x series, no update needed)
- **Serilog.AspNetCore**: 9.0.0 (no update needed)
- **ClosedXML**: 0.105.0 (no update needed)
- **Dapper**: 2.1.66 (no update needed)

## Building the Project

Once you have .NET 10 SDK installed:

```bash
# Restore packages
dotnet restore

# Build the solution
dotnet build

# Run a specific project (example: API)
dotnet run --project src/SchedulerPlatform.API/SchedulerPlatform.API.csproj
```

## Potential Issues and Solutions

### Microsoft.OpenApi.Models Namespace Error (RESOLVED)

**Issue**: `CS0234: The type or namespace name 'Models' does not exist in the namespace 'Microsoft.OpenApi'`

**Root Cause**: Version incompatibility between Swashbuckle and Microsoft.OpenApi. Swashbuckle 9.x required Microsoft.OpenApi 1.x, but we initially tried 2.0.0 which had a different namespace structure.

**Solution Applied**: 
- Upgraded to **Swashbuckle.AspNetCore 10.0.1** (latest version)
- Added explicit reference to **Microsoft.OpenApi 3.0.0** (compatible with Swashbuckle 10.x)
- Removed `Microsoft.AspNetCore.OpenApi` package (not needed when using Swashbuckle)

This issue has been fully resolved. The existing Swagger configuration in Program.cs works without code changes.

### Swagger/OpenAPI - No Code Changes Required

The upgrade to Swashbuckle 10.0.1 + Microsoft.OpenApi 3.0.0 is backward compatible. Your existing `AddSwaggerGen` configuration continues to work as-is:

```csharp
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { ... });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { ... });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { ... });
});
```

No changes needed to Program.cs for the Swagger upgrade.

### Duende IdentityServer

Duende IdentityServer 7.3.2 is fully compatible with .NET 10. No changes should be needed.

### Entity Framework Core

EF Core 10.0.0 includes performance improvements and new features. Your existing migrations should work without modification, but you may want to:

1. Test all database operations thoroughly
2. Review EF Core 10 breaking changes: https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/breaking-changes

## Testing Checklist

After building successfully, test the following:

- [ ] IdentityServer authentication flows
- [ ] API endpoints with JWT authentication
- [ ] Swagger UI functionality
- [ ] Blazor UI with OpenID Connect authentication
- [ ] Quartz.NET job scheduling
- [ ] Database migrations and operations
- [ ] All scheduled jobs execute correctly

## Rollback

If you need to rollback to .NET 9:

1. Revert all .csproj files to use `<TargetFramework>net9.0</TargetFramework>`
2. Revert all Microsoft.* packages to version 9.0.10
3. Run `dotnet restore` and `dotnet build`

## Additional Resources

- [.NET 10 Release Notes](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [ASP.NET Core 10 What's New](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0)
- [EF Core 10 What's New](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
- [Duende IdentityServer Documentation](https://docs.duendesoftware.com/identityserver/v7)
