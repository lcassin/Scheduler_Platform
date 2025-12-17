namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IScheduleRepository Schedules { get; }
    IJobExecutionRepository JobExecutions { get; }
    IRepository<Entities.Client> Clients { get; }
    IRepository<Entities.User> Users { get; }
    IUserPermissionRepository UserPermissions { get; }
    IRepository<Entities.PasswordHistory> PasswordHistories { get; }
    IRepository<Entities.JobParameter> JobParameters { get; }
    IRepository<Entities.NotificationSetting> NotificationSettings { get; }
    
    // ADR repositories
    IAdrAccountRepository AdrAccounts { get; }
    IAdrJobRepository AdrJobs { get; }
    IAdrJobExecutionRepository AdrJobExecutions { get; }
    IAdrOrchestrationRunRepository AdrOrchestrationRuns { get; }
    IRepository<Entities.AdrConfiguration> AdrConfigurations { get; }
    IRepository<Entities.AdrAccountBlacklist> AdrAccountBlacklists { get; }
    IRepository<Entities.AdrAccountRule> AdrAccountRules { get; }
    
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
