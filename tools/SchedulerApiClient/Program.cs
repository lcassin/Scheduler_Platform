using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SchedulerApiClient;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();
    
    private const string API_BASE_URL = "https://your-api-url.com";
    private const string CLIENT_ID = "your-client-id";
    private const string CLIENT_SECRET = "your-client-secret";
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("Scheduler Platform API Client");
        Console.WriteLine("==============================\n");
        
        try
        {
            var accessToken = await GetAccessTokenAsync();
            Console.WriteLine($"✓ Successfully authenticated\n");
            
            await CreateScheduleExample(accessToken);
            
            await GetSchedulesExample(accessToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
    
    private static async Task<string> GetAccessTokenAsync()
    {
        Console.WriteLine("Authenticating with API...");
        
        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{API_BASE_URL}/connect/token");
        
        var parameters = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", CLIENT_ID },
            { "client_secret", CLIENT_SECRET },
            { "scope", "scheduler_api" }
        };
        
        tokenRequest.Content = new FormUrlEncodedContent(parameters);
        
        var response = await httpClient.SendAsync(tokenRequest);
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);
        
        if (tokenResponse?.AccessToken == null)
        {
            throw new Exception("Failed to obtain access token");
        }
        
        return tokenResponse.AccessToken;
    }
    
    private static async Task CreateScheduleExample(string accessToken)
    {
        Console.WriteLine("\nCreating a new schedule...");
        
        var schedule = new
        {
            name = "Test Schedule from Console App",
            description = "This schedule was created via the API client",
            scheduleType = "ApiCall",
            cronExpression = "0 0 * * *",
            isActive = true,
            apiCallConfig = new
            {
                url = "https://api.example.com/webhook",
                method = "POST",
                headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                },
                body = "{\"message\": \"Hello from scheduler\"}"
            }
        };
        
        var request = new HttpRequestMessage(HttpMethod.Post, $"{API_BASE_URL}/api/schedules");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(schedule),
            Encoding.UTF8,
            "application/json"
        );
        
        var response = await httpClient.SendAsync(request);
        
        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"✓ Schedule created successfully");
            Console.WriteLine($"Response: {responseContent}");
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"✗ Failed to create schedule: {response.StatusCode}");
            Console.WriteLine($"Error: {errorContent}");
        }
    }
    
    private static async Task GetSchedulesExample(string accessToken)
    {
        Console.WriteLine("\nFetching all schedules...");
        
        var request = new HttpRequestMessage(HttpMethod.Get, $"{API_BASE_URL}/api/schedules");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        
        var response = await httpClient.SendAsync(request);
        
        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var schedules = JsonSerializer.Deserialize<List<ScheduleResponse>>(responseContent);
            
            Console.WriteLine($"✓ Found {schedules?.Count ?? 0} schedule(s)");
            
            if (schedules != null && schedules.Any())
            {
                foreach (var schedule in schedules)
                {
                    Console.WriteLine($"  - {schedule.Name} (ID: {schedule.Id}, Active: {schedule.IsActive})");
                }
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"✗ Failed to fetch schedules: {response.StatusCode}");
            Console.WriteLine($"Error: {errorContent}");
        }
    }
}

public class TokenResponse
{
    public string? AccessToken { get; set; }
    public int ExpiresIn { get; set; }
    public string? TokenType { get; set; }
}

public class ScheduleResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ScheduleType { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? NextRunTime { get; set; }
    public DateTime? LastRunTime { get; set; }
}
