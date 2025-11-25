using System.Net.Http.Json;
using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public class ScheduleService : IScheduleService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ScheduleService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("SchedulerAPI");

    public async Task<List<Schedule>> GetSchedulesAsync(DateTime? startDate = null, DateTime? endDate = null, int? clientId = null)
    {
        var client = CreateClient();
        var queryParams = new List<string> { "paginated=false" };
        
        if (startDate.HasValue)
            queryParams.Add($"startDate={startDate.Value:yyyy-MM-ddTHH:mm:ss}");
        
        if (endDate.HasValue)
            queryParams.Add($"endDate={endDate.Value:yyyy-MM-ddTHH:mm:ss}");
        
        if (clientId.HasValue)
            queryParams.Add($"clientId={clientId.Value}");
        
        var query = "?" + string.Join("&", queryParams);
        var schedules = await client.GetFromJsonAsync<List<Schedule>>($"schedules{query}");
        return schedules ?? new List<Schedule>();
    }

    public async Task<PagedResult<Schedule>> GetSchedulesPagedAsync(
        int pageNumber = 1,
        int pageSize = 20,
        int? clientId = null,
        string? searchTerm = null,
        bool? isEnabled = null)
    {
        var client = CreateClient();
        var queryParams = new List<string> { "paginated=true" };
        
        queryParams.Add($"pageNumber={pageNumber}");
        queryParams.Add($"pageSize={pageSize}");
        
        if (clientId.HasValue)
            queryParams.Add($"clientId={clientId.Value}");
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
            queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm)}");
        
        if (isEnabled.HasValue)
            queryParams.Add($"isEnabled={isEnabled.Value}");
        
        var query = "?" + string.Join("&", queryParams);
        var result = await client.GetFromJsonAsync<PagedResult<Schedule>>($"schedules{query}");
        
        return result ?? new PagedResult<Schedule>
        {
            Items = new List<Schedule>(),
            TotalCount = 0,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public Task<Schedule?> GetScheduleAsync(int id)
    {
        var client = CreateClient();
        return client.GetFromJsonAsync<Schedule>($"schedules/{id}");
    }

    public async Task<Schedule> CreateScheduleAsync(Schedule schedule)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("schedules", schedule);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Schedule>())!;
    }

    public async Task<Schedule> UpdateScheduleAsync(int id, Schedule schedule)
    {
        var client = CreateClient();
        var response = await client.PutAsJsonAsync($"schedules/{id}", schedule);
        response.EnsureSuccessStatusCode();
        
        if (response.Content.Headers.ContentLength > 0)
        {
            return (await response.Content.ReadFromJsonAsync<Schedule>())!;
        }
        
        return schedule;
    }

    public async Task DeleteScheduleAsync(int id)
    {
        var client = CreateClient();
        var response = await client.DeleteAsync($"schedules/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task TriggerScheduleAsync(int id)
    {
        var client = CreateClient();
        var response = await client.PostAsync($"schedules/{id}/trigger", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task PauseScheduleAsync(int id)
    {
        var client = CreateClient();
        var response = await client.PostAsync($"schedules/{id}/pause", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResumeScheduleAsync(int id)
    {
        var client = CreateClient();
        var response = await client.PostAsync($"schedules/{id}/resume", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]> DownloadSchedulesExportAsync(int? clientId, string? searchTerm, DateTime? startDate, DateTime? endDate, string format)
    {
        var client = CreateClient();
        var queryParams = new List<string>();
        
        if (clientId.HasValue)
            queryParams.Add($"clientId={clientId.Value}");
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
            queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm)}");
        
        if (startDate.HasValue)
        {
            var startOfDay = startDate.Value.Date;
            queryParams.Add($"startDate={startOfDay:yyyy-MM-ddTHH:mm:ss}");
        }
        
        if (endDate.HasValue)
        {
            var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
            queryParams.Add($"endDate={endOfDay:yyyy-MM-ddTHH:mm:ss}");
        }
        
        queryParams.Add($"format={format}");
        
        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        var response = await client.GetAsync($"schedules/export{query}");
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(string connectionString)
    {
        try
        {
            var client = CreateClient();
            var request = new { ConnectionString = connectionString };
            var response = await client.PostAsJsonAsync("schedules/test-connection", request);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<TestConnectionResponseDto>();
            if (result == null)
            {
                return (false, "No response from server.");
            }
            
            return (result.Success, result.Message);
        }
        catch (Exception ex)
        {
            return (false, $"Error testing connection: {ex.Message}");
        }
    }

    private class TestConnectionResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? ErrorNumber { get; set; }
    }

    public async Task<MissedSchedulesResult> GetMissedSchedulesAsync(int? windowDays = 1, int pageNumber = 1, int pageSize = 100)
    {
        var client = CreateClient();
        var queryParams = new List<string>();
        
        if (windowDays.HasValue)
            queryParams.Add($"windowDays={windowDays.Value}");
        
        queryParams.Add($"pageNumber={pageNumber}");
        queryParams.Add($"pageSize={pageSize}");
        
        var query = "?" + string.Join("&", queryParams);
        var result = await client.GetFromJsonAsync<MissedSchedulesResult>($"schedules/missed{query}");
        
        return result ?? new MissedSchedulesResult
        {
            Items = new List<MissedScheduleItem>(),
            TotalCount = 0,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<BulkTriggerResult> BulkTriggerMissedSchedulesAsync(List<int> scheduleIds, int? delayBetweenTriggersMs = 200)
    {
        var client = CreateClient();
        var request = new { ScheduleIds = scheduleIds, DelayBetweenTriggersMs = delayBetweenTriggersMs };
        var response = await client.PostAsJsonAsync("schedules/missed/bulk-trigger", request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<BulkTriggerResult>();
        return result ?? new BulkTriggerResult();
    }
}
