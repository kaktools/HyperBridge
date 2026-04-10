using HyperBridge.Core.Enums;
using HyperBridge.Core.Models;

namespace HyperBridge.Core.Contracts;

public interface ILoggingService
{
    event EventHandler<LogEntry>? LogAdded;
    string CurrentLogPath { get; }
    void Log(LogLevel level, string message);
    void LogDebug(string message);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
    IReadOnlyList<LogEntry> GetRecentEntries(int maxCount = 200);
}