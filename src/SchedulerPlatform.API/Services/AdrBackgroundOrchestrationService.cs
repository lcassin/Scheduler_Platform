using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.API.Services;

/// <summary>
/// Represents a request to run ADR orchestration in the background.
/// </summary>
public class AdrOrchestrationRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string RequestedBy { get; set; } = "System";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public bool RunSync { get; set; } = true;
    public bool RunCreateJobs { get; set; } = true;
    public bool RunCredentialVerification { get; set; } = true;
    public bool RunScraping { get; set; } = true;
    public bool RunStatusCheck { get; set; } = true;
    
    /// <summary>
    /// When true and RunStatusCheck is true, uses CheckAllScrapedStatusesAsync instead of CheckPendingStatusesAsync.
    /// This checks ALL jobs with ScrapeRequested status regardless of timing criteria.
    /// Used by the "Check Statuses Only" button for manual status checks.
    /// </summary>
    public bool CheckAllScrapedStatuses { get; set; } = false;
}

/// <summary>
/// Tracks the status of a background ADR orchestration run.
/// </summary>
public class AdrOrchestrationStatus
{
    public string RequestId { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Queued"; // Queued, Running, Completed, Failed
    public string? CurrentStep { get; set; }
    public string? CurrentStepPhase { get; set; } // Preparing, Calling API, Saving results
    public string? CurrentSubStep { get; set; } // Sub-step within the current step (e.g., "Syncing rules" within "Syncing accounts")
    public string? ErrorMessage { get; set; }
    
    // Progress tracking for current step
    public int CurrentStepProgress { get; set; }
    public int CurrentStepTotal { get; set; }
    
    // Secondary progress tracking for sub-steps (e.g., rule sync progress within account sync)
    public int? SubStepProgress { get; set; }
    public int? SubStepTotal { get; set; }
    
    // Results from each step
    public AdrAccountSyncResult? SyncResult { get; set; }
    public JobCreationResult? JobCreationResult { get; set; }
    public CredentialVerificationResult? CredentialVerificationResult { get; set; }
    public ScrapeResult? ScrapeResult { get; set; }
    public StatusCheckResult? StatusCheckResult { get; set; }
}

/// <summary>
/// Interface for queuing ADR orchestration requests.
/// </summary>
public interface IAdrOrchestrationQueue
{
    ValueTask QueueAsync(AdrOrchestrationRequest request, CancellationToken cancellationToken = default);
    ValueTask<AdrOrchestrationRequest> DequeueAsync(CancellationToken cancellationToken);
    AdrOrchestrationStatus? GetStatus(string requestId);
    IEnumerable<AdrOrchestrationStatus> GetRecentStatuses(int count = 10);
    AdrOrchestrationStatus? GetCurrentRun();
    
    /// <summary>
    /// Cancel a running or queued orchestration request.
    /// </summary>
    bool CancelRequest(string requestId);
    
    /// <summary>
    /// Get the cancellation token for a specific request.
    /// </summary>
    CancellationToken GetRequestToken(string requestId);
}

/// <summary>
/// In-memory queue for ADR orchestration requests with status tracking.
/// </summary>
public class AdrOrchestrationQueue : IAdrOrchestrationQueue
{
    private readonly Channel<AdrOrchestrationRequest> _queue;
    private readonly Dictionary<string, AdrOrchestrationStatus> _statuses = new();
    private readonly Dictionary<string, CancellationTokenSource> _requestTokens = new();
    private readonly object _lock = new();
    private string? _currentRunId;

    public AdrOrchestrationQueue()
    {
        // Bounded channel with capacity of 10 - if queue is full, new requests will wait
        _queue = Channel.CreateBounded<AdrOrchestrationRequest>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async ValueTask QueueAsync(AdrOrchestrationRequest request, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _statuses[request.RequestId] = new AdrOrchestrationStatus
            {
                RequestId = request.RequestId,
                RequestedBy = request.RequestedBy,
                RequestedAt = request.RequestedAt,
                Status = "Queued"
            };
            _requestTokens[request.RequestId] = new CancellationTokenSource();
        }

        await _queue.Writer.WriteAsync(request, cancellationToken);
    }
    
    public CancellationToken GetRequestToken(string requestId)
    {
        lock (_lock)
        {
            return _requestTokens.TryGetValue(requestId, out var cts)
                ? cts.Token
                : CancellationToken.None;
        }
    }
    
