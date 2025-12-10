using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.Core.Domain.Interfaces;

namespace SchedulerPlatform.API.Services;

public interface IAdrOrchestratorService
{
    Task<JobCreationResult> CreateJobsForDueAccountsAsync(CancellationToken cancellationToken = default);
    Task<CredentialVerificationResult> VerifyCredentialsAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
    Task<ScrapeResult> ProcessScrapingAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
    Task<StatusCheckResult> CheckPendingStatusesAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdrOrchestratorService> _logger;

    private const int DefaultCredentialCheckLeadDays = 7;
    private const int DefaultScrapeRetryDays = 5;
    private const int DefaultFollowUpDelayDays = 5;
    private const int DefaultMaxRetries = 5;
    private const int BatchSize = 1000; // Process and save in batches to avoid large transactions
    private const int DefaultMaxParallelRequests = 8; // Default parallel API requests

    public AdrOrchestratorService(
        IUnitOfWork unitOfWork,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AdrOrchestratorService> logger)
    {
        _unitOfWork = unitOfWork;
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private int GetMaxParallelRequests()
    {
        return _configuration.GetValue<int>("AdrOrchestration:MaxParallelRequests", DefaultMaxParallelRequests);
    }

    private int GetCredentialCheckLeadDays()
    {
        return _configuration.GetValue<int>("AdrOrchestration:CredentialCheckLeadDays", DefaultCredentialCheckLeadDays);
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
                        VendorCode = account.VendorCode,
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

    public async Task<CredentialVerificationResult> VerifyCredentialsAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var result = new CredentialVerificationResult();
        var maxParallel = GetMaxParallelRequests();
        var credentialCheckLeadDays = GetCredentialCheckLeadDays();

        try
        {
            _logger.LogInformation("Starting credential verification with {MaxParallel} parallel workers, {LeadDays} day lead time", 
                maxParallel, credentialCheckLeadDays);

            var jobsNeedingVerification = (await _unitOfWork.AdrJobs.GetJobsNeedingCredentialVerificationAsync(DateTime.UtcNow, credentialCheckLeadDays)).ToList();
            _logger.LogInformation("Found {Count} jobs needing credential verification (NextRunDate within {LeadDays} days)", 
                jobsNeedingVerification.Count, credentialCheckLeadDays);

            // Report initial progress (0 of total)
            progressCallback?.Invoke(0, jobsNeedingVerification.Count);

            if (!jobsNeedingVerification.Any())
            {
                return result;
            }

            // Step 1: Mark all jobs as "InProgress" in batches (for idempotency)
            // This prevents double-billing if the process crashes after the API call
            // Batching reduces database round-trips from 2*N to N/batchSize
            const int setupBatchSize = 500;
            var jobsToProcess = new List<(int JobId, int CredentialId, DateTime? StartDate, DateTime? EndDate, int ExecutionId)>();
            int markedCount = 0;
            int processedSinceLastSave = 0;
            var startTime = DateTime.UtcNow;
            
            _logger.LogInformation("Starting to mark {Count} jobs as in-progress (batch size: {BatchSize})", 
                jobsNeedingVerification.Count, setupBatchSize);
            
            foreach (var job in jobsNeedingVerification)
            {
                try
                {
                    job.Status = "CredentialCheckInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    await _unitOfWork.AdrJobs.UpdateAsync(job);

                    var execution = await CreateExecutionAsync(job.Id, (int)AdrRequestType.AttemptLogin, saveChanges: false);
                    jobsToProcess.Add((job.Id, job.CredentialId, job.NextRangeStartDateTime, job.NextRangeEndDateTime, execution.Id));
                    
                    markedCount++;
                    processedSinceLastSave++;
                    
                    // Save in batches to reduce database round-trips
                    if (processedSinceLastSave >= setupBatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        processedSinceLastSave = 0;
                        _logger.LogInformation("Marked {Marked}/{Total} jobs as in-progress (batch saved)", 
                            markedCount, jobsNeedingVerification.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error marking job {JobId} as in-progress", job.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {job.Id}: {ex.Message}");
                    markedCount++;
                }
            }
            
            // Save any remaining jobs
            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }
            
            var setupDuration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Marked {Count} jobs as in-progress in {Duration:F1} seconds, starting parallel API calls", 
                jobsToProcess.Count, setupDuration.TotalSeconds);

            // Step 2: Call ADR API in parallel with semaphore to limit concurrency
            var apiResults = new ConcurrentDictionary<int, (AdrApiResult Result, int ExecutionId)>();
            using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            int completedApiCalls = 0;
            var totalApiCalls = jobsToProcess.Count;
            
            var tasks = jobsToProcess.Select(async jobInfo =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var apiResult = await CallAdrApiAsync(
                        AdrRequestType.AttemptLogin,
                        jobInfo.CredentialId,
                        jobInfo.StartDate,
                        jobInfo.EndDate,
                        jobInfo.JobId,
                        cancellationToken);

                    apiResults[jobInfo.JobId] = (apiResult, jobInfo.ExecutionId);
                    
                    // Log and report progress every 50 completions or at the end
                    var count = Interlocked.Increment(ref completedApiCalls);
                    if (count % 50 == 0 || count == totalApiCalls)
                    {
                        progressCallback?.Invoke(count, totalApiCalls);
                    }
                    if (count % 500 == 0 || count == totalApiCalls)
                    {
                        _logger.LogInformation(
                            "Credential verification API calls: {Completed}/{Total} completed ({Percent:F1}%)",
                            count, totalApiCalls, (double)count / totalApiCalls * 100);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling ADR API for job {JobId}", jobInfo.JobId);
                    apiResults[jobInfo.JobId] = (new AdrApiResult 
                    { 
                        IsSuccess = false, 
                        IsError = true, 
                        ErrorMessage = ex.Message 
                    }, jobInfo.ExecutionId);
                    
                    var count = Interlocked.Increment(ref completedApiCalls);
                    if (count % 50 == 0 || count == totalApiCalls)
                    {
                        progressCallback?.Invoke(count, totalApiCalls);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Completed {Count} parallel API calls, updating job statuses", apiResults.Count);

            // Step 3: Update job statuses sequentially (EF DbContext is not thread-safe)
            int processedSinceLastSave = 0;
            int batchNumber = 1;

            foreach (var jobInfo in jobsToProcess)
            {
                try
                {
                    result.JobsProcessed++;

                    if (!apiResults.TryGetValue(jobInfo.JobId, out var apiResultInfo))
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobInfo.JobId}: No API result found");
                        continue;
                    }

                    var (apiResult, executionId) = apiResultInfo;

                    // Update execution record
                    var execution = await _unitOfWork.AdrJobExecutions.GetByIdAsync(executionId);
                    if (execution != null)
                    {
                        await CompleteExecutionAsync(execution, apiResult);
                    }

                    // Update job record
                    var job = await _unitOfWork.AdrJobs.GetByIdAsync(jobInfo.JobId);
                    if (job == null)
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobInfo.JobId}: Job not found after API call");
                        continue;
                    }

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
                        if (apiResult.IndexId.HasValue)
                        {
                            job.AdrIndexId = apiResult.IndexId;
                        }
                        job.RetryCount++;
                        result.CredentialsFailed++;
                    }

                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    await _unitOfWork.AdrJobs.UpdateAsync(job);
                    processedSinceLastSave++;

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
                    _logger.LogError(ex, "Error updating job {JobId} after API call", jobInfo.JobId);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {jobInfo.JobId}: {ex.Message}");
                }
            }

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

