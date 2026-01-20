using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;
using SchedulerPlatform.UI.Components;
using SchedulerPlatform.UI.Services;
using Azure.Identity;
using System.Security.Claims;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Determine the log path based on environment
// Azure App Service: Use %HOME%\LogFiles\Application\ which persists across deployments
// Local development: Use relative logs/ folder
var azureHome = Environment.GetEnvironmentVariable("HOME");
var isAzureAppService = !string.IsNullOrEmpty(azureHome) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
var logPath = isAzureAppService 
    ? Path.Combine(azureHome!, "LogFiles", "Application", "scheduler-ui-.txt")
    : "logs/scheduler-ui-.txt";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        logPath, 
        rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: true,
        fileSizeLimitBytes: 30 * 1024 * 1024,  // 30MB
        retainedFileCountLimit: 31)
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("UI Log path configured: {LogPath} (Azure: {IsAzure})", logPath, isAzureAppService);

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
    
    options.Events = new CookieAuthenticationEvents
    {
        OnValidatePrincipal = async context =>
        {
            var tokens = context.Properties.GetTokens().ToList();
            var expiresAtToken = tokens.FirstOrDefault(t => t.Name == "expires_at");
            
            if (expiresAtToken != null && 
                DateTimeOffset.TryParse(expiresAtToken.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt))
            {
                // Refresh if token expires within 10 minutes (gives buffer for keepalive interval)
                if (expiresAt <= DateTimeOffset.UtcNow.AddMinutes(10))
                {
                    var refreshToken = tokens.FirstOrDefault(t => t.Name == "refresh_token")?.Value;
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                        
                        try
                        {
                            using var httpClient = new HttpClient();
                            var authority = config["IdentityServer:Authority"];
                            var clientId = config["IdentityServer:ClientId"];
                            var clientSecret = config["IdentityServer:ClientSecret"];
                            
                            var tokenEndpoint = $"{authority}/connect/token";
                            var tokenRequest = new Dictionary<string, string>
                            {
                                ["grant_type"] = "refresh_token",
                                ["refresh_token"] = refreshToken,
                                ["client_id"] = clientId!,
                                ["client_secret"] = clientSecret!
                            };
                            
                            var response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(tokenRequest));
                            if (response.IsSuccessStatusCode)
                            {
                                var content = await response.Content.ReadAsStringAsync();
                                var tokenResponse = JsonDocument.Parse(content);
                                
                                var newAccessToken = tokenResponse.RootElement.GetProperty("access_token").GetString();
                                var newRefreshToken = tokenResponse.RootElement.TryGetProperty("refresh_token", out var rt) 
                                    ? rt.GetString() 
                                    : refreshToken;
                                var expiresIn = tokenResponse.RootElement.GetProperty("expires_in").GetInt32();
                                var newExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
                                
                                // Update tokens
                                var newTokens = new List<AuthenticationToken>
                                {
                                    new() { Name = "access_token", Value = newAccessToken! },
                                    new() { Name = "refresh_token", Value = newRefreshToken! },
                                    new() { Name = "expires_at", Value = newExpiresAt.ToString("o", CultureInfo.InvariantCulture) }
                                };
                                
                                // Preserve id_token if present
                                var idToken = tokens.FirstOrDefault(t => t.Name == "id_token");
                                if (idToken != null)
                                {
                                    newTokens.Add(idToken);
                                }
                                
                                context.Properties.StoreTokens(newTokens);
                                context.ShouldRenew = true;
                                
                                logger.LogInformation("Successfully refreshed access token. New expiry: {ExpiresAt}", newExpiresAt);
                            }
                            else
                            {
                                logger.LogWarning("Failed to refresh token. Status: {Status}", response.StatusCode);
                                // Token refresh failed - user will need to re-authenticate
                                context.RejectPrincipal();
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error refreshing access token");
                        }
                    }
                    else
                    {
                        // No refresh token available and access token is expiring
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogWarning("Access token expiring but no refresh token available");
                    }
                }
            }
        }
    };
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
    options.Scope.Add("offline_access");
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

// Named HttpClient for permission cache service (uses same auth handler)
builder.Services.AddHttpClient("SchedulerApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["API:BaseUrl"]!);
})
.AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddScoped<SessionStateService>();
builder.Services.AddScoped<AccessTokenCacheService>();
builder.Services.AddScoped<AuthTokenHandler>();
builder.Services.AddScoped<AuthenticatedHttpClientService>();
builder.Services.AddScoped<UserPermissionCacheService>();
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

// Keepalive endpoint for token refresh during long-running Blazor Server circuits
// This endpoint is authenticated, so hitting it triggers OnValidatePrincipal which refreshes tokens
app.MapGet("/keepalive", async (HttpContext context) =>
{
    // Get token expiry info for the response
    var expiresAt = await context.GetTokenAsync("expires_at");
    DateTimeOffset? expiresAtParsed = null;
    int? minutesRemaining = null;
    
    if (!string.IsNullOrEmpty(expiresAt) && 
        DateTimeOffset.TryParse(expiresAt, System.Globalization.CultureInfo.InvariantCulture, 
            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
    {
        expiresAtParsed = parsed;
        minutesRemaining = (int)Math.Max(0, (parsed - DateTimeOffset.UtcNow).TotalMinutes);
    }
    
    // Update GlobalTokenStore with the refreshed token
    // This is critical for Blazor Server circuits where API calls use the token store
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userKey = AuthTokenHandler.GetUserKey(context.User);
        if (!string.IsNullOrEmpty(userKey))
        {
            var accessToken = await context.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(accessToken))
            {
                GlobalTokenStore.SetToken(userKey, accessToken, expiresAtParsed);
            }
        }
    }
    
    return Results.Ok(new { 
        authenticated = context.User.Identity?.IsAuthenticated ?? false,
        expiresAt = expiresAtParsed?.ToString("o"),
        minutesRemaining = minutesRemaining,
        serverTime = DateTimeOffset.UtcNow.ToString("o")
    });
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
