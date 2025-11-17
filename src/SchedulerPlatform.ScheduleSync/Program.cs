using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SchedulerPlatform.Infrastructure.Data;
using SchedulerPlatform.ScheduleSync.Services;

namespace SchedulerPlatform.ScheduleSync;

class Program
{
    static async Task<int> Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            Console.WriteLine($"\n!!! UNHANDLED EXCEPTION !!!");
            Console.WriteLine($"Exception: {e.ExceptionObject}");
            if (e.ExceptionObject is Exception ex)
            {
                Console.WriteLine($"Type: {ex.GetType().FullName}");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Console.WriteLine($"\n!!! UNOBSERVED TASK EXCEPTION !!!");
            Console.WriteLine($"Exception: {e.Exception}");
            Console.WriteLine($"Message: {e.Exception.Message}");
            Console.WriteLine($"Stack Trace: {e.Exception.StackTrace}");
            e.SetObserved();
        };

        try
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedule Sync Process Starting");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Current Directory: {Directory.GetCurrentDirectory()}");
            
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Looking for config at: {configPath}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Config file exists: {File.Exists(configPath)}");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Configuration loaded successfully");

            string connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new Exception("Connection string not found");
            
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Connection string found: {connectionString.Substring(0, Math.Min(50, connectionString.Length))}...");

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Reading API configuration...");
            
            string apiBaseUrl = configuration.GetValue<string>("ApiSettings:BaseUrl")
                ?? throw new Exception("API BaseUrl not found");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] API BaseUrl: {apiBaseUrl}");
            
            string apiKey = configuration.GetValue<string>("ApiSettings:ApiKey")
                ?? throw new Exception("API ApiKey not found");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] API Key: {(string.IsNullOrEmpty(apiKey) ? "EMPTY" : apiKey.Substring(0, Math.Min(10, apiKey.Length)) + "...")}");
            
            int apiBatchSize = configuration.GetValue<int>("ApiSettings:BatchSize", 2500);
            int delayBetweenRequestsMs = configuration.GetValue<int>("ApiSettings:DelayBetweenRequestsMs", 100);
            bool includeOnlyTandemAccounts = configuration.GetValue<bool>("ApiSettings:IncludeOnlyTandemAccounts", false);
            
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Batch Size: {apiBatchSize}, Delay: {delayBetweenRequestsMs}ms, Tandem Only: {includeOnlyTandemAccounts}");

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Reading sync settings...");
            
            int saveBatchSize = configuration.GetValue<int>("SyncSettings:SaveBatchSize", 5000);
            string defaultTimeZone = configuration.GetValue<string>("SyncSettings:DefaultTimeZone", "Central Standard Time")
                ?? "Central Standard Time";
            int scheduleGenerationBatchSize = configuration.GetValue<int>("SyncSettings:ScheduleGenerationBatchSize", 1000);
            
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Save Batch: {saveBatchSize}, Time Zone: {defaultTimeZone}, Schedule Batch: {scheduleGenerationBatchSize}");

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Creating database context...");
            var optionsBuilder = new DbContextOptionsBuilder<SchedulerDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Initializing database context...");
            await using var dbContext = new SchedulerDbContext(optionsBuilder.Options);
            
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Database context created successfully");

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] STEP 1: Syncing accounts from API");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");

            var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var apiClient = new AccountsApiClient(httpClient, apiBaseUrl, apiKey, apiBatchSize, delayBetweenRequestsMs);
            var syncService = new SyncService(dbContext, apiClient, saveBatchSize);

            var syncResult = await syncService.RunSyncAsync(includeOnlyTandemAccounts);

            if (!syncResult.Success)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] API sync failed. Aborting schedule generation.");
                return 1;
            }

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] STEP 2: Generating schedules from synced data");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");

            var scheduleService = new ScheduleGenerationService(dbContext, scheduleGenerationBatchSize, defaultTimeZone);
            var scheduleResult = await scheduleService.GenerateSchedulesAsync(syncResult.StartTime);

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] PROCESS COMPLETED SUCCESSFULLY");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] API Sync Results:");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   - Duration: {(syncResult.EndTime - syncResult.StartTime).TotalMinutes:F2} minutes");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   - Processed: {syncResult.ProcessedCount} / {syncResult.ExpectedTotal}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   - Added: {syncResult.Added}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   - Updated: {syncResult.Updated}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   - Reactivated: {syncResult.Reactivated}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   - Deleted: {syncResult.Deleted}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedule Generation Results:");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   - Duration: {(scheduleResult.EndTime - scheduleResult.StartTime).TotalMinutes:F2} minutes");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   - Created: {scheduleResult.Created}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   - Updated: {scheduleResult.Updated}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   - Errors: {scheduleResult.Errors}");

            if (syncResult.Warnings.Any())
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Warnings:");
                foreach (var warning in syncResult.Warnings)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   - {warning}");
                }
            }

            return (scheduleResult.Errors > 0) ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FATAL ERROR");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Exception Type: {ex.GetType().FullName}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Message: {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stack Trace:\n{ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Inner Exception Type: {ex.InnerException.GetType().FullName}");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Inner Message: {ex.InnerException.Message}");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Inner Stack Trace:\n{ex.InnerException.StackTrace}");
            }
            
            Console.WriteLine($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Press any key to exit...");
            Console.ReadKey();
            return 1;
        }
        finally
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Debugger attached - Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
