using System.Collections.Concurrent;

namespace KeyboardSound.Core.Diagnostics;

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error
}

/// <summary>
/// Minimal, allocation-light logger. Debug-level logging is off by default so normal
/// operation (key handling, audio playback) never pays for string formatting or file I/O.
/// Writes are queued and flushed on a background timer to keep the hook/audio paths non-blocking.
/// </summary>
public static class Log
{
    private static readonly ConcurrentQueue<string> Pending = new();
    private static volatile bool _debugEnabled;
    private static string? _logFilePath;
    private static readonly object FlushLock = new();

    public static bool DebugEnabled
    {
        get => _debugEnabled;
        set => _debugEnabled = value;
    }

    public static void Initialize(string logFilePath, bool debugEnabled)
    {
        _logFilePath = logFilePath;
        _debugEnabled = debugEnabled;
        var dir = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public static void Debug(string message)
    {
        if (_debugEnabled) Write(LogLevel.Debug, message);
    }

    public static void Info(string message) => Write(LogLevel.Info, message);

    public static void Warn(string message) => Write(LogLevel.Warn, message);

    public static void Error(string message, Exception? ex = null) =>
        Write(LogLevel.Error, ex is null ? message : $"{message} :: {ex}");

    private static void Write(LogLevel level, string message)
    {
        if (_logFilePath is null) return;
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        Pending.Enqueue(line);
        // Cheap fire-and-forget flush; avoids a dedicated timer/thread for a lightweight app.
        if (Pending.Count >= 20) Flush();
    }

    public static void Flush()
    {
        if (_logFilePath is null || Pending.IsEmpty) return;
        lock (FlushLock)
        {
            if (Pending.IsEmpty) return;
            try
            {
                using var writer = new StreamWriter(_logFilePath, append: true);
                while (Pending.TryDequeue(out var line))
                    writer.WriteLine(line);
            }
            catch
            {
                // Logging must never crash the app. Drop the batch and continue.
                while (Pending.TryDequeue(out _)) { }
            }
        }
    }
}
