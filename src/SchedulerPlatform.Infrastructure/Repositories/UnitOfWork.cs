using Microsoft.EntityFrameworkCore.Storage;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly SchedulerDbContext _context;
    private IDbContextTransaction? _transaction;
    
    public IScheduleRepository Schedules { get; }
    public IJobExecutionRepository JobExecutions { get; }
    public IRepository<Client> Clients { get; }
    public IRepository<User> Users { get; }
    public IUserPermissionRepository UserPermissions { get; }
    public IRepository<PasswordHistory> PasswordHistories { get; }
    public IRepository<JobParameter> JobParameters { get; }
    public IRepository<NotificationSetting> NotificationSettings { get; }
    
    // ADR repositories
    public IAdrAccountRepository AdrAccounts { get; }
    public IAdrJobRepository AdrJobs { get; }
    public IAdrJobExecutionRepository AdrJobExecutions { get; }

    public UnitOfWork(SchedulerDbContext context)
    {
        _context = context;
        Schedules = new ScheduleRepository(_context);
        JobExecutions = new JobExecutionRepository(_context);
        Clients = new Repository<Client>(_context);
        Users = new Repository<User>(_context);
        UserPermissions = new UserPermissionRepository(_context);
        PasswordHistories = new Repository<PasswordHistory>(_context);
        JobParameters = new Repository<JobParameter>(_context);
        NotificationSettings = new Repository<NotificationSetting>(_context);
        
        // ADR repositories
        AdrAccounts = new AdrAccountRepository(_context);
        AdrJobs = new AdrJobRepository(_context);
        AdrJobExecutions = new AdrJobExecutionRepository(_context);
    }

    public Task<int> SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
