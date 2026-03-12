using System.Text.Json.Serialization;

namespace SchedulerPlatform.UI.Models;

/// <summary>
/// Result from starting a background export operation.
/// Maps to the camelCase JSON returned by the API: { "requestId": "...", "message": "..." }
/// </summary>
public class BackgroundExportStartResult
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Status of a background export operation.
/// Maps to the ExportStatus class returned by the API.
/// </summary>
public class BackgroundExportStatus
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("exportType")]
    public string ExportType { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("totalRecords")]
    public int TotalRecords { get; set; }

    [JsonPropertyName("recordsProcessed")]
    public int ProcessedRecords { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("requestedAt")]
    public DateTime RequestedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("requestedBy")]
    public string RequestedBy { get; set; } = string.Empty;
}
