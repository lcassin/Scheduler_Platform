namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IScheduleRepository Schedules { get; }
    IJobExecutionRepository JobExecutions { get; }
    IRepository<Entities.Client> Clients { get; }
    IRepository<Entities.User> Users { get; }
    IRepository<Entities.UserPermission> UserPermissions { get; }
    IRepository<Entities.VendorCredential> VendorCredentials { get; }
    IRepository<Entities.JobParameter> JobParameters { get; }
    IRepository<Entities.NotificationSetting> NotificationSettings { get; }
    
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
