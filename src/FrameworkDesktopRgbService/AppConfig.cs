using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrameworkDesktopRgbService;

public sealed class AppConfig
{
    public string FrameworkToolPath { get; set; } = "framework_tool.exe";
    public string LastPresetName { get; set; } = "Default";
    public int RetryCount { get; set; } = 5;
    public int RetryDelaySeconds { get; set; } = 3;
    public bool RequireElevation { get; set; } = true;
    public List<RgbPreset> Presets { get; set; } = new()
    {
        new RgbPreset
        {
            Name = "Default",
            Colors = new List<string>
            {
                "0xff0000",
                "0xff8000",
                "0xffff00",
                "0x00ff00",
                "0x00ffff",
                "0x0000ff",
                "0x8000ff",
                "0xff00ff",
            },
        },
    };

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed class RgbPreset
{
    public string Name { get; set; } = string.Empty;
    public List<string> Colors { get; set; } = new();
}
