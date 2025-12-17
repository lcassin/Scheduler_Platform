using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.API.Services;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.API.Controllers;

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

    [HttpGet("accounts")]
    public async Task<ActionResult<object>> GetAccounts(
        [FromQuery] int? clientId = null,
        [FromQuery] int? credentialId = null,
        [FromQuery] string? nextRunStatus = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? historicalBillingStatus = null,
        [FromQuery] bool? isOverridden = null,
        [FromQuery] string? jobStatus = null,
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
                accountIdsWithJobStatus);

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
                RuleOverriddenDateTime = ruleOverrideLookup.TryGetValue(a.Id, out var ro3) ? ro3.RuleOverriddenDateTime : null
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

    [HttpGet("accounts/{id}")]
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

    [HttpGet("accounts/by-vm-account/{vmAccountId}")]
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

    [HttpGet("accounts/due-for-run")]
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

    [HttpGet("accounts/needing-credential-check")]
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

    [HttpGet("accounts/stats")]
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

    [HttpPut("accounts/{id}/billing")]
    [Authorize(Policy = "AdrAccounts.Update")]
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

            // Recalculate derived dates based on updated historical data
            if (account.LastInvoiceDateTime.HasValue && account.PeriodDays.HasValue)
            {
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

                var expectedNext = account.LastInvoiceDateTime.Value.AddDays(account.PeriodDays.Value);
                
                // If expected date is in the past, calculate next future date
                var today = DateTime.UtcNow.Date;
                while (expectedNext < today)
                {
                    expectedNext = expectedNext.AddDays(account.PeriodDays.Value);
                }

                account.ExpectedNextDateTime = expectedNext;
                account.ExpectedRangeStartDateTime = expectedNext.AddDays(-windowDays);
                account.ExpectedRangeEndDateTime = expectedNext.AddDays(windowDays);
                account.NextRunDateTime = expectedNext;
                account.NextRangeStartDateTime = expectedNext.AddDays(-windowDays);
                account.NextRangeEndDateTime = expectedNext.AddDays(windowDays);
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

    [HttpPost("accounts/{id}/clear-override")]
    [Authorize(Policy = "AdrAccounts.Update")]
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
        [HttpPost("accounts/{id}/manual-scrape")]
        [Authorize(Policy = "AdrAccounts.Update")]
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
        /// Check the status of a manual ADR job using the same API as orchestrated jobs.
        /// Available to Editors, Admins, and Super Admins.
        /// </summary>
        [HttpPost("jobs/{jobId}/check-status")]
        [Authorize(Policy = "AdrAccounts.Update")]
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

    [HttpGet("accounts/export")]
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
            // Get all accounts matching the filters (no pagination for export)
            var (accounts, _) = await _unitOfWork.AdrAccounts.GetPagedAsync(
                1, int.MaxValue, clientId, null, nextRunStatus, searchTerm, historicalBillingStatus,
                isOverridden, sortColumn, sortDescending);

            // Get account IDs for job status lookup
            var accountIds = accounts.Select(a => a.Id).ToList();
            
            // Get current job status for each account (single query)
            var jobStatuses = await _dbContext.AdrJobs
                .Where(j => !j.IsDeleted && accountIds.Contains(j.AdrAccountId))
                .GroupBy(j => j.AdrAccountId)
                .Select(g => new 
                {
                    AdrAccountId = g.Key,
                    CurrentJobStatus = g
                        .OrderByDescending(j => j.BillingPeriodStartDateTime)
                        .Select(j => j.Status)
                        .FirstOrDefault(),
                    LastCompletedDateTime = g
                        .Where(j => j.ScrapingCompletedDateTime.HasValue)
                        .OrderByDescending(j => j.ScrapingCompletedDateTime)
                        .Select(j => j.ScrapingCompletedDateTime)
                        .FirstOrDefault()
                })
                .ToListAsync();
            
            var jobStatusLookup = jobStatuses.ToDictionary(x => x.AdrAccountId);

            // Get rule override status for each account (single query)
            var ruleOverrideStatuses = await _dbContext.AdrAccountRules
                .Where(r => !r.IsDeleted && accountIds.Contains(r.AdrAccountId))
                .GroupBy(r => r.AdrAccountId)
                .Select(g => new
                {
                    AdrAccountId = g.Key,
                    RuleIsManuallyOverridden = g.OrderBy(r => r.Id).Select(r => r.IsManuallyOverridden).FirstOrDefault(),
                    RuleOverriddenBy = g.OrderBy(r => r.Id).Select(r => r.OverriddenBy).FirstOrDefault(),
                    RuleOverriddenDateTime = g.OrderBy(r => r.Id).Select(r => r.OverriddenDateTime).FirstOrDefault()
                })
                .ToListAsync();
            
            var ruleOverrideLookup = ruleOverrideStatuses.ToDictionary(x => x.AdrAccountId);

            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Account #,VM Account ID,Interface Account ID,Client,Vendor Code,Period Type,Next Run,Run Status,Job Status,Last Completed,Historical Status,Last Invoice,Expected Next,Account Overridden,Account Overridden By,Account Overridden Date,Rule Overridden,Rule Overridden By,Rule Overridden Date");

                foreach (var a in accounts)
                {
                    var hasJobStatus = jobStatusLookup.TryGetValue(a.Id, out var js);
                    var currentJobStatus = hasJobStatus ? js?.CurrentJobStatus : null;
                    var lastCompleted = hasJobStatus ? js?.LastCompletedDateTime : null;
                    var hasRuleOverride = ruleOverrideLookup.TryGetValue(a.Id, out var ro);
                    
                    csv.AppendLine($"{CsvEscape(a.VMAccountNumber)},{a.VMAccountId},{CsvEscape(a.InterfaceAccountId)},{CsvEscape(a.ClientName)},{CsvEscape(a.VendorCode)},{CsvEscape(a.PeriodType)},{a.NextRunDateTime?.ToString("MM/dd/yyyy") ?? ""},{CsvEscape(a.NextRunStatus)},{CsvEscape(currentJobStatus)},{lastCompleted?.ToString("MM/dd/yyyy") ?? ""},{CsvEscape(a.HistoricalBillingStatus)},{a.LastInvoiceDateTime?.ToString("MM/dd/yyyy") ?? ""},{a.ExpectedNextDateTime?.ToString("MM/dd/yyyy") ?? ""},{a.IsManuallyOverridden},{CsvEscape(a.OverriddenBy)},{a.OverriddenDateTime?.ToString("MM/dd/yyyy HH:mm") ?? ""},{(hasRuleOverride && ro!.RuleIsManuallyOverridden ? "Yes" : "No")},{CsvEscape(hasRuleOverride ? ro!.RuleOverriddenBy : null)},{(hasRuleOverride ? ro!.RuleOverriddenDateTime?.ToString("MM/dd/yyyy HH:mm") : "") ?? ""}");
                }

                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"adr_accounts_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            }

            // Excel format
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("ADR Accounts");

            // Headers
            worksheet.Cell(1, 1).Value = "Account #";
            worksheet.Cell(1, 2).Value = "VM Account ID";
            worksheet.Cell(1, 3).Value = "Interface Account ID";
            worksheet.Cell(1, 4).Value = "Client";
            worksheet.Cell(1, 5).Value = "Vendor Code";
            worksheet.Cell(1, 6).Value = "Period Type";
            worksheet.Cell(1, 7).Value = "Next Run";
            worksheet.Cell(1, 8).Value = "Run Status";
            worksheet.Cell(1, 9).Value = "Job Status";
            worksheet.Cell(1, 10).Value = "Last Completed";
            worksheet.Cell(1, 11).Value = "Historical Status";
            worksheet.Cell(1, 12).Value = "Last Invoice";
            worksheet.Cell(1, 13).Value = "Expected Next";
            worksheet.Cell(1, 14).Value = "Account Overridden";
            worksheet.Cell(1, 15).Value = "Account Overridden By";
            worksheet.Cell(1, 16).Value = "Account Overridden Date";
            worksheet.Cell(1, 17).Value = "Rule Overridden";
            worksheet.Cell(1, 18).Value = "Rule Overridden By";
            worksheet.Cell(1, 19).Value = "Rule Overridden Date";

            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;

            int row = 2;
            foreach (var a in accounts)
            {
                var hasJobStatus = jobStatusLookup.TryGetValue(a.Id, out var js);
                var currentJobStatus = hasJobStatus ? js?.CurrentJobStatus : null;
                var lastCompleted = hasJobStatus ? js?.LastCompletedDateTime : null;
                var hasRuleOverride = ruleOverrideLookup.TryGetValue(a.Id, out var ro);
                
                worksheet.Cell(row, 1).Value = a.VMAccountNumber;
                worksheet.Cell(row, 2).Value = a.VMAccountId;
                worksheet.Cell(row, 3).Value = a.InterfaceAccountId;
                worksheet.Cell(row, 4).Value = a.ClientName;
                worksheet.Cell(row, 5).Value = a.VendorCode;
                worksheet.Cell(row, 6).Value = a.PeriodType;
                if (a.NextRunDateTime.HasValue) worksheet.Cell(row, 7).Value = a.NextRunDateTime.Value;
                worksheet.Cell(row, 8).Value = a.NextRunStatus;
                worksheet.Cell(row, 9).Value = currentJobStatus ?? "";
                if (lastCompleted.HasValue) worksheet.Cell(row, 10).Value = lastCompleted.Value;
                worksheet.Cell(row, 11).Value = a.HistoricalBillingStatus;
                if (a.LastInvoiceDateTime.HasValue) worksheet.Cell(row, 12).Value = a.LastInvoiceDateTime.Value;
                if (a.ExpectedNextDateTime.HasValue) worksheet.Cell(row, 13).Value = a.ExpectedNextDateTime.Value;
                worksheet.Cell(row, 14).Value = a.IsManuallyOverridden ? "Yes" : "No";
                worksheet.Cell(row, 15).Value = a.OverriddenBy ?? "";
                if (a.OverriddenDateTime.HasValue) worksheet.Cell(row, 16).Value = a.OverriddenDateTime.Value;
                worksheet.Cell(row, 17).Value = hasRuleOverride && ro!.RuleIsManuallyOverridden ? "Yes" : "No";
                worksheet.Cell(row, 18).Value = hasRuleOverride ? ro!.RuleOverriddenBy ?? "" : "";
                if (hasRuleOverride && ro!.RuleOverriddenDateTime.HasValue) worksheet.Cell(row, 19).Value = ro.RuleOverriddenDateTime.Value;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
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

        [HttpGet("jobs")]
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
            [FromQuery] string? sortColumn = null,
            [FromQuery] bool sortDescending = true)
        {
            try
            {
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
                    sortDescending);

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
                    j.ModifiedBy
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

    [HttpGet("jobs/{id}")]
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

    [HttpGet("jobs/by-account/{adrAccountId}")]
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

    [HttpGet("jobs/by-status/{status}")]
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

    [HttpGet("jobs/needing-credential-verification")]
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

    [HttpGet("jobs/ready-for-scraping")]
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

    [HttpGet("jobs/needing-status-check")]
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

    [HttpGet("jobs/for-retry")]
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

    [HttpGet("jobs/stats")]
    public async Task<ActionResult<object>> GetJobStats(
        [FromQuery] int? adrAccountId = null,
        [FromQuery] int? lastOrchestrationRuns = null)
    {
        try
        {
            int totalCount, pendingCount, credentialVerifiedCount, scrapeRequestedCount, 
                completedCount, failedCount, needsReviewCount, credentialFailedCount;

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
                        scrapeRequestedCount = completedCount = failedCount = needsReviewCount = 0;
                }
            }
            else
            {
                // Original behavior: count all jobs
                totalCount = await _unitOfWork.AdrJobs.GetTotalCountAsync(adrAccountId);
                pendingCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("Pending");
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

            return Ok(new
            {
                totalCount,
                pendingCount,
                credentialVerifiedCount,
                credentialFailedCount,
                scrapeRequestedCount,
                completedCount,
                failedCount,
                needsReviewCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR job stats");
            return StatusCode(500, "An error occurred while retrieving ADR job stats");
        }
    }

    [HttpGet("jobs/export")]
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

            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Job ID,Vendor Code,Account #,VM Account ID,Interface Account ID,Billing Period Start,Billing Period End,Period Type,Next Run,Status,ADR Status,ADR Status Description,Retry Count,Is Manual,Created");

                foreach (var j in jobs)
                {
                    var interfaceAccountId = j.AdrAccount?.InterfaceAccountId ?? "";
                    csv.AppendLine($"{j.Id},{CsvEscape(j.VendorCode)},{CsvEscape(j.VMAccountNumber)},{j.VMAccountId},{CsvEscape(interfaceAccountId)},{j.BillingPeriodStartDateTime:MM/dd/yyyy},{j.BillingPeriodEndDateTime:MM/dd/yyyy},{CsvEscape(j.PeriodType)},{j.NextRunDateTime?.ToString("MM/dd/yyyy") ?? ""},{CsvEscape(j.Status)},{j.AdrStatusId?.ToString() ?? ""},{CsvEscape(j.AdrStatusDescription)},{j.RetryCount},{j.IsManualRequest},{j.CreatedDateTime:MM/dd/yyyy HH:mm}");
                }

                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"adr_jobs_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            }

            // Excel format
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("ADR Jobs");

            // Headers
            worksheet.Cell(1, 1).Value = "Job ID";
            worksheet.Cell(1, 2).Value = "Vendor Code";
            worksheet.Cell(1, 3).Value = "Account #";
            worksheet.Cell(1, 4).Value = "VM Account ID";
            worksheet.Cell(1, 5).Value = "Interface Account ID";
            worksheet.Cell(1, 6).Value = "Billing Period Start";
            worksheet.Cell(1, 7).Value = "Billing Period End";
            worksheet.Cell(1, 8).Value = "Period Type";
            worksheet.Cell(1, 9).Value = "Next Run";
            worksheet.Cell(1, 10).Value = "Status";
            worksheet.Cell(1, 11).Value = "ADR Status";
            worksheet.Cell(1, 12).Value = "ADR Status Description";
            worksheet.Cell(1, 13).Value = "Retry Count";
            worksheet.Cell(1, 14).Value = "Is Manual";
            worksheet.Cell(1, 15).Value = "Created";

            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;

            int row = 2;
            foreach (var j in jobs)
            {
                worksheet.Cell(row, 1).Value = j.Id;
                worksheet.Cell(row, 2).Value = j.VendorCode;
                worksheet.Cell(row, 3).Value = j.VMAccountNumber;
                worksheet.Cell(row, 4).Value = j.VMAccountId;
                worksheet.Cell(row, 5).Value = j.AdrAccount?.InterfaceAccountId ?? "";
                worksheet.Cell(row, 6).Value = j.BillingPeriodStartDateTime;
                worksheet.Cell(row, 7).Value = j.BillingPeriodEndDateTime;
                worksheet.Cell(row, 8).Value = j.PeriodType;
                if (j.NextRunDateTime.HasValue) worksheet.Cell(row, 9).Value = j.NextRunDateTime.Value;
                worksheet.Cell(row, 10).Value = j.Status;
                if (j.AdrStatusId.HasValue) worksheet.Cell(row, 11).Value = j.AdrStatusId.Value;
                worksheet.Cell(row, 12).Value = j.AdrStatusDescription;
                worksheet.Cell(row, 13).Value = j.RetryCount;
                worksheet.Cell(row, 14).Value = j.IsManualRequest ? "Yes" : "No";
                worksheet.Cell(row, 15).Value = j.CreatedDateTime;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"adr_jobs_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting ADR jobs");
            return StatusCode(500, "An error occurred while exporting ADR jobs");
        }
    }

    [HttpPost("jobs")]
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

    [HttpPut("jobs/{id}/status")]
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

    [HttpPost("jobs/{id}/refire")]
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

    [HttpPost("jobs/refire-bulk")]
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

    [HttpGet("executions")]
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

    [HttpGet("executions/{id}")]
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

    [HttpGet("executions/by-job/{adrJobId}")]
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

    [HttpPost("executions")]
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

    [HttpPut("executions/{id}/complete")]
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

    [HttpGet("statuses")]
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

    [HttpGet("request-types")]
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

        [HttpPost("sync/accounts")]
        [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
        public async Task<ActionResult<AdrAccountSyncResult>> SyncAccounts(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Manual account sync triggered by {User}", User.Identity?.Name ?? "Unknown");
            var result = await _syncService.SyncAccountsAsync(null, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual account sync");
            return StatusCode(500, new { error = "An error occurred during account sync", message = ex.Message });
        }
    }

        [HttpPost("orchestrate/create-jobs")]
        [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
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

        [HttpPost("orchestrate/verify-credentials")]
        [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
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

        [HttpPost("orchestrate/process-scraping")]
        [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
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

        [HttpPost("orchestrate/check-statuses")]
        [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
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

        [HttpPost("orchestrate/run-full-cycle")]
        [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
        public async Task<ActionResult<object>> RunFullCycle(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Full ADR cycle triggered by {User}", User.Identity?.Name ?? "Unknown");

            // Step 1: Sync accounts from VendorCred
            var syncResult = await _syncService.SyncAccountsAsync(null, cancellationToken);
            
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
    [HttpPost("orchestrate/run-background")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
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
    [HttpGet("orchestrate/status/{requestId}")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
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
    [HttpGet("orchestrate/current")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
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
    /// Gets the recent orchestration run history from database.
    /// Falls back to in-memory if database is unavailable.
    /// Supports pagination with pageNumber and pageSize parameters.
    /// </summary>
    [HttpGet("orchestrate/history")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
    public async Task<ActionResult<object>> GetOrchestrationHistory(
        [FromQuery] int? count = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // First, detect and fix stale "Running" records (running for more than 30 minutes without completion)
            var staleThreshold = DateTime.UtcNow.AddMinutes(-30);
            var staleRuns = await _dbContext.AdrOrchestrationRuns
                .Where(r => !r.IsDeleted && r.Status == "Running" && r.StartedDateTime.HasValue && r.StartedDateTime < staleThreshold && r.CompletedDateTime == null)
                .ToListAsync();
            
            if (staleRuns.Any())
            {
                foreach (var staleRun in staleRuns)
                {
                    staleRun.Status = "Failed";
                    staleRun.CompletedDateTime = DateTime.UtcNow;
                    staleRun.ErrorMessage = "Orchestration run exceeded maximum expected duration (30 minutes) and was marked as failed. The process may have crashed or been terminated unexpectedly.";
                    staleRun.ModifiedDateTime = DateTime.UtcNow;
                    staleRun.ModifiedBy = "System";
                    _logger.LogWarning("Marking stale orchestration run {RequestId} as Failed - started at {StartedAt}, exceeded 30 minute threshold", 
                        staleRun.RequestId, staleRun.StartedDateTime);
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
        /// Retrieves a paginated list of account rules with optional filtering.
        /// </summary>
        /// <param name="page">Page number (default: 1).</param>
        /// <param name="pageSize">Number of items per page (default: 20).</param>
        /// <param name="vendorCode">Filter by vendor code.</param>
        /// <param name="accountNumber">Filter by account number.</param>
        /// <param name="isEnabled">Filter by enabled status.</param>
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
            [FromQuery] bool? isEnabled = null)
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
            
                var totalCount = await query.CountAsync();
            
                var rules = await query
                    .OrderBy(r => r.AdrAccount != null ? r.AdrAccount.VendorCode : "")
                    .ThenBy(r => r.AdrAccount != null ? r.AdrAccount.VMAccountNumber : "")
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

                if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
                {
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("Vendor Code,Account Number,Job Type,Period Type,Period Days,Next Run,Search Window Start,Search Window End,Enabled,Overridden,Overridden By,Overridden Date");

                    foreach (var r in rules)
                    {
                        csv.AppendLine($"{CsvEscape(r.VendorCode)},{CsvEscape(r.VMAccountNumber)},{r.JobTypeId},{CsvEscape(r.PeriodType)},{r.PeriodDays},{r.NextRunDateTime?.ToString("MM/dd/yyyy") ?? ""},{r.NextRangeStartDateTime?.ToString("MM/dd/yyyy") ?? ""},{r.NextRangeEndDateTime?.ToString("MM/dd/yyyy") ?? ""},{(r.IsEnabled ? "Yes" : "No")},{(r.IsManuallyOverridden ? "Yes" : "No")},{CsvEscape(r.OverriddenBy)},{r.OverriddenDateTime?.ToString("MM/dd/yyyy HH:mm") ?? ""}");
                    }

                    return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"adr_rules_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
                }

                // Excel format
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("ADR Rules");

                // Headers
                worksheet.Cell(1, 1).Value = "Vendor Code";
                worksheet.Cell(1, 2).Value = "Account Number";
                worksheet.Cell(1, 3).Value = "Job Type";
                worksheet.Cell(1, 4).Value = "Period Type";
                worksheet.Cell(1, 5).Value = "Period Days";
                worksheet.Cell(1, 6).Value = "Next Run";
                worksheet.Cell(1, 7).Value = "Search Window Start";
                worksheet.Cell(1, 8).Value = "Search Window End";
                worksheet.Cell(1, 9).Value = "Enabled";
                worksheet.Cell(1, 10).Value = "Overridden";
                worksheet.Cell(1, 11).Value = "Overridden By";
                worksheet.Cell(1, 12).Value = "Overridden Date";

                var headerRow = worksheet.Row(1);
                headerRow.Style.Font.Bold = true;

                int row = 2;
                foreach (var r in rules)
                {
                    worksheet.Cell(row, 1).Value = r.VendorCode ?? "";
                    worksheet.Cell(row, 2).Value = r.VMAccountNumber ?? "";
                    worksheet.Cell(row, 3).Value = r.JobTypeId;
                    worksheet.Cell(row, 4).Value = r.PeriodType ?? "";
                    worksheet.Cell(row, 5).Value = r.PeriodDays ?? 0;
                    if (r.NextRunDateTime.HasValue) worksheet.Cell(row, 6).Value = r.NextRunDateTime.Value;
                    if (r.NextRangeStartDateTime.HasValue) worksheet.Cell(row, 7).Value = r.NextRangeStartDateTime.Value;
                    if (r.NextRangeEndDateTime.HasValue) worksheet.Cell(row, 8).Value = r.NextRangeEndDateTime.Value;
                    worksheet.Cell(row, 9).Value = r.IsEnabled ? "Yes" : "No";
                    worksheet.Cell(row, 10).Value = r.IsManuallyOverridden ? "Yes" : "No";
                    worksheet.Cell(row, 11).Value = r.OverriddenBy ?? "";
                    if (r.OverriddenDateTime.HasValue) worksheet.Cell(row, 12).Value = r.OverriddenDateTime.Value;
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
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
                    rule.PeriodType = request.PeriodType;
                
                if (request.PeriodDays.HasValue)
                    rule.PeriodDays = request.PeriodDays.Value;
                
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
    /// Retrieves a paginated list of blacklist entries.
    /// Only Admin and Super Admin users can access this endpoint.
    /// </summary>
    /// <param name="pageNumber">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20).</param>
    /// <param name="includeInactive">Whether to include inactive entries (default: false).</param>
    /// <returns>A paginated list of blacklist entries.</returns>
    /// <response code="200">Returns the list of blacklist entries.</response>
    /// <response code="500">An error occurred while retrieving blacklist entries.</response>
    [HttpGet("blacklist")]
    [Authorize(Policy = "Users.Manage.Read")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetBlacklist(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false)
    {
        try
        {
            var query = _dbContext.AdrAccountBlacklists
                .Where(b => !b.IsDeleted);
            
            if (!includeInactive)
            {
                query = query.Where(b => b.IsActive);
            }
            
            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(b => b.CreatedDateTime)
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

#endregion
