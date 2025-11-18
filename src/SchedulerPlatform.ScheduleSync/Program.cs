using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SchedulerPlatform.Infrastructure.Data;
using SchedulerPlatform.ScheduleSync.Services;

namespace SchedulerPlatform.ScheduleSync;

class Program
{
    enum StartStep
    {
        Accounts = 1,
        Clients = 2,
        Schedules = 3
    }

    static async Task<int> Main(string[] args)
    {
        FileLogger? logger = null;
        EmailService? emailService = null;
        var processStartTime = DateTime.UtcNow;

        try
        {
            if (args.Any(a => a.Equals("--stagger-existing", StringComparison.OrdinalIgnoreCase)))
            {
                return await RunStaggerExistingAsync(args);
            }

            var startStep = ParseStartStep(args);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            string logDirectory = configuration.GetValue<string>("Logging:Directory", "./logs") ?? "./logs";
            string logFilePrefix = configuration.GetValue<string>("Logging:FilePrefix", "SchedulerSync") ?? "SchedulerSync";
            logger = new FileLogger(logDirectory, logFilePrefix, logToConsole: true);

            logger.LogInfo("========================================");
            logger.LogInfo("Schedule Sync Process Starting");
            logger.LogInfo("========================================");
            logger.LogInfo($"Start Step: {startStep}");

            bool emailEnabled = configuration.GetValue<bool>("Notifications:Enabled", true);
            string smtpHost = configuration.GetValue<string>("Notifications:Smtp:Host", "smtp.cassinfo.com") ?? "smtp.cassinfo.com";
            int smtpPort = configuration.GetValue<int>("Notifications:Smtp:Port", 25);
            bool smtpEnableSsl = configuration.GetValue<bool>("Notifications:Smtp:EnableSsl", false);
            string emailFrom = configuration.GetValue<string>("Notifications:From", "scheduler@cassinfo.com") ?? "scheduler@cassinfo.com";
            string emailTo = configuration.GetValue<string>("Notifications:To", "lcassin@cassinfo.com") ?? "lcassin@cassinfo.com";

            emailService = new EmailService(smtpHost, smtpPort, smtpEnableSsl, emailFrom, emailTo, emailEnabled);

            string connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new Exception("Connection string not found");
            logger.LogInfo($"Connection string found: {connectionString.Substring(0, Math.Min(50, connectionString.Length))}...");

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

            var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var apiClient = new AccountsApiClient(httpClient, apiBaseUrl, apiKey, apiBatchSize, delayBetweenRequestsMs);
            var syncService = new SyncService(dbContext, apiClient, saveBatchSize);
            var scheduleService = new ScheduleGenerationService(dbContext, scheduleGenerationBatchSize, defaultTimeZone);

            DateTime runStart;
            if (startStep == StartStep.Accounts)
            {
                runStart = DateTime.UtcNow;
                logger.LogInfo($"Run timestamp: {runStart:yyyy-MM-dd HH:mm:ss} UTC");
            }
            else
            {
                var lastSyncedAt = await dbContext.ScheduleSyncSources
                    .Where(s => s.LastSyncedAt != null)
                    .MaxAsync(s => (DateTime?)s.LastSyncedAt);

                if (lastSyncedAt.HasValue)
                {
                    runStart = lastSyncedAt.Value;
                    logger.LogInfo($"Resumed run - using last sync timestamp: {runStart:yyyy-MM-dd HH:mm:ss} UTC");
                }
                else
                {
                    runStart = DateTime.UtcNow;
                    logger.LogWarning($"No previous sync found - using current timestamp: {runStart:yyyy-MM-dd HH:mm:ss} UTC");
                }
            }

            SyncResult? syncResult = null;
            ClientSyncResult? clientSyncResult = null;
            ScheduleGenerationResult? scheduleResult = null;
            var warnings = new List<string>();

            if (startStep <= StartStep.Accounts)
            {
                logger.LogInfo("");
                logger.LogInfo("STEP 1: Syncing accounts from API");
                logger.LogInfo("========================================");

                syncResult = await syncService.RunSyncAsync(includeOnlyTandemAccounts);
                clientSyncResult = syncResult.ClientSyncResult;
                warnings.AddRange(syncResult.Warnings);

                if (!syncResult.Success)
                {
                    logger.LogError("API sync failed. Aborting remaining steps.");
                    await SendErrorEmail(emailService, "Accounts Sync", syncResult.ErrorMessage ?? "Unknown error", processStartTime, logger.LogFilePath);
                    return 1;
                }
            }

            if (startStep == StartStep.Clients)
            {
                logger.LogInfo("");
                logger.LogInfo("STEP 2: Syncing clients from database");
                logger.LogInfo("========================================");

                clientSyncResult = await syncService.SyncClientsFromDatabaseAsync(runStart, performSoftDelete: true);

                if (!clientSyncResult.Success)
                {
                    logger.LogError("Client sync failed. Aborting remaining steps.");
                    await SendErrorEmail(emailService, "Client Sync", clientSyncResult.ErrorMessage ?? "Unknown error", processStartTime, logger.LogFilePath);
                    return 1;
                }
            }

            if (startStep <= StartStep.Schedules)
            {
                logger.LogInfo("");
                logger.LogInfo($"STEP {(startStep == StartStep.Schedules ? "1" : startStep == StartStep.Clients ? "2" : "3")}: Generating schedules from synced data");
                logger.LogInfo("========================================");

                scheduleResult = await scheduleService.GenerateSchedulesAsync(runStart);

                if (!scheduleResult.Success)
                {
                    logger.LogError("Schedule generation failed.");
                    await SendErrorEmail(emailService, "Schedule Generation", scheduleResult.ErrorMessage ?? "Unknown error", processStartTime, logger.LogFilePath);
                    return 1;
                }
            }

            var processEndTime = DateTime.UtcNow;
            logger.LogInfo("");
            logger.LogInfo("========================================");
            logger.LogInfo("PROCESS COMPLETED SUCCESSFULLY");
            logger.LogInfo("========================================");
            logger.LogInfo("");
            logger.LogInfo($"Total Duration: {(processEndTime - processStartTime).TotalMinutes:F2} minutes");

            if (syncResult != null)
            {
                logger.LogInfo("");
                logger.LogInfo("Accounts Sync Results:");
                logger.LogInfo($"  Duration: {(syncResult.EndTime - syncResult.StartTime).TotalMinutes:F2} minutes");
                logger.LogInfo($"  Processed: {syncResult.ProcessedCount} / {syncResult.ExpectedTotal}");
                logger.LogInfo($"  Added: {syncResult.Added}");
                logger.LogInfo($"  Updated: {syncResult.Updated}");
                logger.LogInfo($"  Reactivated: {syncResult.Reactivated}");
                logger.LogInfo($"  Deleted: {syncResult.Deleted}");
            }

            if (clientSyncResult != null)
            {
                logger.LogInfo("");
                logger.LogInfo("Client Sync Results:");
                logger.LogInfo($"  Duration: {(clientSyncResult.EndTime - clientSyncResult.StartTime).TotalSeconds:F1} seconds");
                logger.LogInfo($"  Added: {clientSyncResult.Added}");
                logger.LogInfo($"  Updated: {clientSyncResult.Updated}");
                logger.LogInfo($"  Reactivated: {clientSyncResult.Reactivated}");
                logger.LogInfo($"  Deleted: {clientSyncResult.Deleted}");
            }

            if (scheduleResult != null)
            {
                logger.LogInfo("");
                logger.LogInfo("Schedule Generation Results:");
                logger.LogInfo($"  Duration: {(scheduleResult.EndTime - scheduleResult.StartTime).TotalMinutes:F2} minutes");
                logger.LogInfo($"  Created: {scheduleResult.Created}");
                logger.LogInfo($"  Updated: {scheduleResult.Updated}");
                logger.LogInfo($"  Errors: {scheduleResult.Errors}");
            }

            if (warnings.Any())
            {
                logger.LogInfo("");
                logger.LogInfo("Warnings:");
                foreach (var warning in warnings)
                {
                    logger.LogWarning($"  - {warning}");
                }
            }

            var emailSubject = $"[SUCCESS] ScheduleSync - {(processEndTime - processStartTime).TotalMinutes:F0}min";
            var emailBody = EmailService.FormatSuccessEmail(
                processStartTime,
                processEndTime,
                syncResult,
                clientSyncResult,
                scheduleResult,
                warnings);

            await emailService.SendSuccessEmailAsync(emailSubject, emailBody, logger.LogFilePath);

            return (scheduleResult?.Errors ?? 0) > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            var errorTime = DateTime.UtcNow;
            
            if (logger != null)
            {
                logger.LogError("");
                logger.LogError("========================================");
                logger.LogError("FATAL ERROR");
                logger.LogError("========================================");
                logger.LogError("", ex);
            }
            else
            {
                Console.WriteLine($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FATAL ERROR");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Exception Type: {ex.GetType().FullName}");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Message: {ex.Message}");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stack Trace:\n{ex.StackTrace}");
            }

            if (emailService != null)
            {
                await SendErrorEmail(emailService, "Fatal Error", ex.Message, processStartTime, logger?.LogFilePath);
            }

            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Debugger attached - Press any key to exit...");
                Console.ReadKey();
            }

