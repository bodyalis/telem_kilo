using TelemetryVideoOverlay.Core.Models;

namespace TelemetryVideoOverlay.Core.MathEngine;

/// <summary>
/// Interface for telemetry data interpolation.
/// </summary>
public interface ITelemetryInterpolator
{
    /// <summary>
    /// Interpolates telemetry points to match the specified frame rate.
    /// </summary>
    /// <param name="session">The source telemetry session.</param>
    /// <param name="fps">Target frames per second.</param>
    /// <returns>A list of telemetry points, one per frame.</returns>
    IReadOnlyList<TelemetryPoint> Interpolate(TelemetrySession session, double fps);

    /// <summary>
    /// Interpolates telemetry points for a specific time range.
    /// </summary>
    /// <param name="session">The source telemetry session.</param>
    /// <param name="fps">Target frames per second.</param>
    /// <param name="startTime">Start time for interpolation.</param>
    /// <param name="endTime">End time for interpolation.</param>
    /// <returns>A list of telemetry points for the specified time range.</returns>
    IReadOnlyList<TelemetryPoint> InterpolateRange(TelemetrySession session, double fps, DateTime startTime, DateTime endTime);
}
