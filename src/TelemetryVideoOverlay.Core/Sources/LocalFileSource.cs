using TelemetryVideoOverlay.Core.Models;
using TelemetryVideoOverlay.Core.Parsers;

namespace TelemetryVideoOverlay.Core.Sources;

/// <summary>
/// Local file-based telemetry source that uses parsers to read telemetry data.
/// </summary>
public class LocalFileSource : ITelemetrySource
{
    private readonly ITelemetryParser _parser;
    private string? _filePath;
    private bool _isOpen;

    public string SourceName => "LocalFile";
    
    public bool IsAvailable => _isOpen && !string.IsNullOrEmpty(_filePath) && File.Exists(_filePath);

    public IReadOnlyList<string> SupportedExtensions => _parser.SupportedExtensions;

    public LocalFileSource(ITelemetryParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Opens a telemetry file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the telemetry file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task OpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Telemetry file not found.", filePath);
        }

        _filePath = filePath;
        _isOpen = true;
        
        // Allow any async initialization if needed
        await Task.CompletedTask;
    }

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            throw new InvalidOperationException("No file path specified. Call OpenAsync(string filePath) first.");
        }
        _isOpen = true;
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        _isOpen = false;
        _filePath = null;
        return Task.CompletedTask;
    }

    public async Task<TelemetrySession> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Source is not available. Call OpenAsync first.");
        }

        if (string.IsNullOrEmpty(_filePath))
        {
            throw new InvalidOperationException("No file path specified.");
        }

        return await _parser.ParseAsync(_filePath, cancellationToken);
    }

    /// <summary>
    /// Reads telemetry data from a specific file path (convenience method).
    /// </summary>
    /// <param name="filePath">Path to the telemetry file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A telemetry session containing the data.</returns>
    public async Task<TelemetrySession> ReadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await OpenAsync(filePath, cancellationToken);
        return await ReadAllAsync(cancellationToken);
    }
}
