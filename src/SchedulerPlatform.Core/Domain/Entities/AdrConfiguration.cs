using System.Text.Json.Serialization;

namespace SchedulerPlatform.Core.Domain.Entities;

/// <summary>
/// Stores global configuration settings for the ADR orchestration process.
/// This is a single-row table that replaces hardcoded values in appsettings.json.
/// Only Admin and Super Admin users can modify these settings.
/// </summary>
public class AdrConfiguration : BaseEntity
{
    /// <summary>
    /// Number of days before NextRunDateTime to start credential verification.
    /// Default: 7 days
    /// </summary>
    public int CredentialCheckLeadDays { get; set; } = 7;
    
    /// <summary>
    /// DEPRECATED: This field is no longer used by the orchestration logic.
    /// Retry behavior is now controlled by the date range (NextRunDate through NextRangeEndDate)
    /// and MaxRetries. Kept for backward compatibility - can be removed in a future cleanup.
    /// </summary>
    [Obsolete("This field is deprecated and not used by orchestration. Retry behavior is controlled by date range and MaxRetries.")]
    public int ScrapeRetryDays { get; set; } = 5;
    
    /// <summary>
    /// Maximum number of scrape retry attempts before marking job as failed.
    /// Default: 5 retries
    /// </summary>
    public int MaxRetries { get; set; } = 5;
    
    /// <summary>
    /// Number of days after billing window ends to perform final status check.
    /// Default: 5 days
    /// </summary>
    public int FinalStatusCheckDelayDays { get; set; } = 5;
    
    /// <summary>
    /// Number of days to wait between status checks.
    /// Default: 1 day (check status the day after scraping)
    /// </summary>
    public int DailyStatusCheckDelayDays { get; set; } = 1;
    
    /// <summary>
    /// Maximum number of parallel API requests during orchestration.
    /// Default: 8 parallel requests
    /// </summary>
    public int MaxParallelRequests { get; set; } = 8;
    
    /// <summary>
    /// Batch size for processing jobs during orchestration.
    /// Default: 1000 jobs per batch
    /// </summary>
    public int BatchSize { get; set; } = 1000;
    
    /// <summary>
    /// Number of days before expected invoice date to start looking (window start offset).
    /// Used for single-date invoice downloads.
    /// Default: 5 days before
    /// </summary>
    public int DefaultWindowDaysBefore { get; set; } = 5;
    
    /// <summary>
    /// Number of days after expected invoice date to keep looking (window end offset).
    /// Used for single-date invoice downloads.
    /// Default: 5 days after
    /// </summary>
    public int DefaultWindowDaysAfter { get; set; } = 5;
    
    /// <summary>
    /// Whether to automatically create test login rules x days before invoice download.
    /// Default: true
    /// </summary>
    public bool AutoCreateTestLoginRules { get; set; } = true;
    
    /// <summary>
    /// Whether to automatically create missing invoice alerts when scraping fails.
    /// Default: true
    /// </summary>
    public bool AutoCreateMissingInvoiceAlerts { get; set; } = true;
    
    /// <summary>
    /// Email address to send missing invoice alerts to.
    /// </summary>
    public string? MissingInvoiceAlertEmail { get; set; }
    
    /// <summary>
    /// Whether the orchestration process is enabled.
    /// Can be used to temporarily disable orchestration without code changes.
    /// Default: true
    /// </summary>
    public bool IsOrchestrationEnabled { get; set; } = true;
    
    /// <summary>
    /// Optional notes about configuration changes.
    /// </summary>
    public string? Notes { get; set; }
    
    // Data Retention Settings
    
    /// <summary>
    /// Number of months to keep AdrJob records before archiving.
    /// Jobs older than this will be moved to the archive table.
    /// Default: 12 months
    /// </summary>
    public int JobRetentionMonths { get; set; } = 12;
    
    /// <summary>
    /// Number of months to keep AdrJobExecution records before archiving.
    /// Executions older than this will be moved to the archive table.
    /// Default: 12 months
    /// </summary>
    public int JobExecutionRetentionMonths { get; set; } = 12;
    
    /// <summary>
    /// Number of days to keep AuditLog records before archiving.
    /// Audit logs older than this will be moved to the archive table.
    /// Default: 90 days (per BRD requirement)
    /// </summary>
    public int AuditLogRetentionDays { get; set; } = 90;
    
    /// <summary>
    /// Whether the data archival process is enabled.
    /// Default: true
    /// </summary>
    public bool IsArchivalEnabled { get; set; } = true;
    
    /// <summary>
    /// Batch size for archival operations.
    /// Default: 5000 records per batch
    /// </summary>
    public int ArchivalBatchSize { get; set; } = 5000;
    
    /// <summary>
    /// Number of years to keep archived records before permanent deletion.
    /// Archives older than this will be permanently deleted.
    /// Default: 7 years (per regulatory requirements)
    /// </summary>
    public int ArchiveRetentionYears { get; set; } = 7;
    
    /// <summary>
    /// Number of days to keep log files before deletion.
    /// Log files older than this will be deleted during maintenance.
    /// Default: 30 days
    /// Note: Only applies to file-based logs in non-Azure environments.
    /// </summary>
    public int LogRetentionDays { get; set; } = 30;
}
