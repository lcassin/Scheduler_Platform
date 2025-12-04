namespace SchedulerPlatform.Core.Domain.Entities;

public abstract class BaseEntity
{
    public virtual int Id { get; set; }
    public DateTime CreatedDateTime { get; set; }
    public DateTime ModifiedDateTime { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
