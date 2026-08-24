using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DynTypeSerializer.Logging;

/// <summary>
/// Provides logging for the DynTypeSerializer library.
/// </summary>
/// <remarks>
/// Hosts can call <see cref="Configure(ILogger)"/> to provide their own logger.
/// If no logger is configured, <see cref="NullLogger.Instance"/> is used.
/// </remarks>
public static class SerializerLogging
{
    private static ILogger _logger = NullLogger.Instance;

    /// <summary>
    /// Configures the logging facade with the specified logger.
    /// </summary>
    /// <param name="logger">
    /// The logger to use for subsequent DynTypeSerializer log messages.
    /// If <see langword="null"/> is supplied, logging falls back to
    /// <see cref="NullLogger.Instance"/>.
    /// </param>
    public static void Configure(ILogger logger)
    {
        _logger = logger ?? NullLogger.Instance;
        _logger.LogInformation("DynTypeSerializer logging initialized.");
    }

    internal static void Debug(string? message)
        => _logger.LogDebug("{Message}", message);

    internal static void Info(string? message)
        => _logger.LogInformation("{Message}", message);

    internal static void Warning(string? message)
        => _logger.LogWarning("{Message}", message);

    internal static void Error(string? message)
        => _logger.LogError("{Message}", message);

    internal static void Error(Exception ex, string? message = null)
        => _logger.LogError(ex, "{Message}", message ?? ex.Message);
}