    public async Task<ScrapeResult> ProcessScrapingAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var result = new ScrapeResult();
        var maxParallel = GetMaxParallelRequests();

        try
        {
            _logger.LogInformation("Starting invoice scraping with {MaxParallel} parallel workers", maxParallel);

            var jobsReadyForScraping = (await _unitOfWork.AdrJobs.GetJobsReadyForScrapingAsync(DateTime.UtcNow)).ToList();
            _logger.LogInformation("Found {Count} jobs ready for scraping", jobsReadyForScraping.Count);

            // Report initial progress (0 of total)
            progressCallback?.Invoke(0, jobsReadyForScraping.Count);

            if (!jobsReadyForScraping.Any())
            {
                return result;
            }

            // Step 1: Mark all jobs as "InProgress" in batches (for idempotency)
            const int setupBatchSize = 500;
            var jobsToProcess = new List<(int JobId, int CredentialId, DateTime? StartDate, DateTime? EndDate, int ExecutionId)>();
            int markedCount = 0;
            int processedSinceLastSave = 0;
            var startTime = DateTime.UtcNow;
            
            _logger.LogInformation("Starting to mark {Count} jobs as in-progress for scraping (batch size: {BatchSize})", 
                jobsReadyForScraping.Count, setupBatchSize);
            
            foreach (var job in jobsReadyForScraping)
            {
                try
                {
                    job.Status = "ScrapeInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    await _unitOfWork.AdrJobs.UpdateAsync(job);

                    var execution = await CreateExecutionAsync(job.Id, (int)AdrRequestType.DownloadInvoice, saveChanges: false);
                    jobsToProcess.Add((job.Id, job.CredentialId, job.NextRangeStartDateTime, job.NextRangeEndDateTime, execution.Id));
                    
                    markedCount++;
                    processedSinceLastSave++;
                    
                    // Save in batches to reduce database round-trips
                    if (processedSinceLastSave >= setupBatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        processedSinceLastSave = 0;
                        _logger.LogInformation("Marked {Marked}/{Total} jobs as in-progress for scraping (batch saved)", 
                            markedCount, jobsReadyForScraping.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error marking job {JobId} as in-progress for scraping", job.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {job.Id}: {ex.Message}");
                    markedCount++;
                }
            }
            
            // Save any remaining jobs
            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }
            
            var setupDuration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Marked {Count} jobs as in-progress for scraping in {Duration:F1} seconds, starting parallel API calls", 
                jobsToProcess.Count, setupDuration.TotalSeconds);

