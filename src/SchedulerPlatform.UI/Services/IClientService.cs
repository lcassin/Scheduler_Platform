using SchedulerPlatform.UI.Models;

namespace SchedulerPlatform.UI.Services;

public interface IClientService
{
    Task<List<Client>> GetClientsAsync();
}
