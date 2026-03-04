namespace TelemetryVideoOverlay.Core.Models;

/// <summary>
/// Base interface for render settings defining output dimensions and frame rate.
/// </summary>
public interface IRenderSettings
{
    /// <summary>
    /// Output width in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Output height in pixels.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Frames per second for the output.
    /// </summary>
    double Fps { get; }

    /// <summary>
    /// Output video bitrate in bits per second.
    /// </summary>
    int Bitrate { get; }

    /// <summary>
    /// Background color as ARGB integer.
    /// </summary>
    int BackgroundColor { get; }
}

/// <summary>
/// Default implementation of render settings.
/// </summary>
public class RenderSettings : IRenderSettings
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public double Fps { get; set; } = 30.0;
    public int Bitrate { get; set; } = 5_000_000; // 5 Mbps
    public int BackgroundColor { get; set; } = -16777216; // Black (ARGB)

    public RenderSettings()
    {
    }

    public RenderSettings(int width, int height, double fps)
    {
        Width = width;
        Height = height;
        Fps = fps;
    }

    public RenderSettings(int width, int height, double fps, int bitrate, int backgroundColor)
    {
        Width = width;
        Height = height;
        Fps = fps;
        Bitrate = bitrate;
        BackgroundColor = backgroundColor;
    }

    /// <summary>
    /// Creates a copy of these settings.
    /// </summary>
    public IRenderSettings Clone()
    {
        return new RenderSettings(Width, Height, Fps, Bitrate, BackgroundColor);
    }
}
