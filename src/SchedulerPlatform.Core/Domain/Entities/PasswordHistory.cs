namespace SchedulerPlatform.Core.Domain.Entities;

public class PasswordHistory : BaseEntity
{
    public int UserId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    
    public virtual User User { get; set; } = null!;
}
