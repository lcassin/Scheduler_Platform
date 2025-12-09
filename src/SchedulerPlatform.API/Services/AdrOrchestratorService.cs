using System.Net.Http.Json;
using System.Text.Json;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Services;

public interface IAdrOrchestratorService
{
    Task<JobCreationResult> CreateJobsForDueAccountsAsync(CancellationToken cancellationToken = default);
    Task<CredentialVerificationResult> VerifyCredentialsAsync(CancellationToken cancellationToken = default);
    Task<ScrapeResult> ProcessScrapingAsync(CancellationToken cancellationToken = default);
    Task<StatusCheckResult> CheckPendingStatusesAsync(CancellationToken cancellationToken = default);
}

#region Result Classes

public class JobCreationResult
{
    public int JobsCreated { get; set; }
    public int JobsSkipped { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
}

public class CredentialVerificationResult
{
    public int JobsProcessed { get; set; }
    public int CredentialsVerified { get; set; }
    public int CredentialsFailed { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
}

public class ScrapeResult
{
    public int JobsProcessed { get; set; }
    public int ScrapesRequested { get; set; }
    public int ScrapesCompleted { get; set; }
    public int ScrapesFailed { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
}

public class StatusCheckResult
{
    public int JobsChecked { get; set; }
    public int JobsCompleted { get; set; }
    public int JobsNeedingReview { get; set; }
    public int JobsStillProcessing { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
}

#endregion

public class AdrOrchestratorService : IAdrOrchestratorService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdrOrchestratorService> _logger;

    private const int DefaultCredentialCheckLeadDays = 7;
    private const int DefaultScrapeRetryDays = 5;
    private const int DefaultFollowUpDelayDays = 5;
    private const int DefaultMaxRetries = 5;
    private const int BatchSize = 1000; // Process and save in batches to avoid large transactions

