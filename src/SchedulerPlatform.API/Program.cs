using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using SchedulerPlatform.API.Configuration;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;
using SchedulerPlatform.Infrastructure.Interceptors;
using SchedulerPlatform.Infrastructure.Repositories;
using SchedulerPlatform.Infrastructure.Services;
using SchedulerPlatform.Jobs.Quartz;
using SchedulerPlatform.Jobs.Services;
using Serilog;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Microsoft.Extensions.Azure;
using Microsoft.OpenApi;

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

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

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

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value!.Errors.Select(e => new
                {
                    Field = kvp.Key,
                    ErrorMessage = e.ErrorMessage,
                    ExceptionMessage = e.Exception?.Message,
                    ExceptionType = e.Exception?.GetType().Name
                }))
                .ToList();
            
            logger.LogError("ModelState validation failed for {ActionName}. Error count: {ErrorCount}. Errors: {@ValidationErrors}", 
                context.ActionDescriptor.DisplayName, errors.Count, errors);
            
            return new BadRequestObjectResult(context.ModelState);
        };
    });

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "ADR Scheduler API",
		Version = "v1",
		Description = "API for managing scheduled jobs and processes"
	});

	// Include XML comments for API documentation in Swagger
	var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
	var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
	c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

	var authority = builder.Configuration["Authentication:Authority"] ?? "https://localhost:5001";

	c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
	{
		Type = SecuritySchemeType.OAuth2,
		In = ParameterLocation.Header,
		Name = "oauth2",
		Flows = new OpenApiOAuthFlows
		{
			AuthorizationCode = new OpenApiOAuthFlow
			{
				AuthorizationUrl = new Uri($"{authority}/connect/authorize"),
				TokenUrl = new Uri($"{authority}/connect/token"),
				Scopes = new Dictionary<string, string>
				{
					{ "openid", "OpenID" },
					{ "profile", "Profile" },
					{ "email", "Email" },
					{ "scheduler-api", "Scheduler API Access" },
					{ "role", "User Role" },
					{ "permissions", "User Permissions" }
				}
			},
			ClientCredentials = new OpenApiOAuthFlow
			{
				TokenUrl = new Uri($"{authority}/connect/token"),
				Scopes = new Dictionary<string, string>
				{
					{ "scheduler-api", "Scheduler API Access" }
				}
			}
		}
	});

	c.AddSecurityRequirement(document =>
	{
		var requirements = new OpenApiSecurityRequirement();
		var schemeRef = new OpenApiSecuritySchemeReference("oauth2", document);
		requirements.Add(schemeRef, new List<string> { "scheduler-api" });

		return requirements;
	});
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, CurrentActorService>();
builder.Services.AddScoped<AuditLogInterceptor>();

builder.Services.AddDbContext<SchedulerDbContext>((serviceProvider, options) =>
{
    var auditLogInterceptor = serviceProvider.GetRequiredService<AuditLogInterceptor>();
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("SchedulerPlatform.Infrastructure");
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        })
    .AddInterceptors(auditLogInterceptor);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool>("Authentication:RequireHttpsMetadata");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "name"
        };
    })
    .AddScheme<SchedulerPlatform.API.Authorization.SchedulerApiKeyAuthenticationOptions, 
               SchedulerPlatform.API.Authorization.SchedulerApiKeyAuthenticationHandler>(
        SchedulerPlatform.API.Authorization.SchedulerApiKeyAuthenticationOptions.DefaultScheme, 
        options => { });

