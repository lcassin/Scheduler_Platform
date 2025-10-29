using System.Net.Http.Json;
using SchedulerPlatform.Core.Domain.Enums;
using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public class JobExecutionService : IJobExecutionService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public JobExecutionService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("SchedulerAPI");

    public async Task<PagedResult<JobExecution>> GetJobExecutionsAsync(
        int? scheduleId = null, 
        JobStatus? status = null, 
        int pageNumber = 1, 
        int pageSize = 20)
    {
        var client = CreateClient();
        var queryParams = new List<string>();
        
        if (scheduleId.HasValue)
            queryParams.Add($"scheduleId={scheduleId.Value}");
            
        if (status.HasValue)
            queryParams.Add($"status={status.Value}");
        
        var query = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
        var allExecutions = await client.GetFromJsonAsync<List<JobExecution>>($"jobexecutions{query}");
        
        if (allExecutions == null || !allExecutions.Any())
        {
            return new PagedResult<JobExecution>
            {
                Items = new List<JobExecution>(),
                TotalCount = 0,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
        
        var totalCount = allExecutions.Count;
        var items = allExecutions
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        
        return new PagedResult<JobExecution>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<JobExecution>> GetJobExecutionsPagedAsync(
        int? scheduleId = null,
        JobStatus? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int pageNumber = 1,
        int pageSize = 100)
    {
        var client = _httpClientFactory.CreateClient("SchedulerAPI");
        var queryParams = new List<string>();
        
        if (scheduleId.HasValue)
        {
            queryParams.Add($"scheduleId={scheduleId.Value}");
        }
        
        if (status.HasValue)
        {
            queryParams.Add($"status={status.Value}");
        }
        
        if (startDate.HasValue)
        {
            queryParams.Add($"startDate={startDate.Value:yyyy-MM-ddTHH:mm:ss}");
        }
        
        if (endDate.HasValue)
        {
            queryParams.Add($"endDate={endDate.Value:yyyy-MM-ddTHH:mm:ss}");
        }
        
        var url = "jobexecutions";
        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }
        
        var allExecutions = await client.GetFromJsonAsync<List<JobExecution>>(url) ?? new List<JobExecution>();
        
        var pagedItems = allExecutions
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        
        return new PagedResult<JobExecution>
        {
            Items = pagedItems,
            TotalCount = allExecutions.Count,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task RetryJobExecutionAsync(int id)
    {
        var client = CreateClient();
        var response = await client.PostAsync($"jobexecutions/{id}/retry", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]> DownloadJobExecutionsExportAsync(int? scheduleId, string? status, DateTime? startDate, DateTime? endDate, string format)
    {
        var client = CreateClient();
        var queryParams = new List<string>();
        
        if (scheduleId.HasValue)
            queryParams.Add($"scheduleId={scheduleId.Value}");
        
        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            queryParams.Add($"status={status}");
        
        if (startDate.HasValue)
            queryParams.Add($"startDate={startDate.Value:yyyy-MM-ddTHH:mm:ss}");
        
        if (endDate.HasValue)
            queryParams.Add($"endDate={endDate.Value:yyyy-MM-ddTHH:mm:ss}");
        
        queryParams.Add($"format={format}");
        
        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        var response = await client.GetAsync($"jobexecutions/export{query}");
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<JobExecution?> GetJobExecutionAsync(int id)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"jobexecutions/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobExecution>();
    }

    public async Task CancelJobExecutionAsync(int executionId)
    {
        var client = CreateClient();
        var response = await client.PostAsync($"jobexecutions/{executionId}/cancel", null);
        response.EnsureSuccessStatusCode();
    }
}
