using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Quartz;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;
using SchedulerPlatform.Infrastructure.Interceptors;
using SchedulerPlatform.Infrastructure.Repositories;
using SchedulerPlatform.Infrastructure.Services;
using SchedulerPlatform.Jobs.Quartz;
using SchedulerPlatform.Jobs.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/scheduler-api-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

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
        Title = "Scheduler Platform API",
        Version = "v1",
        Description = "API for managing scheduled jobs and processes"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
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
        sqlOptions => sqlOptions.MigrationsAssembly("SchedulerPlatform.Infrastructure"))
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
    });

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
    options.AddPolicy("Users.Manage", policy => 
        policy.Requirements.Add(new SchedulerPlatform.API.Authorization.PermissionRequirement("users:manage")));
});

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IJobExecutionRepository, JobExecutionRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<ISchedulerService, SchedulerService>();
builder.Services.AddScoped<IEmailService, SchedulerPlatform.Infrastructure.Services.EmailService>();

builder.Services.AddHostedService<SchedulerPlatform.API.Services.StartupRecoveryService>();
builder.Services.AddHostedService<SchedulerPlatform.API.Services.ScheduleHydrationService>();

builder.Services.AddHttpClient("ApiCallJob");

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
    app.Environment.IsEnvironment("UAT") || 
    app.Environment.IsEnvironment("Staging"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Scheduler Platform API v1");
    });
}

app.UseSerilogRequestLogging();
// Request body logging middleware for debugging 400 errors
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
        logger.LogInformation("POST /api/schedules Request Body: {RequestBody}", body);
    }
    
    await next();
});



app.UseHttpsRedirection();

app.UseCors("AllowUI");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Starting Scheduler Platform API");
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
