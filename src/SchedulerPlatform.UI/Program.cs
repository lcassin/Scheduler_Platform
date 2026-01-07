using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;
using SchedulerPlatform.UI.Components;
using SchedulerPlatform.UI.Services;
using Azure.Identity;
using System.Globalization;
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
    
    options.Events = new CookieAuthenticationEvents
    {
        OnValidatePrincipal = async context =>
        {
            var tokens = context.Properties.GetTokens().ToList();
            var expiresAtToken = tokens.FirstOrDefault(t => t.Name == "expires_at");
            
            if (expiresAtToken != null && 
                DateTimeOffset.TryParse(expiresAtToken.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt))
            {
                // Refresh if token expires within 5 minutes
                if (expiresAt <= DateTimeOffset.UtcNow.AddMinutes(5))
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
