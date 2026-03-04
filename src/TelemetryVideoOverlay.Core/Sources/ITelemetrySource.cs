using TelemetryVideoOverlay.Core.Models;

namespace TelemetryVideoOverlay.Core.Sources;

/// <summary>
/// Interface for telemetry data sources.
/// </summary>
public interface ITelemetrySource
{
    /// <summary>
    /// Gets the source identifier/name.
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Checks if the source is available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Opens the telemetry source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the telemetry source.
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// Reads all telemetry data from the source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A telemetry session containing the data.</returns>
    Task<TelemetrySession> ReadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets supported file extensions for this source.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }
}