    public bool CancelRequest(string requestId)
    {
        lock (_lock)
        {
            if (_requestTokens.TryGetValue(requestId, out var cts))
            {
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                    // Update status to show cancellation is pending
                    if (_statuses.TryGetValue(requestId, out var status) && status.Status == "Running")
                    {
                        status.Status = "Cancelling";
                    }
                    return true;
                }
            }
            return false;
        }
    }
    
    internal void CompleteRequest(string requestId)
    {
        lock (_lock)
        {
            if (_requestTokens.Remove(requestId, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    public async ValueTask<AdrOrchestrationRequest> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }

    public AdrOrchestrationStatus? GetStatus(string requestId)
    {
        lock (_lock)
        {
            return _statuses.TryGetValue(requestId, out var status) ? status : null;
        }
    }

    public IEnumerable<AdrOrchestrationStatus> GetRecentStatuses(int count = 10)
    {
        lock (_lock)
        {
            return _statuses.Values
                .OrderByDescending(s => s.RequestedAt)
                .Take(count)
                .ToList();
        }
    }

    public AdrOrchestrationStatus? GetCurrentRun()
    {
        lock (_lock)
        {
            if (_currentRunId != null && _statuses.TryGetValue(_currentRunId, out var status))
            {
                return status;
            }
            return null;
        }
    }

    internal void UpdateStatus(string requestId, Action<AdrOrchestrationStatus> update)
    {
        lock (_lock)
        {
            if (_statuses.TryGetValue(requestId, out var status))
            {
                update(status);
            }
        }
    }

    internal void SetCurrentRun(string? requestId)
    {
        lock (_lock)
        {
            _currentRunId = requestId;
        }
    }

    internal void CleanupOldStatuses(TimeSpan maxAge)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var toRemove = _statuses
                .Where(kvp => kvp.Value.CompletedAt.HasValue && kvp.Value.CompletedAt < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                _statuses.Remove(key);
            }
        }
    }
}

/// <summary>
/// Background service that processes ADR orchestration requests from the queue.
/// This runs independently of user sessions and uses internal service authentication.
/// </summary>
public class AdrBackgroundOrchestrationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AdrOrchestrationQueue _queue;
    private readonly ILogger<AdrBackgroundOrchestrationService> _logger;

