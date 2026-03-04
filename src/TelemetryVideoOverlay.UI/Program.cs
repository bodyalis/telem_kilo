using System.CommandLine;
using System.CommandLine.Invocation;
using TelemetryVideoOverlay.Core.Models;
using TelemetryVideoOverlay.Core.Parsers;
using TelemetryVideoOverlay.Video;

namespace TelemetryVideoOverlay.UI;

/// <summary>
/// CLI application for generating telemetry overlay videos.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        var inputOption = new Option<string>(
            aliases: new[] { "-i", "--input" },
            description: "Input telemetry file path (.gpx or .fit)")
        {
            IsRequired = true
        };

        var outputOption = new Option<string>(
            aliases: new[] { "-o", "--output" },
            description: "Output video file path")
        {
            IsRequired = true
        };

        var widthOption = new Option<int>(
            aliases: new[] { "-w", "--width" },
            description: "Output video width (default: 1920)",
            getDefaultValue: () => 1920);

        var heightOption = new Option<int>(
            aliases: new[] { "-h", "--height" },
            description: "Output video height (default: 1080)",
            getDefaultValue: () => 1080);

        var fpsOption = new Option<double>(
            aliases: new[] { "-f", "--fps" },
            description: "Output frames per second (default: 30)",
            getDefaultValue: () => 30.0);

        var bitrateOption = new Option<int>(
            aliases: new[] { "-b", "--bitrate" },
            description: "Video bitrate in bps (default: 5000000)",
            getDefaultValue: () => 5_000_000);

        var rootCommand = new RootCommand("TelemetryVideoOverlay - Generate videos from GPX/FIT telemetry files")
        {
            inputOption,
            outputOption,
            widthOption,
            heightOption,
            fpsOption,
            bitrateOption
        };

        rootCommand.SetHandler(async (InvocationContext context) =>
        {
            var input = context.ParseResult.GetValueForOption(inputOption)!;
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var width = context.ParseResult.GetValueForOption(widthOption);
            var height = context.ParseResult.GetValueForOption(heightOption);
            var fps = context.ParseResult.GetValueForOption(fpsOption);
            var bitrate = context.ParseResult.GetValueForOption(bitrateOption);

            try
            {
                await GenerateVideoAsync(input, output, width, height, fps, bitrate);
                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error: {ex.Message}");
                context.ExitCode = 1;
            }
        });

        // Add subcommand for listing supported formats
        var listFormatsCommand = new Command("list-formats", "List supported input formats");
        listFormatsCommand.SetHandler(() =>
        {
            Console.WriteLine("Supported input formats:");
            Console.WriteLine("  .gpx - GPX (GPS Exchange Format)");
            Console.WriteLine("  .fit - FIT (Garmin Flexible and Interoperable Data Transfer)");
        });
        
        rootCommand.Add(listFormatsCommand);

        // Add subcommand for info
        var infoCommand = new Command("info", "Show information about a telemetry file");
        var infoInputOption = new Option<string>(
            aliases: new[] { "-i", "--input" },
            description: "Input telemetry file path")
        {
            IsRequired = true
        };
        infoCommand.AddOption(infoInputOption);
        
        infoCommand.SetHandler(async (InvocationContext context) =>
        {
            var input = context.ParseResult.GetValueForOption(infoInputOption)!;
            try
            {
                await ShowFileInfoAsync(input);
                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error: {ex.Message}");
                context.ExitCode = 1;
            }
        });
        
        rootCommand.Add(infoCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task GenerateVideoAsync(
        string inputPath, 
        string outputPath,
        int width, 
        int height, 
        double fps,
        int bitrate)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("         TelemetryVideoOverlay - Video Generator          ");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();

        // Validate input file
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        // Get parser
        var parser = TelemetryParserFactory.GetParser(inputPath);
        if (parser == null)
        {
            var supported = string.Join(", ", TelemetryParserFactory.SupportedExtensions);
            throw new InvalidOperationException($"Unsupported file format. Supported formats: {supported}");
        }

        Console.WriteLine($"Input:   {inputPath}");
        Console.WriteLine($"Output:  {outputPath}");
        Console.WriteLine($"Format:  {Path.GetExtension(inputPath).ToUpper()}");
        Console.WriteLine($"Size:    {width}x{height}");
        Console.WriteLine($"FPS:     {fps}");
        Console.WriteLine($"Bitrate: {bitrate / 1_000_000} Mbps");
        Console.WriteLine();

        // Parse telemetry
        Console.WriteLine("Parsing telemetry data...");
        var session = await parser.ParseAsync(inputPath);
        
        Console.WriteLine($"Session: {session.Name}");
        Console.WriteLine($"Points: {session.PointCount:N0}");
        Console.WriteLine($"Duration: {session.Duration:hh\\:mm\\:ss}");
        Console.WriteLine($"Distance: {session.TotalDistanceMeters / 1000:F2} km");
        Console.WriteLine($"Max Speed: {session.MaxSpeedMetersPerSecond * 3.6:F1} km/h");
        Console.WriteLine($"Altitude: {session.MinAltitudeMeters:F0}m - {session.MaxAltitudeMeters:F0}m");
        Console.WriteLine();

        // Create settings
        var settings = new RenderSettings(width, height, fps, bitrate, -16777216);

        // Generate video
        Console.WriteLine("Generating video...");
        var generator = new VideoGenerator(settings);
        
        generator.Progress += (sender, e) =>
        {
            Console.CursorLeft = 0;
            Console.Write($"Progress: {e.ProgressPercent:F1}% ({e.CurrentFrame}/{e.TotalFrames})");
        };

        await generator.GenerateVideoAsync(session, outputPath);
        
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("Video generation complete!");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
    }

    static async Task ShowFileInfoAsync(string inputPath)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        var parser = TelemetryParserFactory.GetParser(inputPath);
        if (parser == null)
        {
            var supported = string.Join(", ", TelemetryParserFactory.SupportedExtensions);
            throw new InvalidOperationException($"Unsupported file format. Supported formats: {supported}");
        }

        Console.WriteLine($"Parsing: {inputPath}");
        var session = await parser.ParseAsync(inputPath);

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("                    Session Information                     ");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"Name:        {session.Name}");
        if (!string.IsNullOrEmpty(session.Description))
        {
            Console.WriteLine($"Description: {session.Description}");
        }
        Console.WriteLine();
        Console.WriteLine("Statistics:");
        Console.WriteLine($"  Points:        {session.PointCount:N0}");
        Console.WriteLine($"  Duration:      {session.Duration:hh\\:mm\\:ss}");
        Console.WriteLine($"  Distance:      {session.TotalDistanceMeters / 1000:F2} km");
        Console.WriteLine($"  Avg Speed:     {session.AverageSpeedMetersPerSecond * 3.6:F1} km/h");
        Console.WriteLine($"  Max Speed:     {session.MaxSpeedMetersPerSecond * 3.6:F1} km/h");
        Console.WriteLine($"  Min Altitude: {session.MinAltitudeMeters:F0} m");
        Console.WriteLine($"  Max Altitude: {session.MaxAltitudeMeters:F0} m");
        Console.WriteLine($"  Elevation:    +{session.TotalElevationGainMeters:F0}m / -{session.TotalElevationLossMeters:F0}m");
        Console.WriteLine();
        Console.WriteLine("Time Range:");
        Console.WriteLine($"  Start: {session.StartTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  End:   {session.EndTime:yyyy-MM-dd HH:mm:ss}");
    }
}
