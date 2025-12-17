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
    Task<StatusCheckResult> CheckAllScrapedStatusesAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default);
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
    private const int DailyStatusCheckDelayDays = 1;  // Check status the day after scraping
    private const int FinalStatusCheckDelayDays = 5;  // Final check 5 days after billing window ends
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

            // Use the new method that includes AdrAccountRules for rule tracking per BRD requirements
            var dueAccounts = await _unitOfWork.AdrAccounts.GetDueAccountsWithRulesAsync();

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

                    // Look up the active rule for this account (JobTypeId = 2 for DownloadInvoice/ADR Request)
                    // If a rule exists, stamp its ID on the job for tracking per BRD requirements
                    var accountRule = account.AdrAccountRules?
                        .FirstOrDefault(r => !r.IsDeleted && r.IsEnabled && r.JobTypeId == 2);

                    var job = new AdrJob
                    {
                        AdrAccountId = account.Id,
                        AdrAccountRuleId = accountRule?.Id,  // Track which rule created this job (null for legacy/manual jobs)
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
            var jobsToProcess = new List<(int JobId, int CredentialId, DateTime? StartDate, DateTime? EndDate, int ExecutionId, long VMAccountId, string? InterfaceAccountId)>();
            int markedCount = 0;
            int setupProcessedSinceLastSave = 0;
            var startTime = DateTime.UtcNow;
            
            _logger.LogInformation("Starting to mark {Count} jobs as in-progress (batch size: {BatchSize})", 
                jobsNeedingVerification.Count, setupBatchSize);
            
            // Store execution objects (not IDs) since IDs aren't assigned until SaveChangesAsync
            var executionsByJobId = new Dictionary<int, AdrJobExecution>();
            
            foreach (var job in jobsNeedingVerification)
            {
                try
                {
                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    // Calling UpdateAsync would scan all tracked entities on each iteration = O(N²)
                    job.Status = "CredentialCheckInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";

                    var execution = await CreateExecutionAsync(job.Id, (int)AdrRequestType.AttemptLogin, saveChanges: false);
                    executionsByJobId[job.Id] = execution;
                    
                    markedCount++;
                    setupProcessedSinceLastSave++;
                    
                    // Save in batches to reduce database round-trips
                    if (setupProcessedSinceLastSave >= setupBatchSize)
                    {
                        _logger.LogInformation("About to save credential-check setup batch: {Marked}/{Total} jobs", 
                            markedCount, jobsNeedingVerification.Count);
                        
                        var batchSaveStart = DateTime.UtcNow;
                        await _unitOfWork.SaveChangesAsync();
                        var batchSaveDuration = (DateTime.UtcNow - batchSaveStart).TotalSeconds;
                        
                        setupProcessedSinceLastSave = 0;
                        _logger.LogInformation("Saved credential-check setup batch: {Marked}/{Total} jobs in {Duration:F1} seconds", 
                            markedCount, jobsNeedingVerification.Count, batchSaveDuration);
                        
                        // Report progress during setup phase (use negative values to indicate setup)
                        // UI can show "Preparing: X / Total" instead of "Processing: X / Total"
                        progressCallback?.Invoke(-markedCount, jobsNeedingVerification.Count);
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
            if (setupProcessedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                progressCallback?.Invoke(-markedCount, jobsNeedingVerification.Count);
            }
            
            // Build jobsToProcess AFTER SaveChangesAsync so execution IDs are populated
            foreach (var job in jobsNeedingVerification)
            {
                if (executionsByJobId.TryGetValue(job.Id, out var execution))
                {
                    jobsToProcess.Add((job.Id, job.CredentialId, job.NextRangeStartDateTime, job.NextRangeEndDateTime, execution.Id, job.VMAccountId, job.AdrAccount?.InterfaceAccountId));
                }
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
                        jobInfo.VMAccountId,
                        jobInfo.InterfaceAccountId,
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
            // Use the job and execution objects we already have from the setup phase - no need to re-fetch
            int processedSinceLastSave = 0;
            int batchNumber = 1;

            // Create a lookup for jobs by ID (they're already tracked by EF from the setup phase)
            var jobsById = jobsNeedingVerification.ToDictionary(j => j.Id);

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

                    // Update execution record - use the one from our dictionary (already tracked by EF)
                    if (executionsByJobId.TryGetValue(jobInfo.JobId, out var execution))
                    {
                        // Update execution properties directly - no need to call UpdateAsync
                        execution.EndDateTime = DateTime.UtcNow;
                        execution.HttpStatusCode = apiResult.HttpStatusCode;
                        execution.IsSuccess = apiResult.IsSuccess;
                        execution.IsError = apiResult.IsError;
                        execution.IsFinal = apiResult.IsFinal;
                        execution.AdrStatusId = apiResult.StatusId;
                        execution.AdrStatusDescription = apiResult.StatusDescription;
                        execution.ErrorMessage = apiResult.IsSuccess ? null : apiResult.ErrorMessage;
                        execution.AdrIndexId = apiResult.IndexId;
                        execution.ModifiedDateTime = DateTime.UtcNow;
                        execution.ModifiedBy = "System Created";
                    }

                    // Update job record - use the one from our dictionary (already tracked by EF)
                    if (!jobsById.TryGetValue(jobInfo.JobId, out var job))
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

                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
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
            var jobsToProcess = new List<(int JobId, int CredentialId, DateTime? StartDate, DateTime? EndDate, int ExecutionId, long VMAccountId, string? InterfaceAccountId)>();
            var executionsByJobId = new Dictionary<int, AdrJobExecution>();
            int markedCount = 0;
            int setupProcessedSinceLastSave = 0;
            var startTime = DateTime.UtcNow;
            
            _logger.LogInformation("Starting to mark {Count} jobs as in-progress for scraping (batch size: {BatchSize})", 
                jobsReadyForScraping.Count, setupBatchSize);
            
            foreach (var job in jobsReadyForScraping)
            {
                try
                {
                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    // Calling UpdateAsync would scan all tracked entities on each iteration = O(N²)
                    job.Status = "ScrapeInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";

                    var execution = await CreateExecutionAsync(job.Id, (int)AdrRequestType.DownloadInvoice, saveChanges: false);
                    executionsByJobId[job.Id] = execution;
                    
                    markedCount++;
                    setupProcessedSinceLastSave++;
                    
                    // Save in batches to reduce database round-trips
                    if (setupProcessedSinceLastSave >= setupBatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        setupProcessedSinceLastSave = 0;
                        _logger.LogInformation("Marked {Marked}/{Total} jobs as in-progress for scraping (batch saved)", 
                            markedCount, jobsReadyForScraping.Count);
                        
                        // Report progress during setup phase (use negative values to indicate setup)
                        progressCallback?.Invoke(-markedCount, jobsReadyForScraping.Count);
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
            if (setupProcessedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                progressCallback?.Invoke(-markedCount, jobsReadyForScraping.Count);
            }
            
            // Build jobsToProcess AFTER SaveChangesAsync so execution IDs are populated
            foreach (var job in jobsReadyForScraping)
            {
                if (executionsByJobId.TryGetValue(job.Id, out var execution))
                {
                    jobsToProcess.Add((job.Id, job.CredentialId, job.NextRangeStartDateTime, job.NextRangeEndDateTime, execution.Id, job.VMAccountId, job.AdrAccount?.InterfaceAccountId));
                }
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
                        jobInfo.VMAccountId,
                        jobInfo.InterfaceAccountId,
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

            // Step 3: Update job statuses sequentially (EF DbContext is not thread-safe)
            // Use the job and execution objects we already have from the setup phase - no need to re-fetch
            int processedSinceLastSave = 0;
            int batchNumber = 1;

            // Create a lookup for jobs by ID (they're already tracked by EF from the setup phase)
            var jobsById = jobsReadyForScraping.ToDictionary(j => j.Id);

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

                    // Update execution record - use the one from our dictionary (already tracked by EF)
                    if (executionsByJobId.TryGetValue(jobInfo.JobId, out var execution))
                    {
                        // Update execution properties directly - no need to call UpdateAsync
                        execution.EndDateTime = DateTime.UtcNow;
                        execution.HttpStatusCode = apiResult.HttpStatusCode;
                        execution.IsSuccess = apiResult.IsSuccess;
                        execution.IsError = apiResult.IsError;
                        execution.IsFinal = apiResult.IsFinal;
                        execution.AdrStatusId = apiResult.StatusId;
                        execution.AdrStatusDescription = apiResult.StatusDescription;
                        execution.ErrorMessage = apiResult.IsSuccess ? null : apiResult.ErrorMessage;
                        execution.AdrIndexId = apiResult.IndexId;
                        execution.ModifiedDateTime = DateTime.UtcNow;
                        execution.ModifiedBy = "System Created";
                    }

                    // Update job record - use the one from our dictionary (already tracked by EF)
                    if (!jobsById.TryGetValue(jobInfo.JobId, out var job))
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

                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
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

            // Split status checks into two categories:
            // 1. Daily status checks (1-day delay): Jobs still in their billing window
            // 2. Final status checks (5-day delay after NextRangeEndDate): Jobs past their billing window
            var now = DateTime.UtcNow;
            
            var dailyJobs = (await _unitOfWork.AdrJobs.GetJobsNeedingDailyStatusCheckAsync(now, DailyStatusCheckDelayDays)).ToList();
            var finalJobs = (await _unitOfWork.AdrJobs.GetJobsNeedingFinalStatusCheckAsync(now, FinalStatusCheckDelayDays)).ToList();
            
            // Merge and deduplicate by job ID
            var jobsNeedingStatusCheck = dailyJobs
                .Concat(finalJobs)
                .GroupBy(j => j.Id)
                .Select(g => g.First())
                .ToList();
            
            _logger.LogInformation(
                "Status check selection: {DailyCount} daily, {FinalCount} final, {Total} total (after dedup)",
                dailyJobs.Count, finalJobs.Count, jobsNeedingStatusCheck.Count);

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
            int setupProcessedSinceLastSave = 0;
            var startTime = DateTime.UtcNow;
            
            _logger.LogInformation("Starting to mark {Count} jobs as in-progress for status check (batch size: {BatchSize})", 
                jobsNeedingStatusCheck.Count, setupBatchSize);
            
            foreach (var job in jobsNeedingStatusCheck)
            {
                try
                {
                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    // Calling UpdateAsync would scan all tracked entities on each iteration = O(N²)
                    job.Status = "StatusCheckInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    jobsToProcess.Add(job.Id);
                    
                    markedCount++;
                    setupProcessedSinceLastSave++;
                    
                    // Save in batches to reduce database round-trips
                    if (setupProcessedSinceLastSave >= setupBatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        setupProcessedSinceLastSave = 0;
                        _logger.LogInformation("Marked {Marked}/{Total} jobs as in-progress for status check (batch saved)", 
                            markedCount, jobsNeedingStatusCheck.Count);
                        
                        // Report progress during setup phase (use negative values to indicate setup)
                        progressCallback?.Invoke(-markedCount, jobsNeedingStatusCheck.Count);
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
            if (setupProcessedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                progressCallback?.Invoke(-markedCount, jobsNeedingStatusCheck.Count);
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

            // Step 3: Update job statuses sequentially (EF DbContext is not thread-safe)
            // Use the job objects we already have from the setup phase - no need to re-fetch
            int processedSinceLastSave = 0;
            int batchNumber = 1;

            // Create a lookup for jobs by ID (they're already tracked by EF from the setup phase)
            var jobsById = jobsNeedingStatusCheck.ToDictionary(j => j.Id);

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

                    // Use the job from our dictionary (already tracked by EF)
                    if (!jobsById.TryGetValue(jobId, out var job))
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
                            
                            // Update account's LastSuccessfulDownloadDate to help calculate next billing cycle
                            // Uses anti-creep logic: allows earlier dates but prevents late vendors from causing schedule drift
                            if (job.AdrAccount != null)
                            {
                                job.AdrAccount.LastSuccessfulDownloadDate = CalculateLastSuccessfulDownloadDate(
                                    job.AdrAccount.LastSuccessfulDownloadDate,
                                    job.NextRunDateTime,
                                    job.AdrAccount.PeriodDays);
                                job.AdrAccount.ModifiedDateTime = DateTime.UtcNow;
                                job.AdrAccount.ModifiedBy = "System Created";
                            }
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

                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
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

    /// <summary>
    /// Manual status check: Checks status for ALL jobs that have been scraped, regardless of timing criteria.
    /// This is used by the "Check Statuses Only" button to check status for all ScrapeRequested jobs
    /// since there's no cost to check status from the ADR API.
    /// </summary>
    public async Task<StatusCheckResult> CheckAllScrapedStatusesAsync(Action<int, int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var result = new StatusCheckResult();
        var maxParallel = GetMaxParallelRequests();

        try
        {
            _logger.LogInformation("Starting MANUAL status check (all scraped jobs) with {MaxParallel} parallel workers", maxParallel);

            // Get ALL jobs that have been scraped, regardless of timing criteria
            var jobsNeedingStatusCheck = (await _unitOfWork.AdrJobs.GetAllJobsForManualStatusCheckAsync()).ToList();
            
            _logger.LogInformation(
                "Manual status check: Found {Count} jobs in ScrapeRequested/StatusCheckInProgress status",
                jobsNeedingStatusCheck.Count);

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
            int setupProcessedSinceLastSave = 0;
            var startTime = DateTime.UtcNow;
            
            _logger.LogInformation("Starting to mark {Count} jobs as in-progress for manual status check (batch size: {BatchSize})", 
                jobsNeedingStatusCheck.Count, setupBatchSize);
            
            foreach (var job in jobsNeedingStatusCheck)
            {
                try
                {
                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    job.Status = "StatusCheckInProgress";
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    jobsToProcess.Add(job.Id);
                    
                    markedCount++;
                    setupProcessedSinceLastSave++;
                    
                    // Save in batches to reduce database round-trips
                    if (setupProcessedSinceLastSave >= setupBatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        setupProcessedSinceLastSave = 0;
                        _logger.LogInformation("Marked {Marked}/{Total} jobs as in-progress for manual status check (batch saved)", 
                            markedCount, jobsNeedingStatusCheck.Count);
                        
                        // Report progress during setup phase (use negative values to indicate setup)
                        progressCallback?.Invoke(-markedCount, jobsNeedingStatusCheck.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error marking job {JobId} as in-progress for manual status check", job.Id);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {job.Id}: {ex.Message}");
                    markedCount++;
                }
            }
            
            // Save any remaining jobs
            if (setupProcessedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                progressCallback?.Invoke(-markedCount, jobsNeedingStatusCheck.Count);
            }
            
            var setupDuration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Marked {Count} jobs as in-progress for manual status check in {Duration:F1} seconds, starting parallel status checks", 
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
                            "Manual status check API calls: {Completed}/{Total} completed ({Percent:F1}%)",
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

            _logger.LogInformation("Completed {Count} parallel manual status checks, updating job statuses", statusResults.Count);

            // Log status distribution summary for debugging
            var statusSummary = statusResults.Values
                .Where(r => r != null)
                .GroupBy(r => new { r!.StatusId, r.IsFinal, r.IsSuccess })
                .Select(g => new { g.Key.StatusId, g.Key.IsFinal, g.Key.IsSuccess, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();
            
            _logger.LogInformation("=== STATUS CHECK DISTRIBUTION SUMMARY ===");
            _logger.LogInformation("Expected Complete StatusId = {CompleteId}, NeedsReview StatusId = {NeedsReviewId}", 
                (int)AdrStatus.Complete, (int)AdrStatus.NeedsHumanReview);
            foreach (var entry in statusSummary)
            {
                _logger.LogInformation(
                    "StatusId={StatusId}, IsFinal={IsFinal}, IsSuccess={IsSuccess}, Count={Count}",
                    entry.StatusId, entry.IsFinal, entry.IsSuccess, entry.Count);
            }
            
            // Log a few sample raw responses for debugging
            var sampleResponses = statusResults.Values
                .Where(r => r != null && !string.IsNullOrEmpty(r!.RawResponse))
                .Take(3)
                .ToList();
            foreach (var sample in sampleResponses)
            {
                _logger.LogInformation("Sample raw response: {RawResponse}", TruncateResponse(sample!.RawResponse, 500));
            }
            _logger.LogInformation("=== END STATUS CHECK DISTRIBUTION SUMMARY ===");

            // Step 3: Update job statuses sequentially (EF DbContext is not thread-safe)
            int processedSinceLastSave = 0;
            int batchNumber = 1;

            // Create a lookup for jobs by ID (they're already tracked by EF from the setup phase)
            var jobsById = jobsNeedingStatusCheck.ToDictionary(j => j.Id);

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

                    // Use the job from our dictionary (already tracked by EF)
                    if (!jobsById.TryGetValue(jobId, out var job))
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Job {jobId}: Job not found after status check");
                        continue;
                    }

                    job.AdrStatusId = statusResult.StatusId;
                    job.AdrStatusDescription = statusResult.StatusDescription;
                    
                    // Store raw API response for debugging (truncated to avoid bloating the database)
                    job.LastStatusCheckResponse = TruncateResponse(statusResult.RawResponse, 1000);
                    job.LastStatusCheckDateTime = DateTime.UtcNow;

                    if (statusResult.IsFinal)
                    {
                        if (statusResult.StatusId == (int)AdrStatus.Complete)
                        {
                            // StatusId 11: Document Retrieval Complete
                            job.Status = "Completed";
                            job.ScrapingCompletedDateTime = DateTime.UtcNow;
                            result.JobsCompleted++;
                            
                            // Update account's LastSuccessfulDownloadDate to help calculate next billing cycle
                            // Uses anti-creep logic: allows earlier dates but prevents late vendors from causing schedule drift
                            if (job.AdrAccount != null)
                            {
                                job.AdrAccount.LastSuccessfulDownloadDate = CalculateLastSuccessfulDownloadDate(
                                    job.AdrAccount.LastSuccessfulDownloadDate,
                                    job.NextRunDateTime,
                                    job.AdrAccount.PeriodDays);
                                job.AdrAccount.ModifiedDateTime = DateTime.UtcNow;
                                job.AdrAccount.ModifiedBy = "System Created";
                            }
                        }
                        else if (statusResult.StatusId == (int)AdrStatus.NeedsHumanReview)
                        {
                            // StatusId 9: Needs Human Review
                            job.Status = "NeedsReview";
                            job.ScrapingCompletedDateTime = DateTime.UtcNow;
                            result.JobsNeedingReview++;
                        }
                        else if (statusResult.StatusId == 3 || statusResult.StatusId == 4 || statusResult.StatusId == 5 ||
                                 statusResult.StatusId == 7 || statusResult.StatusId == 8 || statusResult.StatusId == 14)
                        {
                            // Error statuses:
                            // StatusId 3: Invalid CredentialID
                            // StatusId 4: Cannot Connect To VCM
                            // StatusId 5: Cannot Insert Into Queue
                            // StatusId 7: Cannot Connect To AI
                            // StatusId 8: Cannot Save Result
                            // StatusId 14: Failed To Process All Documents
                            job.Status = "Failed";
                            job.ScrapingCompletedDateTime = DateTime.UtcNow;
                            result.Errors++;
                            result.ErrorMessages.Add($"Job {jobId}: {statusResult.StatusDescription} (StatusId={statusResult.StatusId})");
                        }
                        else
                        {
                            // Unrecognized final status - log for debugging but still mark as completed
                            _logger.LogWarning(
                                "Job {JobId}: Unrecognized final status. StatusId={StatusId}, Description={Description}, Raw={RawResponse}",
                                jobId, statusResult.StatusId, statusResult.StatusDescription, 
                                TruncateResponse(statusResult.RawResponse, 300));
                            job.Status = "Completed";
                            job.ScrapingCompletedDateTime = DateTime.UtcNow;
                            result.JobsCompleted++;
                        }
                    }
                    else
                    {
                        // Job is still processing - revert to ScrapeRequested so it gets picked up again
                        job.Status = "ScrapeRequested";
                        result.JobsStillProcessing++;
                    }

                    // Update job properties directly - no need to call UpdateAsync since
                    // the entity is already tracked by EF (loaded from same DbContext)
                    job.ModifiedDateTime = DateTime.UtcNow;
                    job.ModifiedBy = "System Created";
                    processedSinceLastSave++;

                    if (processedSinceLastSave >= BatchSize)
                    {
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogInformation("Manual status check batch {BatchNumber} saved: {Count} jobs checked so far", 
                            batchNumber, result.JobsChecked);
                        processedSinceLastSave = 0;
                        batchNumber++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating job {JobId} after manual status check", jobId);
                    result.Errors++;
                    result.ErrorMessages.Add($"Job {jobId}: {ex.Message}");
                }
            }

            if (processedSinceLastSave > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Manual status check completed. Checked: {Checked}, Completed: {Completed}, NeedsReview: {NeedsReview}, Processing: {Processing}, Errors: {Errors}",
                result.JobsChecked, result.JobsCompleted, result.JobsNeedingReview, result.JobsStillProcessing, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual status check failed");
            result.Errors++;
            result.ErrorMessages.Add($"Manual status check failed: {ex.Message}");
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
        long vmAccountId,
        string? interfaceAccountId,
        CancellationToken cancellationToken)
    {
        var result = new AdrApiResult();

        try
        {
            var baseUrl = _configuration["AdrApi:BaseUrl"] ?? "https://nuse2etsadrdevfn01.azurewebsites.net/api/";
            var sourceApplicationName = _configuration["AdrApi:SourceApplicationName"] ?? "ADRScheduler";
            var recipientEmail = _configuration["AdrApi:RecipientEmail"] ?? "lcassin@cassinfo.com";

            var client = _httpClientFactory.CreateClient("AdrApi");

            var request = new
            {
                ADRRequestTypeId = (int)requestType,
                CredentialId = credentialId,
                StartDate = startDate?.ToString("yyyy-MM-dd"),
                EndDate = endDate?.ToString("yyyy-MM-dd"),
                SourceApplicationName = sourceApplicationName,
                RecipientEmail = recipientEmail,
                JobId = jobId,
                AccountId = vmAccountId,
                InterfaceAccountId = interfaceAccountId
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
                    // Use Status field if StatusDescription is not provided
                    result.StatusDescription = apiResponse.StatusDescription ?? apiResponse.Status;
                    result.IndexId = apiResponse.IndexId;
                    result.IsSuccess = true;
                    result.IsError = apiResponse.IsError;
                    
                    // Determine IsFinal based on StatusId since the API may not return IsFinal field
                    // Final statuses: Complete (11), NeedsHumanReview (9), and error states like "Cannot Insert Into Queue" (5)
                    result.IsFinal = apiResponse.IsFinal || IsFinalStatus(apiResponse.StatusId);
                }
                else
                {
                    // Log when deserialization returns null - may indicate API format change
                    _logger.LogWarning(
                        "Status check for job {JobId}: Deserialization returned null. Raw response: {RawResponse}",
                        jobId, TruncateResponse(responseContent, 500));
                }
            }
            else
            {
                // Log non-success HTTP responses
                _logger.LogWarning(
                    "Status check for job {JobId}: HTTP {StatusCode}. Raw response: {RawResponse}",
                    jobId, (int)response.StatusCode, TruncateResponse(responseContent, 500));
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
    
    /// <summary>
    /// Determines if a StatusId represents a final state (job is done processing).
    /// Based on ADR API status codes:
    /// - Final success: 11 (Complete/Document Retrieval Complete)
    /// - Final needs review: 9 (Needs Human Review)
    /// - Final errors: 3 (Invalid CredentialID), 4 (Cannot Connect To VCM), 5 (Cannot Insert Into Queue),
    ///                 7 (Cannot Connect To AI), 8 (Cannot Save Result), 14 (Failed To Process All Documents)
    /// - Still processing: 1 (Inserted), 2 (Inserted With Priority), 6 (Sent To AI), 10 (Received From AI),
    ///                     12 (Login Attempt Succeeded), 13 (No Documents Found), 15 (No Documents Processed - TBD)
    /// </summary>
    private static bool IsFinalStatus(int statusId)
    {
        return statusId switch
        {
            11 => true,  // Complete (Document Retrieval Complete)
            9 => true,   // Needs Human Review
            3 => true,   // Invalid CredentialID (error - final)
            4 => true,   // Cannot Connect To VCM (error - final)
            5 => true,   // Cannot Insert Into Queue (error - final)
            7 => true,   // Cannot Connect To AI (error - final)
            8 => true,   // Cannot Save Result (error - final)
            14 => true,  // Failed To Process All Documents (error - final)
            _ => false   // 1 (Inserted), 2 (Inserted With Priority), 6 (Sent To AI), 10 (Received From AI),
                         // 12 (Login Attempt Succeeded), 13 (No Documents Found - retry next day), 15 (No Documents Processed - TBD)
        };
    }

    /// <summary>
    /// Calculates the LastSuccessfulDownloadDate with anti-creep logic.
    /// - If no previous date exists, use the job's scheduled date (establish baseline)
    /// - If job date is earlier or equal to expected, use job date (allow earlier)
    /// - If job date is later than expected (vendor posted late), use expected date (prevent creep)
    /// </summary>
    private static DateTime CalculateLastSuccessfulDownloadDate(
        DateTime? currentLastSuccessfulDownloadDate,
        DateTime? jobNextRunDateTime,
        int? periodDays)
    {
        var jobDate = jobNextRunDateTime?.Date ?? DateTime.UtcNow.Date;
        
        // First successful download - establish baseline
        if (!currentLastSuccessfulDownloadDate.HasValue)
        {
            return jobDate;
        }
        
        // Calculate expected date based on previous anchor + period
        var previousAnchor = currentLastSuccessfulDownloadDate.Value;
        var period = periodDays ?? 30; // Default to monthly if not specified
        var expectedDate = previousAnchor.AddDays(period);
        
        // Allow earlier or same, but don't let late vendors cause creep
        if (jobDate <= expectedDate)
        {
            return jobDate; // OK to move earlier or keep same
        }
        else
        {
            return expectedDate; // Vendor late - use expected date to prevent creep
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
        public string? Status { get; set; }  // API returns "Status" field, not "StatusDescription"
        public long? IndexId { get; set; }
        public bool IsError { get; set; }
        public bool IsFinal { get; set; }
    }

    #endregion
}
