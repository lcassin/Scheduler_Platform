using System.Net.Http.Json;
using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public class AdrService : IAdrService
{
    private readonly AuthenticatedHttpClientService _httpClient;

    public AdrService(AuthenticatedHttpClientService httpClient)
    {
        _httpClient = httpClient;
    }

    #region Account Operations

    public async Task<PagedResult<AdrAccount>> GetAccountsPagedAsync(
        int pageNumber = 1,
        int pageSize = 20,
        int? clientId = null,
        string? searchTerm = null,
        string? nextRunStatus = null,
        string? historicalBillingStatus = null,
        bool? isOverridden = null,
        string? jobStatus = null,
        string? blacklistStatus = null,
        string? sortColumn = null,
        bool sortDescending = false)
    {
        var queryParams = new List<string>
        {
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (clientId.HasValue)
            queryParams.Add($"clientId={clientId.Value}");

        if (!string.IsNullOrWhiteSpace(searchTerm))
            queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm)}");

        if (!string.IsNullOrWhiteSpace(nextRunStatus))
            queryParams.Add($"nextRunStatus={Uri.EscapeDataString(nextRunStatus)}");

        if (!string.IsNullOrWhiteSpace(historicalBillingStatus))
            queryParams.Add($"historicalBillingStatus={Uri.EscapeDataString(historicalBillingStatus)}");

        if (isOverridden.HasValue)
            queryParams.Add($"isOverridden={isOverridden.Value.ToString().ToLowerInvariant()}");

        if (!string.IsNullOrWhiteSpace(jobStatus))
            queryParams.Add($"jobStatus={Uri.EscapeDataString(jobStatus)}");

        if (!string.IsNullOrWhiteSpace(blacklistStatus))
            queryParams.Add($"blacklistStatus={Uri.EscapeDataString(blacklistStatus)}");

        if (!string.IsNullOrWhiteSpace(sortColumn))
            queryParams.Add($"sortColumn={Uri.EscapeDataString(sortColumn)}");

        queryParams.Add($"sortDescending={sortDescending.ToString().ToLower()}");

        var query = "?" + string.Join("&", queryParams);
        var response = await _httpClient.GetAsync($"adr/accounts{query}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<AdrAccount>>();

        return result ?? new PagedResult<AdrAccount>
        {
            Items = new List<AdrAccount>(),
            TotalCount = 0,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AdrAccount?> GetAccountAsync(int id)
    {
        var response = await _httpClient.GetAsync($"adr/accounts/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdrAccount>();
    }

    public async Task<AdrAccount?> GetAccountByVMAccountIdAsync(long vmAccountId)
    {
        var response = await _httpClient.GetAsync($"adr/accounts/by-vm-account-id/{vmAccountId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdrAccount>();
    }

    public async Task<AdrAccountStats> GetAccountStatsAsync()
    {
        var response = await _httpClient.GetAsync($"adr/accounts/stats");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AdrAccountStats>();
        return result ?? new AdrAccountStats();
    }

    public async Task<AdrAccount> UpdateAccountBillingAsync(int accountId, DateTime? expectedBillingDate, string? periodType, string? historicalBillingStatus)
    {
        var request = new
        {
            ExpectedBillingDate = expectedBillingDate,
            PeriodType = periodType,
            HistoricalBillingStatus = historicalBillingStatus
        };
        var response = await _httpClient.PutAsJsonAsync($"adr/accounts/{accountId}/billing", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AdrAccount>();
        return result ?? throw new InvalidOperationException("Failed to update account billing");
    }

    public async Task<AdrAccount> ClearAccountOverrideAsync(int accountId)
    {
        var response = await _httpClient.PostAsJsonAsync($"adr/accounts/{accountId}/clear-override", new { });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AdrAccount>();
        return result ?? throw new InvalidOperationException("Failed to clear account override");
    }

    public async Task<ManualScrapeResult> ManualScrapeRequestAsync(int accountId, DateTime targetDate, DateTime? rangeStartDate = null, DateTime? rangeEndDate = null, string? reason = null, bool isHighPriority = false, int requestType = 2)
    {
        var request = new
        {
            TargetDate = targetDate,
            RangeStartDate = rangeStartDate,
            RangeEndDate = rangeEndDate,
            Reason = reason,
            IsHighPriority = isHighPriority,
            RequestType = requestType
        };
        var response = await _httpClient.PostAsJsonAsync($"adr/accounts/{accountId}/manual-scrape", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ManualScrapeResult>();
        return result ?? throw new InvalidOperationException("Failed to create manual ADR request");
    }

    public async Task<byte[]> DownloadAccountsExportAsync(
        int? clientId = null,
        string? searchTerm = null,
        string? nextRunStatus = null,
        string? historicalBillingStatus = null,
        string format = "excel")
    {
        var queryParams = new List<string> { $"format={format}" };

        if (clientId.HasValue)
            queryParams.Add($"clientId={clientId.Value}");

        if (!string.IsNullOrWhiteSpace(searchTerm))
            queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm)}");

        if (!string.IsNullOrWhiteSpace(nextRunStatus))
            queryParams.Add($"nextRunStatus={Uri.EscapeDataString(nextRunStatus)}");

        if (!string.IsNullOrWhiteSpace(historicalBillingStatus))
            queryParams.Add($"historicalBillingStatus={Uri.EscapeDataString(historicalBillingStatus)}");

        var query = "?" + string.Join("&", queryParams);
        var response = await _httpClient.GetAsync($"adr/accounts/export{query}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    #endregion

    #region Job Operations

    public async Task<PagedResult<AdrJob>> GetJobsPagedAsync(
        int pageNumber = 1,
        int pageSize = 20,
        int? adrAccountId = null,
        string? status = null,
        string? vendorCode = null,
        string? vmAccountNumber = null,
        bool latestPerAccount = false,
        long? vmAccountId = null,
        string? interfaceAccountId = null,
        int? credentialId = null,
        bool? isManualRequest = null,
        string? blacklistStatus = null,
        string? sortColumn = null,
        bool sortDescending = true)
    {
        var queryParams = new List<string>
        {
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (adrAccountId.HasValue)
            queryParams.Add($"adrAccountId={adrAccountId.Value}");

        if (!string.IsNullOrWhiteSpace(status))
            queryParams.Add($"status={Uri.EscapeDataString(status)}");

        if (!string.IsNullOrWhiteSpace(vendorCode))
            queryParams.Add($"vendorCode={Uri.EscapeDataString(vendorCode)}");

        if (!string.IsNullOrWhiteSpace(vmAccountNumber))
            queryParams.Add($"vmAccountNumber={Uri.EscapeDataString(vmAccountNumber)}");

        if (latestPerAccount)
            queryParams.Add("latestPerAccount=true");

        if (vmAccountId.HasValue)
            queryParams.Add($"vmAccountId={vmAccountId.Value}");

        if (!string.IsNullOrWhiteSpace(interfaceAccountId))
            queryParams.Add($"interfaceAccountId={Uri.EscapeDataString(interfaceAccountId)}");

        if (credentialId.HasValue)
            queryParams.Add($"credentialId={credentialId.Value}");

        if (isManualRequest.HasValue)
            queryParams.Add($"isManualRequest={isManualRequest.Value.ToString().ToLower()}");

        if (!string.IsNullOrWhiteSpace(blacklistStatus))
            queryParams.Add($"blacklistStatus={Uri.EscapeDataString(blacklistStatus)}");

        if (!string.IsNullOrWhiteSpace(sortColumn))
            queryParams.Add($"sortColumn={Uri.EscapeDataString(sortColumn)}");

        queryParams.Add($"sortDescending={sortDescending.ToString().ToLower()}");

        var query = "?" + string.Join("&", queryParams);
        var response = await _httpClient.GetAsync($"adr/jobs{query}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<AdrJob>>();

        return result ?? new PagedResult<AdrJob>
        {
            Items = new List<AdrJob>(),
            TotalCount = 0,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AdrJob?> GetJobAsync(int id)
    {
        var response = await _httpClient.GetAsync($"adr/jobs/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdrJob>();
    }

    public async Task<List<AdrJob>> GetJobsByAccountAsync(int adrAccountId)
    {
        var response = await _httpClient.GetAsync($"adr/jobs/by-account/{adrAccountId}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<AdrJob>>();
        return result ?? new List<AdrJob>();
    }

    public async Task<AdrJobStats> GetJobStatsAsync(int? lastOrchestrationRuns = null)
    {
        var url = lastOrchestrationRuns.HasValue 
            ? $"adr/jobs/stats?lastOrchestrationRuns={lastOrchestrationRuns.Value}"
            : "adr/jobs/stats";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AdrJobStats>();
        return result ?? new AdrJobStats();
    }

    public async Task<byte[]> DownloadJobsExportAsync(
        string? status = null,
        string? vendorCode = null,
        string? vmAccountNumber = null,
        bool latestPerAccount = false,
        string format = "excel")
    {
        var queryParams = new List<string> { $"format={format}" };

        if (!string.IsNullOrWhiteSpace(status))
            queryParams.Add($"status={Uri.EscapeDataString(status)}");

        if (!string.IsNullOrWhiteSpace(vendorCode))
            queryParams.Add($"vendorCode={Uri.EscapeDataString(vendorCode)}");

        if (!string.IsNullOrWhiteSpace(vmAccountNumber))
            queryParams.Add($"vmAccountNumber={Uri.EscapeDataString(vmAccountNumber)}");

        if (latestPerAccount)
            queryParams.Add("latestPerAccount=true");

        var query = "?" + string.Join("&", queryParams);
        var response = await _httpClient.GetAsync($"adr/jobs/export{query}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    #endregion

    #region Execution Operations

    public async Task<PagedResult<AdrJobExecution>> GetExecutionsPagedAsync(
        int pageNumber = 1,
        int pageSize = 20,
        int? adrJobId = null)
    {
        var queryParams = new List<string>
        {
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (adrJobId.HasValue)
            queryParams.Add($"adrJobId={adrJobId.Value}");

        var query = "?" + string.Join("&", queryParams);
        var response = await _httpClient.GetAsync($"adr/executions{query}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<AdrJobExecution>>();

        return result ?? new PagedResult<AdrJobExecution>
        {
            Items = new List<AdrJobExecution>(),
            TotalCount = 0,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<List<AdrJobExecution>> GetExecutionsByJobAsync(int adrJobId)
    {
        var response = await _httpClient.GetAsync($"adr/executions/by-job/{adrJobId}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<AdrJobExecution>>();
        return result ?? new List<AdrJobExecution>();
    }

    #endregion

    #region Job Refire Operations

    public async Task<RefireJobResult> RefireJobAsync(int jobId, bool forceRefire = false)
    {
        var url = forceRefire ? $"adr/jobs/{jobId}/refire?forceRefire=true" : $"adr/jobs/{jobId}/refire";
        var response = await _httpClient.PostAsJsonAsync(url, new { });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RefireJobResult>();
        return result ?? new RefireJobResult { Message = "Job refired", JobId = jobId };
    }

    public async Task<RefireJobsBulkResult> RefireJobsBulkAsync(List<int> jobIds, bool forceRefire = false)
    {
        var response = await _httpClient.PostAsJsonAsync("adr/jobs/refire-bulk", new { JobIds = jobIds, ForceRefire = forceRefire });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RefireJobsBulkResult>();
        return result ?? new RefireJobsBulkResult { Message = "Jobs refired", RefiredCount = jobIds.Count, TotalRequested = jobIds.Count };
    }

    public async Task<CheckJobStatusResult> CheckJobStatusAsync(int jobId)
    {
        var response = await _httpClient.PostAsJsonAsync($"adr/jobs/{jobId}/check-status", new { });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CheckJobStatusResult>();
        return result ?? new CheckJobStatusResult { JobId = jobId, IsSuccess = false, ErrorMessage = "Failed to parse response" };
    }

    #endregion

    #region Orchestration Operations

    public async Task<AdrAccountSyncResult> SyncAccountsAsync()
    {
        var response = await _httpClient.PostAsJsonAsync("adr/sync/accounts", new { });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AdrAccountSyncResult>();
        return result ?? new AdrAccountSyncResult();
    }

    public async Task<JobCreationResult> CreateJobsAsync()
    {
        var response = await _httpClient.PostAsJsonAsync("adr/orchestrate/create-jobs", new { });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JobCreationResult>();
        return result ?? new JobCreationResult();
    }

    public async Task<CredentialVerificationResult> VerifyCredentialsAsync()
    {
        var response = await _httpClient.PostAsJsonAsync("adr/orchestrate/verify-credentials", new { });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CredentialVerificationResult>();
        return result ?? new CredentialVerificationResult();
    }

    public async Task<ScrapeResult> ProcessScrapingAsync()
    {
        var response = await _httpClient.PostAsJsonAsync("adr/orchestrate/process-scraping", new { });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ScrapeResult>();
        return result ?? new ScrapeResult();
    }

    public async Task<StatusCheckResult> CheckStatusesAsync()
    {
        var response = await _httpClient.PostAsJsonAsync("adr/orchestrate/check-statuses", new { });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StatusCheckResult>();
        return result ?? new StatusCheckResult();
    }

    public async Task<FullCycleResult> RunFullCycleAsync()
    {
        var response = await _httpClient.PostAsJsonAsync("adr/orchestrate/run-full-cycle", new { });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<FullCycleResult>();
        return result ?? new FullCycleResult();
    }

    #endregion

    #region Background Orchestration Monitoring

    public async Task<BackgroundOrchestrationResponse> StartBackgroundOrchestrationAsync(BackgroundOrchestrationRequest? request = null)
    {
        HttpResponseMessage response;
        if (request != null)
        {
            response = await _httpClient.PostAsJsonAsync("adr/orchestrate/run-background", request);
        }
        else
        {
            response = await _httpClient.PostAsJsonAsync("adr/orchestrate/run-background", new { });
        }
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BackgroundOrchestrationResponse>();
        return result ?? new BackgroundOrchestrationResponse();
    }

    public async Task<OrchestrationCurrentResponse> GetCurrentOrchestrationAsync()
    {
        var response = await _httpClient.GetAsync("adr/orchestrate/current");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OrchestrationCurrentResponse>();
        return result ?? new OrchestrationCurrentResponse { IsRunning = false, Message = "Unable to get status" };
    }

    public async Task<AdrOrchestrationStatus?> GetOrchestrationStatusAsync(string requestId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"adr/orchestrate/status/{requestId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AdrOrchestrationStatus>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<AdrOrchestrationStatus>> GetOrchestrationHistoryAsync(int? count = 10)
    {
        var url = count.HasValue ? $"adr/orchestrate/history?count={count}" : "adr/orchestrate/history";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<AdrOrchestrationStatus>>();
        return result ?? new List<AdrOrchestrationStatus>();
    }

    public async Task<OrchestrationHistoryPagedResponse> GetOrchestrationHistoryPagedAsync(int pageNumber = 1, int pageSize = 20)
    {
        var response = await _httpClient.GetAsync($"adr/orchestrate/history?pageNumber={pageNumber}&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OrchestrationHistoryPagedResponse>();
        return result ?? new OrchestrationHistoryPagedResponse();
    }

    public async Task<CancelOrchestrationResult> CancelOrchestrationAsync(string requestId)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"adr/orchestrate/{requestId}/cancel", new { });
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var successResult = System.Text.Json.JsonSerializer.Deserialize<CancelOrchestrationSuccessResponse>(
                    content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new CancelOrchestrationResult
                {
                    Success = successResult?.Success ?? true,
                    Message = successResult?.Message,
                    RequestId = requestId,
                    Status = successResult?.Status
                };
            }
            else
            {
                var errorResult = System.Text.Json.JsonSerializer.Deserialize<CancelOrchestrationErrorResponse>(
                    content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new CancelOrchestrationResult
                {
                    Success = false,
                    Error = errorResult?.Error ?? errorResult?.Message ?? "Failed to cancel orchestration",
                    Message = errorResult?.Message,
                    RequestId = requestId
                };
            }
        }
        catch (Exception ex)
        {
            return new CancelOrchestrationResult
            {
                Success = false,
                Error = ex.Message,
                RequestId = requestId
            };
        }
    }

    public async Task<BlacklistCountsResult> GetBlacklistCountsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("adr/blacklist/counts");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<BlacklistCountsResult>();
            return result ?? new BlacklistCountsResult();
        }
        catch (HttpRequestException)
        {
            return new BlacklistCountsResult();
        }
    }

    #endregion

    #region Rule Operations

    public async Task<AccountRuleDto?> GetRuleAsync(int ruleId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"adr/rules/{ruleId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AccountRuleDto>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<AccountRuleDto>> GetRulesByAccountAsync(int accountId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"adr/rules/by-account/{accountId}");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<List<AccountRuleDto>>();
            return result ?? new List<AccountRuleDto>();
        }
        catch (HttpRequestException)
        {
            return new List<AccountRuleDto>();
        }
    }

    public async Task<AccountRuleDto> UpdateRuleAsync(int ruleId, UpdateRuleRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"adr/rules/{ruleId}", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AccountRuleDto>();
        return result ?? throw new InvalidOperationException("Failed to update rule");
    }

    public async Task<AccountRuleDto> ClearRuleOverrideAsync(int ruleId)
    {
        var response = await _httpClient.PostAsJsonAsync($"adr/rules/{ruleId}/clear-override", new { });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AccountRuleDto>();
        return result ?? throw new InvalidOperationException("Failed to clear rule override");
    }

    public async Task<byte[]> DownloadRulesExportAsync(
        string? vendorCode = null,
        string? accountNumber = null,
        bool? isEnabled = null,
        bool? isOverridden = null,
        string format = "excel")
    {
        var queryParams = new List<string> { $"format={format}" };
        
        if (!string.IsNullOrWhiteSpace(vendorCode))
            queryParams.Add($"vendorCode={Uri.EscapeDataString(vendorCode)}");
        if (!string.IsNullOrWhiteSpace(accountNumber))
            queryParams.Add($"accountNumber={Uri.EscapeDataString(accountNumber)}");
        if (isEnabled.HasValue)
            queryParams.Add($"isEnabled={isEnabled.Value}");
        if (isOverridden.HasValue)
            queryParams.Add($"isOverridden={isOverridden.Value}");
        
        var url = $"adr/rules/export?{string.Join("&", queryParams)}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    #endregion
}

internal class CancelOrchestrationSuccessResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? RequestId { get; set; }
    public AdrOrchestrationStatus? Status { get; set; }
}

internal class CancelOrchestrationErrorResponse
{
    public string? Error { get; set; }
    public string? Message { get; set; }
    public string? RequestId { get; set; }
    public string? CurrentStatus { get; set; }
}

public class OrchestrationHistoryPagedResponse
{
    public List<AdrOrchestrationStatus> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
