using Microsoft.Extensions.Logging;
using Quartz;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Jobs.Jobs;

namespace SchedulerPlatform.Jobs.Services;

public class SchedulerService : ISchedulerService
{
    private readonly IScheduler _scheduler;
    private readonly ILogger<SchedulerService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public SchedulerService(IScheduler scheduler, ILogger<SchedulerService> logger, IUnitOfWork unitOfWork)
    {
        _scheduler = scheduler;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task ScheduleJob(Schedule schedule)
    {
        Type jobType = schedule.JobType switch
        {
            JobType.Process => typeof(ProcessJob),
            JobType.StoredProcedure => typeof(StoredProcedureJob),
            JobType.ApiCall => typeof(ApiCallJob),
            _ => throw new ArgumentException($"Unsupported job type: {schedule.JobType}")
        };

        IJobDetail jobDetail = JobBuilder.Create(jobType)
            .WithIdentity($"Job_{schedule.Id}", $"Group_{schedule.ClientId}")
            .UsingJobData("ScheduleId", schedule.Id.ToString())
            .UsingJobData("TriggeredBy", "Scheduler")
            .StoreDurably()
            .Build();

        ITrigger trigger;
        if (schedule.NextRunTime.HasValue)
        {
            trigger = TriggerBuilder.Create()
                .WithIdentity($"Trigger_{schedule.Id}", $"Group_{schedule.ClientId}")
                .ForJob(jobDetail)
                .WithCronSchedule(schedule.CronExpression, x => x.WithMisfireHandlingInstructionFireAndProceed())
                .StartAt(schedule.NextRunTime.Value)
                .Build();
        }
        else
        {
            trigger = TriggerBuilder.Create()
                .WithIdentity($"Trigger_{schedule.Id}", $"Group_{schedule.ClientId}")
                .ForJob(jobDetail)
                .WithCronSchedule(schedule.CronExpression, x => x.WithMisfireHandlingInstructionFireAndProceed())
                .StartNow()
                .Build();
        }

        if (await _scheduler.CheckExists(jobDetail.Key))
        {
            _logger.LogInformation("Job already exists for schedule {ScheduleId}, updating", schedule.Id);
            await _scheduler.DeleteJob(jobDetail.Key);
        }

        await _scheduler.ScheduleJob(jobDetail, trigger);
        _logger.LogInformation("Scheduled job {JobKey} with trigger {TriggerKey} and cron expression {CronExpression}",
            jobDetail.Key, trigger.Key, schedule.CronExpression);
    }

    public async Task UnscheduleJob(int scheduleId, int clientId)
    {
        var jobKey = new JobKey($"Job_{scheduleId}", $"Group_{clientId}");
        var triggerKey = new TriggerKey($"Trigger_{scheduleId}", $"Group_{clientId}");

        if (await _scheduler.CheckExists(jobKey))
        {
            await _scheduler.PauseTrigger(triggerKey);
            await _scheduler.UnscheduleJob(triggerKey);
            await _scheduler.DeleteJob(jobKey);
            _logger.LogInformation("Unscheduled job {JobKey}", jobKey);
        }
        else
        {
            _logger.LogWarning("Job {JobKey} not found for unscheduling", jobKey);
        }
    }

    public async Task PauseJob(int scheduleId, int clientId)
    {
        var jobKey = new JobKey($"Job_{scheduleId}", $"Group_{clientId}");
        var triggerKey = new TriggerKey($"Trigger_{scheduleId}", $"Group_{clientId}");

        if (await _scheduler.CheckExists(jobKey))
        {
            await _scheduler.PauseTrigger(triggerKey);
            _logger.LogInformation("Paused job {JobKey}", jobKey);
        }
        else
        {
            _logger.LogWarning("Job {JobKey} not found for pausing", jobKey);
        }
    }

    public async Task ResumeJob(int scheduleId, int clientId)
    {
        var jobKey = new JobKey($"Job_{scheduleId}", $"Group_{clientId}");
        var triggerKey = new TriggerKey($"Trigger_{scheduleId}", $"Group_{clientId}");

        if (await _scheduler.CheckExists(jobKey))
        {
            await _scheduler.ResumeTrigger(triggerKey);
            _logger.LogInformation("Resumed job {JobKey}", jobKey);
        }
        else
        {
            _logger.LogWarning("Job {JobKey} not found for resuming", jobKey);
        }
    }

    public async Task TriggerJobNow(int scheduleId, int clientId, string triggeredBy)
    {
        var jobKey = new JobKey($"Job_{scheduleId}", $"Group_{clientId}");

        if (await _scheduler.CheckExists(jobKey))
        {
            var jobDataMap = new JobDataMap
            {
                { "ScheduleId", scheduleId.ToString() },
                { "TriggeredBy", triggeredBy }
            };

            await _scheduler.TriggerJob(jobKey, jobDataMap);
            _logger.LogInformation("Triggered job {JobKey} manually by {TriggeredBy}", jobKey, triggeredBy);
        }
        else
        {
            _logger.LogWarning("Job {JobKey} not found for manual triggering", jobKey);
        }
    }

    public async Task UpdateNextRunTimeAsync(int scheduleId, int clientId)
    {
        try
        {
            var triggerKey = new TriggerKey($"Trigger_{scheduleId}", $"Group_{clientId}");
            var trigger = await _scheduler.GetTrigger(triggerKey);

            if (trigger == null)
            {
                _logger.LogWarning("Trigger not found for schedule {ScheduleId}, cannot update NextRunTime", scheduleId);
                return;
            }

            var nextFireTime = trigger.GetNextFireTimeUtc();
            var schedule = await _unitOfWork.Schedules.GetByIdAsync(scheduleId);

            if (schedule == null)
            {
                _logger.LogWarning("Schedule {ScheduleId} not found in database, cannot update NextRunTime", scheduleId);
                return;
            }

            schedule.NextRunTime = nextFireTime?.DateTime;
            schedule.UpdatedAt = DateTime.UtcNow;
            
            await _unitOfWork.Schedules.UpdateAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated NextRunTime for schedule {ScheduleId} to {NextRunTime}", 
                scheduleId, schedule.NextRunTime?.ToString() ?? "null");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating NextRunTime for schedule {ScheduleId}", scheduleId);
        }
    }
}