builder.Services.AddSingleton<IAuthorizationHandler, SchedulerPlatform.API.Authorization.PermissionAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ClientAccess", policy => policy.RequireAuthenticatedUser());
    
    options.AddPolicy("Scheduler.Access", policy => 
        policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("scheduler:read")));
    options.AddPolicy("Schedules.Read", policy => 
        policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("schedules:read")));
    options.AddPolicy("Schedules.Create", policy => 
        policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("schedules:create")));
    options.AddPolicy("Schedules.Update", policy => 
        policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("schedules:update")));
    options.AddPolicy("Schedules.Delete", policy => 
        policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("schedules:delete")));
    options.AddPolicy("Schedules.Execute", policy => 
        policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("schedules:execute")));
    options.AddPolicy("Jobs.Read", policy => 
        policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("jobs:read")));
    options.AddPolicy("Users.Manage.Read", policy => 
        policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("users:manage:read")));
    options.AddPolicy("Users.Manage.Update", policy => 
        policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("users:manage:update")));
    options.AddPolicy("Users.Manage.Create", policy => 
        policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("users:manage:create")));
    options.AddPolicy("Users.Manage.Delete", policy => 
        policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("users:manage:delete")));
    
        // ADR Account policies
        options.AddPolicy("AdrAccounts.Update", policy => 
            policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("adr:update")));
        options.AddPolicy("AdrAccounts.Execute", policy => 
            policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("adr:execute")));
});

builder.Services.AddAzureClients(builder =>
{
    builder.UseCredential(new DefaultAzureCredential());
});

builder.Services.Configure<SchedulerSettings>(builder.Configuration.GetSection("SchedulerSettings"));

builder.Services.AddSingleton(new SchedulerPlatform.API.Configuration.AppLifetimeInfo { StartUtc = DateTime.UtcNow });

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IJobExecutionRepository, JobExecutionRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<ISchedulerService, SchedulerService>();
builder.Services.AddScoped<IEmailService, SchedulerPlatform.Infrastructure.Services.EmailService>();

builder.Services.AddScoped<SchedulerPlatform.API.Services.IAdrAccountSyncService, SchedulerPlatform.API.Services.AdrAccountSyncService>();
builder.Services.AddScoped<SchedulerPlatform.API.Services.IAdrOrchestratorService, SchedulerPlatform.API.Services.AdrOrchestratorService>();

// ADR Background Orchestration - runs independently of user sessions
builder.Services.AddSingleton<SchedulerPlatform.API.Services.IAdrOrchestrationQueue, SchedulerPlatform.API.Services.AdrOrchestrationQueue>();
builder.Services.AddHostedService<SchedulerPlatform.API.Services.AdrBackgroundOrchestrationService>();

builder.Services.AddHostedService<SchedulerPlatform.API.Services.StartupRecoveryService>();
builder.Services.AddHostedService<SchedulerPlatform.API.Services.ScheduleHydrationService>();
builder.Services.AddHostedService<SchedulerPlatform.API.Services.MissedSchedulesProcessor>();
builder.Services.AddHostedService<SchedulerPlatform.API.Services.DataArchivalService>();

builder.Services.AddHttpClient("ApiCallJob");
builder.Services.AddHttpClient("AdrApi");

builder.Services.AddQuartzJobServices(builder.Configuration.GetConnectionString("DefaultConnection")!);

builder.Services.AddSingleton<IScheduler>(sp =>
{
    var schedulerFactory = sp.GetRequiredService<ISchedulerFactory>();
    return schedulerFactory.GetScheduler().GetAwaiter().GetResult();
});

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

if (app.Environment.IsDevelopment() || 
    app.Environment.IsEnvironment("AzureDev") ||
    app.Environment.IsEnvironment("UAT") || 
    app.Environment.IsEnvironment("Staging"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ADR Scheduler API v1");
        c.OAuthClientId("swagger-ui");
        c.OAuthUsePkce();
        c.OAuthScopes("openid", "profile", "email", "scheduler-api", "role", "permissions");
    });
}

app.UseSerilogRequestLogging();

// Request body logging middleware - ONLY in Development to prevent logging sensitive data
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Method == "POST" && context.Request.Path.StartsWithSegments("/api/schedules"))
        {
            context.Request.EnableBuffering();
            
            using var reader = new StreamReader(
                context.Request.Body,
                encoding: System.Text.Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
            
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("POST /api/schedules Request Body: {RequestBody}", body);
        }
        
        await next();
    });
}



app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("Content-Security-Policy", 
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self'; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none';");
        
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        
        context.Response.Headers.Append("Permissions-Policy", 
            "geolocation=(), microphone=(), camera=()");
        
        await next();
    });
}

app.UseCors("AllowUI");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<SchedulerPlatform.API.Middleware.AutoUserCreationMiddleware>();

app.MapControllers();

try
{
    Log.Information("Starting ADR Scheduler API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
