using System.Text.Json;

namespace FrameworkDesktopRgbService;

public sealed class ConfigService
{
    private const string ConfigFileName = "config.json";

    public string ConfigDirectory { get; }
    public string ConfigPath { get; }

    public ConfigService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        ConfigDirectory = Path.Combine(appData, "FrameworkDesktopRgbService");
        ConfigPath = Path.Combine(ConfigDirectory, ConfigFileName);
    }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var defaultConfig = new AppConfig();
            Save(defaultConfig);
            return defaultConfig;
        }

        var json = File.ReadAllText(ConfigPath);
        var config = JsonSerializer.Deserialize<AppConfig>(json, AppConfig.JsonOptions);
        return config ?? new AppConfig();
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(config, AppConfig.JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
