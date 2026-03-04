using SkiaSharp;
using TelemetryVideoOverlay.Core.MathEngine;
using TelemetryVideoOverlay.Core.Models;
using TelemetryVideoOverlay.Graphics;

namespace TelemetryVideoOverlay.Video;

/// <summary>
/// Generates video from telemetry data using SkiaSharp for frame rendering.
/// </summary>
public class VideoGenerator
{
    private readonly IRenderSettings _settings;
    private readonly TelemetryRenderer _renderer;
    private readonly LinearInterpolator _interpolator;

    public event EventHandler<VideoGenerationProgressEventArgs>? Progress;

    public VideoGenerator(IRenderSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _renderer = new TelemetryRenderer(settings);
        _interpolator = new LinearInterpolator();
    }

    /// <summary>
    /// Generates a video from a telemetry session.
    /// </summary>
    /// <param name="session">The telemetry session to render.</param>
    /// <param name="outputPath">Output video file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task GenerateVideoAsync(
        TelemetrySession session, 
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        // Interpolate telemetry points to match frame rate
        var framePoints = _interpolator.Interpolate(session, _settings.Fps);
        
        var totalFrames = framePoints.Count;
        var duration = TimeSpan.FromSeconds(totalFrames / _settings.Fps);

        Console.WriteLine($"Generating video: {totalFrames} frames at {_settings.Fps} fps");
        Console.WriteLine($"Duration: {duration:hh\\:mm\\:ss}");
        Console.WriteLine($"Output: {outputPath}");

        // For now, we'll generate individual PNG frames
        // In a production app, you'd use FFmpeg or a native video encoder
        var framesDir = Path.Combine(Path.GetTempPath(), "telemetry_frames_" + Guid.NewGuid());
        Directory.CreateDirectory(framesDir);

        try
        {
            for (int i = 0; i < framePoints.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var point = framePoints[i];
                var frameTime = TimeSpan.FromSeconds(i / _settings.Fps);

                // Render frame
                using var bitmap = _renderer.RenderFrame(point, i, frameTime);
                
                // Save as PNG
                var framePath = Path.Combine(framesDir, $"frame_{i:D8}.png");
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(framePath);
                data.SaveTo(stream);

                // Report progress
                if (i % 10 == 0 || i == framePoints.Count - 1)
                {
                    var progress = (double)(i + 1) / framePoints.Count * 100;
                    Progress?.Invoke(this, new VideoGenerationProgressEventArgs(i + 1, totalFrames, progress));
                    Console.Write($"\rProgress: {progress:F1}% ({i + 1}/{totalFrames})");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Frames saved to: {framesDir}");
            
            // Try to encode video using FFmpeg if available
            await TryEncodeVideoAsync(framesDir, outputPath, cancellationToken);
        }
        finally
        {
            // Cleanup temp frames
            if (Directory.Exists(framesDir))
            {
                try
                {
                    Directory.Delete(framesDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    /// <summary>
    /// Tries to encode video using FFmpeg if available.
    /// </summary>
    private async Task TryEncodeVideoAsync(string framesDir, string outputPath, CancellationToken cancellationToken)
    {
        var ffmpegPath = FindFFmpeg();
        
        if (string.IsNullOrEmpty(ffmpegPath))
        {
            Console.WriteLine("FFmpeg not found. Frames saved but video not encoded.");
            Console.WriteLine($"You can manually encode with: ffmpeg -framerate 30 -i {framesDir}/frame_%08d.png -c:v libx264 -pix_fmt yuv420p {outputPath}");
            return;
        }

        Console.WriteLine("Encoding video with FFmpeg...");

        var args = $"-framerate {_settings.Fps} -i \"{framesDir}/frame_%08d.png\" -c:v libx264 -pix_fmt yuv420p -crf 18 \"{outputPath}\"";
        
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode == 0)
        {
            Console.WriteLine($"Video saved to: {outputPath}");
        }
        else
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            Console.WriteLine($"FFmpeg encoding failed: {error}");
        }
    }

    /// <summary>
    /// Finds FFmpeg in PATH or common locations.
    /// </summary>
    private string? FindFFmpeg()
    {
        // Check PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var path in pathEnv.Split(Path.PathSeparator))
            {
                var ffmpegPath = Path.Combine(path, "ffmpeg");
                if (File.Exists(ffmpegPath))
                    return ffmpegPath;
                if (File.Exists(ffmpegPath + ".exe"))
                    return ffmpegPath + ".exe";
            }
        }

        // Check common locations on Windows
        if (OperatingSystem.IsWindows())
        {
            var commonPaths = new[]
            {
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe"
            };
            
            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                    return path;
            }
        }

        return null;
    }
}

/// <summary>
/// Event args for video generation progress.
/// </summary>
public class VideoGenerationProgressEventArgs : EventArgs
{
    public int CurrentFrame { get; }
    public int TotalFrames { get; }
    public double ProgressPercent { get; }

    public VideoGenerationProgressEventArgs(int currentFrame, int totalFrames, double progressPercent)
    {
        CurrentFrame = currentFrame;
        TotalFrames = totalFrames;
        ProgressPercent = progressPercent;
    }
}
