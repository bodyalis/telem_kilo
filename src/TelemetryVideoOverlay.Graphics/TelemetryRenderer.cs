using SkiaSharp;
using TelemetryVideoOverlay.Core.Models;

namespace TelemetryVideoOverlay.Graphics;

/// <summary>
/// Renders telemetry data as images for video overlay.
/// </summary>
public class TelemetryRenderer
{
    private readonly IRenderSettings _settings;
    
    // Default styling
    private SKColor _textColor = SKColors.White;
    private SKColor _backgroundColor = new SKColor(0, 0, 0, 180);
    private SKColor _accentColor = new SKColor(0, 200, 255);
    private SKColor _warningColor = new SKColor(255, 200, 0);
    private SKColor _dangerColor = new SKColor(255, 50, 50);
    private float _fontSize = 32;
    private float _padding = 20;
    private float _cornerRadius = 10;

    public TelemetryRenderer(IRenderSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Renders a telemetry overlay frame.
    /// </summary>
    public SKBitmap RenderFrame(TelemetryPoint point, int frameNumber, TimeSpan frameTime)
    {
        var bitmap = new SKBitmap(_settings.Width, _settings.Height);
        using var canvas = new SKCanvas(bitmap);
        
        // Clear background
        canvas.Clear(new SKColor((uint)_settings.BackgroundColor));

        // Draw the telemetry overlay panels
        DrawInfoPanel(canvas, point, frameTime);
        DrawSpeedPanel(canvas, point);
        DrawStatsPanel(canvas, point);

        return bitmap;
    }

    /// <summary>
    /// Draws the main info panel (time, coordinates).
    /// </summary>
    private void DrawInfoPanel(SKCanvas canvas, TelemetryPoint point, TimeSpan time)
    {
        var panelWidth = 350f;
        var panelHeight = 180f;
        var x = _padding;
        var y = _padding;

        DrawPanel(canvas, x, y, panelWidth, panelHeight);

        using var titlePaint = CreateTextPaint(SKColors.Gray, _fontSize * 0.75f);
        using var valuePaint = CreateTextPaint(_textColor, _fontSize * 1.2f);
        using var smallPaint = CreateTextPaint(_accentColor, _fontSize * 0.9f);

        // Title
        canvas.DrawText("TELEMETRY", x + _padding, y + _padding + titlePaint.TextSize, titlePaint);

        // Time
        canvas.DrawText(time.ToString(@"hh\:mm\:ss"), x + _padding, y + _padding + titlePaint.TextSize + 10 + valuePaint.TextSize, valuePaint);

        // Coordinates
        var latStr = $"{Math.Abs(point.Latitude):F6}° {(point.Latitude >= 0 ? 'N' : 'S')}";
        var lonStr = $"{Math.Abs(point.Longitude):F6}° {(point.Longitude >= 0 ? 'E' : 'W')}";
        
        canvas.DrawText($"Lat: {latStr}", x + _padding, y + _padding + titlePaint.TextSize + 10 + valuePaint.TextSize + 15 + smallPaint.TextSize, smallPaint);
        canvas.DrawText($"Lon: {lonStr}", x + _padding, y + _padding + titlePaint.TextSize + 10 + valuePaint.TextSize + 30 + smallPaint.TextSize, smallPaint);
    }

    /// <summary>
    /// Draws the speed panel with speedometer-style display.
    /// </summary>
    private void DrawSpeedPanel(SKCanvas canvas, TelemetryPoint point)
    {
        var panelWidth = 280f;
        var panelHeight = 280f;
        var x = _settings.Width - panelWidth - _padding;
        var y = _settings.Height - panelHeight - _padding;

        DrawPanel(canvas, x, y, panelWidth, panelHeight);

        // Speed in km/h
        var speedKmh = point.Speed * 3.6;
        
        using var speedPaint = CreateTextPaint(_textColor, _fontSize * 2.5f);
        using var unitPaint = CreateTextPaint(_accentColor, _fontSize * 1.0f);
        using var labelPaint = CreateTextPaint(SKColors.Gray, _fontSize * 0.75f);

        // Draw speed value
        var speedStr = $"{speedKmh:F1}";
        var speedBounds = new SKRect();
        speedPaint.MeasureText(speedStr, ref speedBounds);
        
        var centerX = x + panelWidth / 2;
        var centerY = y + panelHeight / 2 + 20;

        canvas.DrawText(speedStr, centerX - speedBounds.Width / 2, centerY, speedPaint);
        canvas.DrawText("km/h", centerX - unitPaint.MeasureText("km/h") / 2, centerY + unitPaint.TextSize + 10, unitPaint);
        
        // Draw max speed indicator
        var maxSpeedKmh = (int)(speedKmh * 1.2); // Add some headroom
        canvas.DrawText($"MAX: {maxSpeedKmh} km/h", x + _padding, y + panelHeight - _padding, labelPaint);

        // Draw arc indicator
        DrawSpeedArc(canvas, x + panelWidth / 2, y + 80, 60, speedKmh, maxSpeedKmh);
    }

    /// <summary>
    /// Draws a speed arc indicator.
    /// </summary>
    private void DrawSpeedArc(SKCanvas canvas, float centerX, float centerY, float radius, double currentSpeed, double maxSpeed)
    {
        var arcPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 8,
            IsAntialias = true,
            Color = _backgroundColor
        };

        var speedPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 8,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        // Background arc (gray)
        var bgRect = new SKRect(centerX - radius, centerY - radius, centerX + radius, centerY + radius);
        canvas.DrawArc(bgRect, 135, 270, false, arcPaint);

        // Speed arc (colored based on speed)
        var speedRatio = Math.Min(currentSpeed / maxSpeed, 1.0);
        
        if (speedRatio > 0.85)
            speedPaint.Color = _dangerColor;
        else if (speedRatio > 0.65)
            speedPaint.Color = _warningColor;
        else
            speedPaint.Color = _accentColor;

        var sweepAngle = (float)(speedRatio * 270);
        canvas.DrawArc(bgRect, 135, sweepAngle, false, speedPaint);
    }

