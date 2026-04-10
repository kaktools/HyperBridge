using HyperBridge.Core.Contracts;
using HyperBridge.Core.Enums;
using HyperBridge.Core.Models;

namespace HyperBridge.Services;

public sealed class LoggingService : ILoggingService
{
    private readonly List<LogEntry> _entries = [];
    private readonly object _sync = new();
    private readonly string _logDirectory;

    public LoggingService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logDirectory = Path.Combine(appData, "HyperBridge", "Logs");
        Directory.CreateDirectory(_logDirectory);
        CurrentLogPath = Path.Combine(_logDirectory, $"hyperbridge_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    }

    public event EventHandler<LogEntry>? LogAdded;

    public string CurrentLogPath { get; }

    public void Log(LogLevel level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
        };

        lock (_sync)
        {
            _entries.Add(entry);
            if (_entries.Count > 5000)
            {
                _entries.RemoveRange(0, 500);
            }

            File.AppendAllText(CurrentLogPath, $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Message}{Environment.NewLine}");
        }

        LogAdded?.Invoke(this, entry);
    }

    public void LogDebug(string message) => Log(LogLevel.Debug, message);

    public void LogInfo(string message) => Log(LogLevel.Info, message);

    public void LogWarning(string message) => Log(LogLevel.Warning, message);

    public void LogError(string message) => Log(LogLevel.Error, message);

    public IReadOnlyList<LogEntry> GetRecentEntries(int maxCount = 200)
    {
        lock (_sync)
        {
            return _entries.TakeLast(maxCount).ToList();
        }
    }
}