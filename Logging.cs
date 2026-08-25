using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DynTypeSerializer;

public static partial class Serializer
{
    // ════════════════════════════════════════════════════════════════════════
    // LOGGING
    // ════════════════════════════════════════════════════════════════════════

    // The source-generated log methods below resolve against this field. It
    // defaults to a no-op logger so logging stays disabled until the host
    // supplies an ILoggerFactory.
    private static ILogger _logger =
        NullLoggerFactory.Instance.CreateLogger("DynTypeSerializer");

    /// <summary>
    /// Configures the <see cref="ILoggerFactory"/> used to emit all
    /// DynTypeSerializer log messages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this once at application startup, passing the factory the host is
    /// already using. The factory is the standard
    /// <c>Microsoft.Extensions.Logging</c> injection point: the library creates
    /// its own logger for the <c>"DynTypeSerializer"</c> category while the host
    /// retains full control over providers, levels, and filters.
    /// </para>
    /// <para>
    /// Typical usage in a host that has an <see cref="ILoggerFactory"/> (for
    /// example, resolved from dependency injection):
    /// <code>Serializer.SetLoggerFactory(loggerFactory);</code>
    /// </para>
    /// </remarks>
    /// <param name="loggerFactory">
    /// The host's <see cref="ILoggerFactory"/>. Pass <see langword="null"/>
    /// to disable logging; the default is a no-op logger.
    /// </param>
    public static void SetLoggerFactory(ILoggerFactory? loggerFactory)
    {
        _logger = (loggerFactory ?? NullLoggerFactory.Instance)
            .CreateLogger("DynTypeSerializer");

        LogLoggingInitialized(_logger);
    }

    // ── Log message definitions ────────────────────────────────────────────
    // These use the LoggerMessage source generator: high-performance,
    // allocation-free when the level is disabled, and AOT-compatible.

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "DynTypeSerializer logging initialized.")]
    private static partial void LogLoggingInitialized(ILogger logger);

    [LoggerMessage(EventId = 10, Level = LogLevel.Debug,
        Message = "Serializing object of runtime type {RuntimeType}.")]
    private static partial void LogSerialize(ILogger logger, string runtimeType);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug,
        Message = "Deserializing JSON with declared type {DeclaredType}.")]
    private static partial void LogDeserialize(ILogger logger, string declaredType);

    [LoggerMessage(EventId = 20, Level = LogLevel.Warning,
        Message = "Unable to resolve type '{TypeCode}' during deserialization.")]
    private static partial void LogUnresolvedType(ILogger logger, string typeCode);

    [LoggerMessage(EventId = 21, Level = LogLevel.Warning,
        Message = "Property '{Property}' on type '{Type}' was skipped because it has no public setter.")]
    private static partial void LogSkippedReadOnlyProperty(ILogger logger, string property, string type);

    // ════════════════════════════════════════════════════════════════════════
    // INTERNAL FACADE for the Json/Binary core classes
    // ════════════════════════════════════════════════════════════════════════
    internal static void LogWarning(TypeCodeWarning code, (string, string) data)
    {
        switch (code)
        {
            case TypeCodeWarning.UnresolvedType:
                LogUnresolvedType(_logger, data.Item1);
                break;
            case TypeCodeWarning.SkippedReadOnlyProperty:
                LogSkippedReadOnlyProperty(_logger, data.Item1, data.Item2);
                break;
        }
    }
}

internal enum TypeCodeWarning
{
    UnresolvedType,
    SkippedReadOnlyProperty
}
