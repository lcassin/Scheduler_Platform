using System.Net.Http.Json;
using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public class AdrService : IAdrService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AdrService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("SchedulerAPI");

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
        string? sortColumn = null,
        bool sortDescending = false)
    {
        var client = CreateClient();
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

        if (!string.IsNullOrWhiteSpace(sortColumn))
            queryParams.Add($"sortColumn={Uri.EscapeDataString(sortColumn)}");

        queryParams.Add($"sortDescending={sortDescending.ToString().ToLower()}");

        var query = "?" + string.Join("&", queryParams);
        var result = await client.GetFromJsonAsync<PagedResult<AdrAccount>>($"adr/accounts{query}");

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
        var client = CreateClient();
        return await client.GetFromJsonAsync<AdrAccount>($"adr/accounts/{id}");
    }

    public async Task<AdrAccount?> GetAccountByVMAccountIdAsync(long vmAccountId)
    {
        var client = CreateClient();
        return await client.GetFromJsonAsync<AdrAccount>($"adr/accounts/by-vm-account-id/{vmAccountId}");
    }

    public async Task<AdrAccountStats> GetAccountStatsAsync()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<AdrAccountStats>($"adr/accounts/stats");
        return result ?? new AdrAccountStats();
    }

    public async Task<AdrAccount> UpdateAccountBillingAsync(int accountId, DateTime? expectedBillingDate, string? periodType, string? historicalBillingStatus)
    {
        var client = CreateClient();
        var request = new
        {
            ExpectedBillingDate = expectedBillingDate,
            PeriodType = periodType,
            HistoricalBillingStatus = historicalBillingStatus
        };
        var response = await client.PutAsJsonAsync($"adr/accounts/{accountId}/billing", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AdrAccount>();
        return result ?? throw new InvalidOperationException("Failed to update account billing");
    }

        public async Task<AdrAccount> ClearAccountOverrideAsync(int accountId)
        {
            var client = CreateClient();
            var response = await client.PostAsync($"adr/accounts/{accountId}/clear-override", null);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AdrAccount>();
            return result ?? throw new InvalidOperationException("Failed to clear account override");
        }

        public async Task<ManualScrapeResult> ManualScrapeRequestAsync(int accountId, DateTime targetDate, DateTime? rangeStartDate = null, DateTime? rangeEndDate = null, string? reason = null, bool isHighPriority = false, int requestType = 2)
        {
            var client = CreateClient();
            var request = new
            {
                TargetDate = targetDate,
                RangeStartDate = rangeStartDate,
                RangeEndDate = rangeEndDate,
                Reason = reason,
                IsHighPriority = isHighPriority,
                RequestType = requestType
            };
            var response = await client.PostAsJsonAsync($"adr/accounts/{accountId}/manual-scrape", request);
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
        var client = CreateClient();
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
        var response = await client.GetAsync($"adr/accounts/export{query}");
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
            string? sortColumn = null,
            bool sortDescending = true)
        {
            var client = CreateClient();
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

            if (!string.IsNullOrWhiteSpace(sortColumn))
                queryParams.Add($"sortColumn={Uri.EscapeDataString(sortColumn)}");

            queryParams.Add($"sortDescending={sortDescending.ToString().ToLower()}");

            var query = "?" + string.Join("&", queryParams);
            var result = await client.GetFromJsonAsync<PagedResult<AdrJob>>($"adr/jobs{query}");

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
        var client = CreateClient();
        return await client.GetFromJsonAsync<AdrJob>($"adr/jobs/{id}");
    }

    public async Task<List<AdrJob>> GetJobsByAccountAsync(int adrAccountId)
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<List<AdrJob>>($"adr/jobs/by-account/{adrAccountId}");
        return result ?? new List<AdrJob>();
    }

    public async Task<AdrJobStats> GetJobStatsAsync(int? lastOrchestrationRuns = null)
    {
        var client = CreateClient();
        var url = lastOrchestrationRuns.HasValue 
            ? $"adr/jobs/stats?lastOrchestrationRuns={lastOrchestrationRuns.Value}"
            : "adr/jobs/stats";
        var result = await client.GetFromJsonAsync<AdrJobStats>(url);
        return result ?? new AdrJobStats();
    }

    public async Task<byte[]> DownloadJobsExportAsync(
        string? status = null,
        string? vendorCode = null,
        string? vmAccountNumber = null,
        bool latestPerAccount = false,
        string format = "excel")
    {
        var client = CreateClient();
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
        var response = await client.GetAsync($"adr/jobs/export{query}");
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
        var client = CreateClient();
        var queryParams = new List<string>
        {
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (adrJobId.HasValue)
            queryParams.Add($"adrJobId={adrJobId.Value}");

        var query = "?" + string.Join("&", queryParams);
        var result = await client.GetFromJsonAsync<PagedResult<AdrJobExecution>>($"adr/executions{query}");

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
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<List<AdrJobExecution>>($"adr/executions/by-job/{adrJobId}");
        return result ?? new List<AdrJobExecution>();
    }

    #endregion

    #region Job Refire Operations

    public async Task<RefireJobResult> RefireJobAsync(int jobId, bool forceRefire = false)
    {
        var client = CreateClient();
        var url = forceRefire ? $"adr/jobs/{jobId}/refire?forceRefire=true" : $"adr/jobs/{jobId}/refire";
        var response = await client.PostAsync(url, null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RefireJobResult>();
        return result ?? new RefireJobResult { Message = "Job refired", JobId = jobId };
    }

        public async Task<RefireJobsBulkResult> RefireJobsBulkAsync(List<int> jobIds, bool forceRefire = false)
        {
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("adr/jobs/refire-bulk", new { JobIds = jobIds, ForceRefire = forceRefire });
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<RefireJobsBulkResult>();
            return result ?? new RefireJobsBulkResult { Message = "Jobs refired", RefiredCount = jobIds.Count, TotalRequested = jobIds.Count };
        }

        public async Task<CheckJobStatusResult> CheckJobStatusAsync(int jobId)
        {
            var client = CreateClient();
            var response = await client.PostAsync($"adr/jobs/{jobId}/check-status", null);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CheckJobStatusResult>();
            return result ?? new CheckJobStatusResult { JobId = jobId, IsSuccess = false, ErrorMessage = "Failed to parse response" };
        }

        #endregion

    #region Orchestration Operations

    public async Task<AdrAccountSyncResult> SyncAccountsAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsync("adr/sync/accounts", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AdrAccountSyncResult>();
        return result ?? new AdrAccountSyncResult();
    }

    public async Task<JobCreationResult> CreateJobsAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsync("adr/orchestrate/create-jobs", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JobCreationResult>();
        return result ?? new JobCreationResult();
    }

    public async Task<CredentialVerificationResult> VerifyCredentialsAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsync("adr/orchestrate/verify-credentials", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CredentialVerificationResult>();
        return result ?? new CredentialVerificationResult();
    }

    public async Task<ScrapeResult> ProcessScrapingAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsync("adr/orchestrate/process-scraping", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ScrapeResult>();
        return result ?? new ScrapeResult();
    }

    public async Task<StatusCheckResult> CheckStatusesAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsync("adr/orchestrate/check-statuses", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StatusCheckResult>();
        return result ?? new StatusCheckResult();
    }

    public async Task<FullCycleResult> RunFullCycleAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsync("adr/orchestrate/run-full-cycle", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<FullCycleResult>();
        return result ?? new FullCycleResult();
    }

    #endregion

    #region Background Orchestration Monitoring

    public async Task<BackgroundOrchestrationResponse> StartBackgroundOrchestrationAsync(BackgroundOrchestrationRequest? request = null)
    {
        var client = CreateClient();
        HttpResponseMessage response;
        if (request != null)
        {
            response = await client.PostAsJsonAsync("adr/orchestrate/run-background", request);
        }
        else
        {
            response = await client.PostAsync("adr/orchestrate/run-background", null);
        }
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BackgroundOrchestrationResponse>();
        return result ?? new BackgroundOrchestrationResponse();
    }

    public async Task<OrchestrationCurrentResponse> GetCurrentOrchestrationAsync()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<OrchestrationCurrentResponse>("adr/orchestrate/current");
        return result ?? new OrchestrationCurrentResponse { IsRunning = false, Message = "Unable to get status" };
    }

    public async Task<AdrOrchestrationStatus?> GetOrchestrationStatusAsync(string requestId)
    {
        var client = CreateClient();
        try
        {
            return await client.GetFromJsonAsync<AdrOrchestrationStatus>($"adr/orchestrate/status/{requestId}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<AdrOrchestrationStatus>> GetOrchestrationHistoryAsync(int? count = 10)
    {
        var client = CreateClient();
        var url = count.HasValue ? $"adr/orchestrate/history?count={count}" : "adr/orchestrate/history";
        var result = await client.GetFromJsonAsync<List<AdrOrchestrationStatus>>(url);
        return result ?? new List<AdrOrchestrationStatus>();
    }

    public async Task<OrchestrationHistoryPagedResponse> GetOrchestrationHistoryPagedAsync(int pageNumber = 1, int pageSize = 20)
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<OrchestrationHistoryPagedResponse>(
            $"adr/orchestrate/history?pageNumber={pageNumber}&pageSize={pageSize}");
        return result ?? new OrchestrationHistoryPagedResponse();
    }

    public async Task<CancelOrchestrationResult> CancelOrchestrationAsync(string requestId)
    {
        var client = CreateClient();
        try
        {
            var response = await client.PostAsync($"adr/orchestrate/{requestId}/cancel", null);
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

    #endregion

    #region Rule Operations

    public async Task<AccountRuleDto?> GetRuleAsync(int ruleId)
    {
        var client = CreateClient();
        try
        {
            return await client.GetFromJsonAsync<AccountRuleDto>($"adr/rules/{ruleId}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<AccountRuleDto>> GetRulesByAccountAsync(int accountId)
    {
        var client = CreateClient();
        try
        {
            var result = await client.GetFromJsonAsync<List<AccountRuleDto>>($"adr/rules/by-account/{accountId}");
            return result ?? new List<AccountRuleDto>();
        }
        catch (HttpRequestException)
        {
            return new List<AccountRuleDto>();
        }
    }

    public async Task<AccountRuleDto> UpdateRuleAsync(int ruleId, UpdateRuleRequest request)
    {
        var client = CreateClient();
        var response = await client.PutAsJsonAsync($"adr/rules/{ruleId}", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AccountRuleDto>();
        return result ?? throw new InvalidOperationException("Failed to update rule");
    }

    public async Task<AccountRuleDto> ClearRuleOverrideAsync(int ruleId)
    {
        var client = CreateClient();
        var response = await client.PostAsync($"adr/rules/{ruleId}/clear-override", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AccountRuleDto>();
        return result ?? throw new InvalidOperationException("Failed to clear rule override");
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
