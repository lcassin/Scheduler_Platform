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

    public async Task<IEnumerable<AdrJob>> GetJobsNeedingCredentialVerificationAsync(DateTime currentDate, int credentialCheckLeadDays = 7)
    {
        // Include "CredentialCheckInProgress" to recover jobs that were interrupted mid-step
        // These jobs already had the API called but the process crashed before updating status
        // 
        // Credential verification window: 7 days before NextRunDate up to (but not including) NextRunDate
        // Example: If NextRunDate = Dec 17, credential check window is Dec 10-16
        // - Jobs where NextRunDateTime is within the next N days (configurable, default 7)
        // - Only future NextRunDates - jobs with past NextRunDates should be in scraping phase, not credential check
        var today = currentDate.Date;
        var leadDateCutoff = today.AddDays(credentialCheckLeadDays);
        
        return await _dbSet
            .Where(j => !j.IsDeleted && 
                        (j.Status == "Pending" || j.Status == "CredentialCheckInProgress") &&
                        !j.CredentialVerifiedDateTime.HasValue &&
                        j.NextRunDateTime.HasValue &&
                        j.NextRunDateTime.Value.Date > today &&
                        j.NextRunDateTime.Value.Date <= leadDateCutoff)
            .Include(j => j.AdrAccount)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdrJob>> GetJobsReadyForScrapingAsync(DateTime currentDate)
    {
        // Include "ScrapeInProgress" to recover jobs that were interrupted mid-step
        // These jobs already had the API called but the process crashed before updating status
        //
        // Two categories of jobs are ready for scraping:
        // 1. Normal flow: CredentialVerified or ScrapeInProgress jobs where NextRunDate has arrived
        // 2. Missed credential window: Pending jobs where NextRunDate has arrived AND:
        //    - They have a CredentialId (so we can attempt scraping)
        //    - Their account is not "Missing" (Missing accounts need manual investigation)
        //    The downstream API will handle any credential issues and create helpdesk tickets
        var today = currentDate.Date;
        
        return await _dbSet
            .Where(j => !j.IsDeleted && 
                        j.NextRunDateTime.HasValue &&
                        j.NextRunDateTime.Value.Date <= today &&
                        (
                            // Normal flow: credential verified jobs
                            ((j.Status == "CredentialVerified" || j.Status == "ScrapeInProgress") &&
                             j.CredentialVerifiedDateTime.HasValue)
                            ||
                            // Missed credential window: Pending jobs that can still be scraped
                            (j.Status == "Pending" &&
                             j.CredentialId > 0 &&
                             j.AdrAccount != null &&
                             j.AdrAccount.HistoricalBillingStatus != "Missing")
                        ))
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