            // Step 2: Call ADR API in parallel with semaphore to limit concurrency
            var apiResults = new ConcurrentDictionary<int, (AdrApiResult Result, int ExecutionId)>();
            using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            int completedApiCalls = 0;
            var totalApiCalls = jobsToProcess.Count;
            
            var tasks = jobsToProcess.Select(async jobInfo =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var apiResult = await CallAdrApiAsync(
                        AdrRequestType.DownloadInvoice,
                        jobInfo.CredentialId,
                        jobInfo.StartDate,
                        jobInfo.EndDate,
                        jobInfo.JobId,
                        cancellationToken);

                    apiResults[jobInfo.JobId] = (apiResult, jobInfo.ExecutionId);
                    
                    // Log and report progress every 50 completions or at the end
                    var count = Interlocked.Increment(ref completedApiCalls);
                    if (count % 50 == 0 || count == totalApiCalls)
                    {
                        progressCallback?.Invoke(count, totalApiCalls);
                    }
                    if (count % 500 == 0 || count == totalApiCalls)
                    {
                        _logger.LogInformation(
                            "Invoice scraping API calls: {Completed}/{Total} completed ({Percent:F1}%)",
                            count, totalApiCalls, (double)count / totalApiCalls * 100);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling ADR API for scraping job {JobId}", jobInfo.JobId);
                    apiResults[jobInfo.JobId] = (new AdrApiResult 
                    { 
                        IsSuccess = false, 
                        IsError = true, 
                        ErrorMessage = ex.Message 
                    }, jobInfo.ExecutionId);
                    
                    var count = Interlocked.Increment(ref completedApiCalls);
                    if (count % 50 == 0 || count == totalApiCalls)
                    {
                        progressCallback?.Invoke(count, totalApiCalls);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Completed {Count} parallel API calls, updating job statuses", apiResults.Count);

            // Step 3: Update job statuses sequentially
            int processedSinceLastSave = 0;
            int batchNumber = 1;

            foreach (var jobInfo in jobsToProcess)
            {
                try
                {
                    result.JobsProcessed++;

                    if (!apiResults.TryGetValue(jobInfo.JobId, out var apiResultInfo))
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobInfo.JobId}: No API result found");
                        continue;
                    }

                    var (apiResult, executionId) = apiResultInfo;

                    // Update execution record
                    var execution = await _unitOfWork.AdrJobExecutions.GetByIdAsync(executionId);
                    if (execution != null)
                    {
                        await CompleteExecutionAsync(execution, apiResult);
                    }

                    // Update job record
                    var job = await _unitOfWork.AdrJobs.GetByIdAsync(jobInfo.JobId);
                    if (job == null)
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobInfo.JobId}: Job not found after API call");
                        continue;
                    }

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
                        if (apiResult.IndexId.HasValue)
                        {
                            job.AdrIndexId = apiResult.IndexId;
                        }
                        job.RetryCount++;
                        result.ScrapesFailed++;
                    }

                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    await _unitOfWork.AdrJobs.UpdateAsync(job);
                    processedSinceLastSave++;

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
                    _logger.LogError(ex, "Error updating job {JobId} after scraping API call", jobInfo.JobId);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {jobInfo.JobId}: {ex.Message}");
                }
            }

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

    public async Task<StatusCheckResult> CheckPendingStatusesAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var result = new StatusCheckResult();
        var maxParallel = GetMaxParallelRequests();

        try
        {
            _logger.LogInformation("Starting status check with {MaxParallel} parallel workers", maxParallel);

            var jobsNeedingStatusCheck = (await _unitOfWork.AdrJobs.GetJobsNeedingStatusCheckAsync(
                DateTime.UtcNow, 
                DefaultFollowUpDelayDays)).ToList();
            _logger.LogInformation("Found {Count} jobs needing status check", jobsNeedingStatusCheck.Count);

            // Report initial progress (0 of total)
            progressCallback?.Invoke(0, jobsNeedingStatusCheck.Count);

            if (!jobsNeedingStatusCheck.Any())
            {
                return result;
            }

            // Step 1: Mark all jobs as "InProgress" in batches (for idempotency)
            const int setupBatchSize = 500;
            var jobsToProcess = new List<int>();
            int markedCount = 0;
            int processedSinceLastSave = 0;
            var startTime = DateTime.UtcNow;
            
            _logger.LogInformation("Starting to mark {Count} jobs as in-progress for status check (batch size: {BatchSize})", 
                jobsNeedingStatusCheck.Count, setupBatchSize);
            
            foreach (var job in jobsNeedingStatusCheck)
            {
                try
                {
                    job.Status = "StatusCheckInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    await _unitOfWork.AdrJobs.UpdateAsync(job);
                    jobsToProcess.Add(job.Id);
                    
                    markedCount++;
                    processedSinceLastSave++;
                    
                    // Save in batches to reduce database round-trips
                    if (processedSinceLastSave >= setupBatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        processedSinceLastSave = 0;
                        _logger.LogInformation("Marked {Marked}/{Total} jobs as in-progress for status check (batch saved)", 
                            markedCount, jobsNeedingStatusCheck.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error marking job {JobId} as in-progress for status check", job.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {job.Id}: {ex.Message}");
                    markedCount++;
                }
            }
            
            // Save any remaining jobs
            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }
            
            var setupDuration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Marked {Count} jobs as in-progress for status check in {Duration:F1} seconds, starting parallel status checks", 
                jobsToProcess.Count, setupDuration.TotalSeconds);

            // Step 2: Check status in parallel with semaphore to limit concurrency
            var statusResults = new ConcurrentDictionary<int, AdrApiResult?>();
            using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            int completedStatusChecks = 0;
            var totalStatusChecks = jobsToProcess.Count;
            
            var tasks = jobsToProcess.Select(async jobId =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var statusResult = await CheckJobStatusAsync(jobId, cancellationToken);
                    statusResults[jobId] = statusResult;
                    
                    // Log and report progress every 50 completions or at the end
                    var count = Interlocked.Increment(ref completedStatusChecks);
                    if (count % 50 == 0 || count == totalStatusChecks)
                    {
                        progressCallback?.Invoke(count, totalStatusChecks);
                    }
                    if (count % 500 == 0 || count == totalStatusChecks)
                    {
                        _logger.LogInformation(
                            "Status check API calls: {Completed}/{Total} completed ({Percent:F1}%)",
                            count, totalStatusChecks, (double)count / totalStatusChecks * 100);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking status for job {JobId}", jobId);
                    statusResults[jobId] = null;
                    var count = Interlocked.Increment(ref completedStatusChecks);
                    if (count % 50 == 0 || count == totalStatusChecks)
                    {
                        progressCallback?.Invoke(count, totalStatusChecks);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Completed {Count} parallel status checks, updating job statuses", statusResults.Count);

            // Step 3: Update job statuses sequentially
            int processedSinceLastSave = 0;
            int batchNumber = 1;

            foreach (var jobId in jobsToProcess)
            {
                try
                {
                    result.JobsChecked++;

                    if (!statusResults.TryGetValue(jobId, out var statusResult) || statusResult == null)
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobId}: No status result found");
                        continue;
                    }

                    var job = await _unitOfWork.AdrJobs.GetByIdAsync(jobId);
                    if (job == null)
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobId}: Job not found after status check");
                        continue;
                    }

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
                        // Job is still processing - revert to ScrapeRequested so it gets picked up again
                        job.Status = "ScrapeRequested";
                        result.JobsStillProcessing++;
                    }

                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    await _unitOfWork.AdrJobs.UpdateAsync(job);
                    processedSinceLastSave++;

                    if (processedSinceLastSave >= BatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogInformation("Status check batch {BatchNumber} saved: {Count} jobs checked so far", 
                            batchNumber, result.JobsChecked);
                        processedSinceLastSave = 0;
                        batchNumber++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating job {JobId} after status check", jobId);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {jobId}: {ex.Message}");
                }
            }

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

    private async Task<AdrJobExecution> CreateExecutionAsync(int adrJobId, int adrRequestTypeId, bool saveChanges = true)
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
        
        if (saveChanges)
        {
            await _unitOfWork.SaveChangesAsync();
        }

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
                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    _logger.LogWarning("ADR API returned empty response for job {JobId}", jobId);
                    result.IsSuccess = true;
                    result.IsError = false;
                    result.StatusDescription = "ADR API returned no content.";
                }
                else
                {
                    try
                    {
                        var trimmed = responseContent.TrimStart();
                        if (trimmed.StartsWith("{"))
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
                            else
                            {
                                result.IsSuccess = false;
                                result.IsError = true;
                                result.ErrorMessage = "ADR API returned an empty JSON object.";
                            }
                        }
                        else if (trimmed.StartsWith("["))
                        {
                            var list = JsonSerializer.Deserialize<List<AdrApiResponse>>(responseContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            var apiResponse = list?.FirstOrDefault();
                            if (apiResponse != null)
                            {
                                result.StatusId = apiResponse.StatusId;
                                result.StatusDescription = apiResponse.StatusDescription;
                                result.IndexId = apiResponse.IndexId;
                                result.IsSuccess = true;
                                result.IsError = apiResponse.IsError;
                                result.IsFinal = apiResponse.IsFinal;
                                _logger.LogInformation("ADR API returned array response for job {JobId}, using first element", jobId);
                            }
                            else
                            {
                                result.IsSuccess = false;
                                result.IsError = true;
                                result.ErrorMessage = "ADR API returned an empty JSON array.";
                            }
                        }
                        else if (long.TryParse(trimmed, out var indexId))
                        {
                            // API returned just a number - this is the IndexId
                            result.IndexId = indexId;
                            result.IsSuccess = true;
                            result.IsError = false;
                            result.StatusDescription = "Request submitted successfully";
                            _logger.LogInformation("ADR API returned IndexId {IndexId} for job {JobId}", indexId, jobId);
                        }
                        else
                        {
                            _logger.LogWarning("ADR API returned unexpected content for job {JobId}: {Response}", 
                                jobId, TruncateResponse(responseContent));
                            result.IsSuccess = false;
                            result.IsError = true;
                            result.ErrorMessage = $"ADR API returned unexpected content: {TruncateResponse(responseContent)}";
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Error deserializing ADR API response for job {JobId}: {Response}", 
                            jobId, TruncateResponse(responseContent));
                        result.IsSuccess = false;
                        result.IsError = true;
                        result.ErrorMessage = $"Error deserializing ADR API response: {jsonEx.Message}. Raw: {TruncateResponse(responseContent)}";
                    }
                }
            }
            else
            {
                // 4XX errors may still contain an IndexId if the record was created
                // but credential verification or enqueuing failed
                result.IsSuccess = false;
                result.IsError = true;
                result.ErrorMessage = $"API returned {response.StatusCode}: {TruncateResponse(responseContent)}";
                
                // Try to extract IndexId from error response if present
                if (!string.IsNullOrWhiteSpace(responseContent))
                {
                    try
                    {
                        // Try to parse as JSON object with IndexId
                        var trimmed = responseContent.TrimStart();
                        if (trimmed.StartsWith("{"))
                        {
                            using var doc = JsonDocument.Parse(responseContent);
                            if (doc.RootElement.TryGetProperty("indexId", out var indexIdProp) ||
                                doc.RootElement.TryGetProperty("IndexId", out indexIdProp))
                            {
                                if (indexIdProp.TryGetInt64(out var indexId))
                                {
                                    result.IndexId = indexId;
                                    _logger.LogWarning("ADR API returned error with IndexId {IndexId} for job {JobId}: {Error}", 
                                        indexId, jobId, result.ErrorMessage);
                                }
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Ignore JSON parsing errors for error responses
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling ADR API for job {JobId}", jobId);
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

    private static string TruncateResponse(string? response, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(response))
            return string.Empty;
        return response.Length <= maxLength ? response : response.Substring(0, maxLength) + "...";
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
