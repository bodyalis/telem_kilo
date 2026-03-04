namespace TelemetryVideoOverlay.Core.Models;

/// <summary>
/// Represents a telemetry session containing metadata and a collection of telemetry points.
/// </summary>
public class TelemetrySession
{
    /// <summary>
    /// Name of the telemetry session (e.g., track name).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the telemetry session.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Start time of the session.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End time of the session.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Collection of telemetry points in chronological order.
    /// </summary>
    public List<TelemetryPoint> Points { get; set; } = new();

    /// <summary>
    /// Total distance in meters.
    /// </summary>
    public double TotalDistanceMeters { get; set; }

    /// <summary>
    /// Maximum speed recorded in meters per second.
    /// </summary>
    public double MaxSpeedMetersPerSecond { get; set; }

    /// <summary>
    /// Maximum altitude in meters.
    /// </summary>
    public double MaxAltitudeMeters { get; set; }

    /// <summary>
    /// Minimum altitude in meters.
    /// </summary>
    public double MinAltitudeMeters { get; set; }

    /// <summary>
    /// Total elevation gain in meters.
    /// </summary>
    public double TotalElevationGainMeters { get; set; }

    /// <summary>
    /// Total elevation loss in meters.
    /// </summary>
    public double TotalElevationLossMeters { get; set; }

    /// <summary>
    /// Duration of the session.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Average speed in meters per second.
    /// </summary>
    public double AverageSpeedMetersPerSecond => Duration.TotalSeconds > 0 
        ? TotalDistanceMeters / Duration.TotalSeconds 
        : 0;

    /// <summary>
    /// Gets the number of points in the session.
    /// </summary>
    public int PointCount => Points.Count;

    /// <summary>
    /// Gets the first point in the session.
    /// </summary>
    public TelemetryPoint? FirstPoint => Points.FirstOrDefault();

    /// <summary>
    /// Gets the last point in the session.
    /// </summary>
    public TelemetryPoint? LastPoint => Points.LastOrDefault();

    /// <summary>
    /// Creates an empty TelemetrySession.
    /// </summary>
    public TelemetrySession()
    {
    }

    /// <summary>
    /// Creates a TelemetrySession with the specified name.
    /// </summary>
    public TelemetrySession(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Updates the computed metadata based on the current points.
    /// </summary>
    public void ComputeMetadata()
    {
        if (Points.Count == 0)
        {
            TotalDistanceMeters = 0;
            MaxSpeedMetersPerSecond = 0;
            MaxAltitudeMeters = 0;
            MinAltitudeMeters = 0;
            TotalElevationGainMeters = 0;
            TotalElevationLossMeters = 0;
            StartTime = DateTime.MinValue;
            EndTime = DateTime.MinValue;
            return;
        }

        Points = Points.OrderBy(p => p.Timestamp).ToList();

        StartTime = Points.First().Timestamp;
        EndTime = Points.Last().Timestamp;

        // Compute total distance and max speed
        TotalDistanceMeters = 0;
        MaxSpeedMetersPerSecond = 0;
        
        // Compute altitude stats
        MaxAltitudeMeters = Points.Max(p => p.Altitude);
        MinAltitudeMeters = Points.Min(p => p.Altitude);

        double elevationGain = 0;
        double elevationLoss = 0;

        for (int i = 1; i < Points.Count; i++)
        {
            var prev = Points[i - 1];
            var curr = Points[i];

            // Calculate distance using Haversine formula
            var distance = HaversineDistance(
                prev.Latitude, prev.Longitude,
                curr.Latitude, curr.Longitude);
            
            TotalDistanceMeters += distance;

            // Track max speed
            if (curr.Speed > MaxSpeedMetersPerSecond)
            {
                MaxSpeedMetersPerSecond = curr.Speed;
            }

            // Calculate elevation change
            var elevationDiff = curr.Altitude - prev.Altitude;
            if (elevationDiff > 0)
            {
                elevationGain += elevationDiff;
            }
            else
            {
                elevationLoss += Math.Abs(elevationDiff);
            }
        }

        TotalElevationGainMeters = elevationGain;
        TotalElevationLossMeters = elevationLoss;
    }

    /// <summary>
    /// Calculates the distance between two coordinates using the Haversine formula.
    /// </summary>
    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Earth's radius in meters

        var lat1Rad = lat1 * Math.PI / 180;
        var lat2Rad = lat2 * Math.PI / 180;
        var deltaLat = (lat2 - lat1) * Math.PI / 180;
        var deltaLon = (lon2 - lon1) * Math.PI / 180;

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }
}
