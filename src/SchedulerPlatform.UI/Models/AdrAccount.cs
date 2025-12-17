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
