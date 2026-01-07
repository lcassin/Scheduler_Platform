using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using MudBlazor.Services;
using SchedulerPlatform.UI.Components;
using SchedulerPlatform.UI.Services;
using Azure.Identity;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault configuration for non-Development environments
// Set KeyVault:VaultUri in appsettings.json or environment variable to enable
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri) && !builder.Environment.IsDevelopment())
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}

// Add Application Insights telemetry for non-Development environments
// Set ApplicationInsights:ConnectionString in appsettings.json or APPLICATIONINSIGHTS_CONNECTION_STRING env var
var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "SchedulerPlatform.Auth";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = builder.Configuration["IdentityServer:Authority"];
    options.ClientId = builder.Configuration["IdentityServer:ClientId"];
    options.ClientSecret = builder.Configuration["IdentityServer:ClientSecret"];
    options.ResponseType = "code";
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("scheduler-api");
    options.Scope.Add("permissions");
    options.RequireHttpsMetadata = false;
    
    options.ClaimActions.MapJsonKey("permission", "permission");
    options.ClaimActions.MapJsonKey("is_system_admin", "is_system_admin");
    
    options.Events = new OpenIdConnectEvents
    {
        OnRemoteFailure = context =>
        {
            if (context.Failure?.Message.Contains("access_denied") == true)
            {
                context.Response.Redirect("/");
                context.HandleResponse();
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            
            try
            {
                var accessToken = context.TokenEndpointResponse?.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    logger.LogWarning("No access token available for claims enrichment");
                    return;
                }

                var apiBaseUrl = config["API:BaseUrl"];
                if (string.IsNullOrEmpty(apiBaseUrl))
                {
                    logger.LogWarning("API:BaseUrl not configured for claims enrichment");
                    return;
                }

                // Ensure URL ends with / for proper URI combination
                if (!apiBaseUrl.EndsWith("/"))
                {
                    apiBaseUrl += "/";
                }

                // Use HttpClientHandler to disable auto-redirects and prevent auth loops
                using var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = false
                };
                using var httpClient = new HttpClient(handler);
                httpClient.BaseAddress = new Uri(apiBaseUrl);
                httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var internalApiKey = config["Scheduler:InternalApiKey"];
                if (!string.IsNullOrEmpty(internalApiKey))
                {
                    httpClient.DefaultRequestHeaders.Add("X-Scheduler-Api-Key", internalApiKey);
                }

                logger.LogDebug("Calling API for claims enrichment: {Url}", apiBaseUrl + "users/me");
                var response = await httpClient.GetAsync("users/me");
                
                if (response.StatusCode == System.Net.HttpStatusCode.Redirect || 
                    response.StatusCode == System.Net.HttpStatusCode.Found ||
                    response.StatusCode == System.Net.HttpStatusCode.MovedPermanently)
                {
                    logger.LogWarning("API returned redirect during claims enrichment. Location: {Location}", 
                        response.Headers.Location);
                    return;
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Failed to get user info from API for claims enrichment: {StatusCode}", response.StatusCode);
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                logger.LogDebug("API response for claims enrichment: {Content}", content);
                
                using var userInfo = JsonDocument.Parse(content);
                var root = userInfo.RootElement;

                var identity = context.Principal?.Identity as ClaimsIdentity;
                if (identity == null)
                {
                    return;
                }

                if (root.TryGetProperty("isSystemAdmin", out var isSystemAdmin) && isSystemAdmin.GetBoolean())
                {
                    identity.AddClaim(new Claim("is_system_admin", "true"));
                }

                if (root.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String)
                {
                    identity.AddClaim(new Claim("role", role.GetString()!));
                }

                if (root.TryGetProperty("permissions", out var permissions) && permissions.ValueKind == JsonValueKind.Array)
                {
                    foreach (var permission in permissions.EnumerateArray())
                    {
                        if (permission.ValueKind == JsonValueKind.String)
                        {
                            identity.AddClaim(new Claim("permission", permission.GetString()!));
                        }
                    }
                }

                logger.LogInformation("Successfully enriched claims for user during login");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error enriching claims during OIDC token validation");
            }
        }
    };
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient("SchedulerAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["API:BaseUrl"]!);
})
.AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddScoped<SessionStateService>();
builder.Services.AddScoped<AuthTokenHandler>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IJobExecutionService, JobExecutionService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IAdrService, AdrService>();
builder.Services.AddScoped<IUserTimeZoneService, UserTimeZoneService>();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/logout", async (HttpContext context) =>
{
    // Check if this is a session expiration logout
    var sessionExpired = context.Request.Query["sessionExpired"].ToString() == "true";
    var redirectUri = sessionExpired ? "/Account/Login?sessionExpired=true" : "/";
    
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, 
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties 
        { 
            RedirectUri = redirectUri 
        });
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
