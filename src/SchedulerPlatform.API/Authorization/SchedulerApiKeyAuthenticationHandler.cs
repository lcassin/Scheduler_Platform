using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace SchedulerPlatform.API.Authorization;

public class SchedulerApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "SchedulerApiKey";
    public const string HeaderName = "X-Scheduler-Api-Key";
}

public class SchedulerApiKeyAuthenticationHandler : AuthenticationHandler<SchedulerApiKeyAuthenticationOptions>
{
    private readonly IConfiguration _configuration;

    public SchedulerApiKeyAuthenticationHandler(
        IOptionsMonitor<SchedulerApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SchedulerApiKeyAuthenticationOptions.HeaderName, out var apiKeyHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var configuredApiKey = _configuration["Scheduler:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            Logger.LogWarning("Scheduler:InternalApiKey is not configured. API key authentication will fail.");
            return Task.FromResult(AuthenticateResult.Fail("API key authentication is not configured"));
        }

        if (!string.Equals(providedApiKey, configuredApiKey, StringComparison.Ordinal))
        {
            Logger.LogWarning("Invalid API key provided for scheduler authentication");
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "SchedulerService"),
            new Claim(ClaimTypes.Role, "SchedulerService"),
            new Claim("sub", "scheduler-service"),
            new Claim("name", "Internal Scheduler Service")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogDebug("Scheduler API key authentication successful");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