    /// <summary>
    /// Draws the statistics panel (altitude, distance).
    /// </summary>
    private void DrawStatsPanel(SKCanvas canvas, TelemetryPoint point)
    {
        var panelWidth = 350f;
        var panelHeight = 150f;
        var x = _settings.Width - panelWidth - _padding;
        var y = _padding;

        DrawPanel(canvas, x, y, panelWidth, panelHeight);

        using var labelPaint = CreateTextPaint(SKColors.Gray, _fontSize * 0.75f);
        using var valuePaint = CreateTextPaint(_textColor, _fontSize * 1.1f);
        using var unitPaint = CreateTextPaint(_accentColor, _fontSize * 0.8f);

        // Altitude
        var altStr = $"{point.Altitude:F0} m";
        canvas.DrawText("ALTITUDE", x + _padding, y + _padding + labelPaint.TextSize, labelPaint);
        canvas.DrawText(altStr, x + _padding, y + _padding + labelPaint.TextSize + 10 + valuePaint.TextSize, valuePaint);

        // Distance (estimated based on cumulative distance calculation)
        // For now, we'll show a placeholder
        var distKm = 0.0; // Would need session to calculate
        var distStr = $"{distKm:F2} km";
        canvas.DrawText("DISTANCE", x + panelWidth / 2 + _padding / 2, y + _padding + labelPaint.TextSize, labelPaint);
        canvas.DrawText(distStr, x + panelWidth / 2 + _padding / 2, y + _padding + labelPaint.TextSize + 10 + valuePaint.TextSize, valuePaint);
    }

    /// <summary>
    /// Draws a rounded panel background.
    /// </summary>
    private void DrawPanel(SKCanvas canvas, float x, float y, float width, float height)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = _backgroundColor,
            IsAntialias = true
        };

        var rect = new SKRoundRect(new SKRect(x, y, x + width, y + height), _cornerRadius);
        canvas.DrawRoundRect(rect, paint);
    }

    /// <summary>
    /// Creates a text paint with specified color and size.
    /// </summary>
    private SKPaint CreateTextPaint(SKColor color, float textSize)
    {
        return new SKPaint
        {
            Color = color,
            TextSize = textSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };
    }

    /// <summary>
    /// Sets custom styling.
    /// </summary>
    public TelemetryRenderer WithTextColor(SKColor color)
    {
        _textColor = color;
        return this;
    }

    public TelemetryRenderer WithBackgroundColor(SKColor color)
    {
        _backgroundColor = color;
        return this;
    }

    public TelemetryRenderer WithAccentColor(SKColor color)
    {
        _accentColor = color;
        return this;
    }

    public TelemetryRenderer WithFontSize(float size)
    {
        _fontSize = size;
        return this;
    }
}
