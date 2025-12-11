using ClosedXML.Excel;
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
    private readonly IAdrOrchestrationQueue _orchestrationQueue;
    private readonly ILogger<AdrController> _logger;

    public AdrController(
        IUnitOfWork unitOfWork,
        IAdrAccountSyncService syncService,
        IAdrOrchestratorService orchestratorService,
        IAdrOrchestrationQueue orchestrationQueue,
        ILogger<AdrController> logger)
    {
        _unitOfWork = unitOfWork;
        _syncService = syncService;
        _orchestratorService = orchestratorService;
        _orchestrationQueue = orchestrationQueue;
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

            // Check for existing pending jobs for this account and cancel them if dates changed
            // This prevents duplicate jobs when billing dates are manually corrected
            if (account.NextRangeStartDateTime.HasValue && account.NextRangeEndDateTime.HasValue)
            {
                var existingJobs = await _unitOfWork.AdrJobs.GetByAccountIdAsync(account.Id);
                var pendingJobs = existingJobs.Where(j => 
                    (j.Status == "Pending" || j.Status == "CredentialCheckInProgress" || j.Status == "CredentialVerified") &&
                    (j.BillingPeriodStartDateTime != account.NextRangeStartDateTime.Value ||
                     j.BillingPeriodEndDateTime != account.NextRangeEndDateTime.Value));
                
                foreach (var job in pendingJobs)
                {
                    job.Status = "Cancelled";
                    job.ErrorMessage = $"Cancelled due to manual billing date override by {username}";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = username;
                    await _unitOfWork.AdrJobs.UpdateAsync(job);
                    _logger.LogInformation("Cancelled job {JobId} for account {AccountId} due to billing date override", job.Id, account.Id);
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

    [HttpGet("accounts/export")]
    public async Task<IActionResult> ExportAccounts(
        [FromQuery] int? clientId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? nextRunStatus = null,
        [FromQuery] string? historicalBillingStatus = null,
        [FromQuery] string format = "excel")
    {
        try
        {
            // Get all accounts matching the filters (no pagination for export)
            var (accounts, _) = await _unitOfWork.AdrAccounts.GetPagedAsync(
                1, int.MaxValue, clientId, null, nextRunStatus, searchTerm, historicalBillingStatus);

            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Account #,Interface Account ID,Client,Vendor Code,Period Type,Next Run,Run Status,Historical Status,Last Invoice,Expected Next");

                foreach (var a in accounts)
                {
                    csv.AppendLine($"{CsvEscape(a.VMAccountNumber)},{CsvEscape(a.InterfaceAccountId)},{CsvEscape(a.ClientName)},{CsvEscape(a.VendorCode)},{CsvEscape(a.PeriodType)},{a.NextRunDateTime?.ToString("MM/dd/yyyy") ?? ""},{CsvEscape(a.NextRunStatus)},{CsvEscape(a.HistoricalBillingStatus)},{a.LastInvoiceDateTime?.ToString("MM/dd/yyyy") ?? ""},{a.ExpectedNextDateTime?.ToString("MM/dd/yyyy") ?? ""}");
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
            worksheet.Cell(1, 9).Value = "Historical Status";
            worksheet.Cell(1, 10).Value = "Last Invoice";
            worksheet.Cell(1, 11).Value = "Expected Next";

            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;

            int row = 2;
            foreach (var a in accounts)
            {
                worksheet.Cell(row, 1).Value = a.VMAccountNumber;
                worksheet.Cell(row, 2).Value = a.VMAccountId;
                worksheet.Cell(row, 3).Value = a.InterfaceAccountId;
                worksheet.Cell(row, 4).Value = a.ClientName;
                worksheet.Cell(row, 5).Value = a.VendorCode;
                worksheet.Cell(row, 6).Value = a.PeriodType;
                if (a.NextRunDateTime.HasValue) worksheet.Cell(row, 7).Value = a.NextRunDateTime.Value;
                worksheet.Cell(row, 8).Value = a.NextRunStatus;
                worksheet.Cell(row, 9).Value = a.HistoricalBillingStatus;
                if (a.LastInvoiceDateTime.HasValue) worksheet.Cell(row, 10).Value = a.LastInvoiceDateTime.Value;
                if (a.ExpectedNextDateTime.HasValue) worksheet.Cell(row, 11).Value = a.ExpectedNextDateTime.Value;
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
            [FromQuery] int? credentialId = null)
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
                    credentialId);

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

    [HttpGet("jobs/export")]
    public async Task<IActionResult> ExportJobs(
        [FromQuery] string? status = null,
        [FromQuery] string? vendorCode = null,
        [FromQuery] string? vmAccountNumber = null,
        [FromQuery] bool latestPerAccount = false,
        [FromQuery] string format = "excel")
    {
        try
        {
            // Get all jobs matching the filters (no pagination for export)
            var (jobs, _) = await _unitOfWork.AdrJobs.GetPagedAsync(
                1, int.MaxValue, null, status, null, null, vendorCode, vmAccountNumber, latestPerAccount);

            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Job ID,Vendor Code,Account #,Billing Period Start,Billing Period End,Period Type,Next Run,Status,ADR Status,ADR Status Description,Retry Count,Created");

                foreach (var j in jobs)
                {
                    csv.AppendLine($"{j.Id},{CsvEscape(j.VendorCode)},{CsvEscape(j.VMAccountNumber)},{j.BillingPeriodStartDateTime:MM/dd/yyyy},{j.BillingPeriodEndDateTime:MM/dd/yyyy},{CsvEscape(j.PeriodType)},{j.NextRunDateTime?.ToString("MM/dd/yyyy") ?? ""},{CsvEscape(j.Status)},{j.AdrStatusId?.ToString() ?? ""},{CsvEscape(j.AdrStatusDescription)},{j.RetryCount},{j.CreatedDateTime:MM/dd/yyyy HH:mm}");
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
            worksheet.Cell(1, 4).Value = "Billing Period Start";
            worksheet.Cell(1, 5).Value = "Billing Period End";
            worksheet.Cell(1, 6).Value = "Period Type";
            worksheet.Cell(1, 7).Value = "Next Run";
            worksheet.Cell(1, 8).Value = "Status";
            worksheet.Cell(1, 9).Value = "ADR Status";
            worksheet.Cell(1, 10).Value = "ADR Status Description";
            worksheet.Cell(1, 11).Value = "Retry Count";
            worksheet.Cell(1, 12).Value = "Created";

            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;

            int row = 2;
            foreach (var j in jobs)
            {
                worksheet.Cell(row, 1).Value = j.Id;
                worksheet.Cell(row, 2).Value = j.VendorCode;
                worksheet.Cell(row, 3).Value = j.VMAccountNumber;
                worksheet.Cell(row, 4).Value = j.BillingPeriodStartDateTime;
                worksheet.Cell(row, 5).Value = j.BillingPeriodEndDateTime;
                worksheet.Cell(row, 6).Value = j.PeriodType;
                if (j.NextRunDateTime.HasValue) worksheet.Cell(row, 7).Value = j.NextRunDateTime.Value;
                worksheet.Cell(row, 8).Value = j.Status;
                if (j.AdrStatusId.HasValue) worksheet.Cell(row, 9).Value = j.AdrStatusId.Value;
                worksheet.Cell(row, 10).Value = j.AdrStatusDescription;
                worksheet.Cell(row, 11).Value = j.RetryCount;
                worksheet.Cell(row, 12).Value = j.CreatedDateTime;
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
            _logger.LogInformation("Manual status check triggered by {User}", User.Identity?.Name ?? "Unknown");
            var result = await _orchestratorService.CheckPendingStatusesAsync(null, cancellationToken);
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

            var syncResult = await _syncService.SyncAccountsAsync(null, cancellationToken);
            var jobCreationResult = await _orchestratorService.CreateJobsForDueAccountsAsync(cancellationToken);
            var credentialResult = await _orchestratorService.VerifyCredentialsAsync(null, cancellationToken);
            var scrapeResult = await _orchestratorService.ProcessScrapingAsync(null, cancellationToken);
            var statusResult = await _orchestratorService.CheckPendingStatusesAsync(null, cancellationToken);

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
                RunStatusCheck = request?.RunStatusCheck ?? true
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
    /// Gets the recent orchestration run history.
    /// </summary>
    [HttpGet("orchestrate/history")]
    [Authorize(AuthenticationSchemes = "Bearer,SchedulerApiKey")]
    public ActionResult<IEnumerable<AdrOrchestrationStatus>> GetOrchestrationHistory([FromQuery] int count = 10)
    {
        var statuses = _orchestrationQueue.GetRecentStatuses(count);
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

#endregion
