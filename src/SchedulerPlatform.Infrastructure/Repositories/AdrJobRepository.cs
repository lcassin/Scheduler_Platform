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
        //
        // IDEMPOTENCY CHECK: Also exclude jobs that already have a successful credential check execution
        // This prevents duplicate API calls (and duplicate billing) even if the job status wasn't saved correctly
        // AdrRequestTypeId = 1 is AttemptLogin (credential verification)
        var today = currentDate.Date;
        var leadDateCutoff = today.AddDays(credentialCheckLeadDays);
        const int attemptLoginRequestType = 1; // AdrRequestType.AttemptLogin
        
                return await _dbSet
                    .Where(j => !j.IsDeleted && 
                                !j.IsManualRequest && // Exclude manual jobs from orchestration
                                (j.Status == "Pending" || j.Status == "CredentialCheckInProgress") &&
                                !j.CredentialVerifiedDateTime.HasValue &&
                                j.NextRunDateTime.HasValue &&
                                j.NextRunDateTime.Value.Date > today &&
                                j.NextRunDateTime.Value.Date <= leadDateCutoff &&
                                // IDEMPOTENCY: Exclude jobs that already have a successful credential check (HTTP 200)
                                // Note: Include !e.IsDeleted so Force Refire (soft-delete) works
                                !_context.AdrJobExecutions.Any(e => 
                                    e.AdrJobId == j.Id && 
                                    e.AdrRequestTypeId == attemptLoginRequestType && 
                                    e.HttpStatusCode == 200 &&
                                    !e.IsDeleted))
                    .Include(j => j.AdrAccount)
                    .ToListAsync();
    }

    public async Task<IEnumerable<AdrJob>> GetJobsReadyForScrapingAsync(DateTime currentDate)
    {
        // Include "ScrapeInProgress" to recover jobs that were interrupted mid-step
        // These jobs already had the API called but the process crashed before updating status
        //
        // Four categories of jobs are ready for scraping:
        // 1. Normal flow: CredentialVerified or ScrapeInProgress jobs where NextRunDate has arrived
        // 2. Credential failed: CredentialFailed jobs should still attempt scraping daily
        //    - Helpdesk may have fixed the credential since the last attempt
        //    - Each failed scrape sends another reminder to helpdesk to fix or inactivate
        //    - Continue retrying until NextRangeEndDate is reached
        // 3. Missed credential window: Pending jobs where NextRunDate has arrived AND:
        //    - They have a CredentialId (so we can attempt scraping)
        //    - Their account is not "Missing" (Missing accounts need manual investigation)
        //    The downstream API will handle any credential issues and create helpdesk tickets
        // 4. Stuck in credential check: CredentialCheckInProgress jobs where NextRunDate has passed
        //    - These jobs were interrupted mid-credential-check (system shutdown/crash between API call and status save)
        //    - Once NextRunDate passes, they can no longer be picked up by credential verification query
        //    - Treat them like "missed credential window" jobs and proceed to scraping
        //    - The idempotency check prevents duplicate API calls
        //
        // BOUNDARY CHECK: Only scrape jobs within their billing window (NextRunDate to NextRangeEndDate)
        // Jobs past their NextRangeEndDate should not be retried here - they go to final retry logic
        //
        // IDEMPOTENCY CHECK: Also exclude jobs that already have a successful scrape execution
        // This prevents duplicate API calls (and duplicate billing) even if the job status wasn't saved correctly
        // AdrRequestTypeId = 2 is DownloadInvoice (scraping)
        var today = currentDate.Date;
        const int downloadInvoiceRequestType = 2; // AdrRequestType.DownloadInvoice
        
                return await _dbSet
                    .Where(j => !j.IsDeleted && 
                                !j.IsManualRequest && // Exclude manual jobs from orchestration
                                j.NextRunDateTime.HasValue &&
                                j.NextRunDateTime.Value.Date <= today &&
                                // BOUNDARY: Only include jobs within their scraping window
                                // If NextRangeEndDateTime is set, today must be <= that date
                                // If not set, allow scraping (backwards compatibility)
                                (!j.NextRangeEndDateTime.HasValue || j.NextRangeEndDateTime.Value.Date >= today) &&
                                (
                                    // Normal flow: credential verified jobs
                                    ((j.Status == "CredentialVerified" || j.Status == "ScrapeInProgress") &&
                                     j.CredentialVerifiedDateTime.HasValue)
                                    ||
                                    // Credential failed: still attempt scraping (helpdesk may have fixed it)
                                    // Each failure sends another reminder to helpdesk
                                    (j.Status == "CredentialFailed" &&
                                     j.CredentialId > 0 &&
                                     j.AdrAccount != null &&
                                     j.AdrAccount.HistoricalBillingStatus != "Missing")
                                    ||
                                    // Missed credential window: Pending jobs that can still be scraped
                                    (j.Status == "Pending" &&
                                     j.CredentialId > 0 &&
                                     j.AdrAccount != null &&
                                     j.AdrAccount.HistoricalBillingStatus != "Missing")
                                    ||
                                    // Stuck in credential check: jobs interrupted during credential verification
                                    // whose NextRunDate has now passed (can't go back to credential check)
                                    (j.Status == "CredentialCheckInProgress" &&
                                     j.CredentialId > 0 &&
                                     j.AdrAccount != null &&
                                     j.AdrAccount.HistoricalBillingStatus != "Missing")
                                ) &&
                                // IDEMPOTENCY: Exclude jobs that already have a successful scrape request (HTTP 200)
                                // Note: Include !e.IsDeleted so Force Refire (soft-delete) works
                                !_context.AdrJobExecutions.Any(e => 
                                    e.AdrJobId == j.Id && 
                                    e.AdrRequestTypeId == downloadInvoiceRequestType && 
                                    e.HttpStatusCode == 200 &&
                                    !e.IsDeleted))
                    .Include(j => j.AdrAccount)
                    .ToListAsync();
    }

    public async Task<IEnumerable<AdrJob>> GetJobsNeedingStatusCheckAsync(DateTime currentDate, int followUpDelayDays = 5)
    {
                // Include "StatusCheckInProgress" to recover jobs that were interrupted mid-step
                // These jobs already had the API called but the process crashed before updating status
                // Include "NeedsReview" because the status can be fixed downstream and should be re-checked daily
                var checkDate = currentDate.AddDays(-followUpDelayDays);
                return await _dbSet
                    .Where(j => !j.IsDeleted && 
                                !j.IsManualRequest && // Exclude manual jobs from orchestration
                                (j.Status == "ScrapeRequested" || j.Status == "StatusCheckInProgress" || j.Status == "NeedsReview") &&
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
                            !j.IsManualRequest && // Exclude manual jobs from orchestration
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
            string? masterVendorCode = null,
            string? vmAccountNumber = null,
            bool latestPerAccount = false,
            long? vmAccountId = null,
            string? interfaceAccountId = null,
            int? credentialId = null,
            bool? isManualRequest = null,
            string? sortColumn = null,
            bool sortDescending = true,
            List<int>? jobIds = null)
        {
            // Filter by both job.IsDeleted AND account.IsDeleted to exclude jobs for deleted accounts
            var query = _dbSet.Where(j => !j.IsDeleted && j.AdrAccount != null && !j.AdrAccount.IsDeleted);
            
            // Filter by specific job IDs (used for blacklist filtering)
            if (jobIds != null)
            {
                query = query.Where(j => jobIds.Contains(j.Id));
            }
            
            // Filter by manual request status
            // null = show all jobs (default)
            // true = show only manual jobs
            // false = show only non-manual jobs
            if (isManualRequest.HasValue)
            {
                query = query.Where(j => j.IsManualRequest == isManualRequest.Value);
            }

            if (adrAccountId.HasValue)
            {
                query = query.Where(j => j.AdrAccountId == adrAccountId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                // "ScrapeRequested" is a status group that includes "StatusCheckInProgress"
                // This matches the chart behavior which counts both statuses as "ADR Request Sent"
                if (status == "ScrapeRequested")
                {
                    query = query.Where(j => j.Status == "ScrapeRequested" || j.Status == "StatusCheckInProgress");
                }
                else
                {
                    query = query.Where(j => j.Status == status);
                }
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
                // Filter by Primary Vendor Code - check job's PrimaryVendorCode with fallback to AdrAccount's code
                query = query.Where(j => 
                    (j.PrimaryVendorCode != null && j.PrimaryVendorCode.Contains(vendorCode)) ||
                    (j.PrimaryVendorCode == null && j.AdrAccount != null && j.AdrAccount.PrimaryVendorCode != null && j.AdrAccount.PrimaryVendorCode.Contains(vendorCode)));
            }

            if (!string.IsNullOrWhiteSpace(masterVendorCode))
            {
                // Filter by Master Vendor Code - check job's MasterVendorCode with fallback to AdrAccount's code
                query = query.Where(j => 
                    (j.MasterVendorCode != null && j.MasterVendorCode.Contains(masterVendorCode)) ||
                    (j.MasterVendorCode == null && j.AdrAccount != null && j.AdrAccount.MasterVendorCode != null && j.AdrAccount.MasterVendorCode.Contains(masterVendorCode)));
            }

            if (!string.IsNullOrWhiteSpace(vmAccountNumber))
            {
                query = query.Where(j => j.VMAccountNumber.Contains(vmAccountNumber));
            }

            if (vmAccountId.HasValue)
            {
                query = query.Where(j => j.VMAccountId == vmAccountId.Value);
            }

            if (!string.IsNullOrWhiteSpace(interfaceAccountId))
            {
                query = query.Where(j => j.AdrAccount != null && j.AdrAccount.InterfaceAccountId == interfaceAccountId);
            }

            if (credentialId.HasValue)
            {
                query = query.Where(j => j.CredentialId == credentialId.Value);
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

            // Apply dynamic sorting
            IQueryable<AdrJob> orderedQuery = sortColumn switch
            {
                "Id" => sortDescending 
                    ? finalQuery.OrderByDescending(j => j.Id) 
                    : finalQuery.OrderBy(j => j.Id),
                "PrimaryVendorCode" => sortDescending 
                    ? finalQuery.OrderByDescending(j => j.PrimaryVendorCode ?? "") 
                    : finalQuery.OrderBy(j => j.PrimaryVendorCode ?? ""),
                "MasterVendorCode" => sortDescending 
                    ? finalQuery.OrderByDescending(j => j.MasterVendorCode ?? "") 
                    : finalQuery.OrderBy(j => j.MasterVendorCode ?? ""),
                "VMAccountNumber" => sortDescending 
                    ? finalQuery.OrderByDescending(j => j.VMAccountNumber ?? "") 
                    : finalQuery.OrderBy(j => j.VMAccountNumber ?? ""),
                "BillingPeriodStartDateTime" => sortDescending 
                    ? finalQuery.OrderByDescending(j => j.BillingPeriodStartDateTime) 
                    : finalQuery.OrderBy(j => j.BillingPeriodStartDateTime),
                "PeriodType" => sortDescending 
                    ? finalQuery.OrderByDescending(j => j.PeriodType ?? "") 
                    : finalQuery.OrderBy(j => j.PeriodType ?? ""),
                "NextRunDateTime" => sortDescending 
                    ? finalQuery.OrderByDescending(j => j.NextRunDateTime ?? DateTime.MinValue) 
                    : finalQuery.OrderBy(j => j.NextRunDateTime ?? DateTime.MaxValue),
                "Status" => sortDescending 
                    ? finalQuery.OrderByDescending(j => j.Status ?? "") 
                    : finalQuery.OrderBy(j => j.Status ?? ""),
                "AdrStatusId" => sortDescending 
                    ? finalQuery.OrderByDescending(j => j.AdrStatusId ?? 0) 
                    : finalQuery.OrderBy(j => j.AdrStatusId ?? int.MaxValue),
                "RetryCount" => sortDescending 
                    ? finalQuery.OrderByDescending(j => j.RetryCount) 
                    : finalQuery.OrderBy(j => j.RetryCount),
                _ => sortDescending 
                    ? finalQuery.OrderByDescending(j => j.Id) 
                    : finalQuery.OrderBy(j => j.Id)
            };

            var items = await orderedQuery
                .Include(j => j.AdrAccount)
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

    public async Task<int> GetCountByStatusAndIdsAsync(string status, HashSet<int> jobIds)
    {
        if (!jobIds.Any())
            return 0;
            
        return await _dbSet
            .Where(j => !j.IsDeleted && j.Status == status && jobIds.Contains(j.Id))
            .CountAsync();
    }

    public async Task<Dictionary<string, int>> GetCountsByStatusAndIdsAsync(HashSet<int> jobIds)
    {
        if (!jobIds.Any())
            return new Dictionary<string, int>();

        // Single GROUP BY query instead of 7 separate COUNT queries
        var results = await _dbSet
            .Where(j => !j.IsDeleted && jobIds.Contains(j.Id))
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return results.ToDictionary(x => x.Status ?? "", x => x.Count);
    }

    public async Task<IEnumerable<AdrJob>> GetJobsNeedingDailyStatusCheckAsync(DateTime currentDate, int delayDays = 1)
    {
        // Daily status checks: Jobs that were scraped at least delayDays ago
        // This enables the "day after scraping, check status" workflow
        //
        // The orchestrator's CheckPendingStatusesAsync will handle these jobs appropriately:
        // - Jobs still in billing window: revert to ScrapeRequested for retry
        // - Jobs past billing window: mark as NoInvoiceFound and advance rule
        var checkDate = currentDate.AddDays(-delayDays).Date;
        var today = currentDate.Date;

        // Get scraping-related jobs that need status check
        var scrapingJobs = await _dbSet
            .Where(j => !j.IsDeleted &&
                        !j.IsManualRequest &&
                        (j.Status == "ScrapeRequested" || j.Status == "StatusCheckInProgress" || j.Status == "NeedsReview") &&
                        !j.ScrapingCompletedDateTime.HasValue &&
                        // At least delayDays since last modification
                        j.ModifiedDateTime <= checkDate)
            .Include(j => j.AdrAccount)
            .ToListAsync();

        // Get credential verification jobs that need status check
        // These are jobs where credential check was sent but we need to re-check if credentials were fixed
        // Only include jobs where NextRunDateTime > today (scraping phase hasn't begun yet)
        var credentialJobs = await _dbSet
            .Where(j => !j.IsDeleted &&
                        !j.IsManualRequest &&
                        (j.Status == "CredentialCheckInProgress" || j.Status == "CredentialFailed") &&
                        // At least delayDays since last modification
                        j.ModifiedDateTime <= checkDate &&
                        // Only check credential status if scraping phase hasn't begun
                        // (NextRunDateTime > today means we're still in credential verification window)
                        j.NextRunDateTime.HasValue &&
                        j.NextRunDateTime.Value.Date > today)
            .Include(j => j.AdrAccount)
            .ToListAsync();

        // Combine and return all jobs needing status check
        return scrapingJobs.Concat(credentialJobs);
    }

    public async Task<IEnumerable<AdrJob>> GetAllJobsForManualStatusCheckAsync()
    {
        // Manual status check: Get ALL jobs that need status checking, regardless of timing
        // This is used by the "Check Statuses Only" button to check status for all jobs
        // Since there's no cost to check status, we can check all of them
        // Include "NeedsReview" because the status can be fixed downstream and should be re-checked
        // Include "CredentialCheckInProgress" and "CredentialFailed" to check credential verification status
        // (credentials can be fixed by helpdesk and should be re-checked daily until NextRunDate arrives)
        return await _dbSet
            .Where(j => !j.IsDeleted &&
                        // Include all jobs that need status checking
                        (j.Status == "ScrapeRequested" || 
                         j.Status == "StatusCheckInProgress" ||
                         j.Status == "NeedsReview" ||
                         j.Status == "CredentialCheckInProgress" ||
                         j.Status == "CredentialFailed"))
            .Include(j => j.AdrAccount)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdrJob>> GetStalePendingJobsAsync(DateTime currentDate, int maxLookbackDays = 90)
    {
        // Stale pending jobs: Jobs that are stuck in Pending or CredentialCheckInProgress status
        // but have passed their billing window (NextRangeEndDateTime < today).
        // These jobs missed their processing window and need to be finalized (cancelled)
        // so their rules can be advanced to the next billing cycle.
        //
        // We use a max lookback to avoid processing very old jobs that may have been
        // intentionally left in place or are from before the orchestration system was implemented.
        var today = currentDate.Date;
        var lookbackCutoff = today.AddDays(-maxLookbackDays);

        return await _dbSet
            .Where(j => !j.IsDeleted &&
                        !j.IsManualRequest && // Exclude manual jobs from automatic finalization
                        (j.Status == "Pending" || j.Status == "CredentialCheckInProgress") &&
                        // Billing window has ended (NextRangeEndDateTime < today)
                        j.NextRangeEndDateTime.HasValue &&
                        j.NextRangeEndDateTime.Value.Date < today &&
                        // But not too old (within lookback window)
                        j.NextRangeEndDateTime.Value.Date >= lookbackCutoff &&
                        // Must have a rule to advance
                        j.AdrAccountRuleId.HasValue)
            .Include(j => j.AdrAccount)
            .ToListAsync();
    }
}
