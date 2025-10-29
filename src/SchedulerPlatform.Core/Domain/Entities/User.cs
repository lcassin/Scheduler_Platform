using System.Text.Json.Serialization;

namespace SchedulerPlatform.Core.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int ClientId { get; set; }
    public bool IsActive { get; set; }
    public string? ExternalUserId { get; set; }
    
    [JsonIgnore]
    public Client Client { get; set; } = null!;
    [JsonIgnore]
    public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
}
