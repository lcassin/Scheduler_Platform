using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SchedulerPlatform.Core.Domain.Entities;
using SchedulerPlatform.Core.Domain.Interfaces;
using System.Text.Json;

namespace SchedulerPlatform.Infrastructure.Interceptors;

public class AuditLogInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentActor _currentActor;

    public AuditLogInterceptor(ICurrentActor currentActor)
    {
        _currentActor = currentActor;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CreateAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CreateAuditLogs(eventData.Context);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void CreateAuditLogs(DbContext? context)
    {
        if (context == null)
            return;

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added ||
                       e.State == EntityState.Modified ||
                       e.State == EntityState.Deleted)
            .Where(e => e.Entity is not AuditLog) // Don't audit the audit log itself
            .Where(e => ShouldAudit(e.Entity))
            .ToList();

        var actorName = _currentActor.GetActorName();
        var clientId = _currentActor.GetClientId();
        var isManual = _currentActor.IsManualAction();
        var ipAddress = _currentActor.GetIpAddress();
        var userAgent = _currentActor.GetUserAgent();

        foreach (var entry in entries)
        {
            var auditLog = new AuditLog
            {
                EventType = isManual ? "Manual" : "Automated",
                EntityType = entry.Entity.GetType().Name,
                EntityId = GetEntityId(entry.Entity),
                Action = entry.State.ToString(),
                UserName = actorName,
                ClientId = clientId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                TimestampDateTime = DateTime.UtcNow,
                OldValues = entry.State == EntityState.Modified || entry.State == EntityState.Deleted
                    ? SerializeOldValues(entry)
                    : null,
                NewValues = entry.State == EntityState.Added || entry.State == EntityState.Modified
                    ? SerializeNewValues(entry)
                    : null,
                AdditionalData = null
            };

            context.Set<AuditLog>().Add(auditLog);
        }
    }

    private bool ShouldAudit(object entity)
    {
        return entity is Schedule ||
               entity is JobParameter ||
               entity is NotificationSetting ||
               entity is User ||
               entity is UserPermission ||
               entity is Client;
    }

    private int? GetEntityId(object entity)
    {
        if (entity is BaseEntity baseEntity)
        {
            return baseEntity.Id;
        }
        return null;
    }

    private string? SerializeOldValues(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var oldValues = new Dictionary<string, object?>();
        
        foreach (var property in entry.Properties)
        {
            if (ShouldSkipProperty(property.Metadata.Name))
                continue;

            var originalValue = property.OriginalValue;
            
            if (IsSensitiveProperty(property.Metadata.Name))
            {
                originalValue = "***MASKED***";
            }
            
            oldValues[property.Metadata.Name] = originalValue;
        }

        return oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null;
    }

    private string? SerializeNewValues(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var newValues = new Dictionary<string, object?>();
        
        foreach (var property in entry.Properties)
        {
            if (ShouldSkipProperty(property.Metadata.Name))
                continue;

            var currentValue = property.CurrentValue;
            
            if (IsSensitiveProperty(property.Metadata.Name))
            {
                currentValue = "***MASKED***";
            }
            
            newValues[property.Metadata.Name] = currentValue;
        }

        return newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null;
    }

    private bool ShouldSkipProperty(string propertyName)
    {
        var skipProperties = new[]
        {
            "ModifiedDateTime",
            "LastRunDateTime",
            "NextRunDateTime",
            "CreatedDateTime", // Only skip for Modified, not Added
            "Id" // ID is captured separately in EntityId
        };

        return skipProperties.Contains(propertyName);
    }

    private bool IsSensitiveProperty(string propertyName)
    {
        var sensitiveProperties = new[]
        {
            "PasswordHash",
            "EncryptedPassword",
            "ConnectionString",
            "SourceConnectionString",
            "ApiKey",
            "Secret",
            "Token"
        };

        return sensitiveProperties.Any(s => propertyName.Contains(s, StringComparison.OrdinalIgnoreCase));
    }
}