    public AdrOrchestratorService(
        IUnitOfWork unitOfWork,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AdrOrchestratorService> logger)
    {
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    #region Step 2: Job Creation

    public async Task<JobCreationResult> CreateJobsForDueAccountsAsync(CancellationToken cancellationToken = default)
    {
        var result = new JobCreationResult();

        try
        {
            _logger.LogInformation("Starting job creation for due accounts");

            var dueAccounts = await _unitOfWork.AdrAccounts.FindAsync(a =>
                !a.IsDeleted &&
                (a.NextRunStatus == "Run Now" || a.NextRunStatus == "Due Soon") &&
                a.HistoricalBillingStatus != "Missing" &&
                a.NextRangeStartDateTime.HasValue &&
                a.NextRangeEndDateTime.HasValue);

            int processedSinceLastSave = 0;
            int batchNumber = 1;
            var dueAccountsList = dueAccounts.ToList();
            _logger.LogInformation("Processing {Count} due accounts in batches of {BatchSize}", dueAccountsList.Count, BatchSize);

            foreach (var account in dueAccountsList)
            {
                try
                {
                    if (!account.NextRangeStartDateTime.HasValue || !account.NextRangeEndDateTime.HasValue)
                    {
                        result.JobsSkipped++;
                        continue;
                    }

                    var existingJob = await _unitOfWork.AdrJobs.ExistsForBillingPeriodAsync(
                        account.Id,
                        account.NextRangeStartDateTime.Value,
                        account.NextRangeEndDateTime.Value);

                    if (existingJob)
                    {
                        result.JobsSkipped++;
                        continue;
                    }

                    var job = new AdrJob
                    {
                        AdrAccountId = account.Id,
                        VMAccountId = account.VMAccountId,
                        VMAccountNumber = account.VMAccountNumber,
                        CredentialId = account.CredentialId,
                        PeriodType = account.PeriodType,
                        BillingPeriodStartDateTime = account.NextRangeStartDateTime.Value,
                        BillingPeriodEndDateTime = account.NextRangeEndDateTime.Value,
                        NextRunDateTime = account.NextRunDateTime,
                        NextRangeStartDateTime = account.NextRangeStartDateTime,
                        NextRangeEndDateTime = account.NextRangeEndDateTime,
                        Status = "Pending",
                        IsMissing = account.HistoricalBillingStatus == "Missing",
                        RetryCount = 0,
                        CreatedDateTime = DateTime.UtcNow,
                        CreatedBy = "System Created",
                        ModifiedDateTime = DateTime.UtcNow,
                        ModifiedBy = "System Created"
                    };

                    await _unitOfWork.AdrJobs.AddAsync(job);
                    result.JobsCreated++;
                    processedSinceLastSave++;

                    // Save in batches to reduce transaction size
                    if (processedSinceLastSave >= BatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogInformation("Job creation batch {BatchNumber} saved: {Count} jobs created so far", 
                            batchNumber, result.JobsCreated);
                        processedSinceLastSave = 0;
                        batchNumber++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating job for account {AccountId}", account.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Account {account.Id}: {ex.Message}");
                }
            }

            // Final save for remaining jobs
            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Job creation completed. Created: {Created}, Skipped: {Skipped}, Errors: {Errors}",
                result.JobsCreated, result.JobsSkipped, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job creation failed");
            result.Errors++;
            result.ErrorMessages.Add($"Job creation failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Step 3: Credential Verification

    public async Task<CredentialVerificationResult> VerifyCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var result = new CredentialVerificationResult();

        try
        {
            _logger.LogInformation("Starting credential verification");

            var jobsNeedingVerification = (await _unitOfWork.AdrJobs.GetJobsNeedingCredentialVerificationAsync(DateTime.UtcNow)).ToList();
            int processedSinceLastSave = 0;
            int batchNumber = 1;
            _logger.LogInformation("Processing {Count} jobs for credential verification in batches of {BatchSize}", jobsNeedingVerification.Count, BatchSize);

            foreach (var job in jobsNeedingVerification)
            {
                try
                {
                    result.JobsProcessed++;

                    // Set "in-progress" status and save BEFORE calling external API
                    // This prevents double-billing if the process crashes after the API call
                    job.Status = "CredentialCheckInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    await _unitOfWork.AdrJobs.UpdateAsync(job);
                    await _unitOfWork.SaveChangesAsync();

                    var execution = await CreateExecutionAsync(job.Id, (int)AdrRequestType.AttemptLogin);
                    
                    var apiResult = await CallAdrApiAsync(
                        AdrRequestType.AttemptLogin,
                        job.CredentialId,
                        job.NextRangeStartDateTime,
                        job.NextRangeEndDateTime,
                        job.Id,
                        cancellationToken);

                    await CompleteExecutionAsync(execution, apiResult);

                    if (apiResult.IsSuccess)
                    {
                        job.Status = "CredentialVerified";
                        job.CredentialVerifiedDateTime = DateTime.UtcNow;
                        job.AdrStatusId = apiResult.StatusId;
                        job.AdrStatusDescription = apiResult.StatusDescription;
                        job.AdrIndexId = apiResult.IndexId;
                        result.CredentialsVerified++;
                    }
                    else
                    {
                        job.Status = "CredentialFailed";
                        job.ErrorMessage = apiResult.ErrorMessage;
                        job.AdrStatusId = apiResult.StatusId;
                        job.AdrStatusDescription = apiResult.StatusDescription;
                        job.RetryCount++;
                        result.CredentialsFailed++;
                    }

                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    await _unitOfWork.AdrJobs.UpdateAsync(job);
                    processedSinceLastSave++;

                    // Save in batches to reduce transaction size
                    if (processedSinceLastSave >= BatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogInformation("Credential verification batch {BatchNumber} saved: {Count} jobs processed so far", 
                            batchNumber, result.JobsProcessed);
                        processedSinceLastSave = 0;
                        batchNumber++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error verifying credentials for job {JobId}", job.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {job.Id}: {ex.Message}");
                }
            }

            // Final save for remaining jobs
            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Credential verification completed. Processed: {Processed}, Verified: {Verified}, Failed: {Failed}, Errors: {Errors}",
                result.JobsProcessed, result.CredentialsVerified, result.CredentialsFailed, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Credential verification failed");
            result.Errors++;
            result.ErrorMessages.Add($"Credential verification failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Step 4: Invoice Scraping

    public async Task<ScrapeResult> ProcessScrapingAsync(CancellationToken cancellationToken = default)
    {
        var result = new ScrapeResult();

        try
        {
            _logger.LogInformation("Starting invoice scraping");

            var jobsReadyForScraping = (await _unitOfWork.AdrJobs.GetJobsReadyForScrapingAsync(DateTime.UtcNow)).ToList();
            int processedSinceLastSave = 0;
            int batchNumber = 1;
            _logger.LogInformation("Processing {Count} jobs for scraping in batches of {BatchSize}", jobsReadyForScraping.Count, BatchSize);

            foreach (var job in jobsReadyForScraping)
            {
                try
                {
                    result.JobsProcessed++;

                    // Set "in-progress" status and save BEFORE calling external API
                    // This prevents double-billing if the process crashes after the API call
                    job.Status = "ScrapeInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    await _unitOfWork.AdrJobs.UpdateAsync(job);
                    await _unitOfWork.SaveChangesAsync();

                    var execution = await CreateExecutionAsync(job.Id, (int)AdrRequestType.DownloadInvoice);

                    var apiResult = await CallAdrApiAsync(
                        AdrRequestType.DownloadInvoice,
                        job.CredentialId,
                        job.NextRangeStartDateTime,
                        job.NextRangeEndDateTime,
                        job.Id,
                        cancellationToken);

                    await CompleteExecutionAsync(execution, apiResult);

                    if (apiResult.IsSuccess)
                    {
                        job.Status = "ScrapeRequested";
                        job.AdrStatusId = apiResult.StatusId;
                        job.AdrStatusDescription = apiResult.StatusDescription;
                        job.AdrIndexId = apiResult.IndexId;
                        result.ScrapesRequested++;

                        if (apiResult.IsFinal && apiResult.StatusId == (int)AdrStatus.Complete)
                        {
                            job.Status = "Completed";
                            job.ScrapingCompletedDateTime = DateTime.UtcNow;
                            result.ScrapesCompleted++;
                        }
                    }
                    else
                    {
                        job.Status = "ScrapeFailed";
                        job.ErrorMessage = apiResult.ErrorMessage;
                        job.AdrStatusId = apiResult.StatusId;
                        job.AdrStatusDescription = apiResult.StatusDescription;
                        job.RetryCount++;
                        result.ScrapesFailed++;
                    }

                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    await _unitOfWork.AdrJobs.UpdateAsync(job);
                    processedSinceLastSave++;

                    // Save in batches to reduce transaction size
                    if (processedSinceLastSave >= BatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogInformation("Scraping batch {BatchNumber} saved: {Count} jobs processed so far", 
                            batchNumber, result.JobsProcessed);
                        processedSinceLastSave = 0;
                        batchNumber++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing scrape for job {JobId}", job.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {job.Id}: {ex.Message}");
                }
            }

            // Final save for remaining jobs
            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Invoice scraping completed. Processed: {Processed}, Requested: {Requested}, Completed: {Completed}, Failed: {Failed}, Errors: {Errors}",
                result.JobsProcessed, result.ScrapesRequested, result.ScrapesCompleted, result.ScrapesFailed, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invoice scraping failed");
            result.Errors++;
            result.ErrorMessages.Add($"Invoice scraping failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Status Checking

    public async Task<StatusCheckResult> CheckPendingStatusesAsync(CancellationToken cancellationToken = default)
    {
        var result = new StatusCheckResult();

        try
        {
            _logger.LogInformation("Starting status check for pending jobs");

            var jobsNeedingStatusCheck = (await _unitOfWork.AdrJobs.GetJobsNeedingStatusCheckAsync(
                DateTime.UtcNow, 
                DefaultFollowUpDelayDays)).ToList();
            int processedSinceLastSave = 0;
            int batchNumber = 1;
            _logger.LogInformation("Processing {Count} jobs for status check in batches of {BatchSize}", jobsNeedingStatusCheck.Count, BatchSize);

            foreach (var job in jobsNeedingStatusCheck)
            {
                try
                {
                    result.JobsChecked++;

                    // Set "in-progress" status and save BEFORE calling external API
                    // This prevents double-billing if the process crashes after the API call
                    job.Status = "StatusCheckInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    await _unitOfWork.AdrJobs.UpdateAsync(job);
                    await _unitOfWork.SaveChangesAsync();

                    var statusResult = await CheckJobStatusAsync(job.Id, cancellationToken);

                    if (statusResult != null)
                    {
                        job.AdrStatusId = statusResult.StatusId;
                        job.AdrStatusDescription = statusResult.StatusDescription;

                        if (statusResult.IsFinal)
                        {
                            if (statusResult.StatusId == (int)AdrStatus.Complete)
                            {
                                job.Status = "Completed";
                                job.ScrapingCompletedDateTime = DateTime.UtcNow;
                                result.JobsCompleted++;
                            }
                            else if (statusResult.StatusId == (int)AdrStatus.NeedsHumanReview)
                            {
                                job.Status = "NeedsReview";
                                job.ScrapingCompletedDateTime = DateTime.UtcNow;
                                result.JobsNeedingReview++;
                            }
                        }
                        else
                        {
                            result.JobsStillProcessing++;
                        }

                        job.ModifiedDateTime = DateTime.UtcNow;
                        job.ModifiedBy = "System Created";
                        await _unitOfWork.AdrJobs.UpdateAsync(job);
                        processedSinceLastSave++;

                        // Save in batches to reduce transaction size
                        if (processedSinceLastSave >= BatchSize)
                        {
                            await _unitOfWork.SaveChangesAsync();
                            _logger.LogInformation("Status check batch {BatchNumber} saved: {Count} jobs checked so far", 
                                batchNumber, result.JobsChecked);
                            processedSinceLastSave = 0;
                            batchNumber++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking status for job {JobId}", job.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {job.Id}: {ex.Message}");
                }
            }

            // Final save for remaining jobs
            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Status check completed. Checked: {Checked}, Completed: {Completed}, NeedsReview: {NeedsReview}, Processing: {Processing}, Errors: {Errors}",
                result.JobsChecked, result.JobsCompleted, result.JobsNeedingReview, result.JobsStillProcessing, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Status check failed");
            result.Errors++;
            result.ErrorMessages.Add($"Status check failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task<AdrJobExecution> CreateExecutionAsync(int adrJobId, int adrRequestTypeId)
    {
        var execution = new AdrJobExecution
        {
            AdrJobId = adrJobId,
            AdrRequestTypeId = adrRequestTypeId,
            StartDateTime = DateTime.UtcNow,
            CreatedDateTime = DateTime.UtcNow,
            CreatedBy = "System Created",
            ModifiedDateTime = DateTime.UtcNow,
            ModifiedBy = "System Created"
        };

        await _unitOfWork.AdrJobExecutions.AddAsync(execution);
        await _unitOfWork.SaveChangesAsync();

        return execution;
    }

    private async Task CompleteExecutionAsync(AdrJobExecution execution, AdrApiResult apiResult)
    {
        execution.EndDateTime = DateTime.UtcNow;
        execution.AdrStatusId = apiResult.StatusId;
        execution.AdrStatusDescription = apiResult.StatusDescription;
        execution.AdrIndexId = apiResult.IndexId;
        execution.HttpStatusCode = apiResult.HttpStatusCode;
        execution.IsSuccess = apiResult.IsSuccess;
        execution.IsError = apiResult.IsError;
        execution.IsFinal = apiResult.IsFinal;
        execution.ErrorMessage = apiResult.ErrorMessage;
        execution.ApiResponse = apiResult.RawResponse;
        execution.ModifiedDateTime = DateTime.UtcNow;
        execution.ModifiedBy = "System Created";

        await _unitOfWork.AdrJobExecutions.UpdateAsync(execution);
    }

    private async Task<AdrApiResult> CallAdrApiAsync(
        AdrRequestType requestType,
        int credentialId,
        DateTime? startDate,
        DateTime? endDate,
        int jobId,
        CancellationToken cancellationToken)
    {
        var result = new AdrApiResult();

        try
        {
            var baseUrl = _configuration["AdrApi:BaseUrl"] ?? "https://nuse2etsadrdevfn01.azurewebsites.net/api/";
            var recipientEmail = _configuration["AdrApi:RecipientEmail"] ?? "lcassin@cassinfo.com";

            var client = _httpClientFactory.CreateClient("AdrApi");

            var request = new
            {
                ADRRequestTypeId = (int)requestType,
                CredentialId = credentialId,
                StartDate = startDate?.ToString("yyyy-MM-dd"),
                EndDate = endDate?.ToString("yyyy-MM-dd"),
                SourceApplicationName = "ADRScheduler",
                RecipientEmail = recipientEmail,
                JobId = jobId
            };

            var response = await client.PostAsJsonAsync(
                $"{baseUrl}IngestAdrRequest",
                request,
                cancellationToken);

            result.HttpStatusCode = (int)response.StatusCode;

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            result.RawResponse = responseContent;

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<AdrApiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse != null)
                {
                    result.StatusId = apiResponse.StatusId;
                    result.StatusDescription = apiResponse.StatusDescription;
                    result.IndexId = apiResponse.IndexId;
                    result.IsSuccess = true;
                    result.IsError = apiResponse.IsError;
                    result.IsFinal = apiResponse.IsFinal;
                }
            }
            else
            {
                result.IsSuccess = false;
                result.IsError = true;
                result.ErrorMessage = $"API returned {response.StatusCode}: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling ADR API");
            result.IsSuccess = false;
            result.IsError = true;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<AdrApiResult?> CheckJobStatusAsync(int jobId, CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = _configuration["AdrApi:BaseUrl"] ?? "https://nuse2etsadrdevfn01.azurewebsites.net/api/";

            var client = _httpClientFactory.CreateClient("AdrApi");

            var response = await client.GetAsync(
                $"{baseUrl}GetRequestStatusByJobId/{jobId}",
                cancellationToken);

            var result = new AdrApiResult
            {
                HttpStatusCode = (int)response.StatusCode
            };

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            result.RawResponse = responseContent;

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<AdrApiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse != null)
                {
                    result.StatusId = apiResponse.StatusId;
                    result.StatusDescription = apiResponse.StatusDescription;
                    result.IndexId = apiResponse.IndexId;
                    result.IsSuccess = true;
                    result.IsError = apiResponse.IsError;
                    result.IsFinal = apiResponse.IsFinal;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking job status for job {JobId}", jobId);
            return null;
        }
    }

    #endregion

    #region Private Classes

    private class AdrApiResult
    {
        public int? StatusId { get; set; }
        public string? StatusDescription { get; set; }
        public long? IndexId { get; set; }
        public int? HttpStatusCode { get; set; }
        public bool IsSuccess { get; set; }
        public bool IsError { get; set; }
        public bool IsFinal { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RawResponse { get; set; }
    }

    private class AdrApiResponse
    {
        public int StatusId { get; set; }
        public string? StatusDescription { get; set; }
        public long? IndexId { get; set; }
        public bool IsError { get; set; }
        public bool IsFinal { get; set; }
    }

    #endregion
}
