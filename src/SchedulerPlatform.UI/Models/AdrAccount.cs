namespace SchedulerPlatform.UI.Models;

public class AdrAccount
{
    public int Id { get; set; }
    public long VMAccountId { get; set; }
    public string VMAccountNumber { get; set; } = string.Empty;
    public string? InterfaceAccountId { get; set; }
    public int? ClientId { get; set; }
    public string? ClientName { get; set; }
    public int CredentialId { get; set; }
    public string? VendorCode { get; set; }
    public string? PeriodType { get; set; }
    public int? PeriodDays { get; set; }
    public double? MedianDays { get; set; }
    public int? InvoiceCount { get; set; }
    public DateTime? LastInvoiceDateTime { get; set; }
    public DateTime? ExpectedNextDateTime { get; set; }
    public DateTime? ExpectedRangeStartDateTime { get; set; }
    public DateTime? ExpectedRangeEndDateTime { get; set; }
    public DateTime? NextRunDateTime { get; set; }
    public DateTime? NextRangeStartDateTime { get; set; }
    public DateTime? NextRangeEndDateTime { get; set; }
    public int? DaysUntilNextRun { get; set; }
    public string? NextRunStatus { get; set; }
    public string? HistoricalBillingStatus { get; set; }
    public DateTime? LastSyncedDateTime { get; set; }
    public bool IsManuallyOverridden { get; set; }
    public string? OverriddenBy { get; set; }
    public DateTime? OverriddenDateTime { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDateTime { get; set; }
    public DateTime? ModifiedDateTime { get; set; }
    
    // Job status fields - populated from current billing period's job
    public string? CurrentJobStatus { get; set; }
    public DateTime? LastCompletedDateTime { get; set; }
    
    // Rule override fields - populated from the account's primary rule
    public bool RuleIsManuallyOverridden { get; set; }
    public string? RuleOverriddenBy { get; set; }
    public DateTime? RuleOverriddenDateTime { get; set; }
    
    // Blacklist status fields - populated from matching blacklist entries
    public bool HasCurrentBlacklist { get; set; }
    public bool HasFutureBlacklist { get; set; }
    public int CurrentBlacklistCount { get; set; }
    public int FutureBlacklistCount { get; set; }
    public List<BlacklistSummary> CurrentBlacklists { get; set; } = new();
    public List<BlacklistSummary> FutureBlacklists { get; set; } = new();
}

public class BlacklistSummary
{
    public int Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ExclusionType { get; set; } = string.Empty;
    public DateTime? EffectiveStartDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
    public string? VendorCode { get; set; }
    public long? VMAccountId { get; set; }
    public string? VMAccountNumber { get; set; }
    public int? CredentialId { get; set; }
}

public class AdrAccountStats
{
    public int TotalAccounts { get; set; }
    public int RunNowCount { get; set; }
    public int DueSoonCount { get; set; }
    public int UpcomingCount { get; set; }
    public int FutureCount { get; set; }
    public int MissingCount { get; set; }
    public int OverdueCount { get; set; }
    public int ActiveJobsCount { get; set; }
}
