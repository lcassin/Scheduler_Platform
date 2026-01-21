using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SchedulerPlatform.Infrastructure.Data;

namespace SchedulerPlatform.API.Services;

/// <summary>
/// Represents a request to export data in the background.
/// </summary>
public class ExportRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string ExportType { get; set; } = string.Empty; // accounts, jobs, rules, blacklist
    public string Format { get; set; } = "excel"; // excel or csv
    public Dictionary<string, string?> Filters { get; set; } = new();
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents the status of an export request.
/// </summary>
public class ExportStatus
{
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued"; // Queued, Processing, Completed, Failed
    public string? ErrorMessage { get; set; }
    public int TotalRecords { get; set; }
    public int RecordsProcessed { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public bool IsDownloaded { get; set; }
}

/// <summary>
/// Interface for the background export queue service.
/// </summary>
public interface IBackgroundExportQueue
{
    /// <summary>
    /// Queues an export request for background processing.
    /// </summary>
    Task<string> QueueAsync(ExportRequest request);
    
    /// <summary>
    /// Gets the status of an export request.
    /// </summary>
    ExportStatus? GetStatus(string requestId);
    
    /// <summary>
    /// Gets the export data if completed.
    /// </summary>
    (byte[]? Data, ExportStatus? Status) GetExportData(string requestId);
    
    /// <summary>
    /// Marks an export as downloaded.
    /// </summary>
    void MarkDownloaded(string requestId);
    
    /// <summary>
    /// Dequeues the next export request for processing.
    /// </summary>
    ValueTask<ExportRequest> DequeueAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Updates the status of an export request.
    /// </summary>
    void UpdateStatus(string requestId, Action<ExportStatus> updateAction);
    
    /// <summary>
    /// Stores the completed export data.
    /// </summary>
    void StoreExportData(string requestId, byte[] data, string fileName, string contentType);
}

/// <summary>
/// In-memory implementation of the background export queue.
/// </summary>
public class BackgroundExportQueue : IBackgroundExportQueue
{
    private readonly Channel<ExportRequest> _queue;
    private readonly ConcurrentDictionary<string, ExportStatus> _statuses = new();
    private readonly ConcurrentDictionary<string, byte[]> _exportData = new();
    private readonly TimeSpan _dataExpiration = TimeSpan.FromHours(1);

    public BackgroundExportQueue()
    {
        var options = new BoundedChannelOptions(20)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<ExportRequest>(options);
    }

    public async Task<string> QueueAsync(ExportRequest request)
    {
        var status = new ExportStatus
        {
            RequestId = request.RequestId,
            Status = "Queued",
            RequestedAt = request.RequestedAt
        };
        _statuses[request.RequestId] = status;
        
        await _queue.Writer.WriteAsync(request);
        
        // Cleanup old statuses
        CleanupOldStatuses();
        
        return request.RequestId;
    }

    public ExportStatus? GetStatus(string requestId)
    {
        return _statuses.TryGetValue(requestId, out var status) ? status : null;
    }

    public (byte[]? Data, ExportStatus? Status) GetExportData(string requestId)
    {
        var status = GetStatus(requestId);
        if (status == null || status.Status != "Completed")
            return (null, status);
            
        _exportData.TryGetValue(requestId, out var data);
        return (data, status);
    }

    public void MarkDownloaded(string requestId)
    {
        if (_statuses.TryGetValue(requestId, out var status))
        {
            status.IsDownloaded = true;
        }
    }

    public ValueTask<ExportRequest> DequeueAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }

    public void UpdateStatus(string requestId, Action<ExportStatus> updateAction)
    {
        if (_statuses.TryGetValue(requestId, out var status))
        {
            updateAction(status);
        }
    }

    public void StoreExportData(string requestId, byte[] data, string fileName, string contentType)
    {
        _exportData[requestId] = data;
        UpdateStatus(requestId, s =>
        {
            s.Status = "Completed";
            s.CompletedAt = DateTime.UtcNow;
            s.FileName = fileName;
            s.ContentType = contentType;
        });
    }

