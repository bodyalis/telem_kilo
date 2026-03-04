namespace TelemetryVideoOverlay.Core.Models;

/// <summary>
/// Represents a single GPS telemetry point with coordinates, time, altitude, and speed.
/// </summary>
public class TelemetryPoint
{
    /// <summary>
    /// Latitude in decimal degrees.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude in decimal degrees.
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Altitude in meters.
    /// </summary>
    public double Altitude { get; set; }

    /// <summary>
    /// Timestamp of this telemetry point.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Speed in meters per second. May be calculated from coordinates if not present in source.
    /// </summary>
    public double Speed { get; set; }

    /// <summary>
    /// Bearing/Direction in degrees (0-360).
    /// </summary>
    public double? Bearing { get; set; }

    public TelemetryPoint()
    {
        Timestamp = DateTime.MinValue;
    }

    public TelemetryPoint(double latitude, double longitude, double altitude, DateTime timestamp, double speed = 0)
    {
        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
        Timestamp = timestamp;
        Speed = speed;
    }

    /// <summary>
    /// Creates a deep copy of this telemetry point.
    /// </summary>
    public TelemetryPoint Clone()
    {
        return new TelemetryPoint
        {
            Latitude = Latitude,
            Longitude = Longitude,
            Altitude = Altitude,
            Timestamp = Timestamp,
            Speed = Speed,
            Bearing = Bearing
        };
    }
}
