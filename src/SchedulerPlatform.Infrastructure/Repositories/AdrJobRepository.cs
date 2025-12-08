using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.Infrastructure.Repositories;

public class AdrJobRepository : Repository<AdrJob>, IAdrJobRepository
{
    public AdrJobRepository(SchedulerDbContext context) : base(context)
    {
    }

    public async Task<AdrJob?> GetByAccountAndBillingPeriodAsync(int adrAccountId, DateTime billingPeriodStart, DateTime billingPeriodEnd)
    {
        return await _dbSet
            .FirstOrDefaultAsync(j => j.AdrAccountId == adrAccountId && 
                                      j.BillingPeriodStartDateTime == billingPeriodStart &&
                                      j.BillingPeriodEndDateTime == billingPeriodEnd &&
                                      !j.IsDeleted);
    }

    public async Task<IEnumerable<AdrJob>> GetByAccountIdAsync(int adrAccountId)
    {
        return await _dbSet
            .Where(j => j.AdrAccountId == adrAccountId && !j.IsDeleted)
            .OrderByDescending(j => j.BillingPeriodStartDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdrJob>> GetByStatusAsync(string status)
    {
        return await _dbSet
            .Where(j => j.Status == status && !j.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdrJob>> GetJobsNeedingCredentialVerificationAsync(DateTime currentDate)
    {
        return await _dbSet
            .Where(j => !j.IsDeleted && 
                        j.Status == "Pending" &&
                        !j.CredentialVerifiedDateTime.HasValue)
            .Include(j => j.AdrAccount)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdrJob>> GetJobsReadyForScrapingAsync(DateTime currentDate)
    {
        return await _dbSet
            .Where(j => !j.IsDeleted && 
                        j.Status == "CredentialVerified" &&
                        j.CredentialVerifiedDateTime.HasValue &&
                        j.NextRunDateTime.HasValue &&
                        j.NextRunDateTime.Value.Date <= currentDate.Date)
            .Include(j => j.AdrAccount)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdrJob>> GetJobsNeedingStatusCheckAsync(DateTime currentDate, int followUpDelayDays = 5)
    {
        var checkDate = currentDate.AddDays(-followUpDelayDays);
        return await _dbSet
            .Where(j => !j.IsDeleted && 
                        j.Status == "ScrapeRequested" &&
                        j.AdrStatusId.HasValue &&
                        !j.ScrapingCompletedDateTime.HasValue &&
                        j.ModifiedDateTime <= checkDate)
            .Include(j => j.AdrAccount)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdrJob>> GetJobsForRetryAsync(DateTime currentDate, int maxRetries = 5)
    {
        return await _dbSet
            .Where(j => !j.IsDeleted && 
                        (j.Status == "CredentialFailed" || j.Status == "ScrapeFailed") &&
                        j.RetryCount < maxRetries &&
                        j.NextRunDateTime.HasValue &&
                        j.NextRunDateTime.Value.Date <= currentDate.Date)
            .Include(j => j.AdrAccount)
            .ToListAsync();
    }

    public async Task<(IEnumerable<AdrJob> items, int totalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        int? adrAccountId = null,
        string? status = null,
        DateTime? billingPeriodStart = null,
        DateTime? billingPeriodEnd = null)
    {
        var query = _dbSet.Where(j => !j.IsDeleted);

        if (adrAccountId.HasValue)
        {
            query = query.Where(j => j.AdrAccountId == adrAccountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(j => j.Status == status);
        }

        if (billingPeriodStart.HasValue)
        {
            query = query.Where(j => j.BillingPeriodStartDateTime >= billingPeriodStart.Value);
        }

        if (billingPeriodEnd.HasValue)
        {
            query = query.Where(j => j.BillingPeriodEndDateTime <= billingPeriodEnd.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(j => j.BillingPeriodStartDateTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(j => j.AdrAccount)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<int> GetTotalCountAsync(int? adrAccountId = null)
    {
        var query = _dbSet.Where(j => !j.IsDeleted);
        
        if (adrAccountId.HasValue)
        {
            query = query.Where(j => j.AdrAccountId == adrAccountId.Value);
        }

        return await query.CountAsync();
    }

    public async Task<int> GetCountByStatusAsync(string status)
    {
        return await _dbSet
            .Where(j => !j.IsDeleted && j.Status == status)
            .CountAsync();
    }

    public async Task<bool> ExistsForBillingPeriodAsync(int adrAccountId, DateTime billingPeriodStart, DateTime billingPeriodEnd)
    {
        return await _dbSet
            .AnyAsync(j => j.AdrAccountId == adrAccountId && 
                          j.BillingPeriodStartDateTime == billingPeriodStart &&
                          j.BillingPeriodEndDateTime == billingPeriodEnd &&
                          !j.IsDeleted);
    }
}
