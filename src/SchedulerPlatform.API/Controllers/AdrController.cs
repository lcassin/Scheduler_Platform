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
                LastCompletedDateTime = jobStatusLookup.TryGetValue(a.Id, out var js2) ? js2.LastCompletedDateTime : null
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

            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Account #,VM Account ID,Interface Account ID,Client,Vendor Code,Period Type,Next Run,Run Status,Job Status,Last Completed,Historical Status,Last Invoice,Expected Next,Is Overridden,Overridden By,Overridden Date");

                foreach (var a in accounts)
                {
                    var hasJobStatus = jobStatusLookup.TryGetValue(a.Id, out var js);
                    var currentJobStatus = hasJobStatus ? js?.CurrentJobStatus : null;
                    var lastCompleted = hasJobStatus ? js?.LastCompletedDateTime : null;
                    
                    csv.AppendLine($"{CsvEscape(a.VMAccountNumber)},{a.VMAccountId},{CsvEscape(a.InterfaceAccountId)},{CsvEscape(a.ClientName)},{CsvEscape(a.VendorCode)},{CsvEscape(a.PeriodType)},{a.NextRunDateTime?.ToString("MM/dd/yyyy") ?? ""},{CsvEscape(a.NextRunStatus)},{CsvEscape(currentJobStatus)},{lastCompleted?.ToString("MM/dd/yyyy") ?? ""},{CsvEscape(a.HistoricalBillingStatus)},{a.LastInvoiceDateTime?.ToString("MM/dd/yyyy") ?? ""},{a.ExpectedNextDateTime?.ToString("MM/dd/yyyy") ?? ""},{a.IsManuallyOverridden},{CsvEscape(a.OverriddenBy)},{a.OverriddenDateTime?.ToString("MM/dd/yyyy HH:mm") ?? ""}");
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
            worksheet.Cell(1, 14).Value = "Is Overridden";
            worksheet.Cell(1, 15).Value = "Overridden By";
            worksheet.Cell(1, 16).Value = "Overridden Date";

            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;

            int row = 2;
            foreach (var a in accounts)
            {
                var hasJobStatus = jobStatusLookup.TryGetValue(a.Id, out var js);
                var currentJobStatus = hasJobStatus ? js?.CurrentJobStatus : null;
                var lastCompleted = hasJobStatus ? js?.LastCompletedDateTime : null;
                
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

#endregion
