using Duende.IdentityServer;
using Duende.IdentityServer.Models;

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
                UserClaims = { "name", "role", "email", "permission", "is_system_admin" }
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

    public static IEnumerable<Client> Clients =>
        new List<Client>
        {
            new Client
            {
                ClientId = "scheduler-blazor",
                ClientName = "Scheduler Platform Blazor Client",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = true,
                ClientSecrets = { new Secret("secret".Sha256()) },
                RedirectUris = { "https://localhost:7299/signin-oidc" },
                PostLogoutRedirectUris = { "https://localhost:7299/signout-callback-oidc" },
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
                ClientSecrets = { new Secret("dev-secret-change-in-production".Sha256()) },
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
            }
        };

}
