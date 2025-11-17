using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SchedulerPlatform.Infrastructure.Data;
using SchedulerPlatform.ScheduleSync.Services;

namespace SchedulerPlatform.ScheduleSync;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Schedule Sync Process Starting");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            string connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new Exception("Connection string not found");

            string apiBaseUrl = configuration.GetValue<string>("ApiSettings:BaseUrl")
                ?? throw new Exception("API BaseUrl not found");
            string apiKey = configuration.GetValue<string>("ApiSettings:ApiKey")
                ?? throw new Exception("API ApiKey not found");
            int apiBatchSize = configuration.GetValue<int>("ApiSettings:BatchSize", 2500);
            int delayBetweenRequestsMs = configuration.GetValue<int>("ApiSettings:DelayBetweenRequestsMs", 100);
            bool includeOnlyTandemAccounts = configuration.GetValue<bool>("ApiSettings:IncludeOnlyTandemAccounts", false);

            int saveBatchSize = configuration.GetValue<int>("SyncSettings:SaveBatchSize", 5000);
            string defaultTimeZone = configuration.GetValue<string>("SyncSettings:DefaultTimeZone", "Central Standard Time")
                ?? "Central Standard Time";
            int scheduleGenerationBatchSize = configuration.GetValue<int>("SyncSettings:ScheduleGenerationBatchSize", 1000);

            var optionsBuilder = new DbContextOptionsBuilder<SchedulerDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            await using var dbContext = new SchedulerDbContext(optionsBuilder.Options);

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
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FATAL ERROR");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error: {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stack trace: {ex.StackTrace}");
            return 1;
        }
    }
}
