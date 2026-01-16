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

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, AppConfig.JsonOptions);
            var result = config ?? new AppConfig();
            result.Validate();
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidOperationException($"Failed to load configuration from '{ConfigPath}'.", ex);
        }
    }

    public void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var json = JsonSerializer.Serialize(config, AppConfig.JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to save configuration to '{ConfigPath}'.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Access denied while saving configuration to '{ConfigPath}'.", ex);
        }
    }
}
