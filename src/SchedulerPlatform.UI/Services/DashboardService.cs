using System.Net.Http.Json;
using SchedulerPlatform.UI.Models;
using SchedulerPlatform.Core.Domain.Enums;

namespace SchedulerPlatform.UI.Services;

public class DashboardService : IDashboardService
{
    private readonly AuthenticatedHttpClientService _httpClient;

    public DashboardService(AuthenticatedHttpClientService httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DashboardOverview?> GetOverviewAsync(int? clientId = null, int hours = 24, string? timezone = null)
    {
        var queryParams = new List<string> { $"hours={hours}" };
    
        if (clientId.HasValue)
        {
            queryParams.Add($"clientId={clientId.Value}");
        }
    
        if (!string.IsNullOrEmpty(timezone))
        {
            queryParams.Add($"timezone={Uri.EscapeDataString(timezone)}");
        }
    
        var query = "?" + string.Join("&", queryParams);
        var response = await _httpClient.GetAsync($"dashboard/overview{query}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DashboardOverview>();
    }

    public async Task<List<StatusBreakdownItem>> GetStatusBreakdownAsync(int hours = 24, int? clientId = null)
    {
        var queryParams = new List<string> { $"hours={hours}" };
        
        if (clientId.HasValue)
        {
            queryParams.Add($"clientId={clientId.Value}");
        }
        
        var query = "?" + string.Join("&", queryParams);
        var response = await _httpClient.GetAsync($"dashboard/status-breakdown{query}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<StatusBreakdownItem>>();
        return result ?? new List<StatusBreakdownItem>();
    }

    public async Task<List<ExecutionTrendItem>> GetExecutionTrendsAsync(int hours = 24, int? clientId = null, List<JobStatus>? statuses = null)
    {
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
        var response = await _httpClient.GetAsync($"dashboard/execution-trends{query}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<ExecutionTrendItem>>();
        return result ?? new List<ExecutionTrendItem>();
    }

    public async Task<List<TopLongestExecutionItem>> GetTopLongestExecutionsAsync(int limit = 10, int hours = 24, int? clientId = null, List<JobStatus>? statuses = null)
    {
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
        var response = await _httpClient.GetAsync($"dashboard/top-longest{query}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<TopLongestExecutionItem>>();
        return result ?? new List<TopLongestExecutionItem>();
    }

    public async Task<List<InvalidScheduleInfo>> GetInvalidSchedulesAsync(int? clientId = null)
    {
        var queryParams = new List<string>();
        if (clientId.HasValue)
        {
            queryParams.Add($"clientId={clientId.Value}");
        }

        var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
        var response = await _httpClient.GetAsync($"dashboard/invalid-schedules{queryString}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<InvalidScheduleInfo>>();
        
        return result ?? new List<InvalidScheduleInfo>();
    }
}
