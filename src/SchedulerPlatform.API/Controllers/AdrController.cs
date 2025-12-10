using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchedulerPlatform.API.Services;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdrController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdrAccountSyncService _syncService;
    private readonly IAdrOrchestratorService _orchestratorService;
    private readonly ILogger<AdrController> _logger;

    public AdrController(
        IUnitOfWork unitOfWork,
        IAdrAccountSyncService syncService,
        IAdrOrchestratorService orchestratorService,
        ILogger<AdrController> logger)
    {
        _unitOfWork = unitOfWork;
        _syncService = syncService;
        _orchestratorService = orchestratorService;
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
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var (items, totalCount) = await _unitOfWork.AdrAccounts.GetPagedAsync(
                pageNumber,
                pageSize,
                clientId,
                credentialId,
                nextRunStatus,
                searchTerm,
                historicalBillingStatus);

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
            [FromQuery] int pageSize = 20)
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
                    latestPerAccount);

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
    public async Task<ActionResult<object>> GetJobStats([FromQuery] int? adrAccountId = null)
    {
        try
        {
            var totalCount = await _unitOfWork.AdrJobs.GetTotalCountAsync(adrAccountId);
            var pendingCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("Pending");
            var credentialVerifiedCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("CredentialVerified");
            var scrapeRequestedCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("ScrapeRequested");
            var completedCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("Completed");
            var failedCount = await _unitOfWork.AdrJobs.GetCountByStatusAsync("Failed");

            return Ok(new
            {
                totalCount,
                pendingCount,
                credentialVerifiedCount,
                scrapeRequestedCount,
                completedCount,
                failedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ADR job stats");
            return StatusCode(500, "An error occurred while retrieving ADR job stats");
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
            var result = await _syncService.SyncAccountsAsync(cancellationToken);
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
            var result = await _orchestratorService.VerifyCredentialsAsync(cancellationToken);
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
            var result = await _orchestratorService.ProcessScrapingAsync(cancellationToken);
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
            _logger.LogInformation("Manual status check triggered by {User}", User.Identity?.Name ?? "Unknown");
            var result = await _orchestratorService.CheckPendingStatusesAsync(cancellationToken);
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

            var syncResult = await _syncService.SyncAccountsAsync(cancellationToken);
            var jobCreationResult = await _orchestratorService.CreateJobsForDueAccountsAsync(cancellationToken);
            var credentialResult = await _orchestratorService.VerifyCredentialsAsync(cancellationToken);
            var scrapeResult = await _orchestratorService.ProcessScrapingAsync(cancellationToken);
            var statusResult = await _orchestratorService.CheckPendingStatusesAsync(cancellationToken);

            return Ok(new
            {
                syncResult,
                jobCreationResult,
                credentialResult,
                scrapeResult,
                statusResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during full ADR cycle");
            return StatusCode(500, new { error = "An error occurred during full ADR cycle", message = ex.Message });
        }
    }

    #endregion
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

#endregion
