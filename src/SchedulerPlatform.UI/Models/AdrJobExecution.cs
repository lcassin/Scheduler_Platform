namespace SchedulerPlatform.UI.Models;

public class AdrJobExecution
{
    public int Id { get; set; }
    public int AdrJobId { get; set; }
    public int AdrRequestTypeId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public int? AdrStatusId { get; set; }
    public string? AdrStatusDescription { get; set; }
    public long? AdrIndexId { get; set; }
    public int? HttpStatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsError { get; set; }
    public bool IsFinal { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RequestPayload { get; set; }
    public string? ApiResponse { get; set; }
    public DateTime CreatedDateTime { get; set; }
    public DateTime? ModifiedDateTime { get; set; }
}
