using System.Text;
using TelemetryVideoOverlay.Core.Models;

namespace TelemetryVideoOverlay.Core.Parsers;

/// <summary>
/// Parser for FIT (Flexible and Interoperable Data Transfer) files used by Garmin devices.
/// </summary>
public class FitParser : ITelemetryParser
{
    public IReadOnlyList<string> SupportedExtensions { get; } = new List<string> { ".fit" };

    // FIT file constants
    private const int FitHeaderSize = 14;
    private const int FitSignature = 0x5449462E; // ".FIT"

    // FIT message types
    private const ushort FileIdMsg = 0;
    private const ushort FileCreatorMsg = 1;
    private const ushort EventMsg = 21;
    private const ushort RecordMsg = 20;
    private const ushort LapMsg = 19;
    private const ushort SessionMsg = 18;
    private const ushort ActivityMsg = 34;

    // FIT field definitions for Record message
    private const byte FieldTimestamp = 253;
    private const byte FieldPositionLat = 0;
    private const byte FieldPositionLong = 1;
    private const byte FieldAltitude = 2;
    private const byte FieldHeartRate = 3;
    private const byte FieldCadence = 4;
    private const byte FieldDistance = 5;
    private const byte FieldSpeed = 6;
    private const byte FieldPower = 7;
    private const byte FieldCompressedSpeedDistance = 8;
    private const byte FieldGrade = 9;
    private const byte FieldResistance = 10;
    private const byte FieldTempo = 13;