    private void CleanupOldStatuses()
    {
        var cutoff = DateTime.UtcNow - _dataExpiration;
        var oldKeys = _statuses
            .Where(kvp => kvp.Value.RequestedAt < cutoff && kvp.Value.IsDownloaded)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var key in oldKeys)
        {
            _statuses.TryRemove(key, out _);
            _exportData.TryRemove(key, out _);
        }
    }
}

/// <summary>
/// Background service that processes export requests.
/// </summary>
public class BackgroundExportService : BackgroundService
{
    private readonly IBackgroundExportQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundExportService> _logger;

    public BackgroundExportService(
        IBackgroundExportQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundExportService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Export Service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = await _queue.DequeueAsync(stoppingToken);
                await ProcessExportAsync(request, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing export request");
            }
        }
        
        _logger.LogInformation("Background Export Service stopped");
    }

    private async Task ProcessExportAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing export request {RequestId} for {ExportType}", 
            request.RequestId, request.ExportType);
            
        _queue.UpdateStatus(request.RequestId, s => s.Status = "Processing");
        
        try
        {
            using var scope = _scopeFactory.CreateScope();
            
            var (data, fileName, contentType) = request.ExportType.ToLowerInvariant() switch
            {
                "accounts" => await ExportAccountsAsync(scope, request, cancellationToken),
                "jobs" => await ExportJobsAsync(scope, request, cancellationToken),
                "rules" => await ExportRulesAsync(scope, request, cancellationToken),
                "blacklist" => await ExportBlacklistAsync(scope, request, cancellationToken),
                _ => throw new ArgumentException($"Unknown export type: {request.ExportType}")
            };
            
            _queue.StoreExportData(request.RequestId, data, fileName, contentType);
            
            _logger.LogInformation("Export request {RequestId} completed successfully", request.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export request {RequestId} failed", request.RequestId);
            _queue.UpdateStatus(request.RequestId, s =>
            {
                s.Status = "Failed";
                s.ErrorMessage = ex.Message;
                s.CompletedAt = DateTime.UtcNow;
            });
        }
    }

    private async Task<(byte[] Data, string FileName, string ContentType)> ExportAccountsAsync(
        IServiceScope scope, ExportRequest request, CancellationToken cancellationToken)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();

        // Extract filter parameters
        request.Filters.TryGetValue("clientId", out var clientIdStr);
        request.Filters.TryGetValue("searchTerm", out var searchTerm);
        request.Filters.TryGetValue("nextRunStatus", out var nextRunStatus);
        request.Filters.TryGetValue("historicalStatus", out var historicalStatus);
        request.Filters.TryGetValue("primaryVendorCode", out var primaryVendorCode);
        request.Filters.TryGetValue("masterVendorCode", out var masterVendorCode);
        
        int? clientId = !string.IsNullOrEmpty(clientIdStr) && int.TryParse(clientIdStr, out var cid) ? cid : null;

        // Build query with filters
        var query = dbContext.AdrAccounts.Where(a => !a.IsDeleted);

        if (clientId.HasValue)
            query = query.Where(a => a.ClientId == clientId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(a =>
                a.VMAccountNumber.Contains(searchTerm) ||
                (a.InterfaceAccountId != null && a.InterfaceAccountId.Contains(searchTerm)) ||
                (a.ClientName != null && a.ClientName.Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(nextRunStatus))
            query = query.Where(a => a.NextRunStatus == nextRunStatus);

        if (!string.IsNullOrWhiteSpace(historicalStatus))
            query = query.Where(a => a.HistoricalBillingStatus == historicalStatus);

        if (!string.IsNullOrWhiteSpace(primaryVendorCode))
            query = query.Where(a => a.PrimaryVendorCode == primaryVendorCode);

        if (!string.IsNullOrWhiteSpace(masterVendorCode))
            query = query.Where(a => a.MasterVendorCode == masterVendorCode);

        // Get accounts with correlated subqueries for job status and rule info
        var exportData = await query
            .Select(a => new
            {
                Account = a,
                CurrentJobStatus = dbContext.AdrJobs
                    .Where(j => j.AdrAccountId == a.Id && !j.IsDeleted)
                    .OrderByDescending(j => j.Id)
                    .Select(j => j.Status)
                    .FirstOrDefault(),
                LastCompletedDateTime = dbContext.AdrJobs
                    .Where(j => j.AdrAccountId == a.Id && !j.IsDeleted && j.Status == "Completed")
                    .OrderByDescending(j => j.Id)
                    .Select(j => (DateTime?)j.ModifiedDateTime)
                    .FirstOrDefault(),
                RuleIsManuallyOverridden = dbContext.AdrAccountRules
                    .Where(r => r.AdrAccountId == a.Id && !r.IsDeleted)
                    .OrderBy(r => r.Id)
                    .Select(r => r.IsManuallyOverridden)
                    .FirstOrDefault(),
                RuleOverriddenBy = dbContext.AdrAccountRules
                    .Where(r => r.AdrAccountId == a.Id && !r.IsDeleted)
                    .OrderBy(r => r.Id)
                    .Select(r => r.OverriddenBy)
                    .FirstOrDefault(),
                RuleOverriddenDateTime = dbContext.AdrAccountRules
                    .Where(r => r.AdrAccountId == a.Id && !r.IsDeleted)
                    .OrderBy(r => r.Id)
                    .Select(r => r.OverriddenDateTime)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        _queue.UpdateStatus(request.RequestId, s => s.TotalRecords = exportData.Count);

        // Get blacklist status
        var today = DateTime.UtcNow.Date;
        var activeBlacklists = await dbContext.AdrAccountBlacklists
            .Where(b => !b.IsDeleted && b.IsActive)
            .Where(b => !b.EffectiveEndDate.HasValue || b.EffectiveEndDate.Value >= today)
            .ToListAsync(cancellationToken);

        var blacklistStatusLookup = new Dictionary<int, (bool HasCurrent, bool HasFuture)>();
        foreach (var item in exportData)
        {
            var account = item.Account;
            var matchingBlacklists = activeBlacklists.Where(b =>
            {
                if (!string.IsNullOrEmpty(b.MasterVendorCode) && b.MasterVendorCode == account.MasterVendorCode)
                    return true;
                if (!string.IsNullOrEmpty(b.PrimaryVendorCode) && b.PrimaryVendorCode == account.PrimaryVendorCode)
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

        var headers = new[] { "Account #", "VM Account ID", "Interface Account ID", "Client", "Master Vendor Code", "Primary Vendor Code", "Period Type", "Next Run", "Run Status", "Job Status", "Last Completed", "Historical Status", "Last Invoice", "Expected Next", "Account Overridden", "Account Overridden By", "Account Overridden Date", "Rule Overridden", "Rule Overridden By", "Rule Overridden Date", "Current Blacklist", "Future Blacklist" };

        var isExcel = request.Format.Equals("excel", StringComparison.OrdinalIgnoreCase);
        var contentType = isExcel
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";
        var extension = isExcel ? "xlsx" : "csv";
        var fileName = $"adr_accounts_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";

        byte[] data;
        if (isExcel)
        {
            data = ExcelExportHelper.CreateExcelExport(
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
                        a.MasterVendorCode,
                        a.PrimaryVendorCode,
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
        }
        else
        {
            data = ExcelExportHelper.CreateCsvExport(
                string.Join(",", headers),
                exportData,
                item =>
                {
                    var a = item.Account;
                    var bl = blacklistStatusLookup.TryGetValue(a.Id, out var blStatus) ? blStatus : (HasCurrent: false, HasFuture: false);
                    return $"{ExcelExportHelper.CsvEscape(a.VMAccountNumber)},{a.VMAccountId},{ExcelExportHelper.CsvEscape(a.InterfaceAccountId)},{ExcelExportHelper.CsvEscape(a.ClientName)},{ExcelExportHelper.CsvEscape(a.MasterVendorCode)},{ExcelExportHelper.CsvEscape(a.PrimaryVendorCode)},{ExcelExportHelper.CsvEscape(a.PeriodType)},{a.NextRunDateTime?.ToString("MM/dd/yyyy") ?? ""},{ExcelExportHelper.CsvEscape(a.NextRunStatus)},{ExcelExportHelper.CsvEscape(item.CurrentJobStatus)},{item.LastCompletedDateTime?.ToString("MM/dd/yyyy") ?? ""},{ExcelExportHelper.CsvEscape(a.HistoricalBillingStatus)},{a.LastInvoiceDateTime?.ToString("MM/dd/yyyy") ?? ""},{a.ExpectedNextDateTime?.ToString("MM/dd/yyyy") ?? ""},{a.IsManuallyOverridden},{ExcelExportHelper.CsvEscape(a.OverriddenBy)},{a.OverriddenDateTime?.ToString("MM/dd/yyyy HH:mm") ?? ""},{(item.RuleIsManuallyOverridden ? "Yes" : "No")},{ExcelExportHelper.CsvEscape(item.RuleOverriddenBy)},{item.RuleOverriddenDateTime?.ToString("MM/dd/yyyy HH:mm") ?? ""},{(bl.HasCurrent ? "Yes" : "No")},{(bl.HasFuture ? "Yes" : "No")}";
                });
        }

        _queue.UpdateStatus(request.RequestId, s => s.RecordsProcessed = exportData.Count);

        return (data, fileName, contentType);
    }

    private async Task<(byte[] Data, string FileName, string ContentType)> ExportJobsAsync(
        IServiceScope scope, ExportRequest request, CancellationToken cancellationToken)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();

        // Extract filter parameters
        request.Filters.TryGetValue("clientId", out var clientIdStr);
        request.Filters.TryGetValue("searchTerm", out var searchTerm);
        request.Filters.TryGetValue("startDate", out var startDateStr);
        request.Filters.TryGetValue("endDate", out var endDateStr);
        request.Filters.TryGetValue("status", out var status);
        request.Filters.TryGetValue("primaryVendorCode", out var primaryVendorCode);
        request.Filters.TryGetValue("masterVendorCode", out var masterVendorCode);
        request.Filters.TryGetValue("latestPerAccount", out var latestPerAccountStr);
        request.Filters.TryGetValue("showManualJobs", out var showManualJobsStr);

        int? clientId = !string.IsNullOrEmpty(clientIdStr) && int.TryParse(clientIdStr, out var cid) ? cid : null;
        DateTime? startDate = !string.IsNullOrEmpty(startDateStr) && DateTime.TryParse(startDateStr, out var sd) ? sd : null;
        DateTime? endDate = !string.IsNullOrEmpty(endDateStr) && DateTime.TryParse(endDateStr, out var ed) ? ed : null;
        bool latestPerAccount = !string.IsNullOrEmpty(latestPerAccountStr) && bool.TryParse(latestPerAccountStr, out var lpa) && lpa;
        bool showManualJobs = !string.IsNullOrEmpty(showManualJobsStr) && bool.TryParse(showManualJobsStr, out var smj) && smj;

        var query = dbContext.AdrJobs
            .Include(j => j.AdrAccount)
            .Include(j => j.Status)
            .Where(j => !j.IsDeleted);

        if (clientId.HasValue)
            query = query.Where(j => j.AdrAccount != null && j.AdrAccount.ClientId == clientId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(j =>
                (j.AdrAccount != null && j.AdrAccount.VMAccountNumber.Contains(searchTerm)) ||
                (j.AdrAccount != null && j.AdrAccount.InterfaceAccountId != null && j.AdrAccount.InterfaceAccountId.Contains(searchTerm)) ||
                (j.AdrAccount != null && j.AdrAccount.ClientName != null && j.AdrAccount.ClientName.Contains(searchTerm)));
        }

        if (startDate.HasValue)
            query = query.Where(j => j.NextRunDateTime >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(j => j.NextRunDateTime <= endDate.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(j => j.Status == status);

        if (!string.IsNullOrWhiteSpace(primaryVendorCode))
            query = query.Where(j => j.AdrAccount != null && j.AdrAccount.PrimaryVendorCode == primaryVendorCode);

        if (!string.IsNullOrWhiteSpace(masterVendorCode))
            query = query.Where(j => j.AdrAccount != null && j.AdrAccount.MasterVendorCode == masterVendorCode);

        if (!showManualJobs)
            query = query.Where(j => !j.IsManualRequest);

        if (latestPerAccount)
        {
            query = query
                .GroupBy(j => j.AdrAccountId)
                .Select(g => g.OrderByDescending(j => j.Id).First());
        }

        var jobs = await query.OrderByDescending(j => j.Id).ToListAsync(cancellationToken);

        _queue.UpdateStatus(request.RequestId, s => s.TotalRecords = jobs.Count);

        var headers = new[] { "Job ID", "Account #", "VM Account ID", "Interface Account ID", "Client", "Master Vendor Code", "Primary Vendor Code", "Period Type", "Next Run", "Range Start", "Range End", "Status", "Is Manual", "Manual Reason", "Created", "Modified" };

        var isExcel = request.Format.Equals("excel", StringComparison.OrdinalIgnoreCase);
        var contentType = isExcel
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";
        var extension = isExcel ? "xlsx" : "csv";
        var fileName = $"adr_jobs_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";

        byte[] data;
        if (isExcel)
        {
            data = ExcelExportHelper.CreateExcelExport(
                "ADR Jobs",
                "AdrJobsTable",
                headers,
                jobs,
                j => new object?[]
                {
                    j.Id,
                    j.AdrAccount?.VMAccountNumber ?? "",
                    j.AdrAccount?.VMAccountId,
                    j.AdrAccount?.InterfaceAccountId ?? "",
                    j.AdrAccount?.ClientName ?? "",
                    j.AdrAccount?.MasterVendorCode ?? "",
                    j.AdrAccount?.PrimaryVendorCode ?? "",
                    j.AdrAccount?.PeriodType ?? "",
                    j.NextRunDateTime,
                    j.NextRangeStartDateTime,
                    j.NextRangeEndDateTime,
                    j.Status ?? "",
                    j.IsManualRequest,
                    j.ManualRequestReason ?? "",
                    j.CreatedDateTime,
                    j.ModifiedDateTime
                });
        }
        else
        {
            data = ExcelExportHelper.CreateCsvExport(
                string.Join(",", headers),
                jobs,
                j => $"{j.Id},{ExcelExportHelper.CsvEscape(j.AdrAccount?.VMAccountNumber)},{j.AdrAccount?.VMAccountId},{ExcelExportHelper.CsvEscape(j.AdrAccount?.InterfaceAccountId)},{ExcelExportHelper.CsvEscape(j.AdrAccount?.ClientName)},{ExcelExportHelper.CsvEscape(j.AdrAccount?.MasterVendorCode)},{ExcelExportHelper.CsvEscape(j.AdrAccount?.PrimaryVendorCode)},{ExcelExportHelper.CsvEscape(j.AdrAccount?.PeriodType)},{j.NextRunDateTime?.ToString("MM/dd/yyyy") ?? ""},{j.NextRangeStartDateTime?.ToString("MM/dd/yyyy") ?? ""},{j.NextRangeEndDateTime?.ToString("MM/dd/yyyy") ?? ""},{ExcelExportHelper.CsvEscape(j.Status)},{j.IsManualRequest},{ExcelExportHelper.CsvEscape(j.ManualRequestReason)},{j.CreatedDateTime:MM/dd/yyyy HH:mm},{j.ModifiedDateTime:MM/dd/yyyy HH:mm}");
        }

        _queue.UpdateStatus(request.RequestId, s => s.RecordsProcessed = jobs.Count);

        return (data, fileName, contentType);
    }

    private async Task<(byte[] Data, string FileName, string ContentType)> ExportRulesAsync(
        IServiceScope scope, ExportRequest request, CancellationToken cancellationToken)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();

        // Extract filter parameters
        request.Filters.TryGetValue("searchTerm", out var searchTerm);
        request.Filters.TryGetValue("primaryVendorCode", out var primaryVendorCode);
        request.Filters.TryGetValue("masterVendorCode", out var masterVendorCode);
        request.Filters.TryGetValue("isEnabled", out var isEnabledStr);
        request.Filters.TryGetValue("isOverridden", out var isOverriddenStr);
        request.Filters.TryGetValue("periodType", out var periodType);

        bool? isEnabled = !string.IsNullOrEmpty(isEnabledStr) && bool.TryParse(isEnabledStr, out var ie) ? ie : null;
        bool? isOverridden = !string.IsNullOrEmpty(isOverriddenStr) && bool.TryParse(isOverriddenStr, out var io) ? io : null;

        var query = dbContext.AdrAccountRules
            .Include(r => r.AdrAccount)
            .Where(r => !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(r =>
                (r.AdrAccount != null && r.AdrAccount.VMAccountNumber.Contains(searchTerm)) ||
                (r.AdrAccount != null && r.AdrAccount.InterfaceAccountId != null && r.AdrAccount.InterfaceAccountId.Contains(searchTerm)) ||
                (r.AdrAccount != null && r.AdrAccount.ClientName != null && r.AdrAccount.ClientName.Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(primaryVendorCode))
            query = query.Where(r => r.AdrAccount != null && r.AdrAccount.PrimaryVendorCode == primaryVendorCode);

        if (!string.IsNullOrWhiteSpace(masterVendorCode))
            query = query.Where(r => r.AdrAccount != null && r.AdrAccount.MasterVendorCode == masterVendorCode);

        if (isEnabled.HasValue)
            query = query.Where(r => r.IsEnabled == isEnabled.Value);

        if (isOverridden.HasValue)
            query = query.Where(r => r.IsManuallyOverridden == isOverridden.Value);

        if (!string.IsNullOrWhiteSpace(periodType))
            query = query.Where(r => r.PeriodType == periodType);

        var rules = await query.ToListAsync(cancellationToken);

        _queue.UpdateStatus(request.RequestId, s => s.TotalRecords = rules.Count);

        var headers = new[] { "Rule ID", "Account #", "VM Account ID", "Interface Account ID", "Client", "Master Vendor Code", "Primary Vendor Code", "Period Type", "Period Days", "Next Run", "Range Start", "Range End", "Is Enabled", "Is Overridden", "Overridden By", "Overridden Date" };

        var isExcel = request.Format.Equals("excel", StringComparison.OrdinalIgnoreCase);
        var contentType = isExcel
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";
        var extension = isExcel ? "xlsx" : "csv";
        var fileName = $"adr_rules_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";

        byte[] data;
        if (isExcel)
        {
            data = ExcelExportHelper.CreateExcelExport(
                "ADR Rules",
                "AdrRulesTable",
                headers,
                rules,
                r => new object?[]
                {
                    r.Id,
                    r.AdrAccount?.VMAccountNumber ?? "",
                    r.AdrAccount?.VMAccountId,
                    r.AdrAccount?.InterfaceAccountId ?? "",
                    r.AdrAccount?.ClientName ?? "",
                    r.AdrAccount?.MasterVendorCode ?? "",
                    r.AdrAccount?.PrimaryVendorCode ?? "",
                    r.PeriodType,
                    r.PeriodDays,
                    r.NextRunDateTime,
                    r.NextRangeStartDateTime,
                    r.NextRangeEndDateTime,
                    r.IsEnabled,
                    r.IsManuallyOverridden,
                    r.OverriddenBy ?? "",
                    r.OverriddenDateTime
                });
        }
        else
        {
            data = ExcelExportHelper.CreateCsvExport(
                string.Join(",", headers),
                rules,
                r => $"{r.Id},{ExcelExportHelper.CsvEscape(r.AdrAccount?.VMAccountNumber)},{r.AdrAccount?.VMAccountId},{ExcelExportHelper.CsvEscape(r.AdrAccount?.InterfaceAccountId)},{ExcelExportHelper.CsvEscape(r.AdrAccount?.ClientName)},{ExcelExportHelper.CsvEscape(r.AdrAccount?.MasterVendorCode)},{ExcelExportHelper.CsvEscape(r.AdrAccount?.PrimaryVendorCode)},{ExcelExportHelper.CsvEscape(r.PeriodType)},{r.PeriodDays},{r.NextRunDateTime?.ToString("MM/dd/yyyy") ?? ""},{r.NextRangeStartDateTime?.ToString("MM/dd/yyyy") ?? ""},{r.NextRangeEndDateTime?.ToString("MM/dd/yyyy") ?? ""},{r.IsEnabled},{r.IsManuallyOverridden},{ExcelExportHelper.CsvEscape(r.OverriddenBy)},{r.OverriddenDateTime?.ToString("MM/dd/yyyy HH:mm") ?? ""}");
        }

        _queue.UpdateStatus(request.RequestId, s => s.RecordsProcessed = rules.Count);

        return (data, fileName, contentType);
    }

    private async Task<(byte[] Data, string FileName, string ContentType)> ExportBlacklistAsync(
        IServiceScope scope, ExportRequest request, CancellationToken cancellationToken)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();

        // Extract filter parameters
        request.Filters.TryGetValue("searchTerm", out var searchTerm);
        request.Filters.TryGetValue("primaryVendorCode", out var primaryVendorCode);
        request.Filters.TryGetValue("masterVendorCode", out var masterVendorCode);

        var query = dbContext.AdrAccountBlacklists.Where(b => !b.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(b =>
                (b.VMAccountNumber != null && b.VMAccountNumber.Contains(searchTerm)) ||
                (b.PrimaryVendorCode != null && b.PrimaryVendorCode.Contains(searchTerm)) ||
                (b.MasterVendorCode != null && b.MasterVendorCode.Contains(searchTerm)) ||
                (b.Reason != null && b.Reason.Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(primaryVendorCode))
            query = query.Where(b => b.PrimaryVendorCode == primaryVendorCode);

        if (!string.IsNullOrWhiteSpace(masterVendorCode))
            query = query.Where(b => b.MasterVendorCode == masterVendorCode);

        var entries = await query.ToListAsync(cancellationToken);

        _queue.UpdateStatus(request.RequestId, s => s.TotalRecords = entries.Count);

        var headers = new[] { "ID", "VM Account ID", "VM Account Number", "Credential ID", "Master Vendor Code", "Primary Vendor Code", "Reason", "Is Active", "Effective Start", "Effective End", "Created By", "Created Date" };

        var isExcel = request.Format.Equals("excel", StringComparison.OrdinalIgnoreCase);
        var contentType = isExcel
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";
        var extension = isExcel ? "xlsx" : "csv";
        var fileName = $"adr_blacklist_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";

        byte[] data;
        if (isExcel)
        {
            data = ExcelExportHelper.CreateExcelExport(
                "ADR Blacklist",
                "AdrBlacklistTable",
                headers,
                entries,
                b => new object?[]
                {
                    b.Id,
                    b.VMAccountId,
                    b.VMAccountNumber ?? "",
                    b.CredentialId,
                    b.MasterVendorCode ?? "",
                    b.PrimaryVendorCode ?? "",
                    b.Reason ?? "",
                    b.IsActive,
                    b.EffectiveStartDate,
                    b.EffectiveEndDate,
                    b.CreatedBy ?? "",
                    b.CreatedDateTime
                });
        }
        else
        {
            data = ExcelExportHelper.CreateCsvExport(
                string.Join(",", headers),
                entries,
                b => $"{b.Id},{b.VMAccountId},{ExcelExportHelper.CsvEscape(b.VMAccountNumber)},{b.CredentialId},{ExcelExportHelper.CsvEscape(b.MasterVendorCode)},{ExcelExportHelper.CsvEscape(b.PrimaryVendorCode)},{ExcelExportHelper.CsvEscape(b.Reason)},{b.IsActive},{b.EffectiveStartDate?.ToString("MM/dd/yyyy") ?? ""},{b.EffectiveEndDate?.ToString("MM/dd/yyyy") ?? ""},{ExcelExportHelper.CsvEscape(b.CreatedBy)},{b.CreatedDateTime:MM/dd/yyyy HH:mm}");
        }

        _queue.UpdateStatus(request.RequestId, s => s.RecordsProcessed = entries.Count);

        return (data, fileName, contentType);
    }
}
