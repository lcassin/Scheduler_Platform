using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SchedulerPlatform.API.Controllers;

/// <summary>
/// Controller for viewing API log files.
/// Provides endpoints for listing, viewing, and downloading log files.
/// Access is restricted to Admin and Super Admin users only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LogsController : ControllerBase
{
    private readonly ILogger<LogsController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly string _logsPath;

    public LogsController(ILogger<LogsController> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
        _logsPath = Path.Combine(_environment.ContentRootPath, "logs");
    }

    /// <summary>
    /// Retrieves a list of available log files.
    /// </summary>
    /// <returns>A list of log file information including name, size, and last modified date.</returns>
    /// <response code="200">Returns the list of log files.</response>
    /// <response code="403">User is not authorized to view logs.</response>
    /// <response code="500">An error occurred while retrieving log files.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LogFileInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<IEnumerable<LogFileInfo>> GetLogFiles()
    {
        try
        {
            if (!IsAdminOrAbove())
            {
                _logger.LogWarning("Non-admin user attempted to access log files");
                return Forbid();
            }

            if (!Directory.Exists(_logsPath))
            {
                return Ok(Array.Empty<LogFileInfo>());
            }

            var logFiles = Directory.GetFiles(_logsPath, "*.txt")
                .Concat(Directory.GetFiles(_logsPath, "*.log"))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => new LogFileInfo
                {
                    FileName = f.Name,
                    SizeBytes = f.Length,
                    SizeFormatted = FormatFileSize(f.Length),
                    LastModifiedUtc = f.LastWriteTimeUtc,
                    CreatedUtc = f.CreationTimeUtc
                })
                .ToList();

            return Ok(logFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving log files");
            return StatusCode(500, "An error occurred while retrieving log files");
        }
    }

    /// <summary>
    /// Retrieves the contents of a specific log file.
    /// </summary>
    /// <param name="fileName">The name of the log file to read.</param>
    /// <param name="lines">Optional number of lines to return from the end of the file (default: all lines).</param>
    /// <returns>The contents of the log file.</returns>
    /// <response code="200">Returns the log file contents.</response>
    /// <response code="400">Invalid file name provided.</response>
    /// <response code="403">User is not authorized to view logs.</response>
    /// <response code="404">Log file not found.</response>
    /// <response code="500">An error occurred while reading the log file.</response>
    [HttpGet("{fileName}")]
    [ProducesResponseType(typeof(LogFileContent), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LogFileContent>> GetLogFileContent(string fileName, [FromQuery] int? lines = null)
    {
        try
        {
            if (!IsAdminOrAbove())
            {
                _logger.LogWarning("Non-admin user attempted to read log file {FileName}", fileName);
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            {
                return BadRequest("Invalid file name");
            }

            var filePath = Path.Combine(_logsPath, fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Log file not found");
            }

            var fileInfo = new FileInfo(filePath);
            string content;

            if (lines.HasValue && lines.Value > 0)
            {
                var allLines = await System.IO.File.ReadAllLinesAsync(filePath);
                var linesToTake = Math.Min(lines.Value, allLines.Length);
                content = string.Join(Environment.NewLine, allLines.TakeLast(linesToTake));
            }
            else
            {
                content = await System.IO.File.ReadAllTextAsync(filePath);
            }

            _logger.LogInformation("Admin {User} viewed log file {FileName}", User.Identity?.Name ?? "Unknown", fileName);

            return Ok(new LogFileContent
            {
                FileName = fileName,
                Content = content,
                SizeBytes = fileInfo.Length,
                LastModifiedUtc = fileInfo.LastWriteTimeUtc,
                TotalLines = content.Split('\n').Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading log file {FileName}", fileName);
            return StatusCode(500, "An error occurred while reading the log file");
        }
    }

    /// <summary>
    /// Downloads a log file.
    /// </summary>
    /// <param name="fileName">The name of the log file to download.</param>
    /// <returns>The log file as a downloadable attachment.</returns>
    /// <response code="200">Returns the log file for download.</response>
    /// <response code="400">Invalid file name provided.</response>
    /// <response code="403">User is not authorized to download logs.</response>
    /// <response code="404">Log file not found.</response>
    /// <response code="500">An error occurred while downloading the log file.</response>
    [HttpGet("{fileName}/download")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult DownloadLogFile(string fileName)
    {
        try
        {
            if (!IsAdminOrAbove())
            {
                _logger.LogWarning("Non-admin user attempted to download log file {FileName}", fileName);
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            {
                return BadRequest("Invalid file name");
            }

            var filePath = Path.Combine(_logsPath, fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Log file not found");
            }

            _logger.LogInformation("Admin {User} downloaded log file {FileName}", User.Identity?.Name ?? "Unknown", fileName);

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "text/plain", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading log file {FileName}", fileName);
            return StatusCode(500, "An error occurred while downloading the log file");
        }
    }

    /// <summary>
    /// Deletes old log files older than the specified number of days.
    /// Only Super Admins can delete log files.
    /// </summary>
    /// <param name="olderThanDays">Delete files older than this many days (default: 30).</param>
    /// <returns>The number of files deleted.</returns>
    /// <response code="200">Returns the count of deleted files.</response>
    /// <response code="403">User is not authorized to delete logs.</response>
    /// <response code="500">An error occurred while deleting log files.</response>
    [HttpDelete("cleanup")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult CleanupOldLogs([FromQuery] int olderThanDays = 30)
    {
        try
        {
            if (!IsSystemAdmin())
            {
                _logger.LogWarning("Non-super-admin user attempted to delete log files");
                return Forbid();
            }

            if (!Directory.Exists(_logsPath))
            {
                return Ok(new { deletedCount = 0, message = "No logs directory found" });
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            var deletedCount = 0;

            var logFiles = Directory.GetFiles(_logsPath, "*.txt")
                .Concat(Directory.GetFiles(_logsPath, "*.log"));

            foreach (var file in logFiles)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc < cutoffDate)
                {
                    System.IO.File.Delete(file);
                    deletedCount++;
                    _logger.LogInformation("Deleted old log file {FileName}", fileInfo.Name);
                }
            }

            _logger.LogInformation("Super Admin {User} cleaned up {Count} log files older than {Days} days", 
                User.Identity?.Name ?? "Unknown", deletedCount, olderThanDays);

            return Ok(new { deletedCount, message = $"Deleted {deletedCount} log files older than {olderThanDays} days" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up log files");
            return StatusCode(500, "An error occurred while cleaning up log files");
        }
    }

    private bool IsSystemAdmin()
    {
        var isSystemAdminValue = User.FindFirst("is_system_admin")?.Value;
        return string.Equals(isSystemAdminValue, "True", StringComparison.OrdinalIgnoreCase) 
            || isSystemAdminValue == "1";
    }

    private bool IsAdminOrAbove()
    {
        if (IsSystemAdmin()) return true;
        
        var userRole = User.FindFirst("role")?.Value;
        return userRole == "Admin" || userRole == "Super Admin";
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Information about a log file.
/// </summary>
public class LogFileInfo
{
    /// <summary>
    /// The name of the log file.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// The size of the file in bytes.
    /// </summary>
    public long SizeBytes { get; set; }
    
    /// <summary>
    /// The formatted file size (e.g., "1.5 MB").
    /// </summary>
    public string SizeFormatted { get; set; } = string.Empty;
    
    /// <summary>
    /// The last modified date in UTC.
    /// </summary>
    public DateTime LastModifiedUtc { get; set; }
    
    /// <summary>
    /// The creation date in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// The contents of a log file.
/// </summary>
public class LogFileContent
{
    /// <summary>
    /// The name of the log file.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// The text content of the log file.
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// The size of the file in bytes.
    /// </summary>
    public long SizeBytes { get; set; }
    
    /// <summary>
    /// The last modified date in UTC.
    /// </summary>
    public DateTime LastModifiedUtc { get; set; }
    
    /// <summary>
    /// The total number of lines in the content.
    /// </summary>
    public int TotalLines { get; set; }
}
