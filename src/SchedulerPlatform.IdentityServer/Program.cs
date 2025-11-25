using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.IdentityServer;
using SchedulerPlatform.IdentityServer.Services;
using SchedulerPlatform.Infrastructure.Data;
using SchedulerPlatform.Infrastructure.Repositories;
using Serilog;
using DomainClient = SchedulerPlatform.Core.Domain.Entities.Client;
using DuendeClient = Duende.IdentityServer.Models.Client;

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
    .AddProfileService<SchedulerProfileService>();

var azureTenantId = builder.Configuration["AzureAd:TenantId"];
var azureClientId = builder.Configuration["AzureAd:ClientId"];
var azureClientSecret = builder.Configuration["AzureAd:ClientSecret"];

if (!string.IsNullOrEmpty(azureTenantId) && !string.IsNullOrEmpty(azureClientId))
{
    builder.Services.AddAuthentication()
		/* 
		 * .AddOpenIdConnect("AAD", "Cass Employee Login", options =>
		{
			options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
			options.SignOutScheme = IdentityServerConstants.SignoutScheme;

			options.Authority = "https://login.windows.net/08717c9a-7042-4ddf-b86a-e0a500d32cde";
			options.ClientId = "006b80bc-946b-4754-aa9f-d4c8e8c47a2b"; 
			options.ResponseType = OpenIdConnectResponseType.IdToken;

			options.Scope.Add(IdentityServerConstants.StandardScopes.OpenId);
			options.Scope.Add(IdentityServerConstants.StandardScopes.Profile);
			options.Scope.Add(IdentityServerConstants.StandardScopes.Email);

			options.CallbackPath = "/signin-aad";
			options.SignedOutCallbackPath = "/signout-callback-aad";
			options.RemoteSignOutPath = "/signout-aad";

			options.TokenValidationParameters = new TokenValidationParameters
			{
				NameClaimType = "name",
				RoleClaimType = "role"
			};
			options.DisableTelemetry = true;
			//options.GetClaimsFromUserInfoEndpoint = true;

			options.Events = new OpenIdConnectEvents
			{
				OnRemoteFailure = enterprise.services.authentication.duende.CustomHandlers.HandleCancelAction,
				OnTokenResponseReceived = enterprise.services.authentication.duende.CustomHandlers.CopyAllowedScopesToUserClaims,
				OnTokenValidated = context =>
				{
					var uniqueName = context.Principal.Claims.Where(c => c.Type == ClaimTypes.Upn).First().Value; //User principal name is a username and domain in email address format within Microsoft AD
					if (uniqueName != null) //if ClaimTypes.Name exists, then remove and only have 1 as upn value
					{
						ClaimsIdentity identity = context.Principal.Identity as ClaimsIdentity;
						foreach (var oClaim in context.Principal.Claims.Where(c => c.Type == ClaimTypes.Name || c.Type.ToLower() == "name").OrderByDescending(x => x.Type))
						{
							identity.RemoveClaim(oClaim);
						}
						identity.AddClaim(new Claim("name", uniqueName)); //moving claimtypes.upn to name

						//create new claim to hold filtered name for es login
						var filterUniqueName = uniqueName;
						if (filterUniqueName.Contains("@exo."))
							filterUniqueName = filterUniqueName.Replace("@exo.", "@");
						identity.AddClaim(new Claim("esusername", filterUniqueName, ClaimValueTypes.String));
						Serilog.Log.Information("Cass Entra Issued esusername claim with uniqueName: {filterUniqueName}", filterUniqueName);
					}
					return Task.CompletedTask;
				}
			};
		}) */
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
