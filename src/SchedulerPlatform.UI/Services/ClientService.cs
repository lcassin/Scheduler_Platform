using System.Net.Http.Json;
using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public class ClientService : IClientService
{
    private readonly AuthenticatedHttpClientService _httpClient;

    public ClientService(AuthenticatedHttpClientService httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Client>> GetClientsAsync()
    {
        var response = await _httpClient.GetAsync("clients");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<Client>>();
        return result ?? new List<Client>();
    }
}
