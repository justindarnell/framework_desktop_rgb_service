using System.Diagnostics;
using System.Drawing;

namespace FrameworkDesktopRgbService;

public sealed class TrayAppContext : ApplicationContext
{
    private const int BalloonTipTimeout = 4000;

    private readonly NotifyIcon _trayIcon;
    private readonly ConfigService _configService;
    private readonly RgbController _rgbController;
    private readonly object _configLock = new();
    private readonly object _ctsLock = new();
    private AppConfig _config;
    private CancellationTokenSource? _startupCts;

    public TrayAppContext()
    {
        try
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
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize Framework Desktop RGB Service: {ex.Message}",
                "Initialization Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            throw;
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        AppConfig config;
        lock (_configLock)
        {
            config = _config;
        }

        var presetMenu = new ToolStripMenuItem("Presets");
        foreach (var preset in config.Presets)
        {
            var item = new ToolStripMenuItem(preset.Name)
            {
                Checked = string.Equals(preset.Name, config.LastPresetName, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += async (_, _) =>
            {
                try
                {
                    await ApplyPresetAsync(preset, updateLast: true).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error applying preset: {ex}");
                    var errorMessage = $"Error applying preset: {ex.Message}";
                    Notify("RGB apply failed", errorMessage, ToolTipIcon.Error);
                }
            };
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
        try
        {
            CancellationTokenSource cts;
            lock (_ctsLock)
            {
                var oldCts = _startupCts;
                if (oldCts is not null)
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }

                _startupCts = new CancellationTokenSource();
                cts = _startupCts;
            }

            AppConfig config;
            lock (_configLock)
            {
                config = _config;
            }

            var preset = config.Presets.FirstOrDefault(p =>
                string.Equals(p.Name, config.LastPresetName, StringComparison.OrdinalIgnoreCase));

            if (preset is null)
            {
                Notify("RGB preset not found", "Last preset name does not match any configured preset.", ToolTipIcon.Warning);
                return;
            }

            var attempts = Math.Max(1, config.RetryCount);
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                var result = await _rgbController.ApplyPresetAsync(preset, cts.Token);
                if (result.Succeeded)
                {
                    return;
                }

                if (attempt == attempts)
                {
                    Notify("RGB apply failed", result.ErrorMessage ?? "Unknown error.", ToolTipIcon.Error);
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, config.RetryDelaySeconds)), cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Startup operation was canceled (e.g., superseded by a new preset application); no further action needed.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unexpected error while applying RGB preset: {ex}");
            Notify("RGB apply failed", "An unexpected error occurred while applying the RGB preset.", ToolTipIcon.Error);
        }
    }

    private async Task ApplyPresetAsync(RgbPreset preset, bool updateLast)
    {
        AppConfig config;
        lock (_configLock)
        {
            config = _config;
        }

        var result = await _rgbController.ApplyPresetAsync(preset, CancellationToken.None);
        if (!result.Succeeded)
        {
            Notify("RGB apply failed", result.ErrorMessage ?? "Unknown error.", ToolTipIcon.Error);
            return;
        }

        if (updateLast)
        {
            lock (_configLock)
            {
                _config.LastPresetName = preset.Name;
                _configService.Save(_config);
            }
        }

        UpdateMenuChecks();
    }

    private void UpdateMenuChecks()
    {
        if (_trayIcon.ContextMenuStrip is null)
        {
            return;
        }

        string? lastPresetName;
        lock (_configLock)
        {
            lastPresetName = _config.LastPresetName;
        }

        foreach (ToolStripItem item in _trayIcon.ContextMenuStrip.Items)
        {
            if (item is not ToolStripMenuItem presetsMenuItem || presetsMenuItem.Text != "Presets")
            {
                continue;
            }

            foreach (ToolStripItem presetItem in presetsMenuItem.DropDownItems)
            {
                if (presetItem is ToolStripMenuItem presetMenuItem)
                {
                    presetMenuItem.Checked = string.Equals(
                        presetMenuItem.Text,
                        lastPresetName,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    private void OpenConfigFolder()
    {
        try
        {
            Directory.CreateDirectory(_configService.ConfigDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _configService.ConfigDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open config folder: {ex}");
            Notify("Error", $"Failed to open config folder: {ex.Message}", ToolTipIcon.Error);
        }
    }

    private void ReloadConfig()
    {
        // Cancel and dispose any running startup operation before reloading
        lock (_ctsLock)
        {
            if (_startupCts is not null)
            {
                _startupCts.Cancel();
                _startupCts.Dispose();
                _startupCts = null;
            }
        }

        lock (_configLock)
        {
            _config = _configService.Load();
        }

        var oldMenu = _trayIcon.ContextMenuStrip;
        _trayIcon.ContextMenuStrip = BuildMenu();
        oldMenu?.Dispose();
        ApplyLastPresetWithRetry();
    }

    private void Notify(string title, string message, ToolTipIcon icon)
    {
        _trayIcon.ShowBalloonTip(BalloonTipTimeout, title, message, icon);
    }

    protected override void ExitThreadCore()
    {
        lock (_ctsLock)
        {
            if (_startupCts is not null)
            {
                _startupCts.Cancel();
                _startupCts.Dispose();
                _startupCts = null;
            }
        }

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }
}
