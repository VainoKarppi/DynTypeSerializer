using DynTypeSerializer.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DynTypeSerializer.Tests;

/// <summary>
/// Tests for the logging surface of <see cref="Serializer"/>.
/// </summary>
/// <remarks>
/// The library follows the standard <c>Microsoft.Extensions.Logging</c>
/// pattern: hosts inject their <see cref="ILoggerFactory"/> via
/// <see cref="Serializer.SetLoggerFactory(ILoggerFactory?)"/> and the library
/// creates its own logger for the <c>"DynTypeSerializer"</c> category using
/// the source-generated <c>[LoggerMessage]</c> methods.
/// </remarks>
public class LoggingTests
{
    [Fact]
    public void SetLoggerFactory_Null_DisablesLogging()
    {
        // Passing null falls back to a no-op logger factory.
        Serializer.SetLoggerFactory(null);

        // Serializing with the no-op logger must not throw.
        _ = Serializer.SerializeToBytes(42);
    }

    [Fact]
    public void SetLoggerFactory_EmitsInitializationMessage()
    {
        var factory = new RecordingLoggerFactory();
        Serializer.SetLoggerFactory(factory);

        Assert.Contains(factory.Logger.Entries, e => e.Level == LogLevel.Information);
    }

    [Fact]
    public void Serialize_LogsAtDebug()
    {
        var factory = new RecordingLoggerFactory();
        Serializer.SetLoggerFactory(factory);

        _ = Serializer.SerializeToBytes(new { Name = "Alice" });

        Assert.Contains(factory.Logger.Entries,
            e => e.Level == LogLevel.Debug && e.Message.Contains("Serializing"));
    }

    [Fact]
    public void Deserialize_LogsAtDebug()
    {
        var factory = new RecordingLoggerFactory();
        Serializer.SetLoggerFactory(factory);

        _ = Serializer.Deserialize<int>("42");

        Assert.Contains(factory.Logger.Entries,
            e => e.Level == LogLevel.Debug && e.Message.Contains("Deserializing"));
    }

    [Fact]
    public void UnresolvedType_LogsWarning()
    {
        var factory = new RecordingLoggerFactory();
        Serializer.SetLoggerFactory(factory);

        Assert.Throws<InvalidOperationException>(
            () => Serializer.Deserialize<object>("{\"$t\":\"X.Y.NotARealType\",\"$v\":1}"));

        Assert.Contains(factory.Logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("Unable to resolve type"));
    }

    [Fact]
    public void ReadOnlyProperty_LogsWarning()
    {
        var factory = new RecordingLoggerFactory();
        Serializer.SetLoggerFactory(factory);

        _ = Serializer.Deserialize<Models.ReadOnlyModel>("{\"Id\":7}");

        Assert.Contains(factory.Logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("no public setter"));
    }
}

