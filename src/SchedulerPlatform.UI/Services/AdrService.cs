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
        string? historicalBillingStatus = null)
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

    #endregion

    #region Job Operations

    public async Task<PagedResult<AdrJob>> GetJobsPagedAsync(
        int pageNumber = 1,
        int pageSize = 20,
        int? adrAccountId = null,
        string? status = null)
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

    public async Task<AdrJobStats> GetJobStatsAsync()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<AdrJobStats>($"adr/jobs/stats");
        return result ?? new AdrJobStats();
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
}
