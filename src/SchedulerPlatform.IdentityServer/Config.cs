using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Configuration;

namespace SchedulerPlatform.IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        new List<IdentityResource>
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email(),
            new IdentityResource
            {
                Name = "role",
                UserClaims = new List<string> {"role"}
            },
            new IdentityResource
            {
                Name = "permissions",
                DisplayName = "User Permissions",
                Description = "User permissions and admin status",
                UserClaims = new List<string> { "permission", "is_system_admin" }
            }
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new List<ApiScope>
        {
            new ApiScope("scheduler-api", "Scheduler Platform API")
            {
                UserClaims = { "name", "role", "email", "permission", "is_system_admin", "user_client_id" }
            },
            new ApiScope("admin", "Admin Access"),
            new ApiScope("client", "Client Access")
        };

    public static IEnumerable<ApiResource> ApiResources =>
        new List<ApiResource>
        {
            new ApiResource("scheduler-api", "Scheduler Platform API")
            {
                Scopes = { "scheduler-api", "admin", "client" },
                ApiSecrets = { new Secret("api-secret-key".Sha256()) },
                UserClaims = { "name", "role", "email", "user_client_id", "permission", "is_system_admin" }
            }
        };

    /// <summary>
    /// Gets the configured clients for IdentityServer.
    /// URLs are read from configuration to support multiple deployment environments.
    /// </summary>
    /// <param name="configuration">The application configuration</param>
    /// <returns>List of configured clients</returns>
    public static IEnumerable<Client> GetClients(IConfiguration configuration)
    {
        // Read URLs from configuration with localhost defaults for development
        var uiBaseUrl = configuration["Clients:UI:BaseUrl"] ?? "https://localhost:7299";
        var apiBaseUrl = configuration["Clients:API:BaseUrl"] ?? "https://localhost:7008";
        var blazorClientSecret = configuration["Clients:Blazor:ClientSecret"] ?? "secret";
        var serviceAccountSecret = configuration["Clients:ServiceAccount:ClientSecret"] ?? "dev-secret-change-in-production";
        
        // Parse additional redirect URIs from configuration (comma-separated)
        var additionalUiRedirectUris = configuration["Clients:UI:AdditionalRedirectUris"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        var additionalApiSwaggerUris = configuration["Clients:API:AdditionalSwaggerRedirectUris"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        var additionalApiCorsOrigins = configuration["Clients:API:AdditionalCorsOrigins"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();

        // Build redirect URIs for Blazor client
        var blazorRedirectUris = new List<string> { $"{uiBaseUrl}/signin-oidc" };
        blazorRedirectUris.AddRange(additionalUiRedirectUris.Select(u => $"{u}/signin-oidc"));
        
        var blazorPostLogoutUris = new List<string> { $"{uiBaseUrl}/signout-callback-oidc" };
        blazorPostLogoutUris.AddRange(additionalUiRedirectUris.Select(u => $"{u}/signout-callback-oidc"));

        // Build redirect URIs and CORS origins for Swagger UI
        var swaggerRedirectUris = new List<string>
        {
            $"{apiBaseUrl}/swagger/oauth2-redirect.html",
            apiBaseUrl.Replace("https://", "http://") + "/swagger/oauth2-redirect.html"
        };
        swaggerRedirectUris.AddRange(additionalApiSwaggerUris);

        var swaggerCorsOrigins = new List<string>
        {
            apiBaseUrl,
            apiBaseUrl.Replace("https://", "http://")
        };
        swaggerCorsOrigins.AddRange(additionalApiCorsOrigins);

        return new List<Client>
        {
            new Client
            {
                ClientId = "scheduler-blazor",
                ClientName = "Scheduler Platform Blazor Client",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = true,
                ClientSecrets = { new Secret(blazorClientSecret.Sha256()) },
                RedirectUris = blazorRedirectUris,
                PostLogoutRedirectUris = blazorPostLogoutUris,
                AllowOfflineAccess = true,
                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    "scheduler-api",
                    "admin",
                    "client",
                    "role",
                    "permissions"
                },
                AccessTokenLifetime = 3600,
                RequireConsent = false
            },
            new Client
            {
                ClientId = "svc-adrscheduler",
                ClientName = "ADR Scheduler Service Account",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret(serviceAccountSecret.Sha256()) },
                AllowedScopes = { "scheduler-api" },
                Claims = new List<ClientClaim>
                {
                    new ClientClaim("permission", "scheduler:read"),
                    new ClientClaim("permission", "schedules:read"),
                    new ClientClaim("permission", "schedules:create"),
                    new ClientClaim("permission", "schedules:update"),
                    new ClientClaim("permission", "schedules:delete"),
                    new ClientClaim("permission", "schedules:execute"),
                    new ClientClaim("permission", "jobs:read")
                },
                AlwaysSendClientClaims = true,
                ClientClaimsPrefix = string.Empty,
                AccessTokenLifetime = 3600
            },
            new Client
            {
                ClientId = "swagger-ui",
                ClientName = "Swagger UI",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false,
                RedirectUris = swaggerRedirectUris,
                AllowedCorsOrigins = swaggerCorsOrigins,
                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    "scheduler-api",
                    "role",
                    "permissions"
                },
                AccessTokenLifetime = 3600,
                RequireConsent = false
            }
        };
    }

    // Keep the static property for backward compatibility during migration
    [Obsolete("Use GetClients(IConfiguration) instead for environment-specific configuration")]
    public static IEnumerable<Client> Clients => GetClients(new ConfigurationBuilder().Build());
}
