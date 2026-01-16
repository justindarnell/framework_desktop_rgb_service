using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FrameworkDesktopRgbService;

public sealed class RgbController
{
    private const int LedCount = 8;
    private const int StartKey = 0;

    public async Task<ApplyResult> ApplyPresetAsync(
        RgbPreset preset,
        CancellationToken cancellationToken)
    {
        var colors = NormalizeColors(preset.Colors);

        var animation = (preset.Animation ?? "Static").Trim();
        if (animation.Equals("GradientSweep", StringComparison.OrdinalIgnoreCase))
        {
            var palette = ExtractPaletteForGradient(colors);
            if (palette.Count < 2)
            {
                return ApplyResult.Failure("Gradient sweep requires at least two colors.");
            }

            return await StartGradientSweepAsync(palette, cancellationToken).ConfigureAwait(false);
        }

        if (animation.Equals("Breathe", StringComparison.OrdinalIgnoreCase))
        {
            var palette = ExtractPaletteForBreathe(colors);
            if (palette.Count == 0)
            {
                return ApplyResult.Failure("Breathe requires at least one non-black color.");
            }

            return await StartBreatheAsync(palette, cancellationToken).ConfigureAwait(false);
        }

        if (colors.Count != LedCount)
        {
            return ApplyResult.Failure($"Preset '{preset.Name}' must contain exactly {LedCount} colors for the Framework Cooler Master ARGB fan.");
        }

        return await ApplyStaticAsync(colors, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ApplyResult> ApplyStaticAsync(List<string> colors, CancellationToken cancellationToken)
    {
        try
        {
            await CrosEcDevice.SetRgbKeyboardColorsAsync(StartKey, colors, cancellationToken).ConfigureAwait(false);
            return ApplyResult.Success();
        }
        catch (OperationCanceledException)
        {
            return ApplyResult.Failure("RGB apply was canceled.");
        }
        catch (Exception ex)
        {
            return ApplyResult.Failure($"EC RGB update failed: {ex.Message}");
        }
    }

    private static async Task<ApplyResult> StartBreatheAsync(List<string> palette, CancellationToken cancellationToken)
    {
        try
        {
            var initialFrame = BuildBreatheFrame(palette, 0.0);
            await CrosEcDevice.SetRgbKeyboardColorsAsync(StartKey, initialFrame, cancellationToken).ConfigureAwait(false);

            _ = Task.Run(async () =>
            {
                var delay = TimeSpan.FromMilliseconds(60);
                var phase = 0.0;
                var phaseStep = 0.05;

                while (!cancellationToken.IsCancellationRequested)
                {
                    var intensity = 0.5 - 0.5 * Math.Cos(2 * Math.PI * phase); // smooth in/out 0..1
                    var frame = BuildBreatheFrame(palette, intensity);
                    await CrosEcDevice.SetRgbKeyboardColorsAsync(StartKey, frame, cancellationToken).ConfigureAwait(false);
                    phase = (phase + phaseStep) % 1.0;
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }, cancellationToken);

            return ApplyResult.Success();
        }
        catch (OperationCanceledException)
        {
            return ApplyResult.Failure("RGB apply was canceled.");
        }
        catch (Exception ex)
        {
            return ApplyResult.Failure($"EC RGB update failed: {ex.Message}");
        }
    }

    private static async Task<ApplyResult> StartGradientSweepAsync(List<string> palette, CancellationToken cancellationToken)
    {
        try
        {
            // Apply the initial frame immediately.
            var initialFrame = BuildGradientFrame(palette, 0);
            await CrosEcDevice.SetRgbKeyboardColorsAsync(StartKey, initialFrame, cancellationToken).ConfigureAwait(false);

            _ = Task.Run(async () =>
            {
                var delay = TimeSpan.FromMilliseconds(120);
                var paletteCount = palette.Count;
                var offset = 0.0;
                var step = Math.Max(0.25, paletteCount / 8.0);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var frame = BuildGradientFrame(palette, offset);
                    await CrosEcDevice.SetRgbKeyboardColorsAsync(StartKey, frame, cancellationToken).ConfigureAwait(false);
                    offset = (offset + step) % paletteCount;
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }, cancellationToken);

            return ApplyResult.Success();
        }
        catch (OperationCanceledException)
        {
            return ApplyResult.Failure("RGB apply was canceled.");
        }
        catch (Exception ex)
        {
            return ApplyResult.Failure($"EC RGB update failed: {ex.Message}");
        }
    }

    private static List<string> NormalizeColors(IEnumerable<string> colors)
    {
        return colors.Select(color =>
        {
            var trimmed = color.Trim();
            if (trimmed.StartsWith('#'))
            {
                trimmed = "0x" + trimmed[1..];
            }

            if (!trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = "0x" + trimmed;
            }

            return trimmed.ToLowerInvariant();
        }).ToList();
    }

    private static List<string> ExtractPaletteForGradient(List<string> colors)
    {
        // If user only picked a couple colors and left the rest black, prefer the non-black palette.
        var nonBlack = colors.Where(c => !IsBlack(c)).ToList();
        if (nonBlack.Count >= 2)
        {
            return nonBlack;
        }

        return colors;
    }

    private static List<string> ExtractPaletteForBreathe(List<string> colors)
    {
        var nonBlack = colors.Where(c => !IsBlack(c)).ToList();
        return nonBlack;
    }

    private static bool IsBlack(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        return r == 0 && g == 0 && b == 0;
    }

    private static (byte r, byte g, byte b) ParseHex(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }
        else if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        var intColor = int.Parse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var r = (byte)((intColor >> 16) & 0xFF);
        var g = (byte)((intColor >> 8) & 0xFF);
        var b = (byte)(intColor & 0xFF);
        return (r, g, b);
    }

    private static string ToHex((byte r, byte g, byte b) c)
    {
        return $"0x{c.r:X2}{c.g:X2}{c.b:X2}".ToLowerInvariant();
    }

    private static (byte r, byte g, byte b) Lerp((byte r, byte g, byte b) a, (byte r, byte g, byte b) b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        var r = (byte)Math.Round(a.r + (b.r - a.r) * t);
        var g = (byte)Math.Round(a.g + (b.g - a.g) * t);
        var bb = (byte)Math.Round(a.b + (b.b - a.b) * t);
        return ((byte)r, (byte)g, bb);
    }

    private static (byte r, byte g, byte b) Scale((byte r, byte g, byte b) c, double factor)
    {
        factor = Math.Clamp(factor, 0, 1);
        return (
            (byte)Math.Round(c.r * factor),
            (byte)Math.Round(c.g * factor),
            (byte)Math.Round(c.b * factor));
    }

    private static List<string> BuildGradientFrame(List<string> palette, double offset)
    {
        var result = new List<string>(LedCount);
        var paletteCount = palette.Count;

        for (var i = 0; i < LedCount; i++)
        {
            var position = (i * (double)paletteCount / LedCount + offset) % paletteCount;
            if (position < 0)
            {
                position += paletteCount;
            }

            var idx = (int)Math.Floor(position);
            var nextIdx = (idx + 1) % paletteCount;
            var t = position - idx;

            var a = ParseHex(palette[idx]);
            var b = ParseHex(palette[nextIdx]);
            var lerped = Lerp(a, b, t);
            result.Add(ToHex(lerped));
        }

        return result;
    }

    private static List<string> BuildBreatheFrame(List<string> palette, double intensity)
    {
        var frame = new List<string>(LedCount);
        var paletteCount = palette.Count;

        for (var i = 0; i < LedCount; i++)
        {
            var color = paletteCount > 0 ? palette[i % paletteCount] : "0x000000";
            var scaled = Scale(ParseHex(color), intensity);
            frame.Add(ToHex(scaled));
        }

        return frame;
    }
}

public sealed record ApplyResult(bool Succeeded, string? ErrorMessage)
{
    public static ApplyResult Success() => new(true, null);
    public static ApplyResult Failure(string message) => new(false, message);
}
