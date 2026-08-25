using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DynTypeSerializer.Tests.TestDoubles;

/// <summary>
/// A simple in-memory <see cref="ILogger"/> that records every log entry so
/// tests can assert on the messages emitted by the library.
/// </summary>
public class RecordingLogger : ILogger
{
    private readonly string _categoryName;

    public RecordingLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    /// <summary>Captured log entries in insertion order.</summary>
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Enqueue(new LogEntry
        {
            Level = logLevel,
            EventId = eventId,
            Message = formatter(state, exception),
            Exception = exception
        });
    }

    public string CategoryName => _categoryName;
}

/// <summary>
/// An <see cref="ILoggerFactory"/> that always returns the same
/// <see cref="RecordingLogger"/>, so tests can capture the library's output.
/// </summary>
public class RecordingLoggerFactory : ILoggerFactory
{
    public RecordingLogger Logger { get; } = new("DynTypeSerializer");

    public void AddProvider(ILoggerProvider provider) { }

    public ILogger CreateLogger(string categoryName) => Logger;

    public void Dispose() { }
}

/// <summary>A single captured log entry.</summary>
public record LogEntry
{
    public LogLevel Level { get; init; }
    public EventId EventId { get; init; }
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }
}
