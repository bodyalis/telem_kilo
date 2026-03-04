using System.Xml.Linq;
using TelemetryVideoOverlay.Core.Models;

namespace TelemetryVideoOverlay.Core.Parsers;

/// <summary>
/// GPX (GPS Exchange Format) parser that computes speed and distance using Haversine formula.
/// </summary>
public class GpxParser : ITelemetryParser
{
    private static readonly XNamespace GpxNs = "http://www.topografix.com/GPX/1/1";
    private static readonly XNamespace GpxNs21 = "http://www.topografix.com/GPX/1/0";
    private static readonly XNamespace GpxNs12 = "http://www.topografix.com/GPX/1/2";
    
    public IReadOnlyList<string> SupportedExtensions { get; } = new List<string> { ".gpx" };

    public bool CanParse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;
            
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        return extension == ".gpx";
    }

    public async Task<TelemetrySession> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("GPX file not found.", filePath);
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return ParseGpxContent(content, Path.GetFileNameWithoutExtension(filePath));
    }

    private TelemetrySession ParseGpxContent(string content, string fileName)
    {
        var session = new TelemetrySession
        {
            Name = fileName,
            Description = null
        };

        XDocument doc;
        try
        {
            doc = XDocument.Parse(content);
        }
        catch (Exception ex)
        {
            throw new FormatException("Invalid GPX XML format.", ex);
        }

        var root = doc.Root;
        if (root == null)
        {
            throw new FormatException("GPX document has no root element.");
        }

        // Try different namespace versions
        var trackElement = root.Element(GpxNs + "trk") 
            ?? root.Element(GpxNs21 + "trk")
            ?? root.Element(GpxNs12 + "trk")
            ?? root.Element("trk");

        if (trackElement == null)
        {
            throw new FormatException("No track element found in GPX file.");
        }

        // Parse metadata if available
        var metadata = root.Element(GpxNs + "metadata") 
            ?? root.Element(GpxNs21 + "metadata")
            ?? root.Element("metadata");
        
        if (metadata != null)
        {
            var name = metadata.Element(GpxNs + "name") ?? metadata.Element("name");
            if (name != null)
            {
                session.Name = name.Value;
            }

            var desc = metadata.Element(GpxNs + "description") ?? metadata.Element("description");
            if (desc != null)
            {
                session.Description = desc.Value;
            }
        }

        // Parse track segments
        var trackSegments = trackElement.Elements(GpxNs + "trkseg")
            .Concat(trackElements(GpxNs21 + "trkseg"))
            .Concat(trackElements(GpxNs12 + "trkseg"))
            .Concat(trackElements("trkseg"));

        foreach (var segment in trackSegments)
        {
            var points = ParseTrackPoints(segment);
            session.Points.AddRange(points);
        }

        // Compute speed for each point if not provided
        ComputeSpeedsAndDistances(session);

        // Compute session metadata
        session.ComputeMetadata();

        return session;
    }

    private IEnumerable<XElement> trackElements(XName name)
    {
        return Enumerable.Empty<XElement>();
    }

    private List<TelemetryPoint> ParseTrackPoints(XElement segment)
    {
        var points = new List<TelemetryPoint>();

        var trackPoints = segment.Elements(GpxNs + "trkpt")
            .Concat(segment.Elements(GpxNs21 + "trkpt"))
            .Concat(segment.Elements(GpxNs12 + "trkpt"))
            .Concat(segment.Elements("trkpt"));

        foreach (var trkpt in trackPoints)
        {
            var latAttr = trkpt.Attribute("lat");
            var lonAttr = trkpt.Attribute("lon");

            if (latAttr == null || lonAttr == null)
                continue;

            if (!double.TryParse(latAttr.Value, out var lat) ||
                !double.TryParse(lonAttr.Value, out var lon))
            {
                continue;
            }

            var point = new TelemetryPoint
            {
                Latitude = lat,
                Longitude = lon,
                Altitude = 0,
                Speed = 0
            };

            // Parse time
            var timeElement = trkpt.Element(GpxNs + "time") 
                ?? trkpt.Element(GpxNs21 + "time")
                ?? trkpt.Element(GpxNs12 + "time")
                ?? trkpt.Element("time");
            
            if (timeElement != null && DateTime.TryParse(timeElement.Value, out var time))
            {
                point.Timestamp = time;
            }

            // Parse elevation
            var eleElement = trkpt.Element(GpxNs + "ele") 
                ?? trkpt.Element(GpxNs21 + "ele")
                ?? trkpt.Element(GpxNs12 + "ele")
                ?? trkpt.Element("ele");
            
            if (eleElement != null && double.TryParse(eleElement.Value, out var ele))
            {
                point.Altitude = ele;
            }

            // Parse speed if available (some GPX files include extensions with speed)
            var speedElement = trkpt.Element(GpxNs + "extensions")?
                .Descendants()
                .FirstOrDefault(e => e.Name.LocalName.ToLower().Contains("speed"));
            
            if (speedElement != null && double.TryParse(speedElement.Value, out var speed))
            {
                point.Speed = speed; // Already in m/s
            }

            points.Add(point);
        }

        return points;
    }

    /// <summary>
    /// Computes speed for each point using Haversine distance between consecutive points.
    /// </summary>
    private void ComputeSpeedsAndDistances(TelemetrySession session)
    {
        if (session.Points.Count < 2)
        {
            return;
        }

        // Sort points by time first
        var sortedPoints = session.Points.OrderBy(p => p.Timestamp).ToList();

        // Calculate total distance
        double totalDistance = 0;
        double maxSpeed = 0;

        for (int i = 0; i < sortedPoints.Count; i++)
        {
            var point = sortedPoints[i];

            if (i > 0)
            {
                var prevPoint = sortedPoints[i - 1];
                
                // Calculate distance using Haversine formula
                var distance = HaversineDistance(
                    prevPoint.Latitude, prevPoint.Longitude,
                    point.Latitude, point.Longitude);
                
                totalDistance += distance;

                // Calculate time difference in seconds
                var timeDiff = (point.Timestamp - prevPoint.Timestamp).TotalSeconds;

                // If speed is not already provided (0), calculate it
                if (point.Speed <= 0 && timeDiff > 0)
                {
                    point.Speed = distance / timeDiff;
                }

                // Calculate bearing
                point.Bearing = CalculateBearing(
                    prevPoint.Latitude, prevPoint.Longitude,
                    point.Latitude, point.Longitude);
            }

            // Track max speed
            if (point.Speed > maxSpeed)
            {
                maxSpeed = point.Speed;
            }
        }

        session.TotalDistanceMeters = totalDistance;
    }

    /// <summary>
    /// Calculates the distance between two coordinates using the Haversine formula.
    /// </summary>
    public static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
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

    /// <summary>
    /// Calculates bearing between two points in degrees.
    /// </summary>
    public static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        var lat1Rad = lat1 * Math.PI / 180;
        var lat2Rad = lat2 * Math.PI / 180;
        var deltaLon = (lon2 - lon1) * Math.PI / 180;

        var y = Math.Sin(deltaLon) * Math.Cos(lat2Rad);
        var x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(deltaLon);

        var bearing = Math.Atan2(y, x) * 180 / Math.PI;
        
        // Normalize to 0-360
        return (bearing + 360) % 360;
    }
}
