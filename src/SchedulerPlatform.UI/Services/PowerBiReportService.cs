using System.Net.Http.Json;

namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Service for managing Power BI report links.
/// Uses AuthenticatedHttpClientService to ensure proper authentication for API calls.
/// </summary>
public class PowerBiReportService : IPowerBiReportService
{
    private readonly AuthenticatedHttpClientService _httpClient;
    private readonly ILogger<PowerBiReportService> _logger;

    public PowerBiReportService(
        AuthenticatedHttpClientService httpClient,
        ILogger<PowerBiReportService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<PowerBiReportDto>> GetActiveReportsAsync(string? category = null)
    {
        try
        {
            var url = "api/powerbi-reports";
            if (!string.IsNullOrEmpty(category))
            {
                url += $"?category={Uri.EscapeDataString(category)}";
            }

            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var reports = await response.Content.ReadFromJsonAsync<List<PowerBiReportDto>>();
                return reports ?? new List<PowerBiReportDto>();
            }
            
            _logger.LogWarning("Failed to get active Power BI reports. Status: {StatusCode}", response.StatusCode);
            return new List<PowerBiReportDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active Power BI reports");
            return new List<PowerBiReportDto>();
        }
    }

    /// <inheritdoc />
    public async Task<List<PowerBiReportAdminDto>> GetAllReportsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/powerbi-reports/all");
            
            if (response.IsSuccessStatusCode)
            {
                var reports = await response.Content.ReadFromJsonAsync<List<PowerBiReportAdminDto>>();
                return reports ?? new List<PowerBiReportAdminDto>();
            }
            
            _logger.LogWarning("Failed to get all Power BI reports. Status: {StatusCode}", response.StatusCode);
            return new List<PowerBiReportAdminDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all Power BI reports");
            return new List<PowerBiReportAdminDto>();
        }
    }

    /// <inheritdoc />
    public async Task<PowerBiReportAdminDto?> GetReportAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/powerbi-reports/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PowerBiReportAdminDto>();
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            
            _logger.LogWarning("Failed to get Power BI report {ReportId}. Status: {StatusCode}", id, response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Power BI report {ReportId}", id);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PowerBiReportAdminDto?> CreateReportAsync(CreatePowerBiReportDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/powerbi-reports", request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PowerBiReportAdminDto>();
            }
            
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to create Power BI report. Status: {StatusCode}, Error: {Error}", response.StatusCode, error);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Power BI report");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PowerBiReportAdminDto?> UpdateReportAsync(int id, UpdatePowerBiReportDto request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/powerbi-reports/{id}", request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PowerBiReportAdminDto>();
            }
            
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to update Power BI report {ReportId}. Status: {StatusCode}, Error: {Error}", id, response.StatusCode, error);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Power BI report {ReportId}", id);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteReportAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/powerbi-reports/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            
            _logger.LogWarning("Failed to delete Power BI report {ReportId}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Power BI report {ReportId}", id);
            return false;
        }
    }
}
