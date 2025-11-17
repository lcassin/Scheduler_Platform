using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SchedulerPlatform.ScheduleSync.Models;

namespace SchedulerPlatform.ScheduleSync.Services;

public class AccountsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly int _batchSize;
    private readonly int _delayBetweenRequestsMs;

    public AccountsApiClient(HttpClient httpClient, string apiBaseUrl, string apiKey, int batchSize = 2500, int delayBetweenRequestsMs = 100)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(apiBaseUrl);
        _apiKey = apiKey;
        _batchSize = batchSize;
        _delayBetweenRequestsMs = delayBetweenRequestsMs;
    }

    public async Task<AccountApiResponse?> GetAccountsBatchAsync(int pageNumber, bool includeOnlyTandemAccounts = false, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            batchSize = _batchSize,
            pageNumber = pageNumber,
            includeOnlyTandemAccounts = includeOnlyTandemAccounts
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/Account/GetAccountsByBatchForApi")
        {
            Headers =
            {
                { "accept", "text/plain" },
                { "ApiKey", _apiKey }
            },
            Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
        };

        var maxRetries = 3;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<AccountApiResponse>(cancellationToken: cancellationToken);
                    
                    if (_delayBetweenRequestsMs > 0)
                    {
                        await Task.Delay(_delayBetweenRequestsMs, cancellationToken);
                    }
                    
                    return result;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta?.TotalMilliseconds ?? (Math.Pow(2, retryCount) * 1000);
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Rate limited. Waiting {retryAfter}ms before retry {retryCount + 1}/{maxRetries}");
                    await Task.Delay((int)retryAfter, cancellationToken);
                    retryCount++;
                    continue;
                }

                if ((int)response.StatusCode >= 500)
                {
                    var delay = (int)(Math.Pow(2, retryCount) * 1000);
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Server error {response.StatusCode}. Waiting {delay}ms before retry {retryCount + 1}/{maxRetries}");
                    await Task.Delay(delay, cancellationToken);
                    retryCount++;
                    continue;
                }

                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] API request failed with status {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
                return null;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] HTTP request exception: {ex.Message}");
                if (retryCount < maxRetries - 1)
                {
                    var delay = (int)(Math.Pow(2, retryCount) * 1000);
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Waiting {delay}ms before retry {retryCount + 1}/{maxRetries}");
                    await Task.Delay(delay, cancellationToken);
                    retryCount++;
                    continue;
                }
                throw;
            }
        }

        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Max retries exceeded for page {pageNumber}");
        return null;
    }

    public async IAsyncEnumerable<AccountApiResponse> GetAllAccountsAsync(bool includeOnlyTandemAccounts = false)
    {
        var pageNumber = 1;
        AccountApiResponse? firstPage = null;

        while (true)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Fetching page {pageNumber}...");
            
            var response = await GetAccountsBatchAsync(pageNumber, includeOnlyTandemAccounts);
            
            if (response == null)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Failed to fetch page {pageNumber}. Stopping pagination.");
                yield break;
            }

            if (firstPage == null)
            {
                firstPage = response;
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Total records: {response.Total}, Total pages: {response.PageTotal}, Batch size: {response.Batch}");
            }

            if (response.Data == null || response.Data.Count == 0)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] No data returned for page {pageNumber}. Stopping pagination.");
                yield break;
            }

            yield return response;

            pageNumber++;

            if (pageNumber >= response.PageTotal)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Reached last page {pageNumber - 1}. Total pages: {response.PageTotal}");
                yield break;
            }
        }
    }
}
