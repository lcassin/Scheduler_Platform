namespace SchedulerPlatform.Core.Domain.Interfaces;

public interface ICurrentActor
{
    string GetActorName();
    
    int? GetClientId();
    
    bool IsManualAction();
    
    string? GetIpAddress();
    
    string? GetUserAgent();
}
