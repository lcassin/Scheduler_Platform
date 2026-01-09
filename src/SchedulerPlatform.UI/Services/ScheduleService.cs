using System.Net.Http.Json;
using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public class ScheduleService : IScheduleService
{
    private readonly AuthenticatedHttpClientService _httpClient;

    public ScheduleService(AuthenticatedHttpClientService httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Schedule>> GetSchedulesAsync(DateTime? startDate = null, DateTime? endDate = null, int? clientId = null)
    {
        var queryParams = new List<string> { "paginated=false" };
        
        if (startDate.HasValue)
            queryParams.Add($"startDate={startDate.Value:yyyy-MM-ddTHH:mm:ss}");
        
        if (endDate.HasValue)
            queryParams.Add($"endDate={endDate.Value:yyyy-MM-ddTHH:mm:ss}");
        
        if (clientId.HasValue)
            queryParams.Add($"clientId={clientId.Value}");
        
        var query = "?" + string.Join("&", queryParams);
        var response = await _httpClient.GetAsync($"schedules{query}");
        response.EnsureSuccessStatusCode();
        var schedules = await response.Content.ReadFromJsonAsync<List<Schedule>>();
        return schedules ?? new List<Schedule>();
    }

    public async Task<PagedResult<Schedule>> GetSchedulesPagedAsync(
        int pageNumber = 1,
        int pageSize = 20,
        int? clientId = null,
        string? searchTerm = null,
        bool? isEnabled = null)
    {
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
        var response = await _httpClient.GetAsync($"schedules{query}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<Schedule>>();
        
        return result ?? new PagedResult<Schedule>
        {
            Items = new List<Schedule>(),
            TotalCount = 0,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Schedule?> GetScheduleAsync(int id)
    {
        var response = await _httpClient.GetAsync($"schedules/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Schedule>();
    }

    public async Task<Schedule> CreateScheduleAsync(Schedule schedule)
    {
        var response = await _httpClient.PostAsJsonAsync("schedules", schedule);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Schedule>())!;
    }

    public async Task<Schedule> UpdateScheduleAsync(int id, Schedule schedule)
    {
        var response = await _httpClient.PutAsJsonAsync($"schedules/{id}", schedule);
        response.EnsureSuccessStatusCode();
        
        if (response.Content.Headers.ContentLength > 0)
        {
            return (await response.Content.ReadFromJsonAsync<Schedule>())!;
        }
        
        return schedule;
    }

    public async Task DeleteScheduleAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"schedules/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task TriggerScheduleAsync(int id)
    {
        var response = await _httpClient.PostAsJsonAsync($"schedules/{id}/trigger", new { });
        response.EnsureSuccessStatusCode();
    }

    public async Task PauseScheduleAsync(int id)
    {
        var response = await _httpClient.PostAsJsonAsync($"schedules/{id}/pause", new { });
        response.EnsureSuccessStatusCode();
    }

    public async Task ResumeScheduleAsync(int id)
    {
        var response = await _httpClient.PostAsJsonAsync($"schedules/{id}/resume", new { });
        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]> DownloadSchedulesExportAsync(int? clientId, string? searchTerm, DateTime? startDate, DateTime? endDate, string format)
    {
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
        var response = await _httpClient.GetAsync($"schedules/export{query}");
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(string connectionString)
    {
        try
        {
            var request = new { ConnectionString = connectionString };
            var response = await _httpClient.PostAsJsonAsync("schedules/test-connection", request);
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
        var queryParams = new List<string>();
        
        if (windowDays.HasValue)
            queryParams.Add($"windowDays={windowDays.Value}");
        
        queryParams.Add($"pageNumber={pageNumber}");
        queryParams.Add($"pageSize={pageSize}");
        
        var query = "?" + string.Join("&", queryParams);
        var response = await _httpClient.GetAsync($"schedules/missed{query}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MissedSchedulesResult>();
        
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
        var request = new { ScheduleIds = scheduleIds, DelayBetweenTriggersMs = delayBetweenTriggersMs };
        var response = await _httpClient.PostAsJsonAsync("schedules/missed/bulk-trigger", request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<BulkTriggerResult>();
        return result ?? new BulkTriggerResult();
    }

    public async Task<int> GetMissedSchedulesCountAsync(int? windowDays = 1)
    {
        var query = windowDays.HasValue ? $"?windowDays={windowDays.Value}" : "";
        var response = await _httpClient.GetAsync($"schedules/missed/count{query}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MissedSchedulesCountResult>();
        return result?.Count ?? 0;
    }

    private class MissedSchedulesCountResult
    {
        public int Count { get; set; }
        public int WindowDays { get; set; }
    }
}
