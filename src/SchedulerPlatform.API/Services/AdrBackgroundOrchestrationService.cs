using System.Threading.Channels;
using SchedulerPlatform.Core.Domain.Interfaces;

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
    public string? ErrorMessage { get; set; }
    
    // Progress tracking for current step
    public int CurrentStepProgress { get; set; }
    public int CurrentStepTotal { get; set; }
    
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
}

/// <summary>
/// In-memory queue for ADR orchestration requests with status tracking.
/// </summary>
public class AdrOrchestrationQueue : IAdrOrchestrationQueue
{
    private readonly Channel<AdrOrchestrationRequest> _queue;
    private readonly Dictionary<string, AdrOrchestrationStatus> _statuses = new();
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
        }

        await _queue.Writer.WriteAsync(request, cancellationToken);
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
        _queue.SetCurrentRun(request.RequestId);
        _queue.UpdateStatus(request.RequestId, s =>
        {
            s.Status = "Running";
            s.StartedAt = DateTime.UtcNow;
        });

        try
        {
            // Create a new scope for this request - this gives us fresh DbContext instances
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IAdrAccountSyncService>();
            var orchestratorService = scope.ServiceProvider.GetRequiredService<IAdrOrchestratorService>();

            // Step 1: Sync accounts
            if (request.RunSync)
            {
                _queue.UpdateStatus(request.RequestId, s => s.CurrentStep = "Syncing accounts");
                _logger.LogInformation("Request {RequestId}: Starting account sync", request.RequestId);
                
                var syncResult = await syncService.SyncAccountsAsync(stoppingToken);
                _queue.UpdateStatus(request.RequestId, s => s.SyncResult = syncResult);
                
                _logger.LogInformation(
                    "Request {RequestId}: Account sync completed. Inserted: {Inserted}, Updated: {Updated}",
                    request.RequestId, syncResult.AccountsInserted, syncResult.AccountsUpdated);
            }

            // Step 2: Create jobs
            if (request.RunCreateJobs)
            {
                _queue.UpdateStatus(request.RequestId, s => s.CurrentStep = "Creating jobs");
                _logger.LogInformation("Request {RequestId}: Starting job creation", request.RequestId);
                
                var jobResult = await orchestratorService.CreateJobsForDueAccountsAsync(stoppingToken);
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
                    s.CurrentStepProgress = 0;
                    s.CurrentStepTotal = 0;
                });
                _logger.LogInformation("Request {RequestId}: Starting credential verification", request.RequestId);
                
                var credResult = await orchestratorService.VerifyCredentialsAsync(
                    (progress, total) => _queue.UpdateStatus(request.RequestId, s => 
                    {
                        s.CurrentStepProgress = progress;
                        s.CurrentStepTotal = total;
                    }),
                    stoppingToken);
                _queue.UpdateStatus(request.RequestId, s => s.CredentialVerificationResult = credResult);
                
                _logger.LogInformation(
                    "Request {RequestId}: Credential verification completed. Verified: {Verified}, Failed: {Failed}",
                    request.RequestId, credResult.CredentialsVerified, credResult.CredentialsFailed);
            }

            // Step 4: Process scraping
            if (request.RunScraping)
            {
                _queue.UpdateStatus(request.RequestId, s => 
                {
                    s.CurrentStep = "Processing scraping";
                    s.CurrentStepProgress = 0;
                    s.CurrentStepTotal = 0;
                });
                _logger.LogInformation("Request {RequestId}: Starting scraping", request.RequestId);
                
                var scrapeResult = await orchestratorService.ProcessScrapingAsync(
                    (progress, total) => _queue.UpdateStatus(request.RequestId, s => 
                    {
                        s.CurrentStepProgress = progress;
                        s.CurrentStepTotal = total;
                    }),
                    stoppingToken);
                _queue.UpdateStatus(request.RequestId, s => s.ScrapeResult = scrapeResult);
                
                _logger.LogInformation(
                    "Request {RequestId}: Scraping completed. Requested: {Requested}, Completed: {Completed}",
                    request.RequestId, scrapeResult.ScrapesRequested, scrapeResult.ScrapesCompleted);
            }

            // Step 5: Check statuses
            if (request.RunStatusCheck)
            {
                _queue.UpdateStatus(request.RequestId, s => 
                {
                    s.CurrentStep = "Checking statuses";
                    s.CurrentStepProgress = 0;
                    s.CurrentStepTotal = 0;
                });
                _logger.LogInformation("Request {RequestId}: Starting status check", request.RequestId);
                
                var statusResult = await orchestratorService.CheckPendingStatusesAsync(
                    (progress, total) => _queue.UpdateStatus(request.RequestId, s => 
                    {
                        s.CurrentStepProgress = progress;
                        s.CurrentStepTotal = total;
                    }),
                    stoppingToken);
                _queue.UpdateStatus(request.RequestId, s => s.StatusCheckResult = statusResult);
                
                _logger.LogInformation(
                    "Request {RequestId}: Status check completed. Completed: {Completed}, NeedsReview: {NeedsReview}",
                    request.RequestId, statusResult.JobsCompleted, statusResult.JobsNeedingReview);
            }

            _queue.UpdateStatus(request.RequestId, s =>
            {
                s.Status = "Completed";
                s.CurrentStep = null;
                s.CompletedAt = DateTime.UtcNow;
            });

            _logger.LogInformation("Request {RequestId}: ADR orchestration completed successfully", request.RequestId);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _queue.UpdateStatus(request.RequestId, s =>
            {
                s.Status = "Cancelled";
                s.ErrorMessage = "Operation was cancelled";
                s.CompletedAt = DateTime.UtcNow;
            });
            throw;
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
        }
        finally
        {
            _queue.SetCurrentRun(null);
        }
    }
}
