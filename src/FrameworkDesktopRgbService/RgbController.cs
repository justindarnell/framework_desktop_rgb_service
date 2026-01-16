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
        if (colors.Count != LedCount)
        {
            return ApplyResult.Failure($"Preset '{preset.Name}' must contain exactly {LedCount} colors for the Framework Cooler Master ARGB fan.");
        }

        try
        {
            await CrosEcDevice.SetRgbKeyboardColorsAsync(StartKey, colors, cancellationToken);

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
}

public sealed record ApplyResult(bool Succeeded, string? ErrorMessage)
{
    public static ApplyResult Success() => new(true, null);
    public static ApplyResult Failure(string message) => new(false, message);
}