    public AdrBackgroundOrchestrationService(
        IServiceScopeFactory scopeFactory,
        IAdrOrchestrationQueue queue,
        ILogger<AdrBackgroundOrchestrationService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = (AdrOrchestrationQueue)queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ADR Background Orchestration Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Clean up old statuses every hour
                _queue.CleanupOldStatuses(TimeSpan.FromHours(24));

                // Wait for a request from the queue
                var request = await _queue.DequeueAsync(stoppingToken);

                _logger.LogInformation(
                    "Processing ADR orchestration request {RequestId} from {RequestedBy}",
                    request.RequestId, request.RequestedBy);

                await ProcessRequestAsync(request, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ADR Background Orchestration Service");
                // Wait a bit before trying again to avoid tight error loops
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("ADR Background Orchestration Service stopped");
    }

    private async Task ProcessRequestAsync(AdrOrchestrationRequest request, CancellationToken stoppingToken)
    {
        // Create a linked cancellation token that combines host shutdown with request-specific cancellation
        var requestToken = _queue.GetRequestToken(request.RequestId);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, requestToken);
        var token = linkedCts.Token;
        
        // Check if already cancelled before starting
        if (token.IsCancellationRequested)
        {
            _queue.UpdateStatus(request.RequestId, s =>
            {
                s.Status = "Cancelled";
                s.ErrorMessage = "Cancelled before start";
                s.CompletedAt = DateTime.UtcNow;
            });
            _queue.CompleteRequest(request.RequestId);
            _logger.LogInformation("Request {RequestId}: Cancelled before start", request.RequestId);
            return;
        }
        
        _queue.SetCurrentRun(request.RequestId);
        _queue.UpdateStatus(request.RequestId, s =>
        {
            s.Status = "Running";
            s.StartedAt = DateTime.UtcNow;
        });

        // Create database record for this orchestration run
        int dbRunId = 0;
        try
        {
            using var initScope = _scopeFactory.CreateScope();
            var initDb = initScope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
            var dbRun = new AdrOrchestrationRun
            {
                RequestId = request.RequestId,
                RequestedBy = request.RequestedBy,
                RequestedDateTime = request.RequestedAt,
                StartedDateTime = DateTime.UtcNow,
                Status = "Running",
                CreatedBy = "System",
                CreatedDateTime = DateTime.UtcNow,
                ModifiedBy = "System",
                ModifiedDateTime = DateTime.UtcNow
            };
            initDb.AdrOrchestrationRuns.Add(dbRun);
            await initDb.SaveChangesAsync(token);
            dbRunId = dbRun.Id;
            _logger.LogInformation("Request {RequestId}: Created database record with ID {DbRunId}", request.RequestId, dbRunId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Request {RequestId}: Failed to create database record, continuing with in-memory only", request.RequestId);
        }

        try
        {
            // Create a new scope for this request - this gives us fresh DbContext instances
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IAdrAccountSyncService>();
            var orchestratorService = scope.ServiceProvider.GetRequiredService<IAdrOrchestratorService>();

            // Step 1: Sync accounts
            if (request.RunSync)
            {
                _queue.UpdateStatus(request.RequestId, s => 
                {
                    s.CurrentStep = "Syncing accounts";
                    s.CurrentSubStep = null;
                    s.CurrentStepProgress = 0;
                    s.CurrentStepTotal = 0;
                    s.SubStepProgress = null;
                    s.SubStepTotal = null;
                });
                _logger.LogInformation("Request {RequestId}: Starting account sync", request.RequestId);
                
                var syncResult = await syncService.SyncAccountsAsync(
                    // Main progress callback for account sync
                    (progress, total) => _queue.UpdateStatus(request.RequestId, s => 
                    {
                        s.CurrentStepProgress = progress;
                        s.CurrentStepTotal = total;
                    }),
                    // Sub-step callback for rule sync (and potentially other sub-steps)
                    (subStep, progress, total) => _queue.UpdateStatus(request.RequestId, s =>
                    {
                        s.CurrentSubStep = subStep;
                        s.SubStepProgress = progress;
                        s.SubStepTotal = total;
                    }),
                    token);
                _queue.UpdateStatus(request.RequestId, s => 
                {
                    s.SyncResult = syncResult;
                    s.CurrentSubStep = null;
                    s.SubStepProgress = null;
                    s.SubStepTotal = null;
                });
                
                _logger.LogInformation(
                    "Request {RequestId}: Account sync completed. Inserted: {Inserted}, Updated: {Updated}",
                    request.RequestId, syncResult.AccountsInserted, syncResult.AccountsUpdated);
            }

            // Step 2: Create jobs
            if (request.RunCreateJobs)
            {
                _queue.UpdateStatus(request.RequestId, s => s.CurrentStep = "Creating jobs");
                _logger.LogInformation("Request {RequestId}: Starting job creation", request.RequestId);
                
                var jobResult = await orchestratorService.CreateJobsForDueAccountsAsync(token);
                _queue.UpdateStatus(request.RequestId, s => s.JobCreationResult = jobResult);
                
                _logger.LogInformation(
                    "Request {RequestId}: Job creation completed. Created: {Created}, Skipped: {Skipped}",
                    request.RequestId, jobResult.JobsCreated, jobResult.JobsSkipped);
            }

            // Step 3: Verify credentials
            if (request.RunCredentialVerification)
            {
                _queue.UpdateStatus(request.RequestId, s => 
                {
                    s.CurrentStep = "Verifying credentials";
                    s.CurrentStepPhase = "Preparing";
                    s.CurrentStepProgress = 0;
                    s.CurrentStepTotal = 0;
                });
                _logger.LogInformation("Request {RequestId}: Starting credential verification", request.RequestId);
                
                var credResult = await orchestratorService.VerifyCredentialsAsync(
                    (progress, total) => _queue.UpdateStatus(request.RequestId, s => 
                    {
                        // Negative progress indicates setup/preparing phase
                        if (progress < 0)
                        {
                            s.CurrentStepPhase = "Preparing";
                            s.CurrentStepProgress = Math.Abs(progress);
                        }
                        else
                        {
                            s.CurrentStepPhase = "Calling API";
                            s.CurrentStepProgress = progress;
                        }
                        s.CurrentStepTotal = total;
                    }),
                    token);
                _queue.UpdateStatus(request.RequestId, s => 
                {
                    s.CurrentStepPhase = null;
                    s.CredentialVerificationResult = credResult;
                });
                
                _logger.LogInformation(
                    "Request {RequestId}: Credential verification completed. Verified: {Verified}, Failed: {Failed}",
                    request.RequestId, credResult.CredentialsVerified, credResult.CredentialsFailed);
            }

            // Step 4: Check statuses of yesterday's ScrapeRequested jobs BEFORE sending new scrapes
            // This prevents duplicate scrape requests for jobs that already completed
            if (request.RunStatusCheck)
            {
                var checkAllStatuses = request.CheckAllScrapedStatuses;
                _queue.UpdateStatus(request.RequestId, s => 
                {
                    s.CurrentStep = checkAllStatuses ? "Checking all scraped statuses" : "Checking statuses";
                    s.CurrentStepPhase = "Preparing";
                    s.CurrentStepProgress = 0;
                    s.CurrentStepTotal = 0;
                });
                _logger.LogInformation(
                    "Request {RequestId}: Starting status check (CheckAllScrapedStatuses={CheckAll})", 
                    request.RequestId, checkAllStatuses);
                
                // Use CheckAllScrapedStatusesAsync for manual "Check Statuses Only" button
                // Use CheckPendingStatusesAsync for regular orchestration runs
                StatusCheckResult statusResult;
                if (checkAllStatuses)
                {
                    statusResult = await orchestratorService.CheckAllScrapedStatusesAsync(
                        (progress, total) => _queue.UpdateStatus(request.RequestId, s => 
                        {
                            if (progress < 0)
                            {
                                s.CurrentStepPhase = "Preparing";
                                s.CurrentStepProgress = Math.Abs(progress);
                            }
                            else
                            {
                                s.CurrentStepPhase = "Calling API";
                                s.CurrentStepProgress = progress;
                            }
                            s.CurrentStepTotal = total;
                        }),
                        token);
                }
                else
                {
                    statusResult = await orchestratorService.CheckPendingStatusesAsync(
                        (progress, total) => _queue.UpdateStatus(request.RequestId, s => 
                        {
                            if (progress < 0)
                            {
                                s.CurrentStepPhase = "Preparing";
                                s.CurrentStepProgress = Math.Abs(progress);
                            }
                            else
                            {
                                s.CurrentStepPhase = "Calling API";
                                s.CurrentStepProgress = progress;
                            }
                            s.CurrentStepTotal = total;
                        }),
                        token);
                }
                _queue.UpdateStatus(request.RequestId, s => 
                {
                    s.CurrentStepPhase = null;
                    s.StatusCheckResult = statusResult;
                });
                
                _logger.LogInformation(
                    "Request {RequestId}: Status check completed. Completed: {Completed}, NeedsReview: {NeedsReview}",
                    request.RequestId, statusResult.JobsCompleted, statusResult.JobsNeedingReview);
            }

            // Step 5: Process scraping for jobs that are ready (CredentialVerified status)
            if (request.RunScraping)
            {
                _queue.UpdateStatus(request.RequestId, s => 
                {
                    s.CurrentStep = "Processing scraping";
                    s.CurrentStepPhase = "Preparing";
                    s.CurrentStepProgress = 0;
                    s.CurrentStepTotal = 0;
                });
                _logger.LogInformation("Request {RequestId}: Starting scraping", request.RequestId);
                
                var scrapeResult = await orchestratorService.ProcessScrapingAsync(
                    (progress, total) => _queue.UpdateStatus(request.RequestId, s => 
                    {
                        // Negative progress indicates setup/preparing phase
                        if (progress < 0)
                        {
                            s.CurrentStepPhase = "Preparing";
                            s.CurrentStepProgress = Math.Abs(progress);
                        }
                        else
                        {
                            s.CurrentStepPhase = "Calling API";
                            s.CurrentStepProgress = progress;
                        }
                        s.CurrentStepTotal = total;
                    }),
                    token);
                _queue.UpdateStatus(request.RequestId, s => 
                {
                    s.CurrentStepPhase = null;
                    s.ScrapeResult = scrapeResult;
                });
                
                _logger.LogInformation(
                    "Request {RequestId}: Scraping completed. Requested: {Requested}, Completed: {Completed}",
                    request.RequestId, scrapeResult.ScrapesRequested, scrapeResult.ScrapesCompleted);
            }

            _queue.UpdateStatus(request.RequestId, s =>
            {
                s.Status = "Completed";
                s.CurrentStep = null;
                s.CompletedAt = DateTime.UtcNow;
            });

            _logger.LogInformation("Request {RequestId}: ADR orchestration completed successfully", request.RequestId);
            
            // Save final results to database
            await SaveOrchestrationResultAsync(request.RequestId, dbRunId, "Completed", null, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Determine if this was a host shutdown or user-initiated cancellation
            var isHostShutdown = stoppingToken.IsCancellationRequested;
            var reason = isHostShutdown ? "Host is shutting down" : "Operation was cancelled by user";
            
            _logger.LogInformation("Request {RequestId}: ADR orchestration cancelled - {Reason}", request.RequestId, reason);
            
            _queue.UpdateStatus(request.RequestId, s =>
            {
                s.Status = "Cancelled";
                s.ErrorMessage = reason;
                s.CompletedAt = DateTime.UtcNow;
            });
            await SaveOrchestrationResultAsync(request.RequestId, dbRunId, "Cancelled", reason, CancellationToken.None);
            
            // Only rethrow if host is shutting down (to allow graceful shutdown)
            if (isHostShutdown)
            {
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request {RequestId}: ADR orchestration failed", request.RequestId);
            
            _queue.UpdateStatus(request.RequestId, s =>
            {
                s.Status = "Failed";
                s.ErrorMessage = ex.Message;
                s.CompletedAt = DateTime.UtcNow;
            });
            await SaveOrchestrationResultAsync(request.RequestId, dbRunId, "Failed", ex.Message, CancellationToken.None);
        }
        finally
        {
            _queue.SetCurrentRun(null);
            _queue.CompleteRequest(request.RequestId);
        }
    }
    
    private async Task SaveOrchestrationResultAsync(string requestId, int dbRunId, string status, string? errorMessage, CancellationToken cancellationToken)
    {
        if (dbRunId == 0) return; // No database record was created
        
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
            
            var dbRun = await db.AdrOrchestrationRuns.FindAsync(new object[] { dbRunId }, cancellationToken);
            if (dbRun == null) return;
            
            // Get the in-memory status to copy results from
            var memStatus = _queue.GetStatus(requestId);
            
            dbRun.Status = status;
            dbRun.CompletedDateTime = DateTime.UtcNow;
            dbRun.ErrorMessage = errorMessage;
            dbRun.ModifiedDateTime = DateTime.UtcNow;
            dbRun.ModifiedBy = "System";
            
            if (memStatus != null)
            {
                // Copy step results from in-memory status
                if (memStatus.SyncResult != null)
                {
                    dbRun.SyncAccountsInserted = memStatus.SyncResult.AccountsInserted;
                    dbRun.SyncAccountsUpdated = memStatus.SyncResult.AccountsUpdated;
                    dbRun.SyncAccountsTotal = memStatus.SyncResult.AccountsInserted + memStatus.SyncResult.AccountsUpdated;
                }
                
                if (memStatus.JobCreationResult != null)
                {
                    dbRun.JobsCreated = memStatus.JobCreationResult.JobsCreated;
                    dbRun.JobsSkipped = memStatus.JobCreationResult.JobsSkipped;
                }
                
                if (memStatus.CredentialVerificationResult != null)
                {
                    dbRun.CredentialsVerified = memStatus.CredentialVerificationResult.CredentialsVerified;
                    dbRun.CredentialsFailed = memStatus.CredentialVerificationResult.CredentialsFailed;
                }
                
                if (memStatus.ScrapeResult != null)
                {
                    dbRun.ScrapingRequested = memStatus.ScrapeResult.ScrapesRequested;
                    dbRun.ScrapingFailed = memStatus.ScrapeResult.ScrapesFailed;
                }
                
                if (memStatus.StatusCheckResult != null)
                {
                    dbRun.StatusesChecked = memStatus.StatusCheckResult.JobsCompleted + memStatus.StatusCheckResult.JobsNeedingReview;
                    dbRun.StatusesFailed = memStatus.StatusCheckResult.JobsNeedingReview;
                }
            }
            
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Request {RequestId}: Saved orchestration results to database", requestId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Request {RequestId}: Failed to save orchestration results to database", requestId);
        }
    }
}
