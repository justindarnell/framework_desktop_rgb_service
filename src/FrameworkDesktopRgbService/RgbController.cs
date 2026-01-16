using System.Diagnostics;

namespace FrameworkDesktopRgbService;

public sealed class RgbController
{
    public async Task<ApplyResult> ApplyPresetAsync(
        string toolPath,
        RgbPreset preset,
        bool requireElevation,
        CancellationToken cancellationToken)
    {
        var colors = NormalizeColors(preset.Colors);
        if (colors.Count != 8)
        {
            return ApplyResult.Failure($"Preset '{preset.Name}' must contain exactly 8 colors for the Framework Cooler Master ARGB fan.");
        }

        var args = string.Join(' ', new[] { "--rgbkbd", "0" }.Concat(colors));
        var startInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            Arguments = args,
            UseShellExecute = requireElevation,
            CreateNoWindow = !requireElevation,
            RedirectStandardOutput = !requireElevation,
            RedirectStandardError = !requireElevation,
            Verb = requireElevation ? "runas" : string.Empty,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return ApplyResult.Failure("Failed to start framework_tool process.");
            }

            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                if (!requireElevation)
                {
                    var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                    var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                    return ApplyResult.Failure($"framework_tool exited with code {process.ExitCode}. {error}{output}");
                }

                return ApplyResult.Failure($"framework_tool exited with code {process.ExitCode}.");
            }

            return ApplyResult.Success();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return ApplyResult.Failure("Elevation was canceled.");
        }
        catch (Exception ex)
        {
            return ApplyResult.Failure($"framework_tool failed: {ex.Message}");
        }
    }

    private static List<string> NormalizeColors(IEnumerable<string> colors)
    {
        var normalized = new List<string>();
        foreach (var color in colors)
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

            normalized.Add(trimmed.ToLowerInvariant());
        }

        return normalized;
    }
}

public sealed record ApplyResult(bool Succeeded, string? ErrorMessage)
{
    public static ApplyResult Success() => new(true, null);
    public static ApplyResult Failure(string message) => new(false, message);
}
