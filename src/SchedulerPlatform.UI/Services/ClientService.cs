using System.Net.Http.Json;
using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public class ClientService : IClientService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ClientService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("SchedulerAPI");

    public async Task<List<Client>> GetClientsAsync()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<List<Client>>("clients");
        return result ?? new List<Client>();
    }
}
