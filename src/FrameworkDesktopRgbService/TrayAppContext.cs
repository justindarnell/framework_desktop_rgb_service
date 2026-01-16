using System.Diagnostics;
using System.Drawing;

namespace FrameworkDesktopRgbService;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ConfigService _configService;
    private readonly RgbController _rgbController;
    private AppConfig _config;
    private CancellationTokenSource? _startupCts;

    public TrayAppContext()
    {
        _configService = new ConfigService();
        _rgbController = new RgbController();
        _config = _configService.Load();

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Framework Desktop RGB",
            ContextMenuStrip = BuildMenu(),
        };

        ApplyLastPresetWithRetry();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var presetMenu = new ToolStripMenuItem("Presets");
        foreach (var preset in _config.Presets)
        {
            var item = new ToolStripMenuItem(preset.Name)
            {
                Checked = string.Equals(preset.Name, _config.LastPresetName, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += async (_, _) => await ApplyPresetAsync(preset, updateLast: true);
            presetMenu.DropDownItems.Add(item);
        }

        var openConfig = new ToolStripMenuItem("Open Config Folder");
        openConfig.Click += (_, _) => OpenConfigFolder();

        var reloadConfig = new ToolStripMenuItem("Reload Config");
        reloadConfig.Click += (_, _) => ReloadConfig();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();

        menu.Items.Add(presetMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(openConfig);
        menu.Items.Add(reloadConfig);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    private async void ApplyLastPresetWithRetry()
    {
        _startupCts?.Cancel();
        _startupCts = new CancellationTokenSource();

        var preset = _config.Presets.FirstOrDefault(p =>
            string.Equals(p.Name, _config.LastPresetName, StringComparison.OrdinalIgnoreCase));

        if (preset is null)
        {
            Notify("RGB preset not found", "Last preset name does not match any configured preset.", ToolTipIcon.Warning);
            return;
        }

        var attempts = Math.Max(1, _config.RetryCount);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var result = await _rgbController.ApplyPresetAsync(_config.FrameworkToolPath, preset, _startupCts.Token);
            if (result.Succeeded)
            {
                return;
            }

            if (attempt == attempts)
            {
                Notify("RGB apply failed", result.ErrorMessage ?? "Unknown error.", ToolTipIcon.Error);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _config.RetryDelaySeconds)), _startupCts.Token);
        }
    }

    private async Task ApplyPresetAsync(RgbPreset preset, bool updateLast)
    {
        var result = await _rgbController.ApplyPresetAsync(_config.FrameworkToolPath, preset, CancellationToken.None);
        if (!result.Succeeded)
        {
            Notify("RGB apply failed", result.ErrorMessage ?? "Unknown error.", ToolTipIcon.Error);
            return;
        }

        if (updateLast)
        {
            _config.LastPresetName = preset.Name;
            _configService.Save(_config);
        }

        UpdateMenuChecks();
    }

    private void UpdateMenuChecks()
    {
        if (_trayIcon.ContextMenuStrip is null)
        {
            return;
        }

        foreach (ToolStripMenuItem item in _trayIcon.ContextMenuStrip.Items)
        {
            if (item.Text != "Presets")
            {
                continue;
            }

            foreach (ToolStripMenuItem presetItem in item.DropDownItems)
            {
                presetItem.Checked = string.Equals(presetItem.Text, _config.LastPresetName, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private void OpenConfigFolder()
    {
        Directory.CreateDirectory(_configService.ConfigDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _configService.ConfigDirectory,
            UseShellExecute = true,
        });
    }

    private void ReloadConfig()
    {
        _config = _configService.Load();
        _trayIcon.ContextMenuStrip = BuildMenu();
        ApplyLastPresetWithRetry();
    }

    private void Notify(string title, string message, ToolTipIcon icon)
    {
        _trayIcon.ShowBalloonTip(4000, title, message, icon);
    }

    protected override void ExitThreadCore()
    {
        _startupCts?.Cancel();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }
}
