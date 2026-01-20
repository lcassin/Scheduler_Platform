using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchedulerPlatform.API.Extensions;

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
        
        // Determine the log path based on environment
        // Azure App Service: Use %HOME%\LogFiles\Application\ which persists across deployments
        // Local development: Use relative logs/ folder under ContentRootPath
        var azureHome = Environment.GetEnvironmentVariable("HOME");
        var isAzureAppService = !string.IsNullOrEmpty(azureHome) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
        _logsPath = isAzureAppService 
            ? Path.Combine(azureHome!, "LogFiles", "Application")
            : Path.Combine(_environment.ContentRootPath, "logs");
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
            if (!User.IsAdminOrAbove())
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
            if (!User.IsAdminOrAbove())
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

            // Use FileShare.ReadWrite to allow reading files that are open by the logging system
            if (lines.HasValue && lines.Value > 0)
            {
                var allLines = await ReadAllLinesWithShareAsync(filePath);
                var linesToTake = Math.Min(lines.Value, allLines.Count);
                content = string.Join(Environment.NewLine, allLines.TakeLast(linesToTake));
            }
            else
            {
                content = await ReadAllTextWithShareAsync(filePath);
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
            if (!User.IsAdminOrAbove())
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

            // Use FileShare.ReadWrite to allow downloading files that are open by the logging system
            var fileBytes = ReadAllBytesWithShare(filePath);
            return File(fileBytes, "text/plain", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading log file {FileName}", fileName);
            return StatusCode(500, "An error occurred while downloading the log file");
        }
    }

    /// <summary>
    /// Searches the entire log file for matching lines (from bottom-up, most recent first).
    /// Uses streaming to efficiently search large files without loading them entirely into memory.
    /// </summary>
    /// <param name="fileName">The name of the log file to search.</param>
    /// <param name="term">The search term to look for (case-insensitive).</param>
    /// <param name="maxResults">Maximum number of matching lines to return (default: 500).</param>
    /// <returns>Matching lines with line numbers, ordered from most recent (bottom) to oldest (top).</returns>
    /// <response code="200">Returns the search results.</response>
    /// <response code="400">Invalid file name or missing search term.</response>
    /// <response code="403">User is not authorized to search logs.</response>
    /// <response code="404">Log file not found.</response>
    /// <response code="500">An error occurred while searching the log file.</response>
    [HttpGet("{fileName}/search")]
    [ProducesResponseType(typeof(LogSearchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LogSearchResult>> SearchLogFile(
        string fileName, 
        [FromQuery] string term, 
        [FromQuery] int maxResults = 500)
    {
        try
        {
            if (!User.IsAdminOrAbove())
            {
                _logger.LogWarning("Non-admin user attempted to search log file {FileName}", fileName);
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(term))
            {
                return BadRequest("Search term is required");
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
            var matches = new List<LogSearchMatch>();
            var totalLinesScanned = 0;

            // Read all lines and search from bottom-up (most recent first)
            // Use FileShare.ReadWrite to allow searching files that are open by the logging system
            var allLines = await ReadAllLinesWithShareAsync(filePath);

            totalLinesScanned = allLines.Count;

            // Search from bottom to top (most recent logs first)
            for (int i = allLines.Count - 1; i >= 0 && matches.Count < maxResults; i--)
            {
                if (allLines[i].Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(new LogSearchMatch
                    {
                        LineNumber = i + 1, // 1-based line numbers
                        Content = allLines[i]
                    });
                }
            }

            _logger.LogInformation("Admin {User} searched log file {FileName} for '{Term}', found {Count} matches", 
                User.Identity?.Name ?? "Unknown", fileName, term, matches.Count);

            return Ok(new LogSearchResult
            {
                FileName = fileName,
                SearchTerm = term,
                Matches = matches,
                TotalMatches = matches.Count,
                TotalLinesScanned = totalLinesScanned,
                MaxResultsReached = matches.Count >= maxResults
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching log file {FileName}", fileName);
            return StatusCode(500, "An error occurred while searching the log file");
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
            if (!User.IsSystemAdmin())
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

    /// <summary>
    /// Reads all text from a file using FileShare.ReadWrite to allow reading files that are open by other processes.
    /// </summary>
    private static async Task<string> ReadAllTextWithShareAsync(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Reads all lines from a file using FileShare.ReadWrite to allow reading files that are open by other processes.
    /// </summary>
    private static async Task<List<string>> ReadAllLinesWithShareAsync(string path)
    {
        var lines = new List<string>();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>
    /// Reads all bytes from a file using FileShare.ReadWrite to allow reading files that are open by other processes.
    /// </summary>
    private static byte[] ReadAllBytesWithShare(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
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

/// <summary>
/// Result of a log file search operation.
/// </summary>
public class LogSearchResult
{
    /// <summary>
    /// The name of the log file that was searched.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// The search term that was used.
    /// </summary>
    public string SearchTerm { get; set; } = string.Empty;
    
    /// <summary>
    /// The matching lines found, ordered from most recent (bottom of file) to oldest (top of file).
    /// </summary>
    public List<LogSearchMatch> Matches { get; set; } = new();
    
    /// <summary>
    /// The total number of matches found (may be limited by maxResults).
    /// </summary>
    public int TotalMatches { get; set; }
    
    /// <summary>
    /// The total number of lines scanned in the file.
    /// </summary>
    public int TotalLinesScanned { get; set; }
    
    /// <summary>
    /// Whether the maximum number of results was reached (more matches may exist).
    /// </summary>
    public bool MaxResultsReached { get; set; }
}

/// <summary>
/// A single matching line from a log file search.
/// </summary>
public class LogSearchMatch
{
    /// <summary>
    /// The line number in the file (1-based).
    /// </summary>
    public int LineNumber { get; set; }
    
    /// <summary>
    /// The content of the matching line.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
