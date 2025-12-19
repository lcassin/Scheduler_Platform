using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using SchedulerPlatform.Jobs.Jobs;

namespace SchedulerPlatform.Jobs.Quartz;

public static class SchedulerExtensions
{
    public static IServiceCollection AddQuartzJobServices(this IServiceCollection services, string connectionString)
    {
        services.AddTransient<ProcessJob>();
        services.AddTransient<StoredProcedureJob>();
        services.AddTransient<ApiCallJob>();
        services.AddTransient<MaintenanceJob>();
        
        Console.WriteLine($"[Quartz] Starting Quartz configuration. Connection string provided: {!string.IsNullOrEmpty(connectionString)}");
        
        var _ = typeof(SqlConnection);
        Console.WriteLine("[Quartz] Microsoft.Data.SqlClient assembly loaded successfully");
        
        try
        {
            Console.WriteLine("[Quartz] Calling AddQuartz()...");
            services.AddQuartz(q =>
            {
                Console.WriteLine("[Quartz] Inside AddQuartz configuration lambda");
                q.UsePersistentStore(options =>
                {
                    Console.WriteLine("[Quartz] Configuring persistent store...");
                    options.UseProperties = true;
                    options.RetryInterval = TimeSpan.FromSeconds(15);
                    
                    Console.WriteLine("[Quartz] Calling UseSqlServer()...");
                    options.UseSqlServer(sqlServerOptions =>
                    {
                        sqlServerOptions.ConnectionString = connectionString;
                        sqlServerOptions.TablePrefix = "QRTZ_";
                        Console.WriteLine("[Quartz] SQL Server configuration complete");
                    });
                    
                    options.UseJsonSerializer();
                    Console.WriteLine("[Quartz] Persistent store configuration complete");
                });
                
                q.UseDefaultThreadPool(tp =>
                {
                    tp.MaxConcurrency = 10;
                });
                
                Console.WriteLine("[Quartz] AddQuartz configuration lambda complete");
            });
            Console.WriteLine("[Quartz] AddQuartz() returned successfully");
            
            Console.WriteLine("[Quartz] Calling AddQuartzHostedService()...");
            services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
                options.StartDelay = TimeSpan.FromSeconds(5);
            });
            Console.WriteLine("[Quartz] AddQuartzHostedService() returned successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Quartz] ERROR during Quartz configuration: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[Quartz] Stack trace: {ex.StackTrace}");
            throw new InvalidOperationException(
                $"Failed to configure Quartz scheduler. Connection string provided: {!string.IsNullOrEmpty(connectionString)}. " +
                $"Error: {ex.Message}", ex);
        }
        
        Console.WriteLine("[Quartz] Quartz configuration complete, returning services");
        return services;
    }
}
