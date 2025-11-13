using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SchedulerPlatform.IdentityServer;
using SchedulerPlatform.IdentityServer.Services;
using SchedulerPlatform.Infrastructure.Data;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Repositories;
using DuendeClient = Duende.IdentityServer.Models.Client;
using DomainClient = SchedulerPlatform.Core.Domain.Entities.Client;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/identity-server-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDbContext<SchedulerDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.MigrationsAssembly("SchedulerPlatform.Infrastructure")));

builder.Services.AddIdentityServer(options =>
    {
        options.Events.RaiseErrorEvents = true;
        options.Events.RaiseInformationEvents = true;
        options.Events.RaiseFailureEvents = true;
        options.Events.RaiseSuccessEvents = true;

        options.EmitStaticAudienceClaim = true;

        if (builder.Environment.IsDevelopment())
        {
            options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
        }
    })
    .AddDeveloperSigningCredential()
    .AddInMemoryApiResources(Config.ApiResources)
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryClients(Config.Clients)
    .AddTestUsers(Config.Users)
    .AddProfileService<SchedulerProfileService>();

var azureTenantId = builder.Configuration["AzureAd:TenantId"];
var azureClientId = builder.Configuration["AzureAd:ClientId"];
var azureClientSecret = builder.Configuration["AzureAd:ClientSecret"];

if (!string.IsNullOrEmpty(azureTenantId) && !string.IsNullOrEmpty(azureClientId))
{
    builder.Services.AddAuthentication()
        .AddOpenIdConnect("entra", "Sign in with Microsoft", options =>
        {
            options.Authority = $"https://login.microsoftonline.com/{azureTenantId}/v2.0";
            options.ClientId = azureClientId;
            options.ClientSecret = azureClientSecret;
            options.ResponseType = "code";
            options.CallbackPath = "/signin-entra";
            options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
            options.SaveTokens = true;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = "roles";
        });
}

builder.Services.AddScoped<IRepository<User>, Repository<User>>();
builder.Services.AddScoped<IRepository<DomainClient>, Repository<DomainClient>>();
builder.Services.AddScoped<IRepository<UserPermission>, Repository<UserPermission>>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowUI", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "https://localhost:7299" })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowUI");

app.UseIdentityServer();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapDefaultControllerRoute();
    endpoints.MapRazorPages();
});

app.MapGet("/health", () => "Identity Server is running!");

app.Run();
