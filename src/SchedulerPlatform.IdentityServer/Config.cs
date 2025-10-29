using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using System.Security.Claims;
using System.Text.Json;

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
            }
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new List<ApiScope>
        {
            new ApiScope("scheduler-api", "Scheduler Platform API")
            {
                UserClaims = { "name", "role", "email" }
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
                UserClaims = { "name", "role", "email", "client_id" }
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
                    "role"
                },
                AccessTokenLifetime = 3600,
                RequireConsent = false
            }
        };

    public static List<TestUser> Users =>
        new List<TestUser>
        {
            new TestUser
            {
                SubjectId = "1",
                Username = "admin",
                Password = "Admin123!",
                Claims =
                {
                    new Claim("name", "Admin User"),
                    new Claim("given_name", "Admin"),
                    new Claim("family_name", "User"),
                    new Claim("email", "admin@example.com"),
                    new Claim("email_verified", "true", ClaimValueTypes.Boolean),
                    new Claim("role", "Admin"),
                    new Claim("client_id", "0"),
                    new Claim("test_user", "true")
                }
            },
            new TestUser
            {
                SubjectId = "2",
                Username = "client1",
                Password = "Client123!",
                Claims =
                {
                    new Claim("name", "Client User"),
                    new Claim("given_name", "Client"),
                    new Claim("family_name", "User"),
                    new Claim("email", "client@example.com"),
                    new Claim("email_verified", "true", ClaimValueTypes.Boolean),
                    new Claim("role", "Client"),
                    new Claim("client_id", "1"),
                    new Claim("test_user", "true")
                }
            }
        };
}
