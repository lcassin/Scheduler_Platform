namespace SchedulerPlatform.UI.Models;

/// <summary>
/// Represents the current test mode status from the ADR configuration.
/// </summary>
public class TestModeStatus
{
    /// <summary>
    /// Whether test mode is currently enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>
    /// Maximum number of ADR requests per orchestration run when test mode is enabled.
    /// </summary>
    public int MaxScrapingJobs { get; set; }
    
    /// <summary>
    /// Maximum number of credential checks per orchestration run when test mode is enabled.
    /// </summary>
    public int MaxCredentialChecks { get; set; }
}
