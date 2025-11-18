using System.Collections.Concurrent;

namespace SchedulerPlatform.ScheduleSync.Services;

public class FileLogger : IDisposable
{
    private readonly string _logFilePath;
    private readonly StreamWriter _writer;
    private readonly object _lock = new object();
    private readonly bool _logToConsole;

    public FileLogger(string logDirectory, string filePrefix, bool logToConsole = true)
    {
        _logToConsole = logToConsole;
        
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _logFilePath = Path.Combine(logDirectory, $"{filePrefix}_{timestamp}.log");
        
        _writer = new StreamWriter(_logFilePath, append: true) { AutoFlush = true };
        
        LogInfo($"Log file created: {_logFilePath}");
    }

    public string LogFilePath => _logFilePath;

    public void LogInfo(string message)
    {
        Log("INFO", message);
    }

    public void LogWarning(string message)
    {
        Log("WARNING", message);
    }

    public void LogError(string message)
    {
        Log("ERROR", message);
    }

    public void LogError(string message, Exception ex)
    {
        Log("ERROR", $"{message}\nException Type: {ex.GetType().FullName}\nMessage: {ex.Message}\nStack Trace:\n{ex.StackTrace}");
        
        if (ex.InnerException != null)
        {
            Log("ERROR", $"Inner Exception Type: {ex.InnerException.GetType().FullName}\nInner Message: {ex.InnerException.Message}\nInner Stack Trace:\n{ex.InnerException.StackTrace}");
        }
    }

    private void Log(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logLine = $"[{timestamp}] [{level}] {message}";

        lock (_lock)
        {
            _writer.WriteLine(logLine);
            
            if (_logToConsole)
            {
                Console.WriteLine(logLine);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
        }
    }
}