    public bool CanParse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        return extension == ".fit";
    }

    public async Task<TelemetrySession> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("FIT file not found.", filePath);
        }

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        return ParseFitFile(bytes, Path.GetFileNameWithoutExtension(filePath));
    }

    private TelemetrySession ParseFitFile(byte[] data, string fileName)
    {
        if (data.Length < FitHeaderSize)
        {
            throw new FormatException("FIT file is too small to be valid.");
        }

        // Parse FIT header
        var headerSize = data[0];
        if (headerSize < 12 || headerSize > FitHeaderSize)
        {
            throw new FormatException($"Invalid FIT header size: {headerSize}");
        }

        // Check for FIT signature
        var dataType = Encoding.ASCII.GetString(data, 8, 4);
        if (dataType != ".FIT")
        {
            throw new FormatException("Invalid FIT file signature.");
        }

        // Skip header and parse data
        int dataOffset = headerSize;
        var points = new List<TelemetryPoint>();

        try
        {
            while (dataOffset < data.Length - 2)
            {
                var (messageType, fields) = ReadMessage(data, dataOffset);
                dataOffset = fields.Count > 0 ? (int)(dataOffset + 10) : (int)(dataOffset + 2);
                
                if (messageType == RecordMsg)
                {
                    var point = ParseRecordMessage(fields);
                    if (point != null)
                    {
                        points.Add(point);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Continue with what we have if parsing fails partially
            System.Diagnostics.Debug.WriteLine($"Warning: Error parsing FIT file: {ex.Message}");
        }

        var session = new TelemetrySession
        {
            Name = fileName,
            Description = "Imported from FIT file"
        };

        session.Points = points;
        session.ComputeMetadata();

        return session;
    }

    private (ushort messageType, Dictionary<byte, object?> fields) ReadMessage(byte[] data, int offset)
    {
        var messageType = (ushort)(data[offset] | (data[offset + 1] << 8));
        var newOffset = offset + 2;

        var localMessageType = (byte)(messageType & 0x00FF);
        messageType = (ushort)((messageType >> 8) & 0x00FF);

        var fields = new Dictionary<byte, object?>();

        while (newOffset < data.Length - 2)
        {
            var fieldDefNum = data[newOffset];
            var fieldSize = data[newOffset + 1];
            var baseType = data[newOffset + 2];

            var currentOffset = newOffset + 3;

            if (fieldDefNum == 0xFF || fieldSize == 0 || currentOffset + fieldSize > data.Length)
            {
                break;
            }

            var fieldValue = ReadFieldValue(data, currentOffset, fieldSize, baseType);
            fields[fieldDefNum] = fieldValue;
            newOffset = currentOffset + fieldSize;
        }

        // Align to next record
        if (newOffset % 4 != 0)
        {
            newOffset += 4 - (newOffset % 4);
        }

        return (messageType, fields);
    }

    private object? ReadFieldValue(byte[] data, int offset, int size, byte baseType)
    {
        object? value = null;

        switch (baseType)
        {
            case 0x01: // enum
            case 0x02: // sint8
                value = (sbyte)data[offset];
                offset += 1;
                break;
            case 0x03: // uint8
                value = data[offset];
                offset += 1;
                break;
            case 0x04: // sint16
                value = BitConverter.ToInt16(data, offset);
                offset += 2;
                break;
            case 0x05: // uint16
                value = BitConverter.ToUInt16(data, offset);
                offset += 2;
                break;
            case 0x06: // sint32
                value = BitConverter.ToInt32(data, offset);
                offset += 4;
                break;
            case 0x07: // uint32
                value = BitConverter.ToUInt32(data, offset);
                offset += 4;
                break;
            case 0x08: // string
                value = Encoding.ASCII.GetString(data, offset, size).TrimEnd('\0');
                offset += size;
                break;
            case 0x09: // sint64
                value = BitConverter.ToInt64(data, offset);
                offset += 8;
                break;
            case 0x0A: // uint64
                value = BitConverter.ToUInt64(data, offset);
                offset += 8;
                break;
            case 0x0B: // float32
                value = BitConverter.ToSingle(data, offset);
                offset += 4;
                break;
            case 0x0C: // float64
                value = BitConverter.ToDouble(data, offset);
                offset += 8;
                break;
            default:
                offset += size;
                break;
        }

        return value;
    }

    private TelemetryPoint? ParseRecordMessage(Dictionary<byte, object?> fields)
    {
        TelemetryPoint? point = null;

        // Try to extract timestamp
        DateTime timestamp = DateTime.MinValue;
        if (fields.TryGetValue(FieldTimestamp, out var tsValue) && tsValue != null)
        {
            if (tsValue is uint unixTimestamp)
            {
                // FIT timestamps are Unix timestamps starting from UTC 00:00 Dec 31 1989
                var fitEpoch = new DateTime(1989, 12, 31, 0, 0, 0, DateTimeKind.Utc);
                timestamp = fitEpoch.AddSeconds(unixTimestamp).ToLocalTime();
            }
        }

        // Try to extract position
        double? latitude = null;
        double? longitude = null;

        if (fields.TryGetValue(FieldPositionLat, out var latValue) && latValue != null)
        {
            if (latValue is int latSemicircles)
            {
                latitude = latSemicircles * (180.0 / Math.Pow(2, 31));
            }
        }

        if (fields.TryGetValue(FieldPositionLong, out var lonValue) && lonValue != null)
        {
            if (lonValue is int lonSemicircles)
            {
                longitude = lonSemicircles * (180.0 / Math.Pow(2, 31));
            }
        }

        // Only create point if we have valid position data
        if (latitude.HasValue && longitude.HasValue)
        {
            point = new TelemetryPoint
            {
                Latitude = latitude.Value,
                Longitude = longitude.Value,
                Timestamp = timestamp == DateTime.MinValue ? DateTime.Now : timestamp
            };

            // Extract altitude (in meters, needs scaling)
            if (fields.TryGetValue(FieldAltitude, out var altValue) && altValue != null)
            {
                if (altValue is ushort altRaw)
                {
                    // Altitude is stored in 5 meters units, need to convert
                    point.Altitude = altRaw * 5.0 - 500;
                }
                else if (altValue is int altInt)
                {
                    point.Altitude = altInt * 0.005;
                }
            }

            // Extract speed (in m/s * 1000)
            if (fields.TryGetValue(FieldSpeed, out var speedValue) && speedValue != null)
            {
                if (speedValue is ushort speedRaw)
                {
                    point.Speed = speedRaw / 1000.0;
                }
                else if (speedValue is int speedInt)
                {
                    point.Speed = speedInt * 0.001;
                }
            }

            // Extract distance (in meters * 100)
            if (fields.TryGetValue(FieldDistance, out var distValue) && distValue != null)
            {
                // Distance can be used to calculate speed if not provided
                if (point.Speed <= 0 && distValue is ushort distRaw)
                {
                    // We'll calculate speed from distance differences later
                }
            }
        }

        return point;
    }
}
