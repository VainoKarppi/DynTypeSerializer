using DynTypeSerializer.Logging;
using DynTypeSerializer.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DynTypeSerializer.Tests;

/// <summary>
/// Tests for the public logging surface of <see cref="SerializerLogging"/>.
/// </summary>
/// <remarks>
/// The internal level-specific helpers (<c>Debug/Info/Warning/Error</c>) are
/// library-internal and not part of the public contract; these tests exercise
/// the public <see cref="SerializerLogging.Configure(ILogger)"/> entry point,
/// including null/factory behavior and message emission.
/// </remarks>
public class LoggingTests
{
    [Fact]
    public void Configure_NullLogger_DoesNotThrow()
    {
        // The library falls back to NullLogger.Instance when null is supplied,
        // even though the parameter is not annotated nullable.
        SerializerLogging.Configure(null!);
    }

    [Fact]
    public void Configure_WithRecordingLogger_EmitsInitializationMessage()
    {
        var logger = new RecordingLogger("DynTypeSerializer");
        SerializerLogging.Configure(logger);

        Assert.Single(logger.Entries);
        var entry = logger.Entries.First();
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("initialized", entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Configure_WithNullLoggerInstance_DoesNotThrow()
    {
        // NullLogger.Instance is a valid ILogger and is used internally as the
        // no-op fallback; configuring it must never throw.
        SerializerLogging.Configure(NullLogger.Instance);
    }

    [Fact]
    public void Configure_MultipleTimes_EmitsEachTime()
    {
        var logger = new RecordingLogger("DynTypeSerializer");
        SerializerLogging.Configure(logger);
        SerializerLogging.Configure(logger);

        Assert.Equal(2, logger.Entries.Count);
    }
}
