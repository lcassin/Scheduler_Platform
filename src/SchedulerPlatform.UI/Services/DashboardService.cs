using System.Net.Http.Json;
using SchedulerPlatform.UI.Models;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.UI.Services;

public class DashboardService : IDashboardService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DashboardService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("SchedulerAPI");

    public async Task<DashboardOverview?> GetOverviewAsync(int? clientId = null, int hours = 24)
    {
        var client = CreateClient();
        var queryParams = new List<string> { $"hours={hours}" };
        
        if (clientId.HasValue)
        {
            queryParams.Add($"clientId={clientId.Value}");
        }
        
        var query = "?" + string.Join("&", queryParams);
        var result = await client.GetFromJsonAsync<DashboardOverview>($"dashboard/overview{query}");
        return result;
    }

    public async Task<List<StatusBreakdownItem>> GetStatusBreakdownAsync(int hours = 24, int? clientId = null)
    {
        var client = CreateClient();
        var queryParams = new List<string> { $"hours={hours}" };
        
        if (clientId.HasValue)
        {
            queryParams.Add($"clientId={clientId.Value}");
        }
        
        var query = "?" + string.Join("&", queryParams);
        var result = await client.GetFromJsonAsync<List<StatusBreakdownItem>>($"dashboard/status-breakdown{query}");
        return result ?? new List<StatusBreakdownItem>();
    }

    public async Task<List<ExecutionTrendItem>> GetExecutionTrendsAsync(int hours = 24, int? clientId = null, List<JobStatus>? statuses = null)
    {
        var client = CreateClient();
        var queryParams = new List<string> { $"hours={hours}" };
        
        if (clientId.HasValue)
        {
            queryParams.Add($"clientId={clientId.Value}");
        }
        
        if (statuses != null && statuses.Any())
        {
            foreach (var status in statuses)
            {
                queryParams.Add($"statuses={status}");
            }
        }
        
        var query = "?" + string.Join("&", queryParams);
        var result = await client.GetFromJsonAsync<List<ExecutionTrendItem>>($"dashboard/execution-trends{query}");
        return result ?? new List<ExecutionTrendItem>();
    }

    public async Task<List<TopLongestExecutionItem>> GetTopLongestExecutionsAsync(int limit = 10, int hours = 24, int? clientId = null, List<JobStatus>? statuses = null)
    {
        var client = CreateClient();
        var queryParams = new List<string> { $"limit={limit}", $"hours={hours}" };
        
        if (clientId.HasValue)
        {
            queryParams.Add($"clientId={clientId.Value}");
        }
        
        if (statuses != null && statuses.Any())
        {
            foreach (var status in statuses)
            {
                queryParams.Add($"statuses={status}");
            }
        }
        
        var query = "?" + string.Join("&", queryParams);
        var result = await client.GetFromJsonAsync<List<TopLongestExecutionItem>>($"dashboard/top-longest{query}");
        return result ?? new List<TopLongestExecutionItem>();
    }

    public async Task<List<InvalidScheduleInfo>> GetInvalidSchedulesAsync(int? clientId = null)
    {
        var client = CreateClient();
        var queryParams = new List<string>();
        if (clientId.HasValue)
        {
            queryParams.Add($"clientId={clientId.Value}");
        }

        var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
        var response = await client.GetFromJsonAsync<List<InvalidScheduleInfo>>($"dashboard/invalid-schedules{queryString}");
        
        return response ?? new List<InvalidScheduleInfo>();
    }
}
