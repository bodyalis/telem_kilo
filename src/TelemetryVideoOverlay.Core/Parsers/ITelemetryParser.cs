using TelemetryVideoOverlay.Core.Models;

namespace TelemetryVideoOverlay.Core.Parsers;

/// <summary>
/// Interface for telemetry data parsers.
/// </summary>
public interface ITelemetryParser
{
    /// <summary>
    /// Gets the list of file extensions supported by this parser.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Parses a telemetry file and returns a session with all data.
    /// </summary>
    /// <param name="filePath">Path to the telemetry file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A telemetry session containing the parsed data.</returns>
    Task<TelemetrySession> ParseAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this parser can handle the specified file.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>True if the parser can handle the file.</returns>
    bool CanParse(string filePath);
}