            return 1;
        }
        finally
        {
            logger?.Dispose();
        }
    }

    private static StartStep ParseStartStep(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith("--start-from=", StringComparison.OrdinalIgnoreCase))
            {
                var value = arg.Substring("--start-from=".Length).ToLower();
                return value switch
                {
                    "accounts" => StartStep.Accounts,
                    "clients" => StartStep.Clients,
                    "schedules" => StartStep.Schedules,
                    _ => throw new ArgumentException($"Invalid --start-from value: {value}. Valid values: accounts, clients, schedules")
                };
            }
        }

        if (!Console.IsInputRedirected && args.Length == 0)
        {
            Console.WriteLine();
            Console.WriteLine("Select which step to start from:");
            Console.WriteLine("1. All steps (Accounts → Clients → Schedules)");
            Console.WriteLine("2. Client sync and Schedules (skip Accounts)");
            Console.WriteLine("3. Schedule generation only (skip Accounts and Clients)");
            Console.Write("Enter your choice (1-3): ");

            var input = Console.ReadLine();
            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= 3)
            {
                Console.WriteLine();
                return (StartStep)choice;
            }

            Console.WriteLine("Invalid choice, defaulting to all steps.");
            Console.WriteLine();
        }

        return StartStep.Accounts;
    }

    private static async Task<int> RunStaggerExistingAsync(string[] args)
    {
        FileLogger? logger = null;
        EmailService? emailService = null;
        var processStartTime = DateTime.UtcNow;

        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            string logDirectory = configuration.GetValue<string>("Logging:Directory", "./logs") ?? "./logs";
            string logFilePrefix = configuration.GetValue<string>("Logging:FilePrefix", "SchedulerSync") ?? "SchedulerSync";
            logger = new FileLogger(logDirectory, logFilePrefix, logToConsole: true);

            logger.LogInfo("========================================");
            logger.LogInfo("Stagger Existing Schedules");
            logger.LogInfo("========================================");

            bool emailEnabled = configuration.GetValue<bool>("Notifications:Enabled", true);
            string smtpHost = configuration.GetValue<string>("Notifications:Smtp:Host", "smtp.cassinfo.com") ?? "smtp.cassinfo.com";
            int smtpPort = configuration.GetValue<int>("Notifications:Smtp:Port", 25);
            bool smtpEnableSsl = configuration.GetValue<bool>("Notifications:Smtp:EnableSsl", false);
            string emailFrom = configuration.GetValue<string>("Notifications:From", "scheduler@cassinfo.com") ?? "scheduler@cassinfo.com";
            string emailTo = configuration.GetValue<string>("Notifications:To", "lcassin@cassinfo.com") ?? "lcassin@cassinfo.com";

            emailService = new EmailService(smtpHost, smtpPort, smtpEnableSsl, emailFrom, emailTo, emailEnabled);

            string connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new Exception("Connection string not found");

            int batchSize = configuration.GetValue<int>("SyncSettings:StaggerBatchSize", 10000);
            int startHour = configuration.GetValue<int>("SyncSettings:StaggerStartHour", 4);
            int endHour = configuration.GetValue<int>("SyncSettings:StaggerEndHour", 24);

            var optionsBuilder = new DbContextOptionsBuilder<SchedulerDbContext>();
            optionsBuilder.UseSqlServer(connectionString);
            await using var dbContext = new SchedulerDbContext(optionsBuilder.Options);

            var staggerService = new ScheduleStaggerService(dbContext, batchSize);

            logger.LogInfo($"Batch size: {batchSize:N0}");
            logger.LogInfo($"Time window: {startHour:D2}:00 - {(endHour == 24 ? "midnight" : $"{endHour:D2}:00")}");
            logger.LogInfo("");

            var result = await staggerService.StaggerExistingSchedulesAsync(startHour, endHour);

            var processEndTime = DateTime.UtcNow;
            logger.LogInfo("");
            logger.LogInfo("========================================");
            logger.LogInfo(result.Success ? "STAGGER COMPLETED SUCCESSFULLY" : "STAGGER COMPLETED WITH ERRORS");
            logger.LogInfo("========================================");
            logger.LogInfo($"Total Duration: {(processEndTime - processStartTime).TotalMinutes:F2} minutes");
            logger.LogInfo($"Schedules updated: {result.Updated:N0}");
            logger.LogInfo($"Schedules unchanged: {result.Unchanged:N0}");
            logger.LogInfo($"Errors: {result.Errors}");

            var emailSubject = result.Success
                ? $"[SUCCESS] ScheduleSync Stagger - {(processEndTime - processStartTime).TotalMinutes:F0}min"
                : $"[ERROR] ScheduleSync Stagger - {result.Errors} errors";

            var emailBody = $"Stagger Existing Schedules Operation\n" +
                           $"====================================\n\n" +
                           $"Start Time: {processStartTime:yyyy-MM-dd HH:mm:ss} UTC\n" +
                           $"End Time: {processEndTime:yyyy-MM-dd HH:mm:ss} UTC\n" +
                           $"Duration: {(processEndTime - processStartTime).TotalMinutes:F2} minutes\n\n" +
                           $"Results:\n" +
                           $"  Schedules updated: {result.Updated:N0}\n" +
                           $"  Schedules unchanged: {result.Unchanged:N0}\n" +
                           $"  Errors: {result.Errors}\n\n" +
                           $"Time window: {startHour:D2}:00 - {(endHour == 24 ? "midnight" : $"{endHour:D2}:00")}\n" +
                           $"Batch size: {batchSize:N0}\n\n" +
                           $"See attached log file for detailed information.";

            if (result.Success)
            {
                await emailService.SendSuccessEmailAsync(emailSubject, emailBody, logger.LogFilePath);
            }
            else
            {
                await emailService.SendErrorEmailAsync(emailSubject, emailBody, logger.LogFilePath);
            }

            return result.Errors > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            var errorTime = DateTime.UtcNow;

            if (logger != null)
            {
                logger.LogError("");
                logger.LogError("========================================");
                logger.LogError("FATAL ERROR");
                logger.LogError("========================================");
                logger.LogError("", ex);
            }
            else
            {
                Console.WriteLine($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FATAL ERROR");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ========================================");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Exception Type: {ex.GetType().FullName}");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Message: {ex.Message}");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stack Trace:\n{ex.StackTrace}");
            }

            if (emailService != null)
            {
                await SendErrorEmail(emailService, "Stagger Existing", ex.Message, processStartTime, logger?.LogFilePath);
            }

            return 1;
        }
        finally
        {
            logger?.Dispose();
        }
    }

    private static async Task SendErrorEmail(EmailService emailService, string phase, string errorMessage, DateTime startTime, string? logFilePath)
    {
        var errorTime = DateTime.UtcNow;
        var subject = $"[ERROR] ScheduleSync - {phase}";
        var body = $"ScheduleSync Process Failed\n" +
                   $"===========================\n\n" +
                   $"Failed Phase: {phase}\n" +
                   $"Start Time: {startTime:yyyy-MM-dd HH:mm:ss} UTC\n" +
                   $"Error Time: {errorTime:yyyy-MM-dd HH:mm:ss} UTC\n" +
                   $"Duration Before Failure: {(errorTime - startTime).TotalMinutes:F2} minutes\n\n" +
                   $"Error Message:\n{errorMessage}\n\n" +
                   $"See attached log file for detailed information.";

        await emailService.SendErrorEmailAsync(subject, body, logFilePath);
    }
}
