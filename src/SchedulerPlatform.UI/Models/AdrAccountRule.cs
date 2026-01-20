namespace SchedulerPlatform.UI.Models;

public class AccountRuleDto
{
    public int Id { get; set; }
    public int AdrAccountId { get; set; }
    public string? PrimaryVendorCode { get; set; }
    public string? MasterVendorCode { get; set; }
    public string? VMAccountNumber { get; set; }
    public int JobTypeId { get; set; }
    public string? PeriodType { get; set; }
    public int? PeriodDays { get; set; }
    public DateTime? NextRunDateTime { get; set; }
    public DateTime? NextRangeStartDateTime { get; set; }
    public DateTime? NextRangeEndDateTime { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsManuallyOverridden { get; set; }
    public string? OverriddenBy { get; set; }
    public DateTime? OverriddenDateTime { get; set; }
}

public class UpdateRuleRequest
{
    public DateTime? NextRunDateTime { get; set; }
    public DateTime? NextRangeStartDateTime { get; set; }
    public DateTime? NextRangeEndDateTime { get; set; }
    public string? PeriodType { get; set; }
    public int? PeriodDays { get; set; }
    public int? JobTypeId { get; set; }
    public bool? IsEnabled { get; set; }
}
