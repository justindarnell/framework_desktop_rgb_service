using System.Diagnostics;

namespace FrameworkDesktopRgbService;

public sealed class RgbController
{
    private const int LedCount = 8;

    public async Task<ApplyResult> ApplyPresetAsync(
        string toolPath,
        RgbPreset preset,
        bool requireElevation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toolPath))
        {
            return ApplyResult.Failure("The path to 'framework_tool' must be a non-empty string.");
        }

        var colors = NormalizeColors(preset.Colors);
        if (colors.Count != LedCount)
        {
            return ApplyResult.Failure($"Preset '{preset.Name}' must contain exactly {LedCount} colors for the Framework Cooler Master ARGB fan.");
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

            if (!requireElevation)
            {
                // Start reading streams immediately to prevent buffer overflow
                // Use CancellationToken.None to ensure we read all output even if cancelled
                var stdOutTask = process.StandardOutput.ReadToEndAsync();
                var stdErrTask = process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    var error = await stdErrTask;
                    var output = await stdOutTask;
                    var errorDetails = string.Join(Environment.NewLine, new[] { error, output }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    return ApplyResult.Failure($"framework_tool exited with code {process.ExitCode}.{(string.IsNullOrWhiteSpace(errorDetails) ? string.Empty : $" {errorDetails}")}");
                }
            }
            else
            {
                await process.WaitForExitAsync(cancellationToken);
                if (process.ExitCode != 0)
                {
                    return ApplyResult.Failure($"framework_tool exited with code {process.ExitCode}.");
                }
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
