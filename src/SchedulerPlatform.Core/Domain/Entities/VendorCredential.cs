using System.Text.Json.Serialization;

namespace SchedulerPlatform.Core.Domain.Entities;

public class VendorCredential : BaseEntity
{
    public int ClientId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string VendorUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public DateTime? LastVerified { get; set; }
    public bool IsValid { get; set; }
    public string? AdditionalData { get; set; }
    
    [JsonIgnore]
    public Client Client { get; set; } = null!;
}
