namespace SchedulerPlatform.UI.Models;

/// <summary>
/// Result from starting a background export operation.
/// </summary>
public class BackgroundExportStartResult
{
    public string RequestId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Status of a background export operation.
/// </summary>
public class BackgroundExportStatus
{
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ExportType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public int Progress { get; set; }
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
}
