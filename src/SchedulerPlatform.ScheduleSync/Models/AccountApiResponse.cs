namespace SchedulerPlatform.ScheduleSync.Models;

public class AccountApiResponse
{
    public int Total { get; set; }
    public int Batch { get; set; }
    public int Page { get; set; }
    public int PageTotal { get; set; }
    public List<AccountData> Data { get; set; } = new();
}

public class AccountData
{
    public long AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public long VendorId { get; set; }
    public long ClientId { get; set; }
    public DateTime? LastInvoiceDate { get; set; }
    public string? AccountName { get; set; }
    public string? VendorName { get; set; }
    public string? ClientName { get; set; }
    public string? TandemAcctId { get; set; }
    public int CredentialId { get; set; }
}
