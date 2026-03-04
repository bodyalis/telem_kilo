using System.Reflection;

namespace TelemetryVideoOverlay.Core.Parsers;

/// <summary>
/// Factory for creating telemetry parsers based on file extension.
/// </summary>
public static class TelemetryParserFactory
{
    private static readonly List<ITelemetryParser> _parsers = new()
    {
        new GpxParser(),
        new FitParser()
    };

    /// <summary>
    /// Gets all available parsers.
    /// </summary>
    public static IReadOnlyList<ITelemetryParser> AllParsers => _parsers;

    /// <summary>
    /// Creates a parser that can handle the specified file.
    /// </summary>
    /// <param name="filePath">Path to the telemetry file.</param>
    /// <returns>A parser that can handle the file, or null if none found.</returns>
    public static ITelemetryParser? GetParser(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        return _parsers.FirstOrDefault(p => p.CanParse(filePath));
    }

    /// <summary>
    /// Gets all supported file extensions.
    /// </summary>
    public static IReadOnlyList<string> SupportedExtensions 
    {
        get
        {
            var extensions = new List<string>();
            foreach (var parser in _parsers)
            {
                extensions.AddRange(parser.SupportedExtensions);
            }
            return extensions.Distinct().ToList();
        }
    }

    /// <summary>
    /// Checks if a file type is supported.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>True if the file type is supported.</returns>
    public static bool IsSupported(string filePath)
    {
        return GetParser(filePath) != null;
    }
}
