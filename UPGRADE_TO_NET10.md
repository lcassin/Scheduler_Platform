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

All Microsoft packages have been updated to version 10.0.0:

- Microsoft.AspNetCore.Authentication.JwtBearer: 9.0.10 → 10.0.0
- Microsoft.AspNetCore.OpenApi: 9.0.10 → 10.0.0
- Microsoft.AspNetCore.Authentication.OpenIdConnect: 9.0.10 → 10.0.0
- Microsoft.EntityFrameworkCore.*: 9.0.10 → 10.0.0
- Microsoft.Extensions.*: 9.0.10 → 10.0.0

### 3. Third-Party Packages (No Changes Required)

The following packages remain at their current versions and are compatible with .NET 10:

- **Duende.IdentityServer**: 7.3.2 (supports .NET 8-10)
- **Swashbuckle.AspNetCore**: 9.0.6 (compatible with .NET 10)
- **MudBlazor**: 8.13.0 (compatible with .NET 10)
- **Quartz**: 3.15.0 (compatible with .NET 10)
- **Serilog.AspNetCore**: 9.0.0 (compatible with .NET 10)

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

### Swagger/OpenAPI Compatibility

If you encounter issues with Swagger, note that .NET 9+ introduced native OpenAPI support which can sometimes conflict with Swashbuckle. The current version (9.0.6) should work fine, but if you experience issues:

**Option 1: Update Swashbuckle** (if newer version available)
```bash
dotnet add package Swashbuckle.AspNetCore --version <latest>
```

**Option 2: Use Microsoft's built-in OpenAPI** (alternative approach)
- Remove Swashbuckle.AspNetCore
- The project already uses Microsoft.AspNetCore.OpenApi 10.0.0
- Update Program.cs to use the built-in OpenAPI services

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
