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
        // Include "CredentialCheckInProgress" to recover jobs that were interrupted mid-step
        // These jobs already had the API called but the process crashed before updating status
        return await _dbSet
            .Where(j => !j.IsDeleted && 
                        (j.Status == "Pending" || j.Status == "CredentialCheckInProgress") &&
                        !j.CredentialVerifiedDateTime.HasValue)
            .Include(j => j.AdrAccount)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdrJob>> GetJobsReadyForScrapingAsync(DateTime currentDate)
    {
        // Include "ScrapeInProgress" to recover jobs that were interrupted mid-step
        // These jobs already had the API called but the process crashed before updating status
        // Note: We don't filter by NextRunDateTime here because:
        // 1. The job was already created because the account was due (Run Now/Due Soon)
        // 2. Credential verification has succeeded
        // 3. The job should proceed to scraping immediately - NextRunDateTime is for retry scheduling
        return await _dbSet
            .Where(j => !j.IsDeleted && 
                        (j.Status == "CredentialVerified" || j.Status == "ScrapeInProgress") &&
                        j.CredentialVerifiedDateTime.HasValue)
            .Include(j => j.AdrAccount)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdrJob>> GetJobsNeedingStatusCheckAsync(DateTime currentDate, int followUpDelayDays = 5)
    {
        // Include "StatusCheckInProgress" to recover jobs that were interrupted mid-step
        // These jobs already had the API called but the process crashed before updating status
        var checkDate = currentDate.AddDays(-followUpDelayDays);
        return await _dbSet
            .Where(j => !j.IsDeleted && 
                        (j.Status == "ScrapeRequested" || j.Status == "StatusCheckInProgress") &&
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
            DateTime? billingPeriodEnd = null,
            string? vendorCode = null,
            string? vmAccountNumber = null,
            bool latestPerAccount = false)
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

            if (!string.IsNullOrWhiteSpace(vendorCode))
            {
                // Check both job's VendorCode and fallback to AdrAccount's VendorCode
                query = query.Where(j => 
                    (j.VendorCode != null && j.VendorCode.Contains(vendorCode)) ||
                    (j.VendorCode == null && j.AdrAccount != null && j.AdrAccount.VendorCode != null && j.AdrAccount.VendorCode.Contains(vendorCode)));
            }

            if (!string.IsNullOrWhiteSpace(vmAccountNumber))
            {
                query = query.Where(j => j.VMAccountNumber.Contains(vmAccountNumber));
            }

            int totalCount;
            IQueryable<AdrJob> finalQuery;

            // If latestPerAccount is true, get only the most recent job per account
            // Use ID subquery approach to allow Include to work properly
            if (latestPerAccount)
            {
                // Build a subquery that returns the IDs of the latest job per account
                var latestJobIdsQuery = query
                    .GroupBy(j => j.AdrAccountId)
                    .Select(g => g
                        .OrderByDescending(j => j.BillingPeriodStartDateTime)
                        .Select(j => j.Id)
                        .First());

                // Count is based on the number of accounts (distinct latest jobs)
                totalCount = await latestJobIdsQuery.CountAsync();

                // Rebase query to entity set using the ID subquery - this allows Include to work
                finalQuery = _dbSet
                    .Where(j => latestJobIdsQuery.Contains(j.Id));
            }
            else
            {
                totalCount = await query.CountAsync();
                finalQuery = query;
            }

            var items = await finalQuery
                .Include(j => j.AdrAccount)
                .OrderByDescending(j => j.BillingPeriodStartDateTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
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

    public async Task<int> GetActiveJobsCountAsync()
    {
        // Active jobs are those that are not completed, failed, or cancelled
        var activeStatuses = new[] { "Pending", "CredentialVerified", "ScrapeRequested" };
        return await _dbSet
            .Where(j => !j.IsDeleted && activeStatuses.Contains(j.Status))
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
