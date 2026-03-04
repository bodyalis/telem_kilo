using TelemetryVideoOverlay.Core.Models;

namespace TelemetryVideoOverlay.Core.MathEngine;

/// <summary>
/// Linear interpolator for telemetry data that creates one point per video frame.
/// </summary>
public class LinearInterpolator : ITelemetryInterpolator
{
    /// <summary>
    /// Interpolates telemetry points to match the specified frame rate.
    /// </summary>
    public IReadOnlyList<TelemetryPoint> Interpolate(TelemetrySession session, double fps)
    {
        if (session.Points.Count == 0)
        {
            return Array.Empty<TelemetryPoint>();
        }

        if (session.Points.Count == 1)
        {
            // Single point - return it for all frames
            return new List<TelemetryPoint> { session.Points[0].Clone() };
        }

        var startTime = session.Points.First().Timestamp;
        var endTime = session.Points.Last().Timestamp;
        
        return InterpolateRange(session, fps, startTime, endTime);
    }

    /// <summary>
    /// Interpolates telemetry points for a specific time range.
    /// </summary>
    public IReadOnlyList<TelemetryPoint> InterpolateRange(TelemetrySession session, double fps, DateTime startTime, DateTime endTime)
    {
        if (session.Points.Count == 0 || fps <= 0)
        {
            return Array.Empty<TelemetryPoint>();
        }

        // Sort points by timestamp
        var sortedPoints = session.Points.OrderBy(p => p.Timestamp).ToList();
        
        // Filter points to the requested range
        var pointsInRange = sortedPoints
            .Where(p => p.Timestamp >= startTime && p.Timestamp <= endTime)
            .ToList();

        if (pointsInRange.Count == 0)
        {
            // No points in range, find closest points
            var before = sortedPoints.LastOrDefault(p => p.Timestamp < startTime);
            var after = sortedPoints.FirstOrDefault(p => p.Timestamp > endTime);
            
            if (before != null && after != null)
            {
                pointsInRange = new List<TelemetryPoint> { before, after };
            }
            else if (before != null)
            {
                pointsInRange = new List<TelemetryPoint> { before };
            }
            else if (after != null)
            {
                pointsInRange = new List<TelemetryPoint> { after };
            }
            else
            {
                return Array.Empty<TelemetryPoint>();
            }
        }

        // Calculate frame interval
        var frameInterval = 1.0 / fps;
        
        // Generate interpolated points
        var result = new List<TelemetryPoint>();
        
        var currentTime = startTime;
        var currentIndex = 0;
        
        while (currentTime <= endTime)
        {
            // Find the two points to interpolate between
            var (prevPoint, nextPoint, t) = FindSurroundingPoints(pointsInRange, currentTime);
            
            TelemetryPoint interpolatedPoint;
            
            if (prevPoint == null && nextPoint == null)
            {
                // No valid surrounding points, skip
                currentTime = currentTime.AddSeconds(frameInterval);
                continue;
            }
            else if (prevPoint == null)
            {
                // Before first point - use next point
                interpolatedPoint = nextPoint!.Clone();
            }
            else if (nextPoint == null)
            {
                // After last point - use previous point
                interpolatedPoint = prevPoint.Clone();
            }
            else if (t <= 0)
            {
                // Exactly at a point
                interpolatedPoint = prevPoint.Clone();
            }
            else
            {
                // Linear interpolation between two points
                interpolatedPoint = InterpolatePoint(prevPoint, nextPoint, t);
            }
            
            interpolatedPoint.Timestamp = currentTime;
            result.Add(interpolatedPoint);
            
            currentTime = currentTime.AddSeconds(frameInterval);
        }
        
        return result;
    }

    /// <summary>
    /// Finds the two surrounding points for a given time and the interpolation factor.
    /// </summary>
    private (TelemetryPoint? prev, TelemetryPoint? next, double t) FindSurroundingPoints(
        List<TelemetryPoint> points, 
        DateTime targetTime)
    {
        if (points.Count == 0)
        {
            return (null, null, 0);
        }

        // Find the last point before or at target time
        TelemetryPoint? prev = null;
        TelemetryPoint? next = null;
        int prevIndex = -1;
        
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].Timestamp <= targetTime)
            {
                prev = points[i];
                prevIndex = i;
            }
            else
            {
                next = points[i];
                break;
            }
        }
        
        if (prev == null && next != null)
        {
            // Target time is before first point
            return (null, next, 0);
        }
        
        if (prev != null && next == null)
        {
            // Target time is after last point
            return (prev, null, 1);
        }
        
        if (prev != null && next != null)
        {
            // Calculate interpolation factor
            var timeDiff = (next.Timestamp - prev.Timestamp).TotalSeconds;
            
            if (timeDiff <= 0)
            {
                return (prev, next, 0);
            }
            
            var targetDiff = (targetTime - prev.Timestamp).TotalSeconds;
            var t = targetDiff / timeDiff;
            
            return (prev, next, t);
        }
        
        // Single point
        return (points[0], null, 0);
    }

    /// <summary>
    /// Performs linear interpolation between two telemetry points.
    /// </summary>
    private TelemetryPoint InterpolatePoint(TelemetryPoint prev, TelemetryPoint next, double t)
    {
        return new TelemetryPoint
        {
            Latitude = LinearInterpolate(prev.Latitude, next.Latitude, t),
            Longitude = LinearInterpolate(prev.Longitude, next.Longitude, t),
            Altitude = LinearInterpolate(prev.Altitude, next.Altitude, t),
            Speed = LinearInterpolate(prev.Speed, next.Speed, t),
            Bearing = InterpolateBearing(prev.Bearing, next.Bearing, t),
            Timestamp = DateTime.MinValue // Will be set by caller
        };
    }

    /// <summary>
    /// Linear interpolation between two values.
    /// </summary>
    private double LinearInterpolate(double start, double end, double t)
    {
        return start + (end - start) * t;
    }

    /// <summary>
    /// Interpolates bearing, handling the 360/0 degree wraparound.
    /// </summary>
    private double? InterpolateBearing(double? start, double? end, double t)
    {
        if (start == null || end == null)
        {
            return start ?? end;
        }

        var s = start.Value;
        var e = end.Value;

        // Handle wraparound
        var diff = e - s;
        
        if (Math.Abs(diff) <= 180)
        {
            // No wraparound needed
            return LinearInterpolate(s, e, t);
        }
        
        // Handle wraparound (e.g., from 350 to 10 degrees)
        if (diff > 0)
        {
            s += 360;
        }
        else
        {
            e += 360;
        }
        
        var result = LinearInterpolate(s, e, t);
        
        // Normalize back to 0-360
        return result % 360;
    }
}
