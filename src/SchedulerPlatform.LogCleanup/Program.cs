using System;
using System.IO;
using System.Linq;

namespace SchedulerPlatform.LogCleanup;

class Program
{
    static int Main(string[] args)
    {
        try
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting log cleanup process...");

            int retentionDays = 7;
            if (args.Length > 0 && int.TryParse(args[0], out int customRetention))
            {
                retentionDays = customRetention;
            }

            DateTime cutoffDate = DateTime.Now.AddDays(-retentionDays);
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Deleting log files older than {cutoffDate:yyyy-MM-dd}");

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string solutionRoot = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", ".."));

            string[] logDirectories = new[]
            {
                Path.Combine(solutionRoot, "SchedulerPlatform.API", "logs"),
                Path.Combine(solutionRoot, "SchedulerPlatform.IdentityServer", "Logs")
            };

            int totalDeleted = 0;
            long totalBytesFreed = 0;

            foreach (string logDir in logDirectories)
            {
                if (!Directory.Exists(logDir))
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Warning: Directory not found: {logDir}");
                    continue;
                }

                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Scanning directory: {logDir}");

                var oldFiles = Directory.GetFiles(logDir, "*.txt")
                    .Select(f => new FileInfo(f))
                    .Where(fi => fi.LastWriteTime < cutoffDate)
                    .ToList();

                foreach (var file in oldFiles)
                {
                    try
                    {
                        long fileSize = file.Length;
                        file.Delete();
                        totalDeleted++;
                        totalBytesFreed += fileSize;
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Deleted: {file.Name} ({fileSize:N0} bytes)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error deleting {file.Name}: {ex.Message}");
                    }
                }
            }

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Log cleanup completed successfully");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Total files deleted: {totalDeleted}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Total space freed: {totalBytesFreed / (1024.0 * 1024.0):N2} MB");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Fatal error: {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stack trace: {ex.StackTrace}");
            return 1;
        }
    }
}
