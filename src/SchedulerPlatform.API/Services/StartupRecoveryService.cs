using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Services
{
    public class StartupRecoveryService : IHostedService
    {
        private readonly ILogger<StartupRecoveryService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public StartupRecoveryService(ILogger<StartupRecoveryService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var stuckExecutions = await unitOfWork.JobExecutions.FindAsync(
                    e => e.Status == JobStatus.Running && e.EndTime == null);

                if (stuckExecutions.Any())
                {
                    var now = DateTime.UtcNow;
                    int updated = 0;

                    foreach (var exec in stuckExecutions)
                    {
                        exec.Status = JobStatus.Failed;
                        exec.EndTime = now;
                        exec.DurationSeconds = (int)Math.Max(0, (now - exec.StartTime).TotalSeconds);
                        exec.ErrorMessage = "Application restarted while job was running; marked as Failed by startup recovery.";

                        await unitOfWork.JobExecutions.UpdateAsync(exec);
                        updated++;
                    }

                    await unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("StartupRecoveryService: cleaned up {Updated} stuck job execution(s) that were Running when application last stopped",
                        updated);
                }
                else
                {
                    _logger.LogInformation("StartupRecoveryService: no stuck Running executions found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartupRecoveryService encountered an error during recovery");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
