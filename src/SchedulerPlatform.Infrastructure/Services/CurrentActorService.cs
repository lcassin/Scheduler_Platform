using Microsoft.AspNetCore.Http;
using SchedulerPlatform.Core.Domain.Interfaces;
using System.Security.Claims;

namespace SchedulerPlatform.Infrastructure.Services;

public class CurrentActorService : ICurrentActor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string ServiceAccountClientId = "svc-adrscheduler";

    public CurrentActorService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetActorName()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(email))
                return email;
                
            var name = httpContext.User.FindFirst(ClaimTypes.Name)?.Value;
            if (!string.IsNullOrEmpty(name))
                return name;
                
            var clientId = httpContext.User.FindFirst("client_id")?.Value;
            if (!string.IsNullOrEmpty(clientId))
                return clientId;
                
            return httpContext.User.Identity.Name ?? "Unknown";
        }
        
        return "System";
    }

    public int? GetClientId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var clientIdClaim = httpContext.User.FindFirst("client_id")?.Value;
            if (int.TryParse(clientIdClaim, out var clientId))
            {
                return clientId;
            }
        }
        
        return null;
    }

    public bool IsManualAction()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var identity = user?.Identity;
        
        if (identity?.IsAuthenticated != true)
        {
            return false; // Not authenticated = automated
        }
        
        // Check for ADR service account by client_id
        var clientId = user!.FindFirst("client_id")?.Value;
        if (clientId == ServiceAccountClientId)
        {
            return false; // ADR service account = automated
        }
        
        // Check for SchedulerService API key authentication
        var authType = identity.AuthenticationType;
        if (string.Equals(authType, "SchedulerApiKey", StringComparison.OrdinalIgnoreCase))
        {
            return false; // Internal scheduler service = automated
        }
        
        // Normal users with email = manual
        var email = user.FindFirst(ClaimTypes.Email)?.Value;
        if (!string.IsNullOrEmpty(email))
        {
            return true; // User with email = manual
        }
        
        // Fallback: authenticated but no email and not a known service
        // Treat as automated rather than manual (safer default for unknown service accounts)
        return false;
    }

    public string? GetIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext == null)
            return null;
            
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }
        
        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    public string? GetUserAgent()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext == null)
            return null;
            
        return httpContext.Request.Headers["User-Agent"].FirstOrDefault();
    }
}
