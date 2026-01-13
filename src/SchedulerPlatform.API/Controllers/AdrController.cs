using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.API.Services;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Core.Services;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.API.Controllers;

/// <summary>
/// Controller for managing Automated Data Retrieval (ADR) accounts, jobs, and executions.
/// Provides endpoints for ADR account management, job orchestration, and data scraping operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdrController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdrAccountSyncService _syncService;
    private readonly IAdrOrchestratorService _orchestratorService;
    private readonly IAdrOrchestrationQueue _orchestrationQueue;
    private readonly SchedulerDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdrController> _logger;

    public AdrController(
        IUnitOfWork unitOfWork,
        IAdrAccountSyncService syncService,
        IAdrOrchestratorService orchestratorService,
        IAdrOrchestrationQueue orchestrationQueue,
        SchedulerDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AdrController> logger)
    {
        _unitOfWork = unitOfWork;
        _syncService = syncService;
        _orchestratorService = orchestratorService;
        _orchestrationQueue = orchestrationQueue;
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    #region AdrAccount Endpoints

    /// <summary>
    /// Retrieves a paginated list of ADR accounts with optional filtering and sorting.
    /// </summary>
    /// <param name="clientId">Optional client ID to filter accounts.</param>
    /// <param name="credentialId">Optional credential ID to filter accounts.</param>
    /// <param name="nextRunStatus">Optional next run status filter.</param>
    /// <param name="searchTerm">Optional search term to filter by account number or vendor code.</param>
    /// <param name="historicalBillingStatus">Optional historical billing status filter.</param>
    /// <param name="isOverridden">Optional filter for manually overridden accounts.</param>
    /// <param name="jobStatus">Optional filter by current job status.</param>
    /// <param name="pageNumber">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20).</param>
    /// <param name="sortColumn">Column name to sort by.</param>
    /// <param name="sortDescending">Whether to sort in descending order.</param>
    /// <returns>A paginated list of ADR accounts with job status information.</returns>
    /// <response code="200">Returns the list of ADR accounts.</response>
    /// <response code="500">An error occurred while retrieving ADR accounts.</response>
    [HttpGet("accounts")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetAccounts(
        [FromQuery] int? clientId = null,
        [FromQuery] int? credentialId = null,
        [FromQuery] string? nextRunStatus = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? historicalBillingStatus = null,
        [FromQuery] bool? isOverridden = null,
        [FromQuery] string? jobStatus = null,
        [FromQuery] string? blacklistStatus = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortColumn = null,
        [FromQuery] bool sortDescending = false)
    {
        try
        {
            // If filtering by job status, we need to get the account IDs first
            List<int>? accountIdsWithJobStatus = null;
            if (!string.IsNullOrWhiteSpace(jobStatus))
            {
                if (jobStatus == "NoJob")
                {
                    // Get accounts that have no jobs at all
                    var accountsWithJobs = await _dbContext.AdrJobs
                        .Where(j => !j.IsDeleted)
                        .Select(j => j.AdrAccountId)
                        .Distinct()
                        .ToListAsync();
                    
                    // We'll filter to accounts NOT in this list
                    accountIdsWithJobStatus = await _dbContext.AdrAccounts
                        .Where(a => !a.IsDeleted && !accountsWithJobs.Contains(a.Id))
                        .Select(a => a.Id)
                        .ToListAsync();
                }
                else
                {
                    // Get accounts where the latest job has the specified status
                    accountIdsWithJobStatus = await _dbContext.AdrJobs
                        .Where(j => !j.IsDeleted)
                        .GroupBy(j => j.AdrAccountId)
                        .Select(g => new
                        {
                            AdrAccountId = g.Key,
                            LatestStatus = g.OrderByDescending(j => j.BillingPeriodStartDateTime).Select(j => j.Status).FirstOrDefault()
                        })
                        .Where(x => x.LatestStatus == jobStatus)
                        .Select(x => x.AdrAccountId)
                        .ToListAsync();
                }
            }
            
            // If filtering by blacklist status, we need to get the account IDs first
            List<int>? accountIdsWithBlacklistStatus = null;
            if (!string.IsNullOrWhiteSpace(blacklistStatus))
            {
                var filterToday = DateTime.UtcNow.Date;
                var activeBlacklistsForFilter = await _dbContext.AdrAccountBlacklists
                    .Where(b => !b.IsDeleted && b.IsActive)
                    .Where(b => !b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= filterToday)
                    .ToListAsync();
                
                // Get all non-deleted accounts
                var allAccounts = await _dbContext.AdrAccounts
                    .Where(a => !a.IsDeleted)
                    .Select(a => new { a.Id, a.VendorCode, a.VMAccountId, a.VMAccountNumber, a.CredentialId })
                    .ToListAsync();
                
                if (blacklistStatus == "current")
                {
                    // Get accounts that are currently blacklisted
                    accountIdsWithBlacklistStatus = allAccounts
                        .Where(a => activeBlacklistsForFilter.Any(b =>
                        {
                            var matches = (!string.IsNullOrEmpty(b.VendorCode) && b.VendorCode == a.VendorCode) ||
                                          (b.VMAccountId.HasValue && b.VMAccountId == a.VMAccountId) ||
                                          (!string.IsNullOrEmpty(b.VMAccountNumber) && b.VMAccountNumber == a.VMAccountNumber) ||
                                          (b.CredentialId.HasValue && b.CredentialId == a.CredentialId);
                            if (!matches) return false;
                            
                            var isCurrent = (!b.EffectiveStartDate.HasValue || b.EffectiveStartDate.Value <= filterToday) &&
                                           (!b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= filterToday);
                            return isCurrent;
                        }))
                        .Select(a => a.Id)
                        .ToList();
                }
                else if (blacklistStatus == "future")
                {
                    // Get accounts that have a future blacklist scheduled
                    accountIdsWithBlacklistStatus = allAccounts
                        .Where(a => activeBlacklistsForFilter.Any(b =>
                        {
                            var matches = (!string.IsNullOrEmpty(b.VendorCode) && b.VendorCode == a.VendorCode) ||
                                          (b.VMAccountId.HasValue && b.VMAccountId == a.VMAccountId) ||
                                          (!string.IsNullOrEmpty(b.VMAccountNumber) && b.VMAccountNumber == a.VMAccountNumber) ||
                                          (b.CredentialId.HasValue && b.CredentialId == a.CredentialId);
                            if (!matches) return false;
                            
                            var isFuture = b.EffectiveStartDate.HasValue && b.EffectiveStartDate.Value > filterToday;
                            return isFuture;
                        }))
                        .Select(a => a.Id)
                        .ToList();
                }
                else if (blacklistStatus == "any")
                {
                    // Get accounts that have any blacklist (current or future)
                    accountIdsWithBlacklistStatus = allAccounts
                        .Where(a => activeBlacklistsForFilter.Any(b =>
                            (!string.IsNullOrEmpty(b.VendorCode) && b.VendorCode == a.VendorCode) ||
                            (b.VMAccountId.HasValue && b.VMAccountId == a.VMAccountId) ||
                            (!string.IsNullOrEmpty(b.VMAccountNumber) && b.VMAccountNumber == a.VMAccountNumber) ||
                            (b.CredentialId.HasValue && b.CredentialId == a.CredentialId)))
                        .Select(a => a.Id)
                        .ToList();
                }
            }
            
            // Combine job status and blacklist status filters if both are present
            List<int>? combinedAccountIds = null;
            if (accountIdsWithJobStatus != null && accountIdsWithBlacklistStatus != null)
            {
                combinedAccountIds = accountIdsWithJobStatus.Intersect(accountIdsWithBlacklistStatus).ToList();
            }
            else if (accountIdsWithJobStatus != null)
            {
                combinedAccountIds = accountIdsWithJobStatus;
            }
            else if (accountIdsWithBlacklistStatus != null)
            {
                combinedAccountIds = accountIdsWithBlacklistStatus;
            }

            var (items, totalCount) = await _unitOfWork.AdrAccounts.GetPagedAsync(
                pageNumber,
                pageSize,
                clientId,
                credentialId,
                nextRunStatus,
                searchTerm,
                historicalBillingStatus,
                isOverridden,
                sortColumn,
                sortDescending,
                combinedAccountIds);

            // Get account IDs from the current page
            var accountIds = items.Select(a => a.Id).ToList();
            
            // Get rule override status for each account (single query)
            var ruleOverrideStatuses = await _dbContext.AdrAccountRules
                .Where(r => !r.IsDeleted && accountIds.Contains(r.AdrAccountId))
                .GroupBy(r => r.AdrAccountId)
                .Select(g => new
                {
                    AdrAccountId = g.Key,
                    // Get the primary rule's override status (first rule per account)
                    RuleIsManuallyOverridden = g.OrderBy(r => r.Id).Select(r => r.IsManuallyOverridden).FirstOrDefault(),
                    RuleOverriddenBy = g.OrderBy(r => r.Id).Select(r => r.OverriddenBy).FirstOrDefault(),
                    RuleOverriddenDateTime = g.OrderBy(r => r.Id).Select(r => r.OverriddenDateTime).FirstOrDefault()
                })
                .ToListAsync();
            
            // Create a lookup dictionary for rule override status
            var ruleOverrideLookup = ruleOverrideStatuses.ToDictionary(x => x.AdrAccountId);
            
            // Get current billing period job status for each account (single query)
            var currentJobStatuses = await _dbContext.AdrJobs
                .Where(j => !j.IsDeleted && accountIds.Contains(j.AdrAccountId))
                .GroupBy(j => j.AdrAccountId)
                .Select(g => new 
                {
                    AdrAccountId = g.Key,
                    // Get the job for the current billing period (matching NextRangeStart/End)
                    CurrentJobStatus = g
                        .OrderByDescending(j => j.BillingPeriodStartDateTime)
                        .Select(j => j.Status)
                        .FirstOrDefault(),
                    // Get the last completed job's date
                    LastCompletedDateTime = g
                        .Where(j => j.ScrapingCompletedDateTime.HasValue)
                        .OrderByDescending(j => j.ScrapingCompletedDateTime)
                        .Select(j => j.ScrapingCompletedDateTime)
                        .FirstOrDefault()
                })
                .ToListAsync();
            
            // Create a lookup dictionary
            var jobStatusLookup = currentJobStatuses.ToDictionary(x => x.AdrAccountId);
            
            // Get blacklist status for each account (single query)
            var today = DateTime.UtcNow.Date;
            var activeBlacklists = await _dbContext.AdrAccountBlacklists
                .Where(b => !b.IsDeleted && b.IsActive)
                .Where(b => !b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today)
                .ToListAsync();
            
            // Build blacklist status lookup for each account
            var blacklistStatusLookup = new Dictionary<int, (bool HasCurrent, bool HasFuture, int CurrentCount, int FutureCount, List<BlacklistSummary> CurrentSummaries, List<BlacklistSummary> FutureSummaries)>();
            foreach (var account in items)
            {
                var matchingBlacklists = activeBlacklists.Where(b =>
                {
                    if (!string.IsNullOrEmpty(b.VendorCode) && b.VendorCode == account.VendorCode)
                        return true;
                    if (b.VMAccountId.HasValue && b.VMAccountId == account.VMAccountId)
                        return true;
                    if (!string.IsNullOrEmpty(b.VMAccountNumber) && b.VMAccountNumber == account.VMAccountNumber)
                        return true;
                    if (b.CredentialId.HasValue && b.CredentialId == account.CredentialId)
                        return true;
                    return false;
                }).ToList();
                
                var currentBlacklists = matchingBlacklists
                    .Where(b => (!b.EffectiveStartDate.HasValue || b.EffectiveStartDate.Value <= today) &&
                                (!b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today))
                    .ToList();
                
                var futureBlacklists = matchingBlacklists
                    .Where(b => b.EffectiveStartDate.HasValue && b.EffectiveStartDate.Value > today)
                    .ToList();
                
                var currentSummaries = currentBlacklists.Select(b => new BlacklistSummary
                {
                    Id = b.Id,
                    Reason = b.Reason ?? string.Empty,
                    ExclusionType = b.ExclusionType ?? "All",
                    EffectiveStartDate = b.EffectiveStartDate,
                    EffectiveEndDate = b.EffectiveEndDate,
                    VendorCode = b.VendorCode,
                    VMAccountId = b.VMAccountId,
                    VMAccountNumber = b.VMAccountNumber,
                    CredentialId = b.CredentialId
                }).ToList();
                
                var futureSummaries = futureBlacklists.Select(b => new BlacklistSummary
                {
                    Id = b.Id,
                    Reason = b.Reason ?? string.Empty,
                    ExclusionType = b.ExclusionType ?? "All",
                    EffectiveStartDate = b.EffectiveStartDate,
                    EffectiveEndDate = b.EffectiveEndDate,
                    VendorCode = b.VendorCode,
                    VMAccountId = b.VMAccountId,
                    VMAccountNumber = b.VMAccountNumber,
                    CredentialId = b.CredentialId
                }).ToList();
                
                blacklistStatusLookup[account.Id] = (
                    currentBlacklists.Any(),
                    futureBlacklists.Any(),
                    currentBlacklists.Count,
                    futureBlacklists.Count,
                    currentSummaries,
                    futureSummaries
                );
            }
            
            // Map to response with job status
            var itemsWithJobStatus = items.Select(a => new
            {
                a.Id,
                a.VMAccountId,
                a.VMAccountNumber,
                a.InterfaceAccountId,
                a.ClientId,
                a.ClientName,
                a.CredentialId,
                a.VendorCode,
                a.PeriodType,
                a.PeriodDays,
                a.MedianDays,
                a.InvoiceCount,
                a.LastInvoiceDateTime,
                a.ExpectedNextDateTime,
                a.ExpectedRangeStartDateTime,
                a.ExpectedRangeEndDateTime,
                a.NextRunDateTime,
                a.NextRangeStartDateTime,
                a.NextRangeEndDateTime,
                a.DaysUntilNextRun,
                a.NextRunStatus,
                a.HistoricalBillingStatus,
                a.LastSyncedDateTime,
                a.IsManuallyOverridden,
                a.OverriddenBy,
                a.OverriddenDateTime,
                a.IsDeleted,
                a.CreatedDateTime,
                a.ModifiedDateTime,
                CurrentJobStatus = jobStatusLookup.TryGetValue(a.Id, out var js) ? js.CurrentJobStatus : null,
                LastCompletedDateTime = jobStatusLookup.TryGetValue(a.Id, out var js2) ? js2.LastCompletedDateTime : null,
                RuleIsManuallyOverridden = ruleOverrideLookup.TryGetValue(a.Id, out var ro) && ro.RuleIsManuallyOverridden,
                RuleOverriddenBy = ruleOverrideLookup.TryGetValue(a.Id, out var ro2) ? ro2.RuleOverriddenBy : null,
                RuleOverriddenDateTime = ruleOverrideLookup.TryGetValue(a.Id, out var ro3) ? ro3.RuleOverriddenDateTime : null,
                HasCurrentBlacklist = blacklistStatusLookup.TryGetValue(a.Id, out var bl) && bl.HasCurrent,
                HasFutureBlacklist = blacklistStatusLookup.TryGetValue(a.Id, out var bl2) && bl2.HasFuture,
                CurrentBlacklistCount = blacklistStatusLookup.TryGetValue(a.Id, out var bl3) ? bl3.CurrentCount : 0,
                FutureBlacklistCount = blacklistStatusLookup.TryGetValue(a.Id, out var bl4) ? bl4.FutureCount : 0,
                CurrentBlacklists = blacklistStatusLookup.TryGetValue(a.Id, out var bl5) ? bl5.CurrentSummaries : new List<BlacklistSummary>(),
                FutureBlacklists = blacklistStatusLookup.TryGetValue(a.Id, out var bl6) ? bl6.FutureSummaries : new List<BlacklistSummary>()
            }).ToList();

            return Ok(new
            {
                items = itemsWithJobStatus,
                totalCount,
                pageNumber,
                pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR accounts");
            return StatusCode(500, "An error occurred while retrieving ADR accounts");
        }
    }

    /// <summary>
    /// Retrieves a specific ADR account by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the ADR account.</param>
    /// <returns>The ADR account with the specified ID.</returns>
    /// <response code="200">Returns the requested ADR account.</response>
    /// <response code="404">ADR account with the specified ID was not found.</response>
    /// <response code="500">An error occurred while retrieving the ADR account.</response>
    [HttpGet("accounts/{id}")]
    [ProducesResponseType(typeof(AdrAccount), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrAccount>> GetAccount(int id)
    {
        try
        {
            var account = await _unitOfWork.AdrAccounts.GetByIdAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            return Ok(account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR account {AccountId}", id);
            return StatusCode(500, "An error occurred while retrieving the ADR account");
        }
    }

    /// <summary>
    /// Retrieves an ADR account by its VM Account ID.
    /// </summary>
    /// <param name="vmAccountId">The VM Account ID to search for.</param>
    /// <returns>The ADR account with the specified VM Account ID.</returns>
    /// <response code="200">Returns the requested ADR account.</response>
    /// <response code="404">ADR account with the specified VM Account ID was not found.</response>
    /// <response code="500">An error occurred while retrieving the ADR account.</response>
    [HttpGet("accounts/by-vm-account/{vmAccountId}")]
    [ProducesResponseType(typeof(AdrAccount), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrAccount>> GetAccountByVMAccountId(long vmAccountId)
    {
        try
        {
            var account = await _unitOfWork.AdrAccounts.GetByVMAccountIdAsync(vmAccountId);
            if (account == null)
            {
                return NotFound();
            }

            return Ok(account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR account by VMAccountId {VMAccountId}", vmAccountId);
            return StatusCode(500, "An error occurred while retrieving the ADR account");
        }
    }

    /// <summary>
    /// Retrieves ADR accounts that are due for a run on the specified date.
    /// </summary>
    /// <param name="date">The target date to check (defaults to current UTC date).</param>
    /// <returns>A list of ADR accounts due for a run.</returns>
    /// <response code="200">Returns the list of ADR accounts due for run.</response>
    /// <response code="500">An error occurred while retrieving ADR accounts.</response>
    [HttpGet("accounts/due-for-run")]
    [ProducesResponseType(typeof(IEnumerable<AdrAccount>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<AdrAccount>>> GetAccountsDueForRun([FromQuery] DateTime? date = null)
    {
        try
        {
            var targetDate = date ?? DateTime.UtcNow;
            var accounts = await _unitOfWork.AdrAccounts.GetAccountsDueForRunAsync(targetDate);
            return Ok(accounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR accounts due for run");
            return StatusCode(500, "An error occurred while retrieving ADR accounts due for run");
        }
    }

    /// <summary>
    /// Retrieves ADR accounts that need credential verification within the lead time window.
    /// </summary>
    /// <param name="date">The target date to check (defaults to current UTC date).</param>
    /// <param name="leadTimeDays">Number of days ahead to look for accounts needing credential check (default: 7).</param>
    /// <returns>A list of ADR accounts needing credential verification.</returns>
    /// <response code="200">Returns the list of ADR accounts needing credential check.</response>
    /// <response code="500">An error occurred while retrieving ADR accounts.</response>
    [HttpGet("accounts/needing-credential-check")]
    [ProducesResponseType(typeof(IEnumerable<AdrAccount>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<AdrAccount>>> GetAccountsNeedingCredentialCheck(
        [FromQuery] DateTime? date = null,
        [FromQuery] int leadTimeDays = 7)
    {
        try
        {
            var targetDate = date ?? DateTime.UtcNow;
            var accounts = await _unitOfWork.AdrAccounts.GetAccountsNeedingCredentialCheckAsync(targetDate, leadTimeDays);
            return Ok(accounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR accounts needing credential check");
            return StatusCode(500, "An error occurred while retrieving ADR accounts needing credential check");
        }
    }

    /// <summary>
    /// Retrieves statistics about ADR accounts including counts by status.
    /// </summary>
    /// <param name="clientId">Optional client ID to filter statistics.</param>
    /// <returns>Account statistics including total, run now, due soon, upcoming, future, missing, and active jobs counts.</returns>
    /// <response code="200">Returns the ADR account statistics.</response>
    /// <response code="500">An error occurred while retrieving ADR account stats.</response>
    [HttpGet("accounts/stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetAccountStats([FromQuery] int? clientId = null)
    {
        try
        {
            var totalAccounts = await _unitOfWork.AdrAccounts.GetTotalCountAsync(clientId);
            var runNowCount = await _unitOfWork.AdrAccounts.GetCountByNextRunStatusAsync("Run Now", clientId);
            var dueSoonCount = await _unitOfWork.AdrAccounts.GetCountByNextRunStatusAsync("Due Soon", clientId);
            var upcomingCount = await _unitOfWork.AdrAccounts.GetCountByNextRunStatusAsync("Upcoming", clientId);
            var futureCount = await _unitOfWork.AdrAccounts.GetCountByNextRunStatusAsync("Future", clientId);
            var missingCount = await _unitOfWork.AdrAccounts.GetCountByHistoricalStatusAsync("Missing", clientId);
            var activeJobsCount = await _unitOfWork.AdrJobs.GetActiveJobsCountAsync();

            return Ok(new
            {
                totalAccounts,
                runNowCount,
                dueSoonCount,
                upcomingCount,
                futureCount,
                missingCount,
                overdueCount = 0, // Overdue is calculated based on ExpectedNextDateTime in the UI
                activeJobsCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR account stats");
            return StatusCode(500, "An error occurred while retrieving ADR account stats");
        }
    }

    /// <summary>
    /// Updates the billing information for an ADR account including expected billing date and period type.
    /// This creates a manual override that will be preserved during account sync.
    /// </summary>
    /// <param name="id">The unique identifier of the ADR account.</param>
    /// <param name="request">The billing update request containing new billing information.</param>
    /// <returns>The updated ADR account.</returns>
    /// <response code="200">Returns the updated ADR account.</response>
    /// <response code="404">ADR account with the specified ID was not found.</response>
    /// <response code="500">An error occurred while updating the ADR account billing.</response>
    [HttpPut("accounts/{id}/billing")]
    [Authorize(Policy = "AdrAccounts.Update")]
    [ProducesResponseType(typeof(AdrAccount), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrAccount>> UpdateAccountBilling(int id, [FromBody] UpdateAccountBillingRequest request)
    {
        try
        {
            var account = await _unitOfWork.AdrAccounts.GetByIdAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            var username = User.Identity?.Name ?? "Unknown";

            // Update billing-related fields
            if (request.ExpectedBillingDate.HasValue)
            {
                account.LastInvoiceDateTime = request.ExpectedBillingDate.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.PeriodType))
            {
                account.PeriodType = request.PeriodType;
                // Set PeriodDays based on PeriodType
                account.PeriodDays = request.PeriodType switch
                {
                    "Bi-Weekly" => 14,
                    "Monthly" => 30,
                    "Bi-Monthly" => 60,
                    "Quarterly" => 90,
                    "Semi-Annually" => 180,
                    "Annually" => 365,
                    _ => 30
                };
                account.MedianDays = account.PeriodDays;
            }

            // Recalculate derived dates based on updated historical data using calendar-based arithmetic
            if (account.LastInvoiceDateTime.HasValue)
            {
                // Get window days from BillingPeriodCalculator for consistency
                var (windowBefore, windowAfter) = BillingPeriodCalculator.GetDefaultWindowDays(account.PeriodType);

                // Get anchor day of month to preserve across billing cycles (prevents drift after short months)
                var anchorDayOfMonth = BillingPeriodCalculator.GetAnchorDayOfMonth(account.LastInvoiceDateTime.Value);
                
                // Calculate next expected date using calendar-based arithmetic (AddMonths/AddYears, not AddDays)
                // This prevents date creep over time
                var expectedNext = BillingPeriodCalculator.CalculateNextRunDateOnOrAfterToday(
                    account.PeriodType,
                    account.LastInvoiceDateTime.Value,
                    DateTime.UtcNow.Date,
                    anchorDayOfMonth);

                var today = DateTime.UtcNow.Date;

                account.ExpectedNextDateTime = expectedNext;
                account.ExpectedRangeStartDateTime = expectedNext.AddDays(-windowBefore);
                account.ExpectedRangeEndDateTime = expectedNext.AddDays(windowAfter);
                account.NextRunDateTime = expectedNext;
                account.NextRangeStartDateTime = expectedNext.AddDays(-windowBefore);
                account.NextRangeEndDateTime = expectedNext.AddDays(windowAfter);
                account.DaysUntilNextRun = (int)(expectedNext - today).TotalDays;

                // Update NextRunStatus based on days until next run
                account.NextRunStatus = account.DaysUntilNextRun switch
                {
                    <= 0 => "Run Now",
                    <= 7 => "Due Soon",
                    <= 30 => "Upcoming",
                    _ => "Future"
                };
            }

            // Update Historical Billing Status if provided
            if (!string.IsNullOrWhiteSpace(request.HistoricalBillingStatus))
            {
                account.HistoricalBillingStatus = request.HistoricalBillingStatus;
            }

            // Set override flag and audit fields
            account.IsManuallyOverridden = true;
            account.OverriddenBy = username;
            account.OverriddenDateTime = DateTime.UtcNow;
            account.ModifiedDateTime = DateTime.UtcNow;
            account.ModifiedBy = username;

                        // Check for existing pending jobs for this account and update them if dates changed
                        // Instead of cancelling, we update the job's billing period dates to preserve credential status
                        if (account.NextRangeStartDateTime.HasValue && account.NextRangeEndDateTime.HasValue)
                        {
                            var existingJobs = await _unitOfWork.AdrJobs.GetByAccountIdAsync(account.Id);
                            var jobsWithOldDates = existingJobs.Where(j => 
                                (j.Status == "Pending" || j.Status == "CredentialCheckInProgress" || j.Status == "CredentialVerified" || j.Status == "CredentialFailed") &&
                                (j.BillingPeriodStartDateTime != account.NextRangeStartDateTime.Value ||
                                 j.BillingPeriodEndDateTime != account.NextRangeEndDateTime.Value)).ToList();
                
                            // Check if there's already a job with the new dates
                            var existingJobWithNewDates = existingJobs.FirstOrDefault(j =>
                                j.BillingPeriodStartDateTime == account.NextRangeStartDateTime.Value &&
                                j.BillingPeriodEndDateTime == account.NextRangeEndDateTime.Value &&
                                j.Status != "Cancelled");
                
                            foreach (var job in jobsWithOldDates)
                            {
                                if (existingJobWithNewDates != null && existingJobWithNewDates.Id != job.Id)
                                {
                                    // There's already a job with the new dates, so cancel this one
                                    // But transfer credential status if applicable
                                    if ((job.Status == "CredentialVerified" || job.Status == "CredentialFailed") &&
                                        existingJobWithNewDates.Status == "Pending")
                                    {
                                        existingJobWithNewDates.Status = job.Status;
                                        existingJobWithNewDates.CredentialVerifiedDateTime = job.CredentialVerifiedDateTime;
                                        existingJobWithNewDates.ModifiedDateTime = DateTime.UtcNow;
                                        existingJobWithNewDates.ModifiedBy = username;
                                        await _unitOfWork.AdrJobs.UpdateAsync(existingJobWithNewDates);
                                        _logger.LogInformation("Transferred credential status {Status} from job {OldJobId} to job {NewJobId}", 
                                            job.Status, job.Id, existingJobWithNewDates.Id);
                                    }
                        
                                    job.Status = "Cancelled";
                                    job.ErrorMessage = $"Cancelled due to manual billing date override by {username}. Credential status transferred to job {existingJobWithNewDates.Id}.";
                                }
                                else
                                {
                                    // No existing job with new dates, update this job's dates in place
                                    // This preserves the credential verification status
                                    var oldStart = job.BillingPeriodStartDateTime;
                                    var oldEnd = job.BillingPeriodEndDateTime;
                                    job.BillingPeriodStartDateTime = account.NextRangeStartDateTime.Value;
                                    job.BillingPeriodEndDateTime = account.NextRangeEndDateTime.Value;
                                    job.ErrorMessage = $"Billing period updated from {oldStart:MM/dd/yyyy}-{oldEnd:MM/dd/yyyy} to {account.NextRangeStartDateTime.Value:MM/dd/yyyy}-{account.NextRangeEndDateTime.Value:MM/dd/yyyy} by {username}";
                                    _logger.LogInformation("Updated job {JobId} billing period from {OldStart}-{OldEnd} to {NewStart}-{NewEnd}, preserving status {Status}", 
                                        job.Id, oldStart, oldEnd, account.NextRangeStartDateTime.Value, account.NextRangeEndDateTime.Value, job.Status);
                                }
                    
                                job.ModifiedDateTime = DateTime.UtcNow;
                                job.ModifiedBy = username;
                                await _unitOfWork.AdrJobs.UpdateAsync(job);
                            }
                        }

            await _unitOfWork.AdrAccounts.UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Account {AccountId} billing data updated by {User}. ExpectedBillingDate: {Date}, PeriodType: {Period}",
                id, username, request.ExpectedBillingDate, request.PeriodType);

            return Ok(account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ADR account billing for {AccountId}", id);
            return StatusCode(500, "An error occurred while updating the ADR account billing");
        }
    }

    /// <summary>
    /// Clears the manual override on an ADR account, allowing it to be updated by the next sync.
    /// </summary>
    /// <param name="id">The unique identifier of the ADR account.</param>
    /// <returns>The updated ADR account with override cleared.</returns>
    /// <response code="200">Returns the updated ADR account.</response>
    /// <response code="404">ADR account with the specified ID was not found.</response>
    /// <response code="500">An error occurred while clearing the override.</response>
    [HttpPost("accounts/{id}/clear-override")]
    [Authorize(Policy = "AdrAccounts.Update")]
    [ProducesResponseType(typeof(AdrAccount), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrAccount>> ClearAccountOverride(int id)
    {
        try
        {
            var account = await _unitOfWork.AdrAccounts.GetByIdAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            var username = User.Identity?.Name ?? "Unknown";

            // Clear override flag - next sync will update billing data from external source
            account.IsManuallyOverridden = false;
            account.OverriddenBy = null;
            account.OverriddenDateTime = null;
            account.ModifiedDateTime = DateTime.UtcNow;
            account.ModifiedBy = username;

            await _unitOfWork.AdrAccounts.UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Account {AccountId} override cleared by {User}", id, username);

            return Ok(account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing ADR account override for {AccountId}", id);
            return StatusCode(500, "An error occurred while clearing the ADR account override");
        }
    }

    /// <summary>
    /// Admin-only endpoint to manually fire an ADR request for any date range.
    /// This creates a real AdrJob with IsManualRequest=true and makes the actual API call.
    /// The job is excluded from normal orchestration but visible in the Jobs UI.
    /// </summary>
    /// <param name="id">The unique identifier of the ADR account.</param>
    /// <param name="request">The manual ADR request containing target date, date range, and request type.</param>
    /// <returns>The created job details and API response.</returns>
    /// <response code="200">Returns the created job and API response details.</response>
    /// <response code="400">Account does not have a credential ID or invalid request.</response>
    /// <response code="403">User does not have permission to perform manual ADR requests.</response>
    /// <response code="404">ADR account with the specified ID was not found.</response>
    /// <response code="500">An error occurred while processing the manual ADR request.</response>
    [HttpPost("accounts/{id}/manual-scrape")]
    [Authorize(Policy = "AdrAccounts.Update")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> ManualScrapeRequest(int id, [FromBody] ManualScrapeRequest request)
        {
            try
            {
                // Check if user has permission (Editors, Admins, Super Admins)
                var isSystemAdmin = User.Claims.Any(c => c.Type == "is_system_admin" && string.Equals(c.Value, "True", StringComparison.OrdinalIgnoreCase));
                var isAdmin = User.Claims.Any(c => c.Type == "role" && c.Value == "Admin");
                var isEditor = User.Claims.Any(c => c.Type == "role" && c.Value == "Editor");
                var hasAdrUpdatePermission = User.Claims.Any(c => c.Type == "permission" && c.Value == "adr:update");
            
                if (!isSystemAdmin && !isAdmin && !isEditor && !hasAdrUpdatePermission)
                {
                    return Forbid("You do not have permission to perform manual ADR requests");
                }

                var account = await _unitOfWork.AdrAccounts.GetByIdAsync(id);
                if (account == null)
                {
                    return NotFound("Account not found");
                }

                if (account.CredentialId <= 0)
                {
                    return BadRequest("Account does not have a credential ID assigned");
                }

                var username = User.Identity?.Name ?? "Unknown";

                // Calculate date range based on period type if not provided
                var windowDays = account.PeriodType switch
                {
                    "Bi-Weekly" => 3,
                    "Monthly" => 5,
                    "Bi-Monthly" => 7,
                    "Quarterly" => 10,
                    "Semi-Annually" => 14,
                    "Annually" => 21,
                    _ => 5
                };

                var rangeStart = request.RangeStartDate ?? request.TargetDate.AddDays(-windowDays);
                var rangeEnd = request.RangeEndDate ?? request.TargetDate.AddDays(windowDays);

                // Determine the request type (1 = Vendor Credential Check, 2 = ADR Download Request)
                var requestType = request.RequestType == 1 ? 1 : 2;
                var isCredentialCheck = requestType == 1;
                var initialStatus = isCredentialCheck ? "CredentialCheckInProgress" : "ScrapeInProgress";

                // Step 1: Create a real AdrJob record with IsManualRequest = true
                // This job is excluded from orchestration but visible in Jobs UI
                var job = new AdrJob
                {
                    AdrAccountId = account.Id,
                    VMAccountId = account.VMAccountId,
                    VMAccountNumber = account.VMAccountNumber,
                    VendorCode = account.VendorCode,
                    CredentialId = account.CredentialId,
                    PeriodType = account.PeriodType,
                    BillingPeriodStartDateTime = rangeStart,
                    BillingPeriodEndDateTime = rangeEnd,
                    NextRunDateTime = DateTime.UtcNow,
                    NextRangeStartDateTime = rangeStart,
                    NextRangeEndDateTime = rangeEnd,
                    Status = initialStatus,
                    IsMissing = false,
                    IsManualRequest = true,
                    ManualRequestReason = request.Reason,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedBy = username,
                    ModifiedDateTime = DateTime.UtcNow,
                    ModifiedBy = username
                };

                await _unitOfWork.AdrJobs.AddAsync(job);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "Created manual AdrJob {JobId} for account {AccountId} ({VMAccountNumber}). Range: {RangeStart} to {RangeEnd}",
                    job.Id, id, account.VMAccountNumber, rangeStart, rangeEnd);

                // Step 2: Create an AdrJobExecution linked to the job
                var execution = new AdrJobExecution
                {
                    AdrJobId = job.Id,
                    AdrRequestTypeId = requestType, // 1 = Vendor Credential Check, 2 = Download Invoice
                    StartDateTime = DateTime.UtcNow,
                    IsSuccess = false,
                    RequestPayload = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        JobId = job.Id,
                        AccountId = account.Id,
                        VMAccountId = account.VMAccountId,
                        VMAccountNumber = account.VMAccountNumber,
                        InterfaceAccountId = account.InterfaceAccountId,
                        CredentialId = account.CredentialId,
                        VendorCode = account.VendorCode,
                        ClientName = account.ClientName,
                        RangeStartDate = rangeStart,
                        RangeEndDate = rangeEnd,
                        TargetDate = request.TargetDate,
                        RequestedBy = username,
                        RequestedAt = DateTime.UtcNow,
                        Reason = request.Reason,
                        IsManualRequest = true,
                        IsHighPriority = request.IsHighPriority
                    }),
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedBy = username,
                    ModifiedDateTime = DateTime.UtcNow,
                    ModifiedBy = username
                };

                await _unitOfWork.AdrJobExecutions.AddAsync(execution);
                await _unitOfWork.SaveChangesAsync();

                // Step 3: Make the actual ADR API call (same as orchestrator)
                var baseUrl = _configuration["AdrApi:BaseUrl"] ?? "https://nuse2etsadrdevfn01.azurewebsites.net/api/";
                var sourceApplicationName = _configuration["AdrApi:SourceApplicationName"] ?? "ADRScheduler";
                var recipientEmail = _configuration["AdrApi:RecipientEmail"] ?? "lcassin@cassinfo.com";

                var client = _httpClientFactory.CreateClient("AdrApi");

                var apiRequest = new
                {
                    ADRRequestTypeId = requestType, // 1 = Vendor Credential Check, 2 = Download Invoice
                    CredentialId = account.CredentialId,
                    StartDate = rangeStart.ToString("yyyy-MM-dd"),
                    EndDate = rangeEnd.ToString("yyyy-MM-dd"),
                    SourceApplicationName = sourceApplicationName,
                    RecipientEmail = recipientEmail,
                    JobId = job.Id,
                    AccountId = account.VMAccountId,
                    InterfaceAccountId = account.InterfaceAccountId,
                    IsHighPriority = request.IsHighPriority
                };

                _logger.LogInformation(
                    "Calling ADR API for manual job {JobId}. Request: {@Request}",
                    job.Id, apiRequest);

                int? httpStatusCode = null;
                int? statusId = null;
                string? statusDescription = null;
                long? indexId = null;
                bool isSuccess = false;
                bool isError = false;
                bool isFinal = false;
                string? errorMessage = null;
                string? rawResponse = null;

                try
                {
                    var response = await client.PostAsJsonAsync($"{baseUrl}IngestAdrRequest", apiRequest);
                    httpStatusCode = (int)response.StatusCode;
                    rawResponse = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        if (!string.IsNullOrWhiteSpace(rawResponse))
                        {
                            var trimmed = rawResponse.TrimStart();
                            if (trimmed.StartsWith("{"))
                            {
                                var apiResponse = System.Text.Json.JsonSerializer.Deserialize<ManualAdrApiResponse>(rawResponse, new System.Text.Json.JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                });

                                if (apiResponse != null)
                                {
                                    statusId = apiResponse.StatusId;
                                    statusDescription = apiResponse.StatusDescription;
                                    indexId = apiResponse.IndexId;
                                    isSuccess = true;
                                    isError = apiResponse.IsError;
                                    isFinal = apiResponse.IsFinal;
                                }
                            }
                            else if (trimmed.StartsWith("["))
                            {
                                var list = System.Text.Json.JsonSerializer.Deserialize<List<ManualAdrApiResponse>>(rawResponse, new System.Text.Json.JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                });
                                var apiResponse = list?.FirstOrDefault();
                                if (apiResponse != null)
                                {
                                    statusId = apiResponse.StatusId;
                                    statusDescription = apiResponse.StatusDescription;
                                    indexId = apiResponse.IndexId;
                                    isSuccess = true;
                                    isError = apiResponse.IsError;
                                    isFinal = apiResponse.IsFinal;
                                }
                            }
                            else if (long.TryParse(trimmed, out var parsedIndexId))
                            {
                                indexId = parsedIndexId;
                                isSuccess = true;
                                statusDescription = "Request submitted successfully";
                            }
                            else
                            {
                                isSuccess = true;
                                statusDescription = rawResponse.Length > 200 ? rawResponse.Substring(0, 200) + "..." : rawResponse;
                            }
                        }
                        else
                        {
                            isSuccess = true;
                            statusDescription = "Request submitted (no response body)";
                        }
                    }
                    else
                    {
                        isError = true;
                        errorMessage = $"API returned {response.StatusCode}: {(rawResponse?.Length > 200 ? rawResponse.Substring(0, 200) + "..." : rawResponse)}";
                    
                        // Try to extract IndexId from error response
                        if (!string.IsNullOrWhiteSpace(rawResponse) && rawResponse.TrimStart().StartsWith("{"))
                        {
                            try
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(rawResponse);
                                if (doc.RootElement.TryGetProperty("indexId", out var indexIdProp) ||
                                    doc.RootElement.TryGetProperty("IndexId", out indexIdProp))
                                {
                                    if (indexIdProp.TryGetInt64(out var extractedIndexId))
                                    {
                                        indexId = extractedIndexId;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception apiEx)
                {
                    isError = true;
                    errorMessage = $"API call failed: {apiEx.Message}";
                    _logger.LogError(apiEx, "Error calling ADR API for manual job {JobId}", job.Id);
                }

                // Step 4: Update execution with API response
                execution.EndDateTime = DateTime.UtcNow;
                execution.HttpStatusCode = httpStatusCode;
                execution.AdrStatusId = statusId;
                execution.AdrStatusDescription = statusDescription;
                execution.AdrIndexId = indexId;
                execution.IsSuccess = isSuccess;
                execution.IsError = isError;
                execution.IsFinal = isFinal;
                execution.ErrorMessage = errorMessage;
                execution.ApiResponse = rawResponse?.Length > 4000 ? rawResponse.Substring(0, 4000) : rawResponse;
                execution.ModifiedDateTime = DateTime.UtcNow;
                execution.ModifiedBy = username;

                await _unitOfWork.AdrJobExecutions.UpdateAsync(execution);

                // Update job status based on API response and request type
                if (isSuccess && !isError)
                {
                    if (isCredentialCheck)
                    {
                        job.Status = "CredentialCheckRequested";
                        job.CredentialVerifiedDateTime = DateTime.UtcNow;
                    }
                    else
                    {
                        job.Status = "ScrapeRequested";
                    }
                    job.AdrStatusId = statusId;
                    job.AdrStatusDescription = statusDescription;
                    job.AdrIndexId = indexId;
                }
                else
                {
                    job.Status = isCredentialCheck ? "CredentialFailed" : "ScrapeFailed";
                    job.ErrorMessage = errorMessage;
                }
                job.ModifiedDateTime = DateTime.UtcNow;
                job.ModifiedBy = username;

                await _unitOfWork.AdrJobs.UpdateAsync(job);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "Manual ADR request completed for job {JobId}. Success: {IsSuccess}, StatusCode: {StatusCode}, IndexId: {IndexId}",
                    job.Id, isSuccess, httpStatusCode, indexId);

                // Step 5: Return full API response to UI
                return Ok(new
                {
                    message = isSuccess ? "Manual ADR request submitted successfully" : "Manual ADR request failed",
                    jobId = job.Id,
                    executionId = execution.Id,
                    accountId = id,
                    vmAccountNumber = account.VMAccountNumber,
                    credentialId = account.CredentialId,
                    rangeStartDate = rangeStart,
                    rangeEndDate = rangeEnd,
                    requestedBy = username,
                    requestedAt = execution.StartDateTime,
                    // API Response details
                    httpStatusCode = httpStatusCode,
                    isSuccess = isSuccess,
                    isError = isError,
                    isFinal = isFinal,
                    statusId = statusId,
                    statusDescription = statusDescription,
                    indexId = indexId,
                    errorMessage = errorMessage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating manual ADR request for account {AccountId}", id);
                return StatusCode(500, "An error occurred while creating the manual ADR request");
            }
        }

    /// <summary>
    /// Check the status of an ADR job using the external status check API.
    /// Available to Editors, Admins, and Super Admins.
    /// </summary>
    /// <param name="jobId">The unique identifier of the ADR job to check.</param>
    /// <returns>The current status of the job from the external API.</returns>
    /// <response code="200">Returns the job status details.</response>
    /// <response code="404">Job with the specified ID was not found.</response>
    /// <response code="500">An error occurred while checking job status.</response>
    [HttpPost("jobs/{jobId}/check-status")]
    [Authorize(Policy = "AdrAccounts.Update")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> CheckManualJobStatus(int jobId)
        {
            try
            {
                var job = await _unitOfWork.AdrJobs.GetByIdAsync(jobId);
                if (job == null)
                {
                    return NotFound("Job not found");
                }

                var username = User.Identity?.Name ?? "Unknown";

                // Create execution record for status check
                var execution = new AdrJobExecution
                {
                    AdrJobId = job.Id,
                    AdrRequestTypeId = 3, // Check Status
                    StartDateTime = DateTime.UtcNow,
                    IsSuccess = false,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedBy = username,
                    ModifiedDateTime = DateTime.UtcNow,
                    ModifiedBy = username
                };

                await _unitOfWork.AdrJobExecutions.AddAsync(execution);
                await _unitOfWork.SaveChangesAsync();

                // Call the status check API
                var baseUrl = _configuration["AdrApi:BaseUrl"] ?? "https://nuse2etsadrdevfn01.azurewebsites.net/api/";
                var client = _httpClientFactory.CreateClient("AdrApi");

                int? httpStatusCode = null;
                int? statusId = null;
                string? statusDescription = null;
                long? indexId = null;
                bool isSuccess = false;
                bool isError = false;
                bool isFinal = false;
                string? errorMessage = null;
                string? rawResponse = null;

                try
                {
                    var response = await client.GetAsync($"{baseUrl}GetRequestStatusByJobId/{jobId}");
                    httpStatusCode = (int)response.StatusCode;
                    rawResponse = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(rawResponse))
                    {
                        var apiResponse = System.Text.Json.JsonSerializer.Deserialize<ManualAdrApiResponse>(rawResponse, new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (apiResponse != null)
                        {
                            statusId = apiResponse.StatusId;
                            statusDescription = apiResponse.StatusDescription;
                            indexId = apiResponse.IndexId;
                            isSuccess = true;
                            isError = apiResponse.IsError;
                            isFinal = apiResponse.IsFinal;
                        }
                    }
                    else
                    {
                        isError = true;
                        errorMessage = $"API returned {response.StatusCode}: {rawResponse}";
                    }
                }
                catch (Exception apiEx)
                {
                    isError = true;
                    errorMessage = $"API call failed: {apiEx.Message}";
                    _logger.LogError(apiEx, "Error checking status for job {JobId}", jobId);
                }

                // Update execution
                execution.EndDateTime = DateTime.UtcNow;
                execution.HttpStatusCode = httpStatusCode;
                execution.AdrStatusId = statusId;
                execution.AdrStatusDescription = statusDescription;
                execution.AdrIndexId = indexId;
                execution.IsSuccess = isSuccess;
                execution.IsError = isError;
                execution.IsFinal = isFinal;
                execution.ErrorMessage = errorMessage;
                execution.ApiResponse = rawResponse?.Length > 4000 ? rawResponse.Substring(0, 4000) : rawResponse;
                execution.ModifiedDateTime = DateTime.UtcNow;
                execution.ModifiedBy = username;

                await _unitOfWork.AdrJobExecutions.UpdateAsync(execution);

                // Update job status if we got a final status
                if (isSuccess && isFinal)
                {
                    job.Status = isError ? "ScrapeFailed" : "Completed";
                    job.ScrapingCompletedDateTime = DateTime.UtcNow;
                }
                job.AdrStatusId = statusId ?? job.AdrStatusId;
                job.AdrStatusDescription = statusDescription ?? job.AdrStatusDescription;
                job.AdrIndexId = indexId ?? job.AdrIndexId;
                job.ModifiedDateTime = DateTime.UtcNow;
                job.ModifiedBy = username;

                await _unitOfWork.AdrJobs.UpdateAsync(job);
                await _unitOfWork.SaveChangesAsync();

                return Ok(new
                {
                    jobId = job.Id,
                    executionId = execution.Id,
                    httpStatusCode = httpStatusCode,
                    isSuccess = isSuccess,
                    isError = isError,
                    isFinal = isFinal,
                    statusId = statusId,
                    statusDescription = statusDescription,
                    indexId = indexId,
                    errorMessage = errorMessage,
                    jobStatus = job.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking status for job {JobId}", jobId);
                return StatusCode(500, "An error occurred while checking job status");
            }
        }

    /// <summary>
    /// Exports ADR accounts to Excel format with optional filtering.
    /// </summary>
    /// <param name="clientId">Optional client ID to filter accounts.</param>
    /// <param name="searchTerm">Optional search term to filter accounts.</param>
    /// <param name="nextRunStatus">Optional next run status filter.</param>
    /// <param name="historicalBillingStatus">Optional historical billing status filter.</param>
    /// <param name="isOverridden">Optional filter for manually overridden accounts.</param>
    /// <param name="sortColumn">Column name to sort by.</param>
    /// <param name="sortDescending">Whether to sort in descending order.</param>
    /// <param name="format">Export format (default: excel).</param>
    /// <returns>A file download containing the exported ADR accounts.</returns>
    /// <response code="200">Returns the exported file.</response>
    /// <response code="500">An error occurred while exporting ADR accounts.</response>
    [HttpGet("accounts/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportAccounts(
        [FromQuery] int? clientId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? nextRunStatus = null,
        [FromQuery] string? historicalBillingStatus = null,
        [FromQuery] bool? isOverridden = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] bool sortDescending = false,
        [FromQuery] string format = "excel")
    {
        try
        {
            // Build the base query for accounts (don't materialize yet)
            var accountsQuery = _dbContext.AdrAccounts.Where(a => !a.IsDeleted);

            if (clientId.HasValue)
                accountsQuery = accountsQuery.Where(a => a.ClientId == clientId.Value);

            if (!string.IsNullOrWhiteSpace(nextRunStatus))
                accountsQuery = accountsQuery.Where(a => a.NextRunStatus == nextRunStatus);

            if (!string.IsNullOrWhiteSpace(historicalBillingStatus))
                accountsQuery = accountsQuery.Where(a => a.HistoricalBillingStatus == historicalBillingStatus);

            if (isOverridden.HasValue)
                accountsQuery = accountsQuery.Where(a => a.IsManuallyOverridden == isOverridden.Value);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                accountsQuery = accountsQuery.Where(a =>
                    a.VMAccountNumber.Contains(searchTerm) ||
                    (a.InterfaceAccountId != null && a.InterfaceAccountId.Contains(searchTerm)) ||
                    (a.ClientName != null && a.ClientName.Contains(searchTerm)) ||
                    (a.VendorCode != null && a.VendorCode.Contains(searchTerm)));
            }

            // Project accounts with job status and rule override using correlated subqueries
            // This generates efficient OUTER APPLY queries instead of massive IN clauses with GroupBy
            var exportData = await accountsQuery
                .Select(a => new
                {
                    Account = a,
                    // Correlated subquery for current job status (latest by BillingPeriodStartDateTime)
                    CurrentJobStatus = _dbContext.AdrJobs
                        .Where(j => j.AdrAccountId == a.Id && !j.IsDeleted)
                        .OrderByDescending(j => j.BillingPeriodStartDateTime)
                        .Select(j => j.Status)
                        .FirstOrDefault(),
                    // Correlated subquery for last completed datetime
                    LastCompletedDateTime = _dbContext.AdrJobs
                        .Where(j => j.AdrAccountId == a.Id && !j.IsDeleted && j.ScrapingCompletedDateTime != null)
                        .OrderByDescending(j => j.ScrapingCompletedDateTime)
                        .Select(j => j.ScrapingCompletedDateTime)
                        .FirstOrDefault(),
                    // Correlated subquery for rule override status (first rule by Id)
                    RuleIsManuallyOverridden = _dbContext.AdrAccountRules
                        .Where(r => r.AdrAccountId == a.Id && !r.IsDeleted)
                        .OrderBy(r => r.Id)
                        .Select(r => r.IsManuallyOverridden)
                        .FirstOrDefault(),
                    RuleOverriddenBy = _dbContext.AdrAccountRules
                        .Where(r => r.AdrAccountId == a.Id && !r.IsDeleted)
                        .OrderBy(r => r.Id)
                        .Select(r => r.OverriddenBy)
                        .FirstOrDefault(),
                    RuleOverriddenDateTime = _dbContext.AdrAccountRules
                        .Where(r => r.AdrAccountId == a.Id && !r.IsDeleted)
                        .OrderBy(r => r.Id)
                        .Select(r => r.OverriddenDateTime)
                        .FirstOrDefault()
                })
                .ToListAsync();

            // Get blacklist status for each account (single query)
            var today = DateTime.UtcNow.Date;
            var activeBlacklists = await _dbContext.AdrAccountBlacklists
                .Where(b => !b.IsDeleted && b.IsActive)
                .Where(b => !b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today)
                .ToListAsync();
            
            // Build blacklist status lookup for each account
            var blacklistStatusLookup = new Dictionary<int, (bool HasCurrent, bool HasFuture)>();
            foreach (var item in exportData)
            {
                var account = item.Account;
                var matchingBlacklists = activeBlacklists.Where(b =>
                {
                    if (!string.IsNullOrEmpty(b.VendorCode) && b.VendorCode == account.VendorCode)
                        return true;
                    if (b.VMAccountId.HasValue && b.VMAccountId == account.VMAccountId)
                        return true;
                    if (!string.IsNullOrEmpty(b.VMAccountNumber) && b.VMAccountNumber == account.VMAccountNumber)
                        return true;
                    if (b.CredentialId.HasValue && b.CredentialId == account.CredentialId)
                        return true;
                    return false;
                }).ToList();
                
                var hasCurrent = matchingBlacklists
                    .Any(b => (!b.EffectiveStartDate.HasValue || b.EffectiveStartDate.Value <= today) &&
                              (!b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today));
                
                var hasFuture = matchingBlacklists
                    .Any(b => b.EffectiveStartDate.HasValue && b.EffectiveStartDate.Value > today);
                
                blacklistStatusLookup[account.Id] = (hasCurrent, hasFuture);
            }

            var headers = new[] { "Account #", "VM Account ID", "Interface Account ID", "Client", "Vendor Code", "Period Type", "Next Run", "Run Status", "Job Status", "Last Completed", "Historical Status", "Last Invoice", "Expected Next", "Account Overridden", "Account Overridden By", "Account Overridden Date", "Rule Overridden", "Rule Overridden By", "Rule Overridden Date", "Current Blacklist", "Future Blacklist" };

            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                var csvBytes = ExcelExportHelper.CreateCsvExport(
                    string.Join(",", headers),
                    exportData,
                    item =>
                    {
                        var a = item.Account;
                        var bl = blacklistStatusLookup.TryGetValue(a.Id, out var blStatus) ? blStatus : (HasCurrent: false, HasFuture: false);
                        return $"{ExcelExportHelper.CsvEscape(a.VMAccountNumber)},{a.VMAccountId},{ExcelExportHelper.CsvEscape(a.InterfaceAccountId)},{ExcelExportHelper.CsvEscape(a.ClientName)},{ExcelExportHelper.CsvEscape(a.VendorCode)},{ExcelExportHelper.CsvEscape(a.PeriodType)},{a.NextRunDateTime?.ToString("MM/dd/yyyy") ?? ""},{ExcelExportHelper.CsvEscape(a.NextRunStatus)},{ExcelExportHelper.CsvEscape(item.CurrentJobStatus)},{item.LastCompletedDateTime?.ToString("MM/dd/yyyy") ?? ""},{ExcelExportHelper.CsvEscape(a.HistoricalBillingStatus)},{a.LastInvoiceDateTime?.ToString("MM/dd/yyyy") ?? ""},{a.ExpectedNextDateTime?.ToString("MM/dd/yyyy") ?? ""},{a.IsManuallyOverridden},{ExcelExportHelper.CsvEscape(a.OverriddenBy)},{a.OverriddenDateTime?.ToString("MM/dd/yyyy HH:mm") ?? ""},{(item.RuleIsManuallyOverridden ? "Yes" : "No")},{ExcelExportHelper.CsvEscape(item.RuleOverriddenBy)},{item.RuleOverriddenDateTime?.ToString("MM/dd/yyyy HH:mm") ?? ""},{(bl.HasCurrent ? "Yes" : "No")},{(bl.HasFuture ? "Yes" : "No")}";
                    });
                return File(csvBytes, "text/csv", $"adr_accounts_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            }

            // Excel format using centralized helper
            var excelBytes = ExcelExportHelper.CreateExcelExport(
                "ADR Accounts",
                "AdrAccountsTable",
                headers,
                exportData,
                item =>
                {
                    var a = item.Account;
                    var bl = blacklistStatusLookup.TryGetValue(a.Id, out var blStatus) ? blStatus : (HasCurrent: false, HasFuture: false);
                    return new object?[]
                    {
                        a.VMAccountNumber,
                        a.VMAccountId,
                        a.InterfaceAccountId,
                        a.ClientName,
                        a.VendorCode,
                        a.PeriodType,
                        a.NextRunDateTime,
                        a.NextRunStatus,
                        item.CurrentJobStatus ?? "",
                        item.LastCompletedDateTime,
                        a.HistoricalBillingStatus,
                        a.LastInvoiceDateTime,
                        a.ExpectedNextDateTime,
                        a.IsManuallyOverridden,
                        a.OverriddenBy ?? "",
                        a.OverriddenDateTime,
                        item.RuleIsManuallyOverridden,
                        item.RuleOverriddenBy ?? "",
                        item.RuleOverriddenDateTime,
                        bl.HasCurrent ? "Yes" : "No",
                        bl.HasFuture ? "Yes" : "No"
                    };
                });
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"adr_accounts_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting ADR accounts");
            return StatusCode(500, "An error occurred while exporting ADR accounts");
        }
    }

    #endregion

    #region AdrJob Endpoints

    /// <summary>
    /// Retrieves a paginated list of ADR jobs with optional filtering and sorting.
    /// </summary>
    /// <param name="adrAccountId">Optional ADR account ID to filter jobs.</param>
    /// <param name="status">Optional status to filter jobs.</param>
    /// <param name="billingPeriodStart">Optional billing period start date filter.</param>
    /// <param name="billingPeriodEnd">Optional billing period end date filter.</param>
    /// <param name="vendorCode">Optional vendor code to filter jobs.</param>
    /// <param name="vmAccountNumber">Optional VM account number to filter jobs.</param>
    /// <param name="latestPerAccount">If true, returns only the latest job per account.</param>
    /// <param name="pageNumber">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20).</param>
    /// <param name="vmAccountId">Optional VM account ID to filter jobs.</param>
    /// <param name="interfaceAccountId">Optional interface account ID to filter jobs.</param>
    /// <param name="credentialId">Optional credential ID to filter jobs.</param>
    /// <param name="isManualRequest">Optional filter for manual vs automated requests.</param>
    /// <param name="sortColumn">Column name to sort by.</param>
    /// <param name="sortDescending">Whether to sort in descending order (default: true).</param>
    /// <returns>A paginated list of ADR jobs.</returns>
    /// <response code="200">Returns the paginated list of ADR jobs.</response>
    /// <response code="500">An error occurred while retrieving ADR jobs.</response>
    [HttpGet("jobs")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetJobs(
        [FromQuery] int? adrAccountId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? billingPeriodStart = null,
        [FromQuery] DateTime? billingPeriodEnd = null,
        [FromQuery] string? vendorCode = null,
        [FromQuery] string? vmAccountNumber = null,
        [FromQuery] bool latestPerAccount = false,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? vmAccountId = null,
        [FromQuery] string? interfaceAccountId = null,
        [FromQuery] int? credentialId = null,
        [FromQuery] bool? isManualRequest = null,
        [FromQuery] string? blacklistStatus = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] bool sortDescending = true)
    {
            try
            {
                // If filtering by blacklist status, we need to get the job IDs first
                List<int>? jobIdsWithBlacklistStatus = null;
                if (!string.IsNullOrWhiteSpace(blacklistStatus))
                {
                    var filterToday = DateTime.UtcNow.Date;
                    var activeBlacklistsForFilter = await _dbContext.AdrAccountBlacklists
                        .Where(b => !b.IsDeleted && b.IsActive)
                        .Where(b => !b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= filterToday)
                        .ToListAsync();
                    
                    // Get all non-deleted jobs with their account info
                    var allJobs = await _dbContext.AdrJobs
                        .Where(j => !j.IsDeleted)
                        .Include(j => j.AdrAccount)
                        .Select(j => new { j.Id, j.VendorCode, j.VMAccountId, j.VMAccountNumber, j.CredentialId, AccountVendorCode = j.AdrAccount != null ? j.AdrAccount.VendorCode : null })
                        .ToListAsync();
                    
                    if (blacklistStatus == "current")
                    {
                        // Get jobs that are currently blacklisted
                        jobIdsWithBlacklistStatus = allJobs
                            .Where(j => activeBlacklistsForFilter.Any(b =>
                            {
                                var jobVendorCode = !string.IsNullOrEmpty(j.VendorCode) ? j.VendorCode : j.AccountVendorCode;
                                var matches = (!string.IsNullOrEmpty(b.VendorCode) && b.VendorCode == jobVendorCode) ||
                                              (b.VMAccountId.HasValue && b.VMAccountId == j.VMAccountId) ||
                                              (!string.IsNullOrEmpty(b.VMAccountNumber) && b.VMAccountNumber == j.VMAccountNumber) ||
                                              (b.CredentialId.HasValue && b.CredentialId == j.CredentialId);
                                if (!matches) return false;
                                
                                var isCurrent = (!b.EffectiveStartDate.HasValue || b.EffectiveStartDate.Value <= filterToday) &&
                                               (!b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= filterToday);
                                return isCurrent;
                            }))
                            .Select(j => j.Id)
                            .ToList();
                    }
                    else if (blacklistStatus == "future")
                    {
                        // Get jobs that have a future blacklist scheduled
                        jobIdsWithBlacklistStatus = allJobs
                            .Where(j => activeBlacklistsForFilter.Any(b =>
                            {
                                var jobVendorCode = !string.IsNullOrEmpty(j.VendorCode) ? j.VendorCode : j.AccountVendorCode;
                                var matches = (!string.IsNullOrEmpty(b.VendorCode) && b.VendorCode == jobVendorCode) ||
                                              (b.VMAccountId.HasValue && b.VMAccountId == j.VMAccountId) ||
                                              (!string.IsNullOrEmpty(b.VMAccountNumber) && b.VMAccountNumber == j.VMAccountNumber) ||
                                              (b.CredentialId.HasValue && b.CredentialId == j.CredentialId);
                                if (!matches) return false;
                                
                                var isFuture = b.EffectiveStartDate.HasValue && b.EffectiveStartDate.Value > filterToday;
                                return isFuture;
                            }))
                            .Select(j => j.Id)
                            .ToList();
                    }
                    else if (blacklistStatus == "any")
                    {
                        // Get jobs that have any blacklist (current or future)
                        jobIdsWithBlacklistStatus = allJobs
                            .Where(j => activeBlacklistsForFilter.Any(b =>
                            {
                                var jobVendorCode = !string.IsNullOrEmpty(j.VendorCode) ? j.VendorCode : j.AccountVendorCode;
                                return (!string.IsNullOrEmpty(b.VendorCode) && b.VendorCode == jobVendorCode) ||
                                       (b.VMAccountId.HasValue && b.VMAccountId == j.VMAccountId) ||
                                       (!string.IsNullOrEmpty(b.VMAccountNumber) && b.VMAccountNumber == j.VMAccountNumber) ||
                                       (b.CredentialId.HasValue && b.CredentialId == j.CredentialId);
                            }))
                            .Select(j => j.Id)
                            .ToList();
                    }
                }
                
                var (items, totalCount) = await _unitOfWork.AdrJobs.GetPagedAsync(
                    pageNumber,
                    pageSize,
                    adrAccountId,
                    status,
                    billingPeriodStart,
                    billingPeriodEnd,
                    vendorCode,
                    vmAccountNumber,
                    latestPerAccount,
                    vmAccountId,
                    interfaceAccountId,
                    credentialId,
                    isManualRequest,
                    sortColumn,
                    sortDescending,
                    jobIdsWithBlacklistStatus);

                // Get blacklist status for each job (single query)
                var today = DateTime.UtcNow.Date;
                var activeBlacklists = await _dbContext.AdrAccountBlacklists
                    .Where(b => !b.IsDeleted && b.IsActive)
                    .Where(b => !b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today)
                    .ToListAsync();
                
                // Build blacklist status lookup for each job
                var blacklistStatusLookup = new Dictionary<int, (bool HasCurrent, bool HasFuture, int CurrentCount, int FutureCount, List<BlacklistSummary> CurrentSummaries, List<BlacklistSummary> FutureSummaries)>();
                foreach (var job in items)
                {
                    var jobVendorCode = !string.IsNullOrEmpty(job.VendorCode) ? job.VendorCode : job.AdrAccount?.VendorCode;
                    var matchingBlacklists = activeBlacklists.Where(b =>
                    {
                        if (!string.IsNullOrEmpty(b.VendorCode) && b.VendorCode == jobVendorCode)
                            return true;
                        if (b.VMAccountId.HasValue && b.VMAccountId == job.VMAccountId)
                            return true;
                        if (!string.IsNullOrEmpty(b.VMAccountNumber) && b.VMAccountNumber == job.VMAccountNumber)
                            return true;
                        if (b.CredentialId.HasValue && b.CredentialId == job.CredentialId)
                            return true;
                        return false;
                    }).ToList();
                    
                    var currentBlacklists = matchingBlacklists
                        .Where(b => (!b.EffectiveStartDate.HasValue || b.EffectiveStartDate.Value <= today) &&
                                    (!b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today))
                        .ToList();
                    
                    var futureBlacklists = matchingBlacklists
                        .Where(b => b.EffectiveStartDate.HasValue && b.EffectiveStartDate.Value > today)
                        .ToList();
                    
                    var currentSummaries = currentBlacklists.Select(b => new BlacklistSummary
                    {
                        Id = b.Id,
                        Reason = b.Reason ?? string.Empty,
                        ExclusionType = b.ExclusionType ?? "All",
                        EffectiveStartDate = b.EffectiveStartDate,
                        EffectiveEndDate = b.EffectiveEndDate,
                        VendorCode = b.VendorCode,
                        VMAccountId = b.VMAccountId,
                        VMAccountNumber = b.VMAccountNumber,
                        CredentialId = b.CredentialId
                    }).ToList();
                    
                    var futureSummaries = futureBlacklists.Select(b => new BlacklistSummary
                    {
                        Id = b.Id,
                        Reason = b.Reason ?? string.Empty,
                        ExclusionType = b.ExclusionType ?? "All",
                        EffectiveStartDate = b.EffectiveStartDate,
                        EffectiveEndDate = b.EffectiveEndDate,
                        VendorCode = b.VendorCode,
                        VMAccountId = b.VMAccountId,
                        VMAccountNumber = b.VMAccountNumber,
                        CredentialId = b.CredentialId
                    }).ToList();
                    
                    blacklistStatusLookup[job.Id] = (
                        currentBlacklists.Any(),
                        futureBlacklists.Any(),
                        currentBlacklists.Count,
                        futureBlacklists.Count,
                        currentSummaries,
                        futureSummaries
                    );
                }

                // Map to DTOs with VendorCode fallback from AdrAccount when job's VendorCode is null
                var mappedItems = items.Select(j => new
                {
                    j.Id,
                    j.AdrAccountId,
                    j.VMAccountId,
                    j.VMAccountNumber,
                    VendorCode = !string.IsNullOrEmpty(j.VendorCode) ? j.VendorCode : j.AdrAccount?.VendorCode,
                    j.CredentialId,
                    j.PeriodType,
                    j.BillingPeriodStartDateTime,
                    j.BillingPeriodEndDateTime,
                    j.NextRunDateTime,
                    j.NextRangeStartDateTime,
                    j.NextRangeEndDateTime,
                    j.Status,
                    j.AdrStatusId,
                    j.AdrStatusDescription,
                    j.AdrIndexId,
                    j.IsMissing,
                    j.RetryCount,
                    j.CredentialVerifiedDateTime,
                    j.ScrapingCompletedDateTime,
                    j.ErrorMessage,
                    j.CreatedDateTime,
                    j.CreatedBy,
                    j.ModifiedDateTime,
                    j.ModifiedBy,
                    HasCurrentBlacklist = blacklistStatusLookup.TryGetValue(j.Id, out var bl) && bl.HasCurrent,
                    HasFutureBlacklist = blacklistStatusLookup.TryGetValue(j.Id, out var bl2) && bl2.HasFuture,
                    CurrentBlacklistCount = blacklistStatusLookup.TryGetValue(j.Id, out var bl3) ? bl3.CurrentCount : 0,
                    FutureBlacklistCount = blacklistStatusLookup.TryGetValue(j.Id, out var bl4) ? bl4.FutureCount : 0,
                    CurrentBlacklists = blacklistStatusLookup.TryGetValue(j.Id, out var bl5) ? bl5.CurrentSummaries : new List<BlacklistSummary>(),
                    FutureBlacklists = blacklistStatusLookup.TryGetValue(j.Id, out var bl6) ? bl6.FutureSummaries : new List<BlacklistSummary>()
                }).ToList();

                return Ok(new
                {
                    items = mappedItems,
                    totalCount,
                    pageNumber,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ADR jobs");
                return StatusCode(500, "An error occurred while retrieving ADR jobs");
            }
        }

    /// <summary>
    /// Retrieves a specific ADR job by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the ADR job.</param>
    /// <returns>The ADR job with the specified ID.</returns>
    /// <response code="200">Returns the ADR job.</response>
    /// <response code="404">ADR job with the specified ID was not found.</response>
    /// <response code="500">An error occurred while retrieving the ADR job.</response>
    [HttpGet("jobs/{id}")]
    [ProducesResponseType(typeof(AdrJob), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrJob>> GetJob(int id)
    {
        try
        {
            var job = await _unitOfWork.AdrJobs.GetByIdAsync(id);
            if (job == null)
            {
                return NotFound();
            }

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR job {JobId}", id);
            return StatusCode(500, "An error occurred while retrieving the ADR job");
        }
    }

    /// <summary>
    /// Retrieves all ADR jobs for a specific account.
    /// </summary>
    /// <param name="adrAccountId">The unique identifier of the ADR account.</param>
    /// <returns>A list of ADR jobs for the specified account.</returns>
    /// <response code="200">Returns the list of ADR jobs.</response>
    /// <response code="500">An error occurred while retrieving ADR jobs.</response>
    [HttpGet("jobs/by-account/{adrAccountId}")]
    [ProducesResponseType(typeof(IEnumerable<AdrJob>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<AdrJob>>> GetJobsByAccount(int adrAccountId)
    {
        try
        {
            var jobs = await _unitOfWork.AdrJobs.GetByAccountIdAsync(adrAccountId);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR jobs for account {AccountId}", adrAccountId);
            return StatusCode(500, "An error occurred while retrieving ADR jobs");
        }
    }

    /// <summary>
    /// Retrieves all ADR jobs with a specific status.
    /// </summary>
    /// <param name="status">The status to filter jobs by.</param>
    /// <returns>A list of ADR jobs with the specified status.</returns>
    /// <response code="200">Returns the list of ADR jobs.</response>
    /// <response code="500">An error occurred while retrieving ADR jobs.</response>
    [HttpGet("jobs/by-status/{status}")]
    [ProducesResponseType(typeof(IEnumerable<AdrJob>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<AdrJob>>> GetJobsByStatus(string status)
    {
        try
        {
            var jobs = await _unitOfWork.AdrJobs.GetByStatusAsync(status);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR jobs by status {Status}", status);
            return StatusCode(500, "An error occurred while retrieving ADR jobs");
        }
    }

    /// <summary>
    /// Retrieves ADR jobs that need credential verification.
    /// </summary>
    /// <param name="date">Optional target date for filtering (defaults to current UTC time).</param>
    /// <returns>A list of ADR jobs needing credential verification.</returns>
    /// <response code="200">Returns the list of ADR jobs needing credential verification.</response>
    /// <response code="500">An error occurred while retrieving ADR jobs.</response>
    [HttpGet("jobs/needing-credential-verification")]
    [ProducesResponseType(typeof(IEnumerable<AdrJob>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<AdrJob>>> GetJobsNeedingCredentialVerification([FromQuery] DateTime? date = null)
    {
        try
        {
            var targetDate = date ?? DateTime.UtcNow;
            var jobs = await _unitOfWork.AdrJobs.GetJobsNeedingCredentialVerificationAsync(targetDate);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR jobs needing credential verification");
            return StatusCode(500, "An error occurred while retrieving ADR jobs needing credential verification");
        }
    }

    /// <summary>
    /// Retrieves ADR jobs that are ready for scraping (credential verified and at or past NextRunDateTime).
    /// </summary>
    /// <param name="date">Optional target date for filtering (defaults to current UTC time).</param>
    /// <returns>A list of ADR jobs ready for scraping.</returns>
    /// <response code="200">Returns the list of ADR jobs ready for scraping.</response>
    /// <response code="500">An error occurred while retrieving ADR jobs.</response>
    [HttpGet("jobs/ready-for-scraping")]
    [ProducesResponseType(typeof(IEnumerable<AdrJob>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<AdrJob>>> GetJobsReadyForScraping([FromQuery] DateTime? date = null)
    {
        try
        {
            var targetDate = date ?? DateTime.UtcNow;
            var jobs = await _unitOfWork.AdrJobs.GetJobsReadyForScrapingAsync(targetDate);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR jobs ready for scraping");
            return StatusCode(500, "An error occurred while retrieving ADR jobs ready for scraping");
        }
    }

    /// <summary>
    /// Retrieves ADR jobs that need a status check from the external API.
    /// </summary>
    /// <param name="date">Optional target date for filtering (defaults to current UTC time).</param>
    /// <param name="followUpDelayDays">Number of days to wait before following up on a job (default: 5).</param>
    /// <returns>A list of ADR jobs needing status check.</returns>
    /// <response code="200">Returns the list of ADR jobs needing status check.</response>
    /// <response code="500">An error occurred while retrieving ADR jobs.</response>
    [HttpGet("jobs/needing-status-check")]
    [ProducesResponseType(typeof(IEnumerable<AdrJob>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<AdrJob>>> GetJobsNeedingStatusCheck(
        [FromQuery] DateTime? date = null,
        [FromQuery] int followUpDelayDays = 5)
    {
        try
        {
            var targetDate = date ?? DateTime.UtcNow;
            var jobs = await _unitOfWork.AdrJobs.GetJobsNeedingStatusCheckAsync(targetDate, followUpDelayDays);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR jobs needing status check");
            return StatusCode(500, "An error occurred while retrieving ADR jobs needing status check");
        }
    }

    /// <summary>
    /// Retrieves ADR jobs that are eligible for retry after previous failures.
    /// </summary>
    /// <param name="date">Optional target date for filtering (defaults to current UTC time).</param>
    /// <param name="maxRetries">Maximum number of retries allowed before giving up (default: 5).</param>
    /// <returns>A list of ADR jobs eligible for retry.</returns>
    /// <response code="200">Returns the list of ADR jobs eligible for retry.</response>
    /// <response code="500">An error occurred while retrieving ADR jobs.</response>
    [HttpGet("jobs/for-retry")]
    [ProducesResponseType(typeof(IEnumerable<AdrJob>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<AdrJob>>> GetJobsForRetry(
        [FromQuery] DateTime? date = null,
        [FromQuery] int maxRetries = 5)
    {
        try
        {
            var targetDate = date ?? DateTime.UtcNow;
            var jobs = await _unitOfWork.AdrJobs.GetJobsForRetryAsync(targetDate, maxRetries);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR jobs for retry");
            return StatusCode(500, "An error occurred while retrieving ADR jobs for retry");
        }
    }

    /// <summary>
    /// Retrieves statistics about ADR jobs including counts by status.
    /// </summary>
    /// <param name="adrAccountId">Optional ADR account ID to filter statistics.</param>
    /// <param name="lastOrchestrationRuns">Optional number of recent orchestration runs to include in statistics.</param>
    /// <returns>Job statistics including counts by status (pending, credential verified, scrape requested, completed, failed, needs review, credential failed).</returns>
    /// <response code="200">Returns the ADR job statistics.</response>
    /// <response code="500">An error occurred while retrieving ADR job stats.</response>
    [HttpGet("jobs/stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetJobStats(
        [FromQuery] int? adrAccountId = null,
        [FromQuery] int? lastOrchestrationRuns = null)
    {
        try
        {
            int totalCount, pendingCount, credentialVerifiedCount, scrapeRequestedCount, 
                completedCount, failedCount, needsReviewCount, credentialFailedCount,
                credentialCheckRequestedCount, credentialCheckInProgressCount;

            if (lastOrchestrationRuns.HasValue && lastOrchestrationRuns.Value > 0)
            {
                // Get the last N orchestration runs to determine the time window
                var recentRuns = await _unitOfWork.AdrOrchestrationRuns.GetRecentRunsAsync(lastOrchestrationRuns.Value);
                
                if (recentRuns.Any())
                {
                    // Get the earliest start time from recent runs to define our window
                    var earliestRunTime = recentRuns.Min(r => r.StartedDateTime ?? r.RequestedDateTime);
                    
                    // Get distinct job IDs that have executions created during these runs
                    var jobIds = await _unitOfWork.AdrJobExecutions.GetJobIdsModifiedSinceAsync(earliestRunTime);
                    var jobIdSet = jobIds.ToHashSet();
                    
                    // Count jobs by status using a single GROUP BY query (instead of 7 separate queries)
                    totalCount = jobIdSet.Count;
                    var statusCounts = await _unitOfWork.AdrJobs.GetCountsByStatusAndIdsAsync(jobIdSet);
                    
                    pendingCount = statusCounts.TryGetValue("Pending", out var p) ? p : 0;
                    credentialCheckRequestedCount = statusCounts.TryGetValue("CredentialCheckRequested", out var ccr) ? ccr : 0;
                    credentialCheckInProgressCount = statusCounts.TryGetValue("CredentialCheckInProgress", out var ccip) ? ccip : 0;
                    credentialVerifiedCount = statusCounts.TryGetValue("CredentialVerified", out var cv) ? cv : 0;
                    credentialFailedCount = statusCounts.TryGetValue("CredentialFailed", out var cf) ? cf : 0;
                    // Include StatusCheckInProgress in ScrapeRequested count - these are jobs mid-status-check
                    var sr = statusCounts.TryGetValue("ScrapeRequested", out var srVal) ? srVal : 0;
                    var sci = statusCounts.TryGetValue("StatusCheckInProgress", out var sciVal) ? sciVal : 0;
                    scrapeRequestedCount = sr + sci;
                    completedCount = statusCounts.TryGetValue("Completed", out var c) ? c : 0;
                    failedCount = statusCounts.TryGetValue("Failed", out var f) ? f : 0;
                    needsReviewCount = statusCounts.TryGetValue("NeedsReview", out var nr) ? nr : 0;
                }
                else
                {
                    // No recent runs, return zeros
                    totalCount = pendingCount = credentialVerifiedCount = credentialFailedCount = 
                        scrapeRequestedCount = completedCount = failedCount = needsReviewCount = 
                        credentialCheckRequestedCount = credentialCheckInProgressCount = 0;
                }
            }
            else
            {
                // Original behavior: count all jobs
                totalCount = await _unitOfWork.AdrJobs.GetTotalCountAsync(adrAccountId);
                pendingCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("Pending");
                credentialCheckRequestedCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("CredentialCheckRequested");
                credentialCheckInProgressCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("CredentialCheckInProgress");
                credentialVerifiedCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("CredentialVerified");
                credentialFailedCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("CredentialFailed");
                // Include StatusCheckInProgress in ScrapeRequested count - these are jobs mid-status-check
                scrapeRequestedCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("ScrapeRequested");
                var statusCheckInProgressCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("StatusCheckInProgress");
                scrapeRequestedCount += statusCheckInProgressCount;
                completedCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("Completed");
                failedCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("Failed");
                needsReviewCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("NeedsReview");
            }

            // Calculate phase breakdown counts
            // Credential Phase: Pending + CredentialCheckRequested + CredentialCheckInProgress + CredentialVerified + CredentialFailed
            var credentialPhaseCount = pendingCount + credentialCheckRequestedCount + credentialCheckInProgressCount + credentialVerifiedCount + credentialFailedCount;
            
            // ADR Document Phase: ScrapeRequested (includes StatusCheckInProgress) + Completed + Failed + NeedsReview
            var adrDocumentPhaseCount = scrapeRequestedCount + completedCount + failedCount + needsReviewCount;

            return Ok(new
            {
                totalCount,
                pendingCount,
                credentialCheckRequestedCount,
                credentialCheckInProgressCount,
                credentialVerifiedCount,
                credentialFailedCount,
                scrapeRequestedCount,
                completedCount,
                failedCount,
                needsReviewCount,
                // Phase breakdown
                credentialPhaseCount,
                adrDocumentPhaseCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR job stats");
            return StatusCode(500, "An error occurred while retrieving ADR job stats");
        }
    }

    /// <summary>
    /// Exports ADR jobs to Excel or CSV format with optional filtering.
    /// </summary>
    /// <param name="status">Optional status to filter jobs.</param>
    /// <param name="vendorCode">Optional vendor code to filter jobs.</param>
    /// <param name="vmAccountNumber">Optional VM account number to filter jobs.</param>
    /// <param name="latestPerAccount">If true, returns only the latest job per account.</param>
    /// <param name="isManualRequest">Optional filter for manual vs automated requests.</param>
    /// <param name="sortColumn">Column name to sort by.</param>
    /// <param name="sortDescending">Whether to sort in descending order (default: true).</param>
    /// <param name="format">Export format: 'excel' or 'csv' (default: excel).</param>
    /// <returns>A file download containing the exported ADR jobs.</returns>
    /// <response code="200">Returns the exported file.</response>
    /// <response code="500">An error occurred while exporting ADR jobs.</response>
    [HttpGet("jobs/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportJobs(
        [FromQuery] string? status = null,
        [FromQuery] string? vendorCode = null,
        [FromQuery] string? vmAccountNumber = null,
        [FromQuery] bool latestPerAccount = false,
        [FromQuery] bool? isManualRequest = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] bool sortDescending = true,
        [FromQuery] string format = "excel")
    {
        try
        {
            // Get all jobs matching the filters (no pagination for export)
            var (jobs, _) = await _unitOfWork.AdrJobs.GetPagedAsync(
                1, int.MaxValue, null, status, null, null, vendorCode, vmAccountNumber, latestPerAccount,
                null, null, null, isManualRequest, sortColumn, sortDescending);

            // Get blacklist status for each job (single query)
            var today = DateTime.UtcNow.Date;
            var activeBlacklists = await _dbContext.AdrAccountBlacklists
                .Where(b => !b.IsDeleted && b.IsActive)
                .Where(b => !b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today)
                .ToListAsync();
            
            // Build blacklist status lookup for each job
            var blacklistStatusLookup = new Dictionary<int, (int CurrentCount, int FutureCount, string Details)>();
            foreach (var job in jobs)
            {
                var jobVendorCode = !string.IsNullOrEmpty(job.VendorCode) ? job.VendorCode : job.AdrAccount?.VendorCode;
                var matchingBlacklists = activeBlacklists.Where(b =>
                {
                    if (!string.IsNullOrEmpty(b.VendorCode) && b.VendorCode == jobVendorCode)
                        return true;
                    if (b.VMAccountId.HasValue && b.VMAccountId == job.VMAccountId)
                        return true;
                    if (!string.IsNullOrEmpty(b.VMAccountNumber) && b.VMAccountNumber == job.VMAccountNumber)
                        return true;
                    if (b.CredentialId.HasValue && b.CredentialId == job.CredentialId)
                        return true;
                    return false;
                }).ToList();
                
                var currentBlacklists = matchingBlacklists
                    .Where(b => (!b.EffectiveStartDate.HasValue || b.EffectiveStartDate.Value <= today) &&
                                (!b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today))
                    .ToList();
                
                var futureBlacklists = matchingBlacklists
                    .Where(b => b.EffectiveStartDate.HasValue && b.EffectiveStartDate.Value > today)
                    .ToList();
                
                // Build details string
                var details = new List<string>();
                foreach (var bl in currentBlacklists.Concat(futureBlacklists).Take(5))
                {
                    var dateRange = bl.EffectiveStartDate.HasValue || bl.EffectiveEndDate.HasValue
                        ? $" ({bl.EffectiveStartDate?.ToString("MM/dd/yy") ?? "N/A"} - {bl.EffectiveEndDate?.ToString("MM/dd/yy") ?? "N/A"})"
                        : "";
                    details.Add($"{bl.ExclusionType}: {bl.Reason}{dateRange}");
                }
                var detailsStr = string.Join("; ", details);
                if (matchingBlacklists.Count > 5)
                    detailsStr += $" (+{matchingBlacklists.Count - 5} more)";
                
                blacklistStatusLookup[job.Id] = (currentBlacklists.Count, futureBlacklists.Count, detailsStr);
            }

            var headers = new[] { "Job ID", "Vendor Code", "Account #", "VM Account ID", "Interface Account ID", "Billing Period Start", "Billing Period End", "Period Type", "Next Run", "Status", "ADR Status", "ADR Status Description", "Retry Count", "Is Manual", "Created", "Current Blacklist Count", "Future Blacklist Count", "Blacklist Details" };

            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                var csvBytes = ExcelExportHelper.CreateCsvExport(
                    string.Join(",", headers),
                    jobs,
                    j =>
                    {
                        var bl = blacklistStatusLookup.TryGetValue(j.Id, out var blStatus) ? blStatus : (CurrentCount: 0, FutureCount: 0, Details: "");
                        return $"{j.Id},{ExcelExportHelper.CsvEscape(j.VendorCode)},{ExcelExportHelper.CsvEscape(j.VMAccountNumber)},{j.VMAccountId},{ExcelExportHelper.CsvEscape(j.AdrAccount?.InterfaceAccountId)},{j.BillingPeriodStartDateTime:MM/dd/yyyy},{j.BillingPeriodEndDateTime:MM/dd/yyyy},{ExcelExportHelper.CsvEscape(j.PeriodType)},{j.NextRunDateTime?.ToString("MM/dd/yyyy") ?? ""},{ExcelExportHelper.CsvEscape(j.Status)},{j.AdrStatusId?.ToString() ?? ""},{ExcelExportHelper.CsvEscape(j.AdrStatusDescription)},{j.RetryCount},{j.IsManualRequest},{j.CreatedDateTime:MM/dd/yyyy HH:mm},{bl.CurrentCount},{bl.FutureCount},{ExcelExportHelper.CsvEscape(bl.Details)}";
                    });
                return File(csvBytes, "text/csv", $"adr_jobs_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            }

            // Excel format using centralized helper
            var excelBytes = ExcelExportHelper.CreateExcelExport(
                "ADR Jobs",
                "AdrJobsTable",
                headers,
                jobs,
                j =>
                {
                    var bl = blacklistStatusLookup.TryGetValue(j.Id, out var blStatus) ? blStatus : (CurrentCount: 0, FutureCount: 0, Details: "");
                    return new object?[]
                    {
                        j.Id,
                        j.VendorCode,
                        j.VMAccountNumber,
                        j.VMAccountId,
                        j.AdrAccount?.InterfaceAccountId ?? "",
                        j.BillingPeriodStartDateTime,
                        j.BillingPeriodEndDateTime,
                        j.PeriodType,
                        j.NextRunDateTime,
                        j.Status,
                        j.AdrStatusId,
                        j.AdrStatusDescription,
                        j.RetryCount,
                        j.IsManualRequest,
                        j.CreatedDateTime,
                        bl.CurrentCount,
                        bl.FutureCount,
                        bl.Details
                    };
                });
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"adr_jobs_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting ADR jobs");
            return StatusCode(500, "An error occurred while exporting ADR jobs");
        }
    }

    /// <summary>
    /// Creates a new ADR job for a specific account and billing period.
    /// </summary>
    /// <param name="request">The job creation request containing account ID and billing period dates.</param>
    /// <returns>The newly created ADR job.</returns>
    /// <response code="201">Returns the newly created ADR job.</response>
    /// <response code="400">ADR account not found.</response>
    /// <response code="409">A job already exists for this account and billing period.</response>
    /// <response code="500">An error occurred while creating the ADR job.</response>
    [HttpPost("jobs")]
    [ProducesResponseType(typeof(AdrJob), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrJob>> CreateJob([FromBody] CreateAdrJobRequest request)
    {
        try
        {
            var account = await _unitOfWork.AdrAccounts.GetByIdAsync(request.AdrAccountId);
            if (account == null)
            {
                return BadRequest("ADR Account not found");
            }

            var existingJob = await _unitOfWork.AdrJobs.ExistsForBillingPeriodAsync(
                request.AdrAccountId,
                request.BillingPeriodStartDateTime,
                request.BillingPeriodEndDateTime);

            if (existingJob)
            {
                return Conflict("A job already exists for this account and billing period");
            }

            var job = new AdrJob
            {
                AdrAccountId = request.AdrAccountId,
                VMAccountId = account.VMAccountId,
                VMAccountNumber = account.VMAccountNumber,
                CredentialId = account.CredentialId,
                PeriodType = account.PeriodType,
                BillingPeriodStartDateTime = request.BillingPeriodStartDateTime,
                BillingPeriodEndDateTime = request.BillingPeriodEndDateTime,
                NextRunDateTime = account.NextRunDateTime,
                NextRangeStartDateTime = account.NextRangeStartDateTime,
                NextRangeEndDateTime = account.NextRangeEndDateTime,
                Status = "Pending",
                IsMissing = false,
                RetryCount = 0,
                CreatedDateTime = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System Created",
                ModifiedDateTime = DateTime.UtcNow,
                ModifiedBy = User.Identity?.Name ?? "System Created"
            };

            await _unitOfWork.AdrJobs.AddAsync(job);
            await _unitOfWork.SaveChangesAsync();

            return CreatedAtAction(nameof(GetJob), new { id = job.Id }, job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ADR job");
            return StatusCode(500, "An error occurred while creating the ADR job");
        }
    }

    /// <summary>
    /// Updates the status of an ADR job.
    /// </summary>
    /// <param name="id">The unique identifier of the ADR job.</param>
    /// <param name="request">The status update request containing new status and optional ADR status details.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Job status was successfully updated.</response>
    /// <response code="404">ADR job with the specified ID was not found.</response>
    /// <response code="500">An error occurred while updating the ADR job status.</response>
    [HttpPut("jobs/{id}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateJobStatus(int id, [FromBody] UpdateJobStatusRequest request)
    {
        try
        {
            var job = await _unitOfWork.AdrJobs.GetByIdAsync(id);
            if (job == null)
            {
                return NotFound();
            }

            job.Status = request.Status;
            job.AdrStatusId = request.AdrStatusId;
            job.AdrStatusDescription = request.AdrStatusDescription;
            job.AdrIndexId = request.AdrIndexId;
            job.ErrorMessage = request.ErrorMessage;
            job.ModifiedDateTime = DateTime.UtcNow;
            job.ModifiedBy = User.Identity?.Name ?? "System Created";

            if (request.Status == "CredentialVerified")
            {
                job.CredentialVerifiedDateTime = DateTime.UtcNow;
            }
            else if (request.Status == "Completed" || request.Status == "Failed")
            {
                job.ScrapingCompletedDateTime = DateTime.UtcNow;
            }

            await _unitOfWork.AdrJobs.UpdateAsync(job);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ADR job status {JobId}", id);
            return StatusCode(500, "An error occurred while updating the ADR job status");
        }
    }

    /// <summary>
    /// Resets an ADR job to Pending status so it can be reprocessed by the orchestrator.
    /// </summary>
    /// <param name="id">The unique identifier of the ADR job.</param>
    /// <param name="forceRefire">If true, deletes execution history to bypass idempotency check.</param>
    /// <returns>Details about the refired job including any deleted execution records.</returns>
    /// <response code="200">Returns the refired job details.</response>
    /// <response code="404">ADR job with the specified ID was not found.</response>
    /// <response code="500">An error occurred while refiring the ADR job.</response>
    [HttpPost("jobs/{id}/refire")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> RefireJob(int id, [FromQuery] bool forceRefire = false)
    {
        try
        {
            var job = await _unitOfWork.AdrJobs.GetByIdAsync(id);
            if (job == null)
            {
                return NotFound();
            }

            var executionsDeleted = 0;
            if (forceRefire)
            {
                // Force refire: delete execution history to bypass idempotency check
                executionsDeleted = await _unitOfWork.AdrJobExecutions.DeleteByJobIdAsync(id);
                _logger.LogInformation("Force refire: deleted {Count} execution records for job {JobId}", executionsDeleted, id);
            }

            // Reset job to Pending status so it gets picked up by the orchestrator
            job.Status = "Pending";
            job.AdrStatusId = null;
            job.AdrStatusDescription = null;
            job.AdrIndexId = null;
            job.ErrorMessage = null;
            job.CredentialVerifiedDateTime = null;
            job.ScrapingCompletedDateTime = null;
            job.RetryCount = 0;
            job.ModifiedDateTime = DateTime.UtcNow;
            job.ModifiedBy = User.Identity?.Name ?? "System Created";

            await _unitOfWork.AdrJobs.UpdateAsync(job);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Job {JobId} refired by {User} (forceRefire={ForceRefire})", id, User.Identity?.Name ?? "Unknown", forceRefire);

            return Ok(new { message = forceRefire ? $"Job force refired successfully ({executionsDeleted} execution records cleared)" : "Job refired successfully", jobId = id, forceRefire, executionsDeleted });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refiring ADR job {JobId}", id);
            return StatusCode(500, "An error occurred while refiring the ADR job");
        }
    }

    /// <summary>
    /// Resets multiple ADR jobs to Pending status so they can be reprocessed by the orchestrator.
    /// </summary>
    /// <param name="request">The bulk refire request containing job IDs and force refire option.</param>
    /// <returns>Summary of the bulk refire operation including success count and any errors.</returns>
    /// <response code="200">Returns the bulk refire operation summary.</response>
    /// <response code="400">No job IDs were provided in the request.</response>
    /// <response code="500">An error occurred while refiring the ADR jobs.</response>
    [HttpPost("jobs/refire-bulk")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> RefireJobsBulk([FromBody] RefireJobsRequest request)
    {
        try
        {
            if (request.JobIds == null || !request.JobIds.Any())
            {
                return BadRequest("No job IDs provided");
            }

            var refiredCount = 0;
            var totalExecutionsDeleted = 0;
            var errors = new List<string>();

            foreach (var jobId in request.JobIds)
            {
                try
                {
                    var job = await _unitOfWork.AdrJobs.GetByIdAsync(jobId);
                    if (job == null)
                    {
                        errors.Add($"Job {jobId} not found");
                        continue;
                    }

                    if (request.ForceRefire)
                    {
                        // Force refire: delete execution history to bypass idempotency check
                        var executionsDeleted = await _unitOfWork.AdrJobExecutions.DeleteByJobIdAsync(jobId);
                        totalExecutionsDeleted += executionsDeleted;
                    }

                    // Reset job to Pending status so it gets picked up by the orchestrator
                    job.Status = "Pending";
                    job.AdrStatusId = null;
                    job.AdrStatusDescription = null;
                    job.AdrIndexId = null;
                    job.ErrorMessage = null;
                    job.CredentialVerifiedDateTime = null;
                    job.ScrapingCompletedDateTime = null;
                    job.RetryCount = 0;
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = User.Identity?.Name ?? "System Created";

                    await _unitOfWork.AdrJobs.UpdateAsync(job);
                    refiredCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Job {jobId}: {ex.Message}");
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Bulk refire: {Count} jobs refired by {User} (forceRefire={ForceRefire}, executionsDeleted={ExecutionsDeleted})", 
                refiredCount, User.Identity?.Name ?? "Unknown", request.ForceRefire, totalExecutionsDeleted);

            var message = request.ForceRefire 
                ? $"{refiredCount} job(s) force refired successfully ({totalExecutionsDeleted} execution records cleared)"
                : $"{refiredCount} job(s) refired successfully";

            return Ok(new
            {
                message,
                refiredCount,
                totalRequested = request.JobIds.Count,
                forceRefire = request.ForceRefire,
                executionsDeleted = totalExecutionsDeleted,
                errors = errors.Any() ? errors : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk job refire");
            return StatusCode(500, "An error occurred while refiring jobs");
        }
    }

    #endregion

    #region AdrJobExecution Endpoints

    /// <summary>
    /// Retrieves a paginated list of ADR job executions with optional filtering.
    /// </summary>
    /// <param name="adrJobId">Optional ADR job ID to filter executions.</param>
    /// <param name="adrRequestTypeId">Optional request type ID to filter executions.</param>
    /// <param name="isSuccess">Optional filter for successful/failed executions.</param>
    /// <param name="pageNumber">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20).</param>
    /// <returns>A paginated list of ADR job executions.</returns>
    /// <response code="200">Returns the paginated list of ADR job executions.</response>
    /// <response code="500">An error occurred while retrieving ADR job executions.</response>
    [HttpGet("executions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetExecutions(
        [FromQuery] int? adrJobId = null,
        [FromQuery] int? adrRequestTypeId = null,
        [FromQuery] bool? isSuccess = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var (items, totalCount) = await _unitOfWork.AdrJobExecutions.GetPagedAsync(
                pageNumber,
                pageSize,
                adrJobId,
                adrRequestTypeId,
                isSuccess);

            return Ok(new
            {
                items,
                totalCount,
                pageNumber,
                pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR job executions");
            return StatusCode(500, "An error occurred while retrieving ADR job executions");
        }
    }

    /// <summary>
    /// Retrieves a specific ADR job execution by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the ADR job execution.</param>
    /// <returns>The ADR job execution with the specified ID.</returns>
    /// <response code="200">Returns the ADR job execution.</response>
    /// <response code="404">ADR job execution with the specified ID was not found.</response>
    /// <response code="500">An error occurred while retrieving the ADR job execution.</response>
    [HttpGet("executions/{id}")]
    [ProducesResponseType(typeof(AdrJobExecution), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrJobExecution>> GetExecution(int id)
    {
        try
        {
            var execution = await _unitOfWork.AdrJobExecutions.GetByIdAsync(id);
            if (execution == null)
            {
                return NotFound();
            }

            return Ok(execution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR job execution {ExecutionId}", id);
            return StatusCode(500, "An error occurred while retrieving the ADR job execution");
        }
    }

    /// <summary>
    /// Retrieves all ADR job executions for a specific job.
    /// </summary>
    /// <param name="adrJobId">The unique identifier of the ADR job.</param>
    /// <returns>A list of ADR job executions for the specified job.</returns>
    /// <response code="200">Returns the list of ADR job executions.</response>
    /// <response code="500">An error occurred while retrieving ADR job executions.</response>
    [HttpGet("executions/by-job/{adrJobId}")]
    [ProducesResponseType(typeof(IEnumerable<AdrJobExecution>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<AdrJobExecution>>> GetExecutionsByJob(int adrJobId)
    {
        try
        {
            var executions = await _unitOfWork.AdrJobExecutions.GetByJobIdAsync(adrJobId);
            return Ok(executions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR job executions for job {JobId}", adrJobId);
            return StatusCode(500, "An error occurred while retrieving ADR job executions");
        }
    }

    /// <summary>
    /// Creates a new ADR job execution record for tracking API calls.
    /// </summary>
    /// <param name="request">The execution creation request containing job ID and request type.</param>
    /// <returns>The newly created ADR job execution.</returns>
    /// <response code="201">Returns the newly created ADR job execution.</response>
    /// <response code="400">ADR job not found.</response>
    /// <response code="500">An error occurred while creating the ADR job execution.</response>
    [HttpPost("executions")]
    [ProducesResponseType(typeof(AdrJobExecution), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrJobExecution>> CreateExecution([FromBody] CreateAdrJobExecutionRequest request)
    {
        try
        {
            var job = await _unitOfWork.AdrJobs.GetByIdAsync(request.AdrJobId);
            if (job == null)
            {
                return BadRequest("ADR Job not found");
            }

            var execution = new AdrJobExecution
            {
                AdrJobId = request.AdrJobId,
                AdrRequestTypeId = request.AdrRequestTypeId,
                StartDateTime = DateTime.UtcNow,
                CreatedDateTime = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System Created",
                ModifiedDateTime = DateTime.UtcNow,
                ModifiedBy = User.Identity?.Name ?? "System Created"
            };

            await _unitOfWork.AdrJobExecutions.AddAsync(execution);
            await _unitOfWork.SaveChangesAsync();

            return CreatedAtAction(nameof(GetExecution), new { id = execution.Id }, execution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ADR job execution");
            return StatusCode(500, "An error occurred while creating the ADR job execution");
        }
    }

    /// <summary>
    /// Completes an ADR job execution with the API response details.
    /// </summary>
    /// <param name="id">The unique identifier of the ADR job execution.</param>
    /// <param name="request">The completion request containing status, response details, and error information.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Execution was successfully completed.</response>
    /// <response code="404">ADR job execution with the specified ID was not found.</response>
    /// <response code="500">An error occurred while completing the ADR job execution.</response>
    [HttpPut("executions/{id}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CompleteExecution(int id, [FromBody] CompleteExecutionRequest request)
    {
        try
        {
            var execution = await _unitOfWork.AdrJobExecutions.GetByIdAsync(id);
            if (execution == null)
            {
                return NotFound();
            }

            execution.EndDateTime = DateTime.UtcNow;
            execution.AdrStatusId = request.AdrStatusId;
            execution.AdrStatusDescription = request.AdrStatusDescription;
            execution.AdrIndexId = request.AdrIndexId;
            execution.HttpStatusCode = request.HttpStatusCode;
            execution.IsSuccess = request.IsSuccess;
            execution.IsError = request.IsError;
            execution.IsFinal = request.IsFinal;
            execution.ErrorMessage = request.ErrorMessage;
            execution.ApiResponse = request.ApiResponse;
            execution.ModifiedDateTime = DateTime.UtcNow;
            execution.ModifiedBy = User.Identity?.Name ?? "System Created";

            await _unitOfWork.AdrJobExecutions.UpdateAsync(execution);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing ADR job execution {ExecutionId}", id);
            return StatusCode(500, "An error occurred while completing the ADR job execution");
        }
    }

    #endregion

    #region ADR Status Reference

    /// <summary>
    /// Retrieves all ADR status codes with their descriptions and metadata.
    /// </summary>
    /// <returns>A list of ADR status codes with ID, name, description, and flags for error/final states.</returns>
    /// <response code="200">Returns the list of ADR status codes.</response>
    [HttpGet("statuses")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<object>> GetAdrStatuses()
    {
        var statuses = Enum.GetValues<AdrStatus>()
            .Select(s => new
            {
                id = (int)s,
                name = s.ToString(),
                description = s.GetDescription(),
                isError = s.IsError(),
                isFinal = s.IsFinal()
            });

        return Ok(statuses);
    }

    /// <summary>
    /// Retrieves all ADR request types (credential check, download invoice, status check).
    /// </summary>
    /// <returns>A list of ADR request types with ID and name.</returns>
    /// <response code="200">Returns the list of ADR request types.</response>
    [HttpGet("request-types")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<object>> GetAdrRequestTypes()
    {
        var types = Enum.GetValues<AdrRequestType>()
            .Select(t => new
            {
                id = (int)t,
                name = t.ToString()
            });

        return Ok(types);
    }

    #endregion

        #region Orchestration Endpoints

    /// <summary>
    /// Triggers a manual sync of ADR accounts from the external VendorCred system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The sync result including counts of added, updated, and unchanged accounts.</returns>
    /// <response code="200">Returns the account sync result.</response>
    /// <response code="500">An error occurred during account sync.</response>
    [HttpPost("sync/accounts")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
    [ProducesResponseType(typeof(AdrAccountSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrAccountSyncResult>> SyncAccounts(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Manual account sync triggered by {User}", User.Identity?.Name ?? "Unknown");
            var result = await _syncService.SyncAccountsAsync(null, null, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual account sync");
            return StatusCode(500, new { error = "An error occurred during account sync", message = ex.Message });
        }
    }

    /// <summary>
    /// Triggers manual job creation for ADR accounts that are due for processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The job creation result including counts of jobs created and skipped.</returns>
    /// <response code="200">Returns the job creation result.</response>
    /// <response code="500">An error occurred during job creation.</response>
    [HttpPost("orchestrate/create-jobs")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
    [ProducesResponseType(typeof(JobCreationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCreationResult>> CreateJobs(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Manual job creation triggered by {User}", User.Identity?.Name ?? "Unknown");
            var result = await _orchestratorService.CreateJobsForDueAccountsAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual job creation");
            return StatusCode(500, new { error = "An error occurred during job creation", message = ex.Message });
        }
    }

    /// <summary>
    /// Triggers manual credential verification for ADR jobs approaching their NextRunDate.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The credential verification result including counts of verified and failed credentials.</returns>
    /// <response code="200">Returns the credential verification result.</response>
    /// <response code="500">An error occurred during credential verification.</response>
    [HttpPost("orchestrate/verify-credentials")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
    [ProducesResponseType(typeof(CredentialVerificationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CredentialVerificationResult>> VerifyCredentials(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Manual credential verification triggered by {User}", User.Identity?.Name ?? "Unknown");
            var result = await _orchestratorService.VerifyCredentialsAsync(null, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual credential verification");
            return StatusCode(500, new { error = "An error occurred during credential verification", message = ex.Message });
        }
    }

    /// <summary>
    /// Triggers manual ADR request processing for jobs that are ready (credential verified and at NextRunDate).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The ADR request result including counts of requests sent and failed.</returns>
    /// <response code="200">Returns the ADR request processing result.</response>
    /// <response code="500">An error occurred during ADR request processing.</response>
    [HttpPost("orchestrate/process-scraping")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
    [ProducesResponseType(typeof(ScrapeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScrapeResult>> ProcessScraping(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Manual scraping triggered by {User}", User.Identity?.Name ?? "Unknown");
            var result = await _orchestratorService.ProcessScrapingAsync(null, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual scraping");
            return StatusCode(500, new { error = "An error occurred during scraping", message = ex.Message });
        }
    }

    /// <summary>
    /// Triggers manual status check for all ADR jobs that have been sent for processing.
    /// This checks ALL scraped jobs regardless of timing criteria since status checks have no cost.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The status check result including counts of completed, failed, and still processing jobs.</returns>
    /// <response code="200">Returns the status check result.</response>
    /// <response code="500">An error occurred during status check.</response>
    [HttpPost("orchestrate/check-statuses")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
    [ProducesResponseType(typeof(StatusCheckResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StatusCheckResult>> CheckStatuses(CancellationToken cancellationToken)
    {
        try
        {
            // Manual status check: Check ALL scraped jobs regardless of timing criteria
            // This is used by the "Check Statuses Only" button since there's no cost to check status
            _logger.LogInformation("Manual status check (all scraped jobs) triggered by {User}", User.Identity?.Name ?? "Unknown");
            var result = await _orchestratorService.CheckAllScrapedStatusesAsync(null, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual status check");
            return StatusCode(500, new { error = "An error occurred during status check", message = ex.Message });
        }
    }

    /// <summary>
    /// Runs the complete ADR orchestration cycle: sync accounts, create jobs, verify credentials, check statuses, and process ADR requests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Combined results from all orchestration steps.</returns>
    /// <response code="200">Returns the combined orchestration results.</response>
    /// <response code="500">An error occurred during the full ADR cycle.</response>
    [HttpPost("orchestrate/run-full-cycle")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> RunFullCycle(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Full ADR cycle triggered by {User}", User.Identity?.Name ?? "Unknown");

            // Step 1: Sync accounts from VendorCred
            var syncResult = await _syncService.SyncAccountsAsync(null, null, cancellationToken);
            
            // Step 2: Create jobs for accounts due for processing
            var jobCreationResult = await _orchestratorService.CreateJobsForDueAccountsAsync(cancellationToken);
            
            // Step 3: Verify credentials for jobs approaching their NextRunDate
            var credentialResult = await _orchestratorService.VerifyCredentialsAsync(null, cancellationToken);
            
            // Step 4: Check status of yesterday's ScrapeRequested jobs BEFORE sending new scrapes
            // This prevents duplicate scrape requests for jobs that already completed
            var statusResult = await _orchestratorService.CheckPendingStatusesAsync(null, cancellationToken);
            
            // Step 5: Send scrape requests for jobs that are ready (CredentialVerified status)
            var scrapeResult = await _orchestratorService.ProcessScrapingAsync(null, cancellationToken);

            return Ok(new
            {
                syncResult,
                jobCreationResult,
                credentialResult,
                statusResult,
                scrapeResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during full ADR cycle");
            return StatusCode(500, new { error = "An error occurred during full ADR cycle", message = ex.Message });
        }
    }

    #endregion

    #region Background Orchestration Endpoints

    /// <summary>
    /// Triggers ADR orchestration to run in the background. Returns immediately with a request ID
    /// that can be used to check status. This endpoint does NOT depend on user session - the
    /// background job will continue running even if the user logs out.
    /// </summary>
    /// <param name="request">Optional request specifying which orchestration steps to run.</param>
    /// <returns>The queued request details including request ID for status tracking.</returns>
    /// <response code="200">Returns the queued orchestration request details.</response>
    /// <response code="500">An error occurred while queuing the orchestration.</response>
    [HttpPost("orchestrate/run-background")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> RunBackgroundOrchestration([FromBody] BackgroundOrchestrationRequest? request = null)
    {
        try
        {
            var orchestrationRequest = new AdrOrchestrationRequest
            {
                RequestedBy = User.Identity?.Name ?? "Unknown",
                RunSync = request?.RunSync ?? true,
                RunCreateJobs = request?.RunCreateJobs ?? true,
                RunCredentialVerification = request?.RunCredentialVerification ?? true,
                RunScraping = request?.RunScraping ?? true,
                RunStatusCheck = request?.RunStatusCheck ?? true,
                CheckAllScrapedStatuses = request?.CheckAllScrapedStatuses ?? false
            };

            await _orchestrationQueue.QueueAsync(orchestrationRequest);

            _logger.LogInformation(
                "Background ADR orchestration queued with request ID {RequestId} by {User}",
                orchestrationRequest.RequestId, orchestrationRequest.RequestedBy);

            return Ok(new
            {
                message = "ADR orchestration queued successfully. The job will run in the background.",
                requestId = orchestrationRequest.RequestId,
                requestedAt = orchestrationRequest.RequestedAt,
                requestedBy = orchestrationRequest.RequestedBy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing background ADR orchestration");
            return StatusCode(500, new { error = "An error occurred while queuing ADR orchestration", message = ex.Message });
        }
    }

    /// <summary>
    /// Gets the status of a specific background orchestration request.
    /// </summary>
    /// <param name="requestId">The unique request ID returned when the orchestration was queued.</param>
    /// <returns>The orchestration status including progress and results.</returns>
    /// <response code="200">Returns the orchestration status.</response>
    /// <response code="404">The specified request ID was not found.</response>
    [HttpGet("orchestrate/status/{requestId}")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
    [ProducesResponseType(typeof(AdrOrchestrationStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AdrOrchestrationStatus> GetOrchestrationStatus(string requestId)
    {
        var status = _orchestrationQueue.GetStatus(requestId);
        if (status == null)
        {
            return NotFound(new { error = "Request not found", requestId });
        }

        return Ok(status);
    }

    /// <summary>
    /// Gets the status of the currently running orchestration, if any.
    /// </summary>
    /// <returns>The current orchestration status or a message indicating no orchestration is running.</returns>
    /// <response code="200">Returns the current orchestration status or indicates no orchestration is running.</response>
    [HttpGet("orchestrate/current")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult<object> GetCurrentOrchestration()
    {
        var current = _orchestrationQueue.GetCurrentRun();
        if (current == null)
        {
            return Ok(new { isRunning = false, message = "No orchestration is currently running" });
        }

        return Ok(new { isRunning = true, status = current });
    }

    /// <summary>
    /// Cancels a running or queued orchestration request.
    /// Available to Editors, Admins, and Super Admins.
    /// </summary>
    [HttpPost("orchestrate/{requestId}/cancel")]
    [Authorize(Policy = "AdrAccounts.Update")]
    public ActionResult<object> CancelOrchestration(string requestId)
    {
        try
        {
            var status = _orchestrationQueue.GetStatus(requestId);
            if (status == null)
            {
                return NotFound(new { error = "Request not found", requestId });
            }

            // Can only cancel Running or Queued requests
            if (status.Status != "Running" && status.Status != "Queued" && status.Status != "Cancelling")
            {
                return BadRequest(new { 
                    error = "Cannot cancel request", 
                    requestId, 
                    currentStatus = status.Status,
                    message = $"Request is already {status.Status} and cannot be cancelled"
                });
            }

            var cancelled = _orchestrationQueue.CancelRequest(requestId);
            if (cancelled)
            {
                _logger.LogInformation("Orchestration request {RequestId} cancellation initiated by {User}", 
                    requestId, User.Identity?.Name ?? "Unknown");
                
                return Ok(new { 
                    success = true, 
                    message = "Cancellation initiated. The orchestration will stop after the current operation completes.",
                    requestId,
                    status = _orchestrationQueue.GetStatus(requestId)
                });
            }
            else
            {
                return BadRequest(new { 
                    error = "Failed to cancel request", 
                    requestId,
                    message = "The request may have already been cancelled or completed"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling orchestration request {RequestId}", requestId);
            return StatusCode(500, new { error = "An error occurred while cancelling the orchestration", message = ex.Message });
        }
    }

    /// <summary>
    /// Gets the recent orchestration run history from database.
    /// Falls back to in-memory if database is unavailable.
    /// Supports pagination with pageNumber and pageSize parameters.
    /// </summary>
    /// <param name="count">Optional legacy parameter for limiting results (deprecated, use pageSize instead).</param>
    /// <param name="pageNumber">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20).</param>
    /// <returns>A paginated list of orchestration run history.</returns>
    /// <response code="200">Returns the orchestration history.</response>
    /// <response code="500">An error occurred while retrieving orchestration history.</response>
    [HttpGet("orchestrate/history")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
    [ProducesResponseType(typeof(OrchestrationHistoryPagedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetOrchestrationHistory(
        [FromQuery] int? count = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // Get the configurable max orchestration duration from AdrConfiguration
            // Default to 240 minutes (4 hours) if not configured
            var config = await _dbContext.AdrConfigurations.FirstOrDefaultAsync(c => !c.IsDeleted);
            var maxDurationMinutes = config?.MaxOrchestrationDurationMinutes ?? 240;
            
            // First, detect and fix stale "Running" records (running longer than configured max duration without completion)
            var staleThreshold = DateTime.UtcNow.AddMinutes(-maxDurationMinutes);
            var staleRuns = await _dbContext.AdrOrchestrationRuns
                .Where(r => !r.IsDeleted && r.Status == "Running" && r.StartedDateTime.HasValue && r.StartedDateTime < staleThreshold && r.CompletedDateTime == null)
                .ToListAsync();
            
            if (staleRuns.Any())
            {
                foreach (var staleRun in staleRuns)
                {
                    staleRun.Status = "Failed";
                    staleRun.CompletedDateTime = DateTime.UtcNow;
                    staleRun.ErrorMessage = $"Orchestration run exceeded maximum expected duration ({maxDurationMinutes} minutes) and was marked as failed. The process may have crashed or been terminated unexpectedly.";
                    staleRun.ModifiedDateTime = DateTime.UtcNow;
                    staleRun.ModifiedBy = "System";
                    _logger.LogWarning("Marking stale orchestration run {RequestId} as Failed - started at {StartedAt}, exceeded {MaxDuration} minute threshold", 
                        staleRun.RequestId, staleRun.StartedDateTime, maxDurationMinutes);
                }
                await _dbContext.SaveChangesAsync();
            }
            
            // Try to get history from database first
            var query = _dbContext.AdrOrchestrationRuns
                .Where(r => !r.IsDeleted)
                .OrderByDescending(r => r.RequestedDateTime);
            
            // Get total count for pagination
            var totalCount = await query.CountAsync();
            
            // Apply pagination or simple count limit
            IQueryable<AdrOrchestrationRun> pagedQuery;
            if (count.HasValue)
            {
                // Legacy mode: just return count items (no pagination info)
                pagedQuery = query.Take(count.Value);
            }
            else
            {
                // Pagination mode
                pagedQuery = query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize);
            }
            
            var dbHistory = await pagedQuery
                .Select(r => new AdrOrchestrationStatus
                {
                    RequestId = r.RequestId,
                    RequestedBy = r.RequestedBy,
                    RequestedAt = r.RequestedDateTime,
                    StartedAt = r.StartedDateTime,
                    CompletedAt = r.CompletedDateTime,
                    Status = r.Status,
                    CurrentStep = r.CurrentStep,
                    ErrorMessage = r.ErrorMessage,
                    SyncResult = r.SyncAccountsInserted.HasValue ? new AdrAccountSyncResult
                    {
                        AccountsInserted = r.SyncAccountsInserted ?? 0,
                        AccountsUpdated = r.SyncAccountsUpdated ?? 0
                    } : null,
                    JobCreationResult = r.JobsCreated.HasValue ? new JobCreationResult
                    {
                        JobsCreated = r.JobsCreated ?? 0,
                        JobsSkipped = r.JobsSkipped ?? 0
                    } : null,
                    CredentialVerificationResult = r.CredentialsVerified.HasValue ? new CredentialVerificationResult
                    {
                        CredentialsVerified = r.CredentialsVerified ?? 0,
                        CredentialsFailed = r.CredentialsFailed ?? 0
                    } : null,
                    ScrapeResult = r.ScrapingRequested.HasValue ? new ScrapeResult
                    {
                        ScrapesRequested = r.ScrapingRequested ?? 0,
                        ScrapesFailed = r.ScrapingFailed ?? 0
                    } : null,
                    StatusCheckResult = r.StatusesChecked.HasValue ? new StatusCheckResult
                    {
                        JobsCompleted = r.StatusesChecked ?? 0,
                        JobsNeedingReview = r.StatusesFailed ?? 0
                    } : null
                })
                .ToListAsync();
            
            if (dbHistory.Any())
            {
                // Return with pagination info if not using legacy count mode
                if (!count.HasValue)
                {
                    return Ok(new OrchestrationHistoryPagedResponse
                    {
                        Items = dbHistory,
                        TotalCount = totalCount,
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                    });
                }
                return Ok(dbHistory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get orchestration history from database, falling back to in-memory");
        }
        
        // Fall back to in-memory statuses
        var statuses = _orchestrationQueue.GetRecentStatuses(count ?? 100);
        return Ok(statuses);
    }

        #endregion

        #region AdrAccountRule Endpoints

        /// <summary>
        /// Retrieves a paginated list of account rules with optional filtering and sorting.
        /// </summary>
        /// <param name="page">Page number (default: 1).</param>
        /// <param name="pageSize">Number of items per page (default: 20).</param>
        /// <param name="vendorCode">Filter by vendor code.</param>
        /// <param name="accountNumber">Filter by account number.</param>
        /// <param name="isEnabled">Filter by enabled status.</param>
        /// <param name="isOverridden">Filter by override status.</param>
        /// <param name="sortColumn">Column to sort by (default: VendorCode).</param>
        /// <param name="sortDescending">Sort in descending order (default: false).</param>
        /// <returns>A paginated list of account rules.</returns>
        /// <response code="200">Returns the paginated list of account rules.</response>
        /// <response code="500">An error occurred while retrieving account rules.</response>
        [HttpGet("rules")]
        [ProducesResponseType(typeof(RulesPagedResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<RulesPagedResponse>> GetRules(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? vendorCode = null,
            [FromQuery] string? accountNumber = null,
            [FromQuery] bool? isEnabled = null,
            [FromQuery] bool? isOverridden = null,
            [FromQuery] string? sortColumn = null,
            [FromQuery] bool sortDescending = false)
        {
            try
            {
                var query = _dbContext.AdrAccountRules
                    .Include(r => r.AdrAccount)
                    .Where(r => !r.IsDeleted);
            
                if (!string.IsNullOrWhiteSpace(vendorCode))
                {
                    query = query.Where(r => r.AdrAccount != null && r.AdrAccount.VendorCode != null && 
                        r.AdrAccount.VendorCode.Contains(vendorCode));
                }
            
                if (!string.IsNullOrWhiteSpace(accountNumber))
                {
                    query = query.Where(r => r.AdrAccount != null && r.AdrAccount.VMAccountNumber != null && 
                        r.AdrAccount.VMAccountNumber.Contains(accountNumber));
                }
            
                if (isEnabled.HasValue)
                {
                    query = query.Where(r => r.IsEnabled == isEnabled.Value);
                }

                if (isOverridden.HasValue)
                {
                    query = query.Where(r => r.IsManuallyOverridden == isOverridden.Value);
                }
            
                var totalCount = await query.CountAsync();

                // Apply sorting
                IOrderedQueryable<SchedulerPlatform.Core.Domain.Entities.AdrAccountRule> orderedQuery;
                switch (sortColumn?.ToLowerInvariant())
                {
                    case "vmaccountnumber":
                    case "accountnumber":
                        orderedQuery = sortDescending 
                            ? query.OrderByDescending(r => r.AdrAccount != null ? r.AdrAccount.VMAccountNumber : "")
                            : query.OrderBy(r => r.AdrAccount != null ? r.AdrAccount.VMAccountNumber : "");
                        break;
                    case "periodtype":
                        orderedQuery = sortDescending 
                            ? query.OrderByDescending(r => r.PeriodType)
                            : query.OrderBy(r => r.PeriodType);
                        break;
                    case "nextrundatetime":
                    case "nextrun":
                        orderedQuery = sortDescending 
                            ? query.OrderByDescending(r => r.NextRunDateTime)
                            : query.OrderBy(r => r.NextRunDateTime);
                        break;
                    case "isenabled":
                    case "status":
                        orderedQuery = sortDescending 
                            ? query.OrderByDescending(r => r.IsEnabled)
                            : query.OrderBy(r => r.IsEnabled);
                        break;
                    case "ismanuallyoverridden":
                    case "override":
                        orderedQuery = sortDescending 
                            ? query.OrderByDescending(r => r.IsManuallyOverridden)
                            : query.OrderBy(r => r.IsManuallyOverridden);
                        break;
                    case "jobtypeid":
                    case "jobtype":
                        orderedQuery = sortDescending 
                            ? query.OrderByDescending(r => r.JobTypeId)
                            : query.OrderBy(r => r.JobTypeId);
                        break;
                    case "vendorcode":
                    default:
                        orderedQuery = sortDescending 
                            ? query.OrderByDescending(r => r.AdrAccount != null ? r.AdrAccount.VendorCode : "")
                            : query.OrderBy(r => r.AdrAccount != null ? r.AdrAccount.VendorCode : "");
                        break;
                }
            
                var rules = await orderedQuery
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new AccountRuleDto
                    {
                        Id = r.Id,
                        AdrAccountId = r.AdrAccountId,
                        VendorCode = r.AdrAccount != null ? r.AdrAccount.VendorCode : null,
                        VMAccountNumber = r.AdrAccount != null ? r.AdrAccount.VMAccountNumber : null,
                        JobTypeId = r.JobTypeId,
                        PeriodType = r.PeriodType,
                        PeriodDays = r.PeriodDays,
                        NextRunDateTime = r.NextRunDateTime,
                        NextRangeStartDateTime = r.NextRangeStartDateTime,
                        NextRangeEndDateTime = r.NextRangeEndDateTime,
                        IsEnabled = r.IsEnabled,
                        IsManuallyOverridden = r.IsManuallyOverridden,
                        OverriddenBy = r.OverriddenBy,
                        OverriddenDateTime = r.OverriddenDateTime
                    })
                    .ToListAsync();
            
                return Ok(new RulesPagedResponse
                {
                    Items = rules,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ADR account rules");
                return StatusCode(500, "An error occurred while retrieving ADR account rules");
            }
        }

        /// <summary>
        /// Exports account rules to Excel or CSV format.
        /// </summary>
        /// <param name="vendorCode">Filter by vendor code.</param>
        /// <param name="accountNumber">Filter by account number.</param>
        /// <param name="isEnabled">Filter by enabled status.</param>
        /// <param name="isOverridden">Filter by override status.</param>
        /// <param name="format">Export format: 'excel' or 'csv' (default: excel).</param>
        /// <returns>File download with the exported rules.</returns>
        /// <response code="200">Returns the exported file.</response>
        /// <response code="500">An error occurred while exporting rules.</response>
        [HttpGet("rules/export")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportRules(
            [FromQuery] string? vendorCode = null,
            [FromQuery] string? accountNumber = null,
            [FromQuery] bool? isEnabled = null,
            [FromQuery] bool? isOverridden = null,
            [FromQuery] string format = "excel")
        {
            try
            {
                var query = _dbContext.AdrAccountRules
                    .Include(r => r.AdrAccount)
                    .Where(r => !r.IsDeleted);
            
                if (!string.IsNullOrWhiteSpace(vendorCode))
                {
                    query = query.Where(r => r.AdrAccount != null && r.AdrAccount.VendorCode != null && 
                        r.AdrAccount.VendorCode.Contains(vendorCode));
                }
            
                if (!string.IsNullOrWhiteSpace(accountNumber))
                {
                    query = query.Where(r => r.AdrAccount != null && r.AdrAccount.VMAccountNumber != null && 
                        r.AdrAccount.VMAccountNumber.Contains(accountNumber));
                }
            
                if (isEnabled.HasValue)
                {
                    query = query.Where(r => r.IsEnabled == isEnabled.Value);
                }

                if (isOverridden.HasValue)
                {
                    query = query.Where(r => r.IsManuallyOverridden == isOverridden.Value);
                }
            
                var rules = await query
                    .OrderBy(r => r.AdrAccount != null ? r.AdrAccount.VendorCode : "")
                    .ThenBy(r => r.AdrAccount != null ? r.AdrAccount.VMAccountNumber : "")
                    .Select(r => new
                    {
                        VendorCode = r.AdrAccount != null ? r.AdrAccount.VendorCode : null,
                        VMAccountNumber = r.AdrAccount != null ? r.AdrAccount.VMAccountNumber : null,
                        r.JobTypeId,
                        r.PeriodType,
                        r.PeriodDays,
                        r.NextRunDateTime,
                        r.NextRangeStartDateTime,
                        r.NextRangeEndDateTime,
                        r.IsEnabled,
                        r.IsManuallyOverridden,
                        r.OverriddenBy,
                        r.OverriddenDateTime
                    })
                    .ToListAsync();

                var headers = new[] { "Vendor Code", "Account Number", "Job Type", "Period Type", "Period Days", "Next Run", "Search Window Start", "Search Window End", "Enabled", "Overridden", "Overridden By", "Overridden Date" };

                if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
                {
                    var csvBytes = ExcelExportHelper.CreateCsvExport(
                        string.Join(",", headers),
                        rules,
                        r => $"{ExcelExportHelper.CsvEscape(r.VendorCode)},{ExcelExportHelper.CsvEscape(r.VMAccountNumber)},{r.JobTypeId},{ExcelExportHelper.CsvEscape(r.PeriodType)},{r.PeriodDays},{r.NextRunDateTime?.ToString("MM/dd/yyyy") ?? ""},{r.NextRangeStartDateTime?.ToString("MM/dd/yyyy") ?? ""},{r.NextRangeEndDateTime?.ToString("MM/dd/yyyy") ?? ""},{(r.IsEnabled ? "Yes" : "No")},{(r.IsManuallyOverridden ? "Yes" : "No")},{ExcelExportHelper.CsvEscape(r.OverriddenBy)},{r.OverriddenDateTime?.ToString("MM/dd/yyyy HH:mm") ?? ""}");
                    return File(csvBytes, "text/csv", $"adr_rules_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
                }

                // Excel format using centralized helper
                var excelBytes = ExcelExportHelper.CreateExcelExport(
                    "ADR Rules",
                    "AdrRulesTable",
                    headers,
                    rules,
                    r => new object?[]
                    {
                        r.VendorCode ?? "",
                        r.VMAccountNumber ?? "",
                        r.JobTypeId,
                        r.PeriodType ?? "",
                        r.PeriodDays ?? 0,
                        r.NextRunDateTime,
                        r.NextRangeStartDateTime,
                        r.NextRangeEndDateTime,
                        r.IsEnabled,
                        r.IsManuallyOverridden,
                        r.OverriddenBy ?? "",
                        r.OverriddenDateTime
                    });
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"adr_rules_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting ADR rules");
                return StatusCode(500, "An error occurred while exporting ADR rules");
            }
        }

        /// <summary>
        /// Retrieves account rules by account ID.
        /// </summary>
        /// <param name="accountId">The account ID.</param>
        /// <returns>List of account rules for the specified account.</returns>
        /// <response code="200">Returns the list of account rules.</response>
        /// <response code="500">An error occurred while retrieving the rules.</response>
        [HttpGet("rules/by-account/{accountId}")]
        [ProducesResponseType(typeof(List<AccountRuleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<AccountRuleDto>>> GetRulesByAccount(int accountId)
        {
            try
            {
                var rules = await _dbContext.AdrAccountRules
                    .Include(r => r.AdrAccount)
                    .Where(r => r.AdrAccountId == accountId && !r.IsDeleted)
                    .Select(r => new AccountRuleDto
                    {
                        Id = r.Id,
                        AdrAccountId = r.AdrAccountId,
                        VendorCode = r.AdrAccount != null ? r.AdrAccount.VendorCode : null,
                        VMAccountNumber = r.AdrAccount != null ? r.AdrAccount.VMAccountNumber : null,
                        JobTypeId = r.JobTypeId,
                        PeriodType = r.PeriodType,
                        PeriodDays = r.PeriodDays,
                        NextRunDateTime = r.NextRunDateTime,
                        NextRangeStartDateTime = r.NextRangeStartDateTime,
                        NextRangeEndDateTime = r.NextRangeEndDateTime,
                        IsEnabled = r.IsEnabled,
                        IsManuallyOverridden = r.IsManuallyOverridden,
                        OverriddenBy = r.OverriddenBy,
                        OverriddenDateTime = r.OverriddenDateTime
                    })
                    .ToListAsync();

                return Ok(rules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ADR account rules for account {AccountId}", accountId);
                return StatusCode(500, "An error occurred while retrieving account rules");
            }
        }

        /// <summary>
        /// Retrieves a single account rule by ID.
        /// </summary>
        /// <param name="id">The rule ID.</param>
        /// <returns>The account rule.</returns>
        /// <response code="200">Returns the account rule.</response>
        /// <response code="404">Rule not found.</response>
        /// <response code="500">An error occurred while retrieving the rule.</response>
        [HttpGet("rules/{id}")]
        [ProducesResponseType(typeof(AccountRuleDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AccountRuleDto>> GetRule(int id)
        {
            try
            {
                var rule = await _dbContext.AdrAccountRules
                    .Include(r => r.AdrAccount)
                    .Where(r => r.Id == id && !r.IsDeleted)
                    .Select(r => new AccountRuleDto
                    {
                        Id = r.Id,
                        AdrAccountId = r.AdrAccountId,
                        VendorCode = r.AdrAccount != null ? r.AdrAccount.VendorCode : null,
                        VMAccountNumber = r.AdrAccount != null ? r.AdrAccount.VMAccountNumber : null,
                        JobTypeId = r.JobTypeId,
                        PeriodType = r.PeriodType,
                        PeriodDays = r.PeriodDays,
                        NextRunDateTime = r.NextRunDateTime,
                        NextRangeStartDateTime = r.NextRangeStartDateTime,
                        NextRangeEndDateTime = r.NextRangeEndDateTime,
                        IsEnabled = r.IsEnabled,
                        IsManuallyOverridden = r.IsManuallyOverridden,
                        OverriddenBy = r.OverriddenBy,
                        OverriddenDateTime = r.OverriddenDateTime
                    })
                    .FirstOrDefaultAsync();

                if (rule == null)
                {
                    return NotFound($"Rule with ID {id} not found");
                }

                return Ok(rule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ADR account rule {RuleId}", id);
                return StatusCode(500, "An error occurred while retrieving the account rule");
            }
        }

        /// <summary>
        /// Updates an account rule's scheduling configuration.
        /// </summary>
        /// <param name="id">The rule ID.</param>
        /// <param name="request">The update request containing new values.</param>
        /// <returns>The updated account rule.</returns>
        /// <response code="200">Returns the updated account rule.</response>
        /// <response code="400">Invalid request data.</response>
        /// <response code="404">Rule not found.</response>
        /// <response code="500">An error occurred while updating the rule.</response>
        [HttpPut("rules/{id}")]
        [Authorize(Policy = "AdrAccounts.Update")]
        [ProducesResponseType(typeof(AccountRuleDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AccountRuleDto>> UpdateRule(int id, [FromBody] UpdateRuleRequest request)
        {
            try
            {
                var rule = await _dbContext.AdrAccountRules
                    .Include(r => r.AdrAccount)
                    .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

                if (rule == null)
                {
                    return NotFound($"Rule with ID {id} not found");
                }

                // Update fields
                if (request.NextRunDateTime.HasValue)
                    rule.NextRunDateTime = request.NextRunDateTime.Value;
                
                if (request.NextRangeStartDateTime.HasValue)
                    rule.NextRangeStartDateTime = request.NextRangeStartDateTime.Value;
                
                if (request.NextRangeEndDateTime.HasValue)
                    rule.NextRangeEndDateTime = request.NextRangeEndDateTime.Value;
                
                if (!string.IsNullOrEmpty(request.PeriodType))
                {
                    rule.PeriodType = request.PeriodType;
                    // Auto-calculate PeriodDays from PeriodType for backward compatibility
                    // This ensures PeriodDays stays in sync even though the UI no longer edits it directly
                    rule.PeriodDays = BillingPeriodCalculator.GetApproximatePeriodDays(request.PeriodType);
                }
                
                if (request.JobTypeId.HasValue)
                    rule.JobTypeId = request.JobTypeId.Value;
                
                if (request.IsEnabled.HasValue)
                    rule.IsEnabled = request.IsEnabled.Value;

                // Mark as manually overridden
                rule.IsManuallyOverridden = true;
                rule.OverriddenBy = User.Identity?.Name ?? "Unknown";
                rule.OverriddenDateTime = DateTime.UtcNow;
                rule.ModifiedDateTime = DateTime.UtcNow;
                rule.ModifiedBy = User.Identity?.Name ?? "Unknown";

                await _dbContext.SaveChangesAsync();

                // Return updated rule
                var updatedRule = new AccountRuleDto
                {
                    Id = rule.Id,
                    AdrAccountId = rule.AdrAccountId,
                    VendorCode = rule.AdrAccount?.VendorCode,
                    VMAccountNumber = rule.AdrAccount?.VMAccountNumber,
                    JobTypeId = rule.JobTypeId,
                    PeriodType = rule.PeriodType,
                    PeriodDays = rule.PeriodDays,
                    NextRunDateTime = rule.NextRunDateTime,
                    NextRangeStartDateTime = rule.NextRangeStartDateTime,
                    NextRangeEndDateTime = rule.NextRangeEndDateTime,
                    IsEnabled = rule.IsEnabled,
                    IsManuallyOverridden = rule.IsManuallyOverridden,
                    OverriddenBy = rule.OverriddenBy,
                    OverriddenDateTime = rule.OverriddenDateTime
                };

                return Ok(updatedRule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ADR account rule {RuleId}", id);
                return StatusCode(500, "An error occurred while updating the account rule");
            }
        }

        /// <summary>
        /// Clears the manual override on an account rule, allowing it to be updated by sync.
        /// </summary>
        /// <param name="id">The rule ID.</param>
        /// <returns>The updated account rule.</returns>
        /// <response code="200">Returns the updated account rule.</response>
        /// <response code="404">Rule not found.</response>
        /// <response code="500">An error occurred while clearing the override.</response>
        [HttpPost("rules/{id}/clear-override")]
        [Authorize(Policy = "AdrAccounts.Update")]
        [ProducesResponseType(typeof(AccountRuleDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AccountRuleDto>> ClearRuleOverride(int id)
        {
            try
            {
                var rule = await _dbContext.AdrAccountRules
                    .Include(r => r.AdrAccount)
                    .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

                if (rule == null)
                {
                    return NotFound($"Rule with ID {id} not found");
                }

                rule.IsManuallyOverridden = false;
                rule.OverriddenBy = null;
                rule.OverriddenDateTime = null;
                rule.ModifiedDateTime = DateTime.UtcNow;
                rule.ModifiedBy = User.Identity?.Name ?? "Unknown";

                await _dbContext.SaveChangesAsync();

                var updatedRule = new AccountRuleDto
                {
                    Id = rule.Id,
                    AdrAccountId = rule.AdrAccountId,
                    VendorCode = rule.AdrAccount?.VendorCode,
                    VMAccountNumber = rule.AdrAccount?.VMAccountNumber,
                    JobTypeId = rule.JobTypeId,
                    PeriodType = rule.PeriodType,
                    PeriodDays = rule.PeriodDays,
                    NextRunDateTime = rule.NextRunDateTime,
                    NextRangeStartDateTime = rule.NextRangeStartDateTime,
                    NextRangeEndDateTime = rule.NextRangeEndDateTime,
                    IsEnabled = rule.IsEnabled,
                    IsManuallyOverridden = rule.IsManuallyOverridden,
                    OverriddenBy = rule.OverriddenBy,
                    OverriddenDateTime = rule.OverriddenDateTime
                };

                return Ok(updatedRule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing override on ADR account rule {RuleId}", id);
                return StatusCode(500, "An error occurred while clearing the override");
            }
        }

        #endregion

        #region AdrConfiguration Endpoints (Admin/Super Admin only)

    /// <summary>
    /// Retrieves the current ADR configuration settings.
    /// Only Admin and Super Admin users can access this endpoint.
    /// </summary>
    /// <returns>The current ADR configuration or default values if not configured.</returns>
    /// <response code="200">Returns the ADR configuration.</response>
    /// <response code="500">An error occurred while retrieving the configuration.</response>
    [HttpGet("configuration")]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(typeof(AdrConfiguration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrConfiguration>> GetConfiguration()
    {
        try
        {
            var configs = await _unitOfWork.AdrConfigurations.FindAsync(c => !c.IsDeleted);
            var config = configs.FirstOrDefault();
            
            if (config == null)
            {
                // Return default configuration if none exists
                config = new AdrConfiguration
                {
                    CredentialCheckLeadDays = 7,
                    ScrapeRetryDays = 5,
                    MaxRetries = 5,
                    FinalStatusCheckDelayDays = 5,
                    DailyStatusCheckDelayDays = 1,
                    MaxParallelRequests = 8,
                    BatchSize = 1000,
                    DefaultWindowDaysBefore = 5,
                    DefaultWindowDaysAfter = 5,
                    AutoCreateTestLoginRules = true,
                    AutoCreateMissingInvoiceAlerts = true,
                    IsOrchestrationEnabled = true
                };
            }
            
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR configuration");
            return StatusCode(500, "An error occurred while retrieving ADR configuration");
        }
    }

    /// <summary>
    /// Updates the ADR configuration settings.
    /// Only Admin and Super Admin users can access this endpoint.
    /// </summary>
    /// <param name="request">The configuration update request.</param>
    /// <returns>The updated ADR configuration.</returns>
    /// <response code="200">Returns the updated ADR configuration.</response>
    /// <response code="400">Invalid configuration values provided.</response>
    /// <response code="500">An error occurred while updating the configuration.</response>
    [HttpPut("configuration")]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(typeof(AdrConfiguration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrConfiguration>> UpdateConfiguration([FromBody] UpdateAdrConfigurationRequest request)
    {
        try
        {
            var username = User.Identity?.Name ?? "System Created";
            var configs = await _unitOfWork.AdrConfigurations.FindAsync(c => !c.IsDeleted);
            var config = configs.FirstOrDefault();
            
            if (config == null)
            {
                // Create new configuration
                config = new AdrConfiguration
                {
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedBy = username,
                    ModifiedDateTime = DateTime.UtcNow,
                    ModifiedBy = username
                };
                await _unitOfWork.AdrConfigurations.AddAsync(config);
            }
            
            // Update configuration values
            config.CredentialCheckLeadDays = request.CredentialCheckLeadDays ?? config.CredentialCheckLeadDays;
            config.ScrapeRetryDays = request.ScrapeRetryDays ?? config.ScrapeRetryDays;
            config.MaxRetries = request.MaxRetries ?? config.MaxRetries;
            config.FinalStatusCheckDelayDays = request.FinalStatusCheckDelayDays ?? config.FinalStatusCheckDelayDays;
            config.DailyStatusCheckDelayDays = request.DailyStatusCheckDelayDays ?? config.DailyStatusCheckDelayDays;
            config.MaxParallelRequests = request.MaxParallelRequests ?? config.MaxParallelRequests;
            config.BatchSize = request.BatchSize ?? config.BatchSize;
            config.DefaultWindowDaysBefore = request.DefaultWindowDaysBefore ?? config.DefaultWindowDaysBefore;
            config.DefaultWindowDaysAfter = request.DefaultWindowDaysAfter ?? config.DefaultWindowDaysAfter;
            config.AutoCreateTestLoginRules = request.AutoCreateTestLoginRules ?? config.AutoCreateTestLoginRules;
            config.AutoCreateMissingInvoiceAlerts = request.AutoCreateMissingInvoiceAlerts ?? config.AutoCreateMissingInvoiceAlerts;
            config.MissingInvoiceAlertEmail = request.MissingInvoiceAlertEmail ?? config.MissingInvoiceAlertEmail;
            config.IsOrchestrationEnabled = request.IsOrchestrationEnabled ?? config.IsOrchestrationEnabled;
            config.Notes = request.Notes ?? config.Notes;
            config.MaxOrchestrationDurationMinutes = request.MaxOrchestrationDurationMinutes ?? config.MaxOrchestrationDurationMinutes;
            config.DatabaseCommandTimeoutSeconds = request.DatabaseCommandTimeoutSeconds ?? config.DatabaseCommandTimeoutSeconds;
            config.ModifiedDateTime = DateTime.UtcNow;
            config.ModifiedBy = username;
            
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("ADR configuration updated by {User}", username);
            
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ADR configuration");
            return StatusCode(500, "An error occurred while updating ADR configuration");
        }
    }

    #endregion

    #region AdrAccountBlacklist Endpoints (Admin/Super Admin only)

    /// <summary>
    /// Retrieves a paginated list of blacklist entries with sorting support.
    /// Only Admin and Super Admin users can access this endpoint.
    /// </summary>
    /// <param name="pageNumber">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 50).</param>
    /// <param name="status">Filter by status: "current" (active now), "future" (starts in future), "expired" (end date passed), "inactive" (manually disabled), "all" (default).</param>
    /// <param name="vendorCode">Optional filter by vendor code.</param>
    /// <param name="accountNumber">Optional filter by account number.</param>
    /// <param name="isActive">Optional filter by active status.</param>
    /// <param name="sortColumn">Column to sort by: VendorCode, VMAccountNumber, EffectiveStartDate, EffectiveEndDate, CreatedDateTime, IsActive (default: EffectiveEndDate).</param>
    /// <param name="sortDescending">Whether to sort in descending order (default: true).</param>
    /// <returns>A paginated list of blacklist entries.</returns>
    /// <response code="200">Returns the list of blacklist entries.</response>
    /// <response code="500">An error occurred while retrieving blacklist entries.</response>
    [HttpGet("blacklist")]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetBlacklist(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? status = null,
        [FromQuery] string? vendorCode = null,
        [FromQuery] string? accountNumber = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string sortColumn = "EffectiveEndDate",
        [FromQuery] bool sortDescending = true)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var query = _dbContext.AdrAccountBlacklists
                .Where(b => !b.IsDeleted);
            
            // Apply status filter
            switch (status?.ToLowerInvariant())
            {
                case "current":
                    // Active now: IsActive=true AND (start is null or <= today) AND (end is null or >= today)
                    query = query.Where(b => b.IsActive &&
                        (!b.EffectiveStartDate.HasValue || b.EffectiveStartDate.Value <= today) &&
                        (!b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today));
                    break;
                case "future":
                    // Future: IsActive=true AND start date is in the future
                    query = query.Where(b => b.IsActive &&
                        b.EffectiveStartDate.HasValue && b.EffectiveStartDate.Value > today);
                    break;
                case "expired":
                    // Expired: end date has passed (regardless of IsActive)
                    query = query.Where(b => b.EffectiveEndDate.HasValue && b.EffectiveEndDate.Value < today);
                    break;
                case "inactive":
                    // Manually disabled
                    query = query.Where(b => !b.IsActive);
                    break;
                case "all":
                default:
                    // Show all entries, but apply isActive filter if provided
                    if (isActive.HasValue)
                    {
                        query = query.Where(b => b.IsActive == isActive.Value);
                    }
                    break;
            }
            
            // Apply additional filters
            if (!string.IsNullOrWhiteSpace(vendorCode))
            {
                query = query.Where(b => b.VendorCode != null && b.VendorCode.Contains(vendorCode));
            }
            if (!string.IsNullOrWhiteSpace(accountNumber))
            {
                query = query.Where(b => b.VMAccountNumber != null && b.VMAccountNumber.Contains(accountNumber));
            }
            
            var totalCount = await query.CountAsync();
            
            // Apply sorting based on sortColumn parameter
            IOrderedQueryable<AdrAccountBlacklist> orderedQuery = sortColumn.ToLowerInvariant() switch
            {
                "vendorcode" => sortDescending ? query.OrderByDescending(b => b.VendorCode) : query.OrderBy(b => b.VendorCode),
                "vmaccountnumber" => sortDescending ? query.OrderByDescending(b => b.VMAccountNumber) : query.OrderBy(b => b.VMAccountNumber),
                "effectivestartdate" => sortDescending ? query.OrderByDescending(b => b.EffectiveStartDate) : query.OrderBy(b => b.EffectiveStartDate),
                "effectiveenddate" => sortDescending ? query.OrderByDescending(b => b.EffectiveEndDate) : query.OrderBy(b => b.EffectiveEndDate),
                "createddatetime" => sortDescending ? query.OrderByDescending(b => b.CreatedDateTime) : query.OrderBy(b => b.CreatedDateTime),
                "isactive" => sortDescending ? query.OrderByDescending(b => b.IsActive) : query.OrderBy(b => b.IsActive),
                _ => sortDescending ? query.OrderByDescending(b => b.EffectiveEndDate) : query.OrderBy(b => b.EffectiveEndDate) // Default to EffectiveEndDate
            };
            
            var items = await orderedQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            return Ok(new
            {
                items,
                totalCount,
                pageNumber,
                pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR blacklist entries");
            return StatusCode(500, "An error occurred while retrieving ADR blacklist entries");
        }
    }

    /// <summary>
    /// Gets the count of currently active blacklist entries.
    /// Used by the ADR Monitor page to display blacklist summary.
    /// Available to all authenticated users (Viewers and above).
    /// </summary>
    /// <returns>Count of current and future blacklist entries.</returns>
    /// <response code="200">Returns the blacklist counts.</response>
    /// <response code="500">An error occurred while retrieving blacklist counts.</response>
    [HttpGet("blacklist/counts")]
    [Authorize]
    [ProducesResponseType(typeof(BlacklistCountsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BlacklistCountsResult>> GetBlacklistCounts()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            
            // Count current blacklists (active now)
            var currentCount = await _dbContext.AdrAccountBlacklists
                .Where(b => !b.IsDeleted && b.IsActive)
                .Where(b => !b.EffectiveStartDate.HasValue || b.EffectiveStartDate.Value <= today)
                .Where(b => !b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today)
                .CountAsync();
            
            // Count future blacklists
            var futureCount = await _dbContext.AdrAccountBlacklists
                .Where(b => !b.IsDeleted && b.IsActive)
                .Where(b => b.EffectiveStartDate.HasValue && b.EffectiveStartDate.Value > today)
                .CountAsync();
            
            return Ok(new BlacklistCountsResult
            {
                CurrentCount = currentCount,
                FutureCount = futureCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving blacklist counts");
            return StatusCode(500, "An error occurred while retrieving blacklist counts");
        }
    }

    /// <summary>
    /// Checks blacklist status for a batch of accounts or jobs.
    /// Returns current and future blacklist information for each item.
    /// Available to all authenticated users (Viewers and above).
    /// </summary>
    /// <param name="request">The batch of items to check for blacklist status.</param>
    /// <returns>Blacklist status for each requested item.</returns>
    /// <response code="200">Returns the blacklist status for each item.</response>
    /// <response code="500">An error occurred while checking blacklist status.</response>
    [HttpPost("blacklist/check")]
    [Authorize]
    [ProducesResponseType(typeof(List<BlacklistStatusResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<BlacklistStatusResult>>> CheckBlacklistStatus([FromBody] BlacklistCheckRequest request)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            
            // Fetch all active blacklist entries (current and future)
            var allBlacklists = await _dbContext.AdrAccountBlacklists
                .Where(b => !b.IsDeleted && b.IsActive)
                .Where(b => !b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today)
                .ToListAsync();
            
            var results = new List<BlacklistStatusResult>();
            
            foreach (var item in request.Items)
            {
                var matchingBlacklists = allBlacklists.Where(b =>
                {
                    // Match by VendorCode
                    if (!string.IsNullOrEmpty(b.VendorCode) && b.VendorCode == item.VendorCode)
                        return true;
                    // Match by VMAccountId
                    if (b.VMAccountId.HasValue && b.VMAccountId == item.VMAccountId)
                        return true;
                    // Match by VMAccountNumber
                    if (!string.IsNullOrEmpty(b.VMAccountNumber) && b.VMAccountNumber == item.VMAccountNumber)
                        return true;
                    // Match by CredentialId
                    if (b.CredentialId.HasValue && b.CredentialId == item.CredentialId)
                        return true;
                    return false;
                }).ToList();
                
                var currentBlacklists = matchingBlacklists
                    .Where(b => (!b.EffectiveStartDate.HasValue || b.EffectiveStartDate.Value <= today) &&
                                (!b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today))
                    .Select(b => new BlacklistSummary
                    {
                        Id = b.Id,
                        Reason = b.Reason,
                        ExclusionType = b.ExclusionType,
                        EffectiveStartDate = b.EffectiveStartDate,
                        EffectiveEndDate = b.EffectiveEndDate,
                        VendorCode = b.VendorCode,
                        VMAccountId = b.VMAccountId,
                        VMAccountNumber = b.VMAccountNumber,
                        CredentialId = b.CredentialId
                    })
                    .ToList();
                
                var futureBlacklists = matchingBlacklists
                    .Where(b => b.EffectiveStartDate.HasValue && b.EffectiveStartDate.Value > today)
                    .Select(b => new BlacklistSummary
                    {
                        Id = b.Id,
                        Reason = b.Reason,
                        ExclusionType = b.ExclusionType,
                        EffectiveStartDate = b.EffectiveStartDate,
                        EffectiveEndDate = b.EffectiveEndDate,
                        VendorCode = b.VendorCode,
                        VMAccountId = b.VMAccountId,
                        VMAccountNumber = b.VMAccountNumber,
                        CredentialId = b.CredentialId
                    })
                    .ToList();
                
                results.Add(new BlacklistStatusResult
                {
                    AccountId = item.AccountId,
                    JobId = item.JobId,
                    HasCurrentBlacklist = currentBlacklists.Any(),
                    HasFutureBlacklist = futureBlacklists.Any(),
                    CurrentBlacklistCount = currentBlacklists.Count,
                    FutureBlacklistCount = futureBlacklists.Count,
                    CurrentBlacklists = currentBlacklists,
                    FutureBlacklists = futureBlacklists
                });
            }
            
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking blacklist status");
            return StatusCode(500, "An error occurred while checking blacklist status");
        }
    }

    /// <summary>
    /// Retrieves a specific blacklist entry by ID.
    /// Only Admin and Super Admin users can access this endpoint.
    /// </summary>
    /// <param name="id">The unique identifier of the blacklist entry.</param>
    /// <returns>The blacklist entry with the specified ID.</returns>
    /// <response code="200">Returns the requested blacklist entry.</response>
    /// <response code="404">Blacklist entry with the specified ID was not found.</response>
    /// <response code="500">An error occurred while retrieving the blacklist entry.</response>
    [HttpGet("blacklist/{id}")]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(typeof(AdrAccountBlacklist), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrAccountBlacklist>> GetBlacklistEntry(int id)
    {
        try
        {
            var entry = await _unitOfWork.AdrAccountBlacklists.GetByIdAsync(id);
            if (entry == null || entry.IsDeleted)
            {
                return NotFound();
            }
            
            return Ok(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR blacklist entry {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the ADR blacklist entry");
        }
    }

    /// <summary>
    /// Creates a new blacklist entry.
    /// Only Admin and Super Admin users can access this endpoint.
    /// </summary>
    /// <param name="request">The blacklist entry creation request.</param>
    /// <returns>The created blacklist entry.</returns>
    /// <response code="201">Returns the created blacklist entry.</response>
    /// <response code="400">Invalid blacklist entry data provided.</response>
    /// <response code="500">An error occurred while creating the blacklist entry.</response>
    [HttpPost("blacklist")]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(typeof(AdrAccountBlacklist), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrAccountBlacklist>> CreateBlacklistEntry([FromBody] CreateBlacklistEntryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest("Reason is required for blacklist entries");
            }
            
            // At least one exclusion criteria must be provided
            if (string.IsNullOrWhiteSpace(request.VendorCode) && 
                !request.VMAccountId.HasValue && 
                string.IsNullOrWhiteSpace(request.VMAccountNumber) && 
                !request.CredentialId.HasValue)
            {
                return BadRequest("At least one exclusion criteria (VendorCode, VMAccountId, VMAccountNumber, or CredentialId) must be provided");
            }
            
            // Both effective dates are required
            if (!request.EffectiveStartDate.HasValue)
            {
                return BadRequest("Effective Start Date is required");
            }
            
            if (!request.EffectiveEndDate.HasValue)
            {
                return BadRequest("Effective End Date is required");
            }
            
            if (request.EffectiveEndDate.Value < request.EffectiveStartDate.Value)
            {
                return BadRequest("Effective End Date must be on or after Effective Start Date");
            }
            
            var username = User.Identity?.Name ?? "System Created";
            
            var entry = new AdrAccountBlacklist
            {
                VendorCode = request.VendorCode,
                VMAccountId = request.VMAccountId,
                VMAccountNumber = request.VMAccountNumber,
                CredentialId = request.CredentialId,
                ExclusionType = request.ExclusionType ?? "All",
                Reason = request.Reason,
                IsActive = true,
                EffectiveStartDate = request.EffectiveStartDate,
                EffectiveEndDate = request.EffectiveEndDate,
                BlacklistedBy = username,
                BlacklistedDateTime = DateTime.UtcNow,
                Notes = request.Notes,
                CreatedDateTime = DateTime.UtcNow,
                CreatedBy = username,
                ModifiedDateTime = DateTime.UtcNow,
                ModifiedBy = username
            };
            
            await _unitOfWork.AdrAccountBlacklists.AddAsync(entry);
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Blacklist entry {Id} created by {User}. VendorCode: {VendorCode}, VMAccountId: {VMAccountId}, Reason: {Reason}",
                entry.Id, username, request.VendorCode, request.VMAccountId, request.Reason);
            
            return CreatedAtAction(nameof(GetBlacklistEntry), new { id = entry.Id }, entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ADR blacklist entry");
            return StatusCode(500, "An error occurred while creating the ADR blacklist entry");
        }
    }

    /// <summary>
    /// Updates an existing blacklist entry.
    /// Only Admin and Super Admin users can access this endpoint.
    /// </summary>
    /// <param name="id">The unique identifier of the blacklist entry.</param>
    /// <param name="request">The blacklist entry update request.</param>
    /// <returns>The updated blacklist entry.</returns>
    /// <response code="200">Returns the updated blacklist entry.</response>
    /// <response code="400">Invalid blacklist entry data provided.</response>
    /// <response code="404">Blacklist entry with the specified ID was not found.</response>
    /// <response code="500">An error occurred while updating the blacklist entry.</response>
    [HttpPut("blacklist/{id}")]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(typeof(AdrAccountBlacklist), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdrAccountBlacklist>> UpdateBlacklistEntry(int id, [FromBody] UpdateBlacklistEntryRequest request)
    {
        try
        {
            var entry = await _unitOfWork.AdrAccountBlacklists.GetByIdAsync(id);
            if (entry == null || entry.IsDeleted)
            {
                return NotFound();
            }
            
            var username = User.Identity?.Name ?? "System Created";
            
            // Update fields if provided
            if (request.VendorCode != null) entry.VendorCode = request.VendorCode;
            if (request.VMAccountId.HasValue) entry.VMAccountId = request.VMAccountId;
            if (request.VMAccountNumber != null) entry.VMAccountNumber = request.VMAccountNumber;
            if (request.CredentialId.HasValue) entry.CredentialId = request.CredentialId;
            if (request.ExclusionType != null) entry.ExclusionType = request.ExclusionType;
            if (request.Reason != null) entry.Reason = request.Reason;
            if (request.IsActive.HasValue) entry.IsActive = request.IsActive.Value;
            if (request.EffectiveStartDate.HasValue) entry.EffectiveStartDate = request.EffectiveStartDate;
            if (request.EffectiveEndDate.HasValue) entry.EffectiveEndDate = request.EffectiveEndDate;
            if (request.Notes != null) entry.Notes = request.Notes;
            
            // Validate at least one exclusion criteria exists after update
            if (string.IsNullOrWhiteSpace(entry.VendorCode) && 
                !entry.VMAccountId.HasValue && 
                string.IsNullOrWhiteSpace(entry.VMAccountNumber) && 
                !entry.CredentialId.HasValue)
            {
                return BadRequest("At least one exclusion criteria (VendorCode, VMAccountId, VMAccountNumber, or CredentialId) must be provided");
            }
            
            // Validate both effective dates are set
            if (!entry.EffectiveStartDate.HasValue)
            {
                return BadRequest("Effective Start Date is required");
            }
            
            if (!entry.EffectiveEndDate.HasValue)
            {
                return BadRequest("Effective End Date is required");
            }
            
            if (entry.EffectiveEndDate.Value < entry.EffectiveStartDate.Value)
            {
                return BadRequest("Effective End Date must be on or after Effective Start Date");
            }
            
            entry.ModifiedDateTime = DateTime.UtcNow;
            entry.ModifiedBy = username;
            
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Blacklist entry {Id} updated by {User}", id, username);
            
            return Ok(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ADR blacklist entry {Id}", id);
            return StatusCode(500, "An error occurred while updating the ADR blacklist entry");
        }
    }

    /// <summary>
    /// Soft deletes a blacklist entry.
    /// Only Admin and Super Admin users can access this endpoint.
    /// </summary>
    /// <param name="id">The unique identifier of the blacklist entry.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Blacklist entry was successfully deleted.</response>
    /// <response code="404">Blacklist entry with the specified ID was not found.</response>
    /// <response code="500">An error occurred while deleting the blacklist entry.</response>
    [HttpDelete("blacklist/{id}")]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteBlacklistEntry(int id)
    {
        try
        {
            var entry = await _unitOfWork.AdrAccountBlacklists.GetByIdAsync(id);
            if (entry == null || entry.IsDeleted)
            {
                return NotFound();
            }
            
            var username = User.Identity?.Name ?? "System Created";
            
            // Soft delete
            entry.IsDeleted = true;
            entry.IsActive = false;
            entry.ModifiedDateTime = DateTime.UtcNow;
            entry.ModifiedBy = username;
            
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Blacklist entry {Id} deleted by {User}", id, username);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting ADR blacklist entry {Id}", id);
            return StatusCode(500, "An error occurred while deleting the ADR blacklist entry");
        }
    }

    /// <summary>
    /// Exports blacklist entries to Excel format.
    /// Only Admin and Super Admin users can access this endpoint.
    /// </summary>
    /// <param name="status">Filter by status: "current", "future", "expired", "inactive", or "all" (default).</param>
    /// <param name="vendorCode">Optional filter by vendor code.</param>
    /// <param name="accountNumber">Optional filter by account number.</param>
    /// <returns>Excel file containing blacklist entries.</returns>
    /// <response code="200">Returns the Excel file.</response>
    /// <response code="500">An error occurred while exporting blacklist entries.</response>
    [HttpGet("blacklist/export")]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> ExportBlacklist(
        [FromQuery] string? status = null,
        [FromQuery] string? vendorCode = null,
        [FromQuery] string? accountNumber = null)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var query = _dbContext.AdrAccountBlacklists
                .Where(b => !b.IsDeleted);
            
            // Apply status filter (same logic as GetBlacklist)
            switch (status?.ToLowerInvariant())
            {
                case "current":
                    query = query.Where(b => b.IsActive &&
                        (!b.EffectiveStartDate.HasValue || b.EffectiveStartDate.Value <= today) &&
                        (!b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today));
                    break;
                case "future":
                    query = query.Where(b => b.IsActive &&
                        b.EffectiveStartDate.HasValue && b.EffectiveStartDate.Value > today);
                    break;
                case "expired":
                    query = query.Where(b => b.EffectiveEndDate.HasValue && b.EffectiveEndDate.Value < today);
                    break;
                case "inactive":
                    query = query.Where(b => !b.IsActive);
                    break;
                // "all" or default: no additional filter
            }
            
            // Apply additional filters
            if (!string.IsNullOrWhiteSpace(vendorCode))
            {
                query = query.Where(b => b.VendorCode != null && b.VendorCode.Contains(vendorCode));
            }
            if (!string.IsNullOrWhiteSpace(accountNumber))
            {
                query = query.Where(b => b.VMAccountNumber != null && b.VMAccountNumber.Contains(accountNumber));
            }
            
            var entries = await query
                .OrderByDescending(b => b.CreatedDateTime)
                .ToListAsync();
            
            var headers = new[]
            {
                "ID", "Vendor Code", "VM Account ID", "Account Number", "Credential ID",
                "Exclusion Type", "Reason", "Is Active", "Effective Start", "Effective End",
                "Blacklisted By", "Blacklisted Date", "Notes", "Created By", "Created Date"
            };
            
            var excelBytes = Services.ExcelExportHelper.CreateExcelExport(
                "Blacklist",
                "BlacklistTable",
                headers,
                entries,
                entry => new object?[]
                {
                    entry.Id,
                    entry.VendorCode,
                    entry.VMAccountId,
                    entry.VMAccountNumber,
                    entry.CredentialId,
                    entry.ExclusionType,
                    entry.Reason,
                    entry.IsActive,
                    entry.EffectiveStartDate,
                    entry.EffectiveEndDate,
                    entry.BlacklistedBy,
                    entry.BlacklistedDateTime,
                    entry.Notes,
                    entry.CreatedBy,
                    entry.CreatedDateTime
                });
            
            var fileName = $"ADR_Blacklist_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting ADR blacklist entries");
            return StatusCode(500, "An error occurred while exporting ADR blacklist entries");
        }
    }

        #endregion

        #region AdrJobType Endpoints (Admin/Super Admin only)

        /// <summary>
        /// Retrieves all active job types.
        /// Only Admin and Super Admin users can access this endpoint.
        /// </summary>
        /// <param name="includeInactive">Whether to include inactive job types (default: false).</param>
        /// <returns>A list of job types.</returns>
        /// <response code="200">Returns the list of job types.</response>
        /// <response code="500">An error occurred while retrieving job types.</response>
        [HttpGet("job-types")]
        [Authorize(Policy = "Users.Manage.Read")]
        [ProducesResponseType(typeof(IEnumerable<AdrJobType>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<AdrJobType>>> GetJobTypes([FromQuery] bool includeInactive = false)
        {
            try
            {
                var query = _dbContext.AdrJobTypes
                    .Where(jt => !jt.IsDeleted);
            
                if (!includeInactive)
                {
                    query = query.Where(jt => jt.IsActive);
                }
            
                var jobTypes = await query
                    .OrderBy(jt => jt.DisplayOrder)
                    .ToListAsync();
            
                return Ok(jobTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ADR job types");
                return StatusCode(500, "An error occurred while retrieving ADR job types");
            }
        }

        /// <summary>
        /// Retrieves a specific job type by ID.
        /// Only Admin and Super Admin users can access this endpoint.
        /// </summary>
        /// <param name="id">The unique identifier of the job type.</param>
        /// <returns>The job type with the specified ID.</returns>
        /// <response code="200">Returns the requested job type.</response>
        /// <response code="404">Job type with the specified ID was not found.</response>
        /// <response code="500">An error occurred while retrieving the job type.</response>
        [HttpGet("job-types/{id}")]
        [Authorize(Policy = "Users.Manage.Read")]
        [ProducesResponseType(typeof(AdrJobType), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AdrJobType>> GetJobType(int id)
        {
            try
            {
                var jobType = await _dbContext.AdrJobTypes
                    .FirstOrDefaultAsync(jt => jt.Id == id && !jt.IsDeleted);
            
                if (jobType == null)
                {
                    return NotFound($"Job type with ID {id} was not found.");
                }
            
                return Ok(jobType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ADR job type {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the ADR job type");
            }
        }

        /// <summary>
        /// Creates a new job type.
        /// Only Admin and Super Admin users can access this endpoint.
        /// </summary>
        /// <param name="request">The job type creation request.</param>
        /// <returns>The created job type.</returns>
        /// <response code="201">Returns the created job type.</response>
        /// <response code="400">Invalid job type data provided or code already exists.</response>
        /// <response code="500">An error occurred while creating the job type.</response>
        [HttpPost("job-types")]
        [Authorize(Policy = "Users.Manage.Read")]
        [ProducesResponseType(typeof(AdrJobType), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AdrJobType>> CreateJobType([FromBody] CreateJobTypeRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Code))
                {
                    return BadRequest("Job type code is required.");
                }
            
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest("Job type name is required.");
                }
            
                // Check if code already exists
                var existingJobType = await _dbContext.AdrJobTypes
                    .FirstOrDefaultAsync(jt => jt.Code == request.Code && !jt.IsDeleted);
            
                if (existingJobType != null)
                {
                    return BadRequest($"A job type with code '{request.Code}' already exists.");
                }
            
                var username = User.Identity?.Name ?? "System";
            
                var jobType = new AdrJobType
                {
                    Code = request.Code.ToUpperInvariant(),
                    Name = request.Name,
                    Description = request.Description,
                    EndpointUrl = request.EndpointUrl,
                    AdrRequestTypeId = request.AdrRequestTypeId,
                    IsActive = request.IsActive ?? true,
                    DisplayOrder = request.DisplayOrder ?? 0,
                    Notes = request.Notes,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedBy = username,
                    ModifiedDateTime = DateTime.UtcNow,
                    ModifiedBy = username
                };
            
                _dbContext.AdrJobTypes.Add(jobType);
                await _dbContext.SaveChangesAsync();
            
                _logger.LogInformation("Job type {Code} created by {User}", jobType.Code, username);
            
                return CreatedAtAction(nameof(GetJobType), new { id = jobType.Id }, jobType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ADR job type");
                return StatusCode(500, "An error occurred while creating the ADR job type");
            }
        }

        /// <summary>
        /// Updates an existing job type.
        /// Only Admin and Super Admin users can access this endpoint.
        /// </summary>
        /// <param name="id">The unique identifier of the job type to update.</param>
        /// <param name="request">The job type update request.</param>
        /// <returns>The updated job type.</returns>
        /// <response code="200">Returns the updated job type.</response>
        /// <response code="400">Invalid job type data provided or code already exists.</response>
        /// <response code="404">Job type with the specified ID was not found.</response>
        /// <response code="500">An error occurred while updating the job type.</response>
        [HttpPut("job-types/{id}")]
        [Authorize(Policy = "Users.Manage.Read")]
        [ProducesResponseType(typeof(AdrJobType), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AdrJobType>> UpdateJobType(int id, [FromBody] UpdateJobTypeRequest request)
        {
            try
            {
                var jobType = await _dbContext.AdrJobTypes
                    .FirstOrDefaultAsync(jt => jt.Id == id && !jt.IsDeleted);
            
                if (jobType == null)
                {
                    return NotFound($"Job type with ID {id} was not found.");
                }
            
                // Check if code is being changed and if new code already exists
                if (!string.IsNullOrWhiteSpace(request.Code) && request.Code != jobType.Code)
                {
                    var existingJobType = await _dbContext.AdrJobTypes
                        .FirstOrDefaultAsync(jt => jt.Code == request.Code && !jt.IsDeleted && jt.Id != id);
                
                    if (existingJobType != null)
                    {
                        return BadRequest($"A job type with code '{request.Code}' already exists.");
                    }
                
                    jobType.Code = request.Code.ToUpperInvariant();
                }
            
                var username = User.Identity?.Name ?? "System";
            
                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    jobType.Name = request.Name;
                }
            
                if (request.Description != null)
                {
                    jobType.Description = request.Description;
                }
            
                if (request.EndpointUrl != null)
                {
                    jobType.EndpointUrl = request.EndpointUrl;
                }
            
                if (request.AdrRequestTypeId.HasValue)
                {
                    jobType.AdrRequestTypeId = request.AdrRequestTypeId.Value;
                }
            
                if (request.IsActive.HasValue)
                {
                    jobType.IsActive = request.IsActive.Value;
                }
            
                if (request.DisplayOrder.HasValue)
                {
                    jobType.DisplayOrder = request.DisplayOrder.Value;
                }
            
                if (request.Notes != null)
                {
                    jobType.Notes = request.Notes;
                }
            
                jobType.ModifiedDateTime = DateTime.UtcNow;
                jobType.ModifiedBy = username;
            
                await _dbContext.SaveChangesAsync();
            
                _logger.LogInformation("Job type {Id} ({Code}) updated by {User}", id, jobType.Code, username);
            
                return Ok(jobType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ADR job type {Id}", id);
                return StatusCode(500, "An error occurred while updating the ADR job type");
            }
        }

        /// <summary>
        /// Soft deletes a job type.
        /// Only Admin and Super Admin users can access this endpoint.
        /// Note: Job types that are referenced by existing rules cannot be deleted.
        /// </summary>
        /// <param name="id">The unique identifier of the job type to delete.</param>
        /// <returns>No content on success.</returns>
        /// <response code="204">Job type was successfully deleted.</response>
        /// <response code="400">Job type is referenced by existing rules and cannot be deleted.</response>
        /// <response code="404">Job type with the specified ID was not found.</response>
        /// <response code="500">An error occurred while deleting the job type.</response>
        [HttpDelete("job-types/{id}")]
        [Authorize(Policy = "Users.Manage.Read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteJobType(int id)
        {
            try
            {
                var jobType = await _dbContext.AdrJobTypes
                    .FirstOrDefaultAsync(jt => jt.Id == id && !jt.IsDeleted);
            
                if (jobType == null)
                {
                    return NotFound($"Job type with ID {id} was not found.");
                }
            
                // Check if job type is referenced by any rules
                var ruleCount = await _dbContext.AdrAccountRules
                    .CountAsync(r => r.JobTypeId == id && !r.IsDeleted);
            
                if (ruleCount > 0)
                {
                    return BadRequest($"Cannot delete job type '{jobType.Code}' because it is referenced by {ruleCount} rule(s). Deactivate the job type instead.");
                }
            
                var username = User.Identity?.Name ?? "System";
            
                // Soft delete
                jobType.IsDeleted = true;
                jobType.IsActive = false;
                jobType.ModifiedDateTime = DateTime.UtcNow;
                jobType.ModifiedBy = username;
            
                await _dbContext.SaveChangesAsync();
            
                _logger.LogInformation("Job type {Id} ({Code}) deleted by {User}", id, jobType.Code, username);
            
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting ADR job type {Id}", id);
                return StatusCode(500, "An error occurred while deleting the ADR job type");
            }
        }

        #endregion

        private static string CsvEscape(string? value)
    {
        if (value == null) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}

#region Request DTOs

public class CreateAdrJobRequest
{
    public int AdrAccountId { get; set; }
    public DateTime BillingPeriodStartDateTime { get; set; }
    public DateTime BillingPeriodEndDateTime { get; set; }
}

public class UpdateJobStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public int? AdrStatusId { get; set; }
    public string? AdrStatusDescription { get; set; }
    public long? AdrIndexId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CreateAdrJobExecutionRequest
{
    public int AdrJobId { get; set; }
    public int AdrRequestTypeId { get; set; }
    public string? RequestPayload { get; set; }
}

public class CompleteExecutionRequest
{
    public int? AdrStatusId { get; set; }
    public string? AdrStatusDescription { get; set; }
    public long? AdrIndexId { get; set; }
    public int? HttpStatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsError { get; set; }
    public bool IsFinal { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ApiResponse { get; set; }
}

public class RefireJobsRequest
{
    public List<int> JobIds { get; set; } = new();
    public bool ForceRefire { get; set; } = false;
}

public class BackgroundOrchestrationRequest
{
    public bool RunSync { get; set; } = true;
    public bool RunCreateJobs { get; set; } = true;
    public bool RunCredentialVerification { get; set; } = true;
    public bool RunScraping { get; set; } = true;
    public bool RunStatusCheck { get; set; } = true;
    
    /// <summary>
    /// When true and RunStatusCheck is true, checks ALL jobs with ScrapeRequested status
    /// regardless of timing criteria. Used by the "Check Statuses Only" button.
    /// </summary>
    public bool CheckAllScrapedStatuses { get; set; } = false;
}

public class UpdateAccountBillingRequest
{
    /// <summary>
    /// The expected billing date (displayed as "Expected Billing Date" in UI)
    /// This updates the LastInvoiceDateTime field which drives the billing calculations
    /// </summary>
    public DateTime? ExpectedBillingDate { get; set; }
    
    /// <summary>
    /// Billing frequency: Bi-Weekly, Monthly, Bi-Monthly, Quarterly, Semi-Annually, Annually
    /// </summary>
    public string? PeriodType { get; set; }
    
    /// <summary>
    /// Historical billing status: Missing, Overdue, Due Now, Due Soon, Upcoming, Future
    /// </summary>
    public string? HistoricalBillingStatus { get; set; }
}

public class OrchestrationHistoryPagedResponse
{
    public List<AdrOrchestrationStatus> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// Request for admin-only manual ADR operation
/// </summary>
public class ManualScrapeRequest
{
    /// <summary>
    /// Target date for the invoice search (center of the date range)
    /// </summary>
    public DateTime TargetDate { get; set; }
    
    /// <summary>
    /// Optional custom range start date (if not provided, calculated from period type)
    /// </summary>
    public DateTime? RangeStartDate { get; set; }
    
    /// <summary>
    /// Optional custom range end date (if not provided, calculated from period type)
    /// </summary>
    public DateTime? RangeEndDate { get; set; }
    
    /// <summary>
    /// Reason for the manual ADR request (for audit purposes)
    /// </summary>
    public string? Reason { get; set; }
    
    /// <summary>
    /// Whether to use high priority for the ADR request (processed before normal priority)
    /// </summary>
    public bool IsHighPriority { get; set; }
    
    /// <summary>
    /// ADR Request Type: 1 = Vendor Credential Check, 2 = ADR Download Request (default)
    /// </summary>
    public int RequestType { get; set; } = 2;
}

/// <summary>
/// Response from ADR API for manual requests
/// </summary>
public class ManualAdrApiResponse
{
    public int StatusId { get; set; }
    public string? StatusDescription { get; set; }
    public long? IndexId { get; set; }
    public bool IsError { get; set; }
    public bool IsFinal { get; set; }
}

/// <summary>
/// Request to update ADR configuration settings
/// </summary>
public class UpdateAdrConfigurationRequest
{
    public int? CredentialCheckLeadDays { get; set; }
    public int? ScrapeRetryDays { get; set; }
    public int? MaxRetries { get; set; }
    public int? FinalStatusCheckDelayDays { get; set; }
    public int? DailyStatusCheckDelayDays { get; set; }
    public int? MaxParallelRequests { get; set; }
    public int? BatchSize { get; set; }
    public int? DefaultWindowDaysBefore { get; set; }
    public int? DefaultWindowDaysAfter { get; set; }
    public bool? AutoCreateTestLoginRules { get; set; }
    public bool? AutoCreateMissingInvoiceAlerts { get; set; }
    public string? MissingInvoiceAlertEmail { get; set; }
    public bool? IsOrchestrationEnabled { get; set; }
    public string? Notes { get; set; }
    public int? MaxOrchestrationDurationMinutes { get; set; }
    public int? DatabaseCommandTimeoutSeconds { get; set; }
}

/// <summary>
/// Request to create a new blacklist entry
/// </summary>
public class CreateBlacklistEntryRequest
{
    public string? VendorCode { get; set; }
    public long? VMAccountId { get; set; }
    public string? VMAccountNumber { get; set; }
    public int? CredentialId { get; set; }
    public string? ExclusionType { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime? EffectiveStartDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request to update an existing blacklist entry
/// </summary>
public class UpdateBlacklistEntryRequest
{
    public string? VendorCode { get; set; }
    public long? VMAccountId { get; set; }
    public string? VMAccountNumber { get; set; }
    public int? CredentialId { get; set; }
    public string? ExclusionType { get; set; }
    public string? Reason { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? EffectiveStartDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request to check blacklist status for a batch of accounts or jobs
/// </summary>
public class BlacklistCheckRequest
{
    /// <summary>
    /// List of items to check for blacklist status
    /// </summary>
    public List<BlacklistCheckItem> Items { get; set; } = new();
}

/// <summary>
/// Individual item to check for blacklist status
/// </summary>
public class BlacklistCheckItem
{
    /// <summary>
    /// Account ID (for account-based checks)
    /// </summary>
    public int? AccountId { get; set; }
    
    /// <summary>
    /// Job ID (for job-based checks)
    /// </summary>
    public int? JobId { get; set; }
    
    /// <summary>
    /// Vendor code to match against blacklist entries
    /// </summary>
    public string? VendorCode { get; set; }
    
    /// <summary>
    /// VM Account ID to match against blacklist entries
    /// </summary>
    public long? VMAccountId { get; set; }
    
    /// <summary>
    /// VM Account Number to match against blacklist entries
    /// </summary>
    public string? VMAccountNumber { get; set; }
    
    /// <summary>
    /// Credential ID to match against blacklist entries
    /// </summary>
    public int? CredentialId { get; set; }
}

/// <summary>
/// Result of blacklist status check for a single item
/// </summary>
public class BlacklistStatusResult
{
    /// <summary>
    /// Account ID (if this was an account check)
    /// </summary>
    public int? AccountId { get; set; }
    
    /// <summary>
    /// Job ID (if this was a job check)
    /// </summary>
    public int? JobId { get; set; }
    
    /// <summary>
    /// Whether there is at least one current (active now) blacklist affecting this item
    /// </summary>
    public bool HasCurrentBlacklist { get; set; }
    
    /// <summary>
    /// Whether there is at least one future blacklist affecting this item
    /// </summary>
    public bool HasFutureBlacklist { get; set; }
    
    /// <summary>
    /// Count of current blacklists affecting this item
    /// </summary>
    public int CurrentBlacklistCount { get; set; }
    
    /// <summary>
    /// Count of future blacklists affecting this item
    /// </summary>
    public int FutureBlacklistCount { get; set; }
    
    /// <summary>
    /// Details of current blacklists (for tooltips and exports)
    /// </summary>
    public List<BlacklistSummary> CurrentBlacklists { get; set; } = new();
    
    /// <summary>
    /// Details of future blacklists (for tooltips and exports)
    /// </summary>
    public List<BlacklistSummary> FutureBlacklists { get; set; } = new();
}

/// <summary>
/// Summary of a blacklist entry for display purposes
/// </summary>
public class BlacklistSummary
{
    public int Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ExclusionType { get; set; } = string.Empty;
    public DateTime? EffectiveStartDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
    public string? VendorCode { get; set; }
    public long? VMAccountId { get; set; }
    public string? VMAccountNumber { get; set; }
    public int? CredentialId { get; set; }
}

/// <summary>
/// Request to create a new job type
/// </summary>
public class CreateJobTypeRequest
{
    /// <summary>
    /// Short code for the job type (e.g., "CREDENTIAL_CHECK", "DOWNLOAD_INVOICE")
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name for the job type
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed description of what this job type does
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// The URL endpoint to call when executing jobs of this type
    /// </summary>
    public string? EndpointUrl { get; set; }
    
    /// <summary>
    /// The AdrRequestTypeId to send to the downstream ADR API (1 = AttemptLogin, 2 = DownloadInvoice)
    /// </summary>
    public int AdrRequestTypeId { get; set; }
    
    /// <summary>
    /// Whether this job type is currently active (default: true)
    /// </summary>
    public bool? IsActive { get; set; }
    
    /// <summary>
    /// Display order for UI sorting
    /// </summary>
    public int? DisplayOrder { get; set; }
    
    /// <summary>
    /// Optional notes about this job type
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Request to update an existing job type
/// </summary>
public class UpdateJobTypeRequest
{
    /// <summary>
    /// Short code for the job type (e.g., "CREDENTIAL_CHECK", "DOWNLOAD_INVOICE")
    /// </summary>
    public string? Code { get; set; }
    
    /// <summary>
    /// Display name for the job type
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Detailed description of what this job type does
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// The URL endpoint to call when executing jobs of this type
    /// </summary>
    public string? EndpointUrl { get; set; }
    
    /// <summary>
    /// The AdrRequestTypeId to send to the downstream ADR API (1 = AttemptLogin, 2 = DownloadInvoice)
    /// </summary>
    public int? AdrRequestTypeId { get; set; }
    
    /// <summary>
    /// Whether this job type is currently active
    /// </summary>
    public bool? IsActive { get; set; }
    
    /// <summary>
    /// Display order for UI sorting
    /// </summary>
    public int? DisplayOrder { get; set; }
    
    /// <summary>
    /// Optional notes about this job type
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Paginated response for account rules
/// </summary>
public class RulesPagedResponse
{
    public List<AccountRuleDto>? Items { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// DTO for account rule data
/// </summary>
public class AccountRuleDto
{
    public int Id { get; set; }
    public int AdrAccountId { get; set; }
    public string? VendorCode { get; set; }
    public string? VMAccountNumber { get; set; }
    public int JobTypeId { get; set; }
    public string? PeriodType { get; set; }
    public int? PeriodDays { get; set; }
    public DateTime? NextRunDateTime { get; set; }
    public DateTime? NextRangeStartDateTime { get; set; }
    public DateTime? NextRangeEndDateTime { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsManuallyOverridden { get; set; }
    public string? OverriddenBy { get; set; }
    public DateTime? OverriddenDateTime { get; set; }
}

/// <summary>
/// Request model for updating an account rule
/// </summary>
public class UpdateRuleRequest
{
    public DateTime? NextRunDateTime { get; set; }
    public DateTime? NextRangeStartDateTime { get; set; }
    public DateTime? NextRangeEndDateTime { get; set; }
    public string? PeriodType { get; set; }
    public int? PeriodDays { get; set; }
    public int? JobTypeId { get; set; }
    public bool? IsEnabled { get; set; }
}

/// <summary>
/// Result containing counts of blacklist entries by status
/// </summary>
public class BlacklistCountsResult
{
    /// <summary>
    /// Count of currently active blacklist entries (affecting accounts now)
    /// </summary>
    public int CurrentCount { get; set; }
    
    /// <summary>
    /// Count of future blacklist entries (will become active in the future)
    /// </summary>
    public int FutureCount { get; set; }
}

#endregion
