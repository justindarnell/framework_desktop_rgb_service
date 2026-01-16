# framework_desktop_rgb_service

This project is a Windows-only tray application that keeps the Framework Desktop RGB fan set to your preferred colors after reboot. It talks directly to the Framework EC driver and stores presets in a local JSON config.

## How it works

- Runs as a **Windows tray app**.
- Applies the last-used preset on startup, with retry logic.
- Uses the Framework Windows EC driver to send RGB commands for the 8-LED Cooler Master ARGB fan.

## Project goals (for new contributors)

This project exists to:

- Keep the Framework Desktop Cooler Master ARGB fan colors applied across reboots.
- Provide a simple **tray-first** UX for selecting presets (no CLI).
- Persist presets locally in JSON for easy editing.
- Use a native/managed EC implementation to avoid external CLI dependencies.

## Configuration

On first run, a default config is created at:

```
%AppData%\FrameworkDesktopRgbService\config.json
```

Example:

```json
{
  "LastPresetName": "Default",
  "RetryCount": 5,
  "RetryDelaySeconds": 3,
  "Presets": [
    {
      "Name": "Default",
      "Colors": [
        "0xff0000",
        "0xff8000",
        "0xffff00",
        "0x00ff00",
        "0x00ffff",
        "0x0000ff",
        "0x8000ff",
        "0xff00ff"
      ]
    }
  ]
}
```

Notes:
- The Cooler Master ARGB fan has **8 LEDs**, so each preset must supply exactly **8 colors**.
- Colors can be entered as `0xff0000` or `#ff0000`.
- The app requires the Framework Windows EC driver (CrosEC) to be installed and accessible at `\\.\GLOBALROOT\Device\CrosEC`.

## Usage

- Launch the app (it will appear in the system tray).
- Use the **Presets** menu to apply a preset and save it as the new default.
- Use **Open Config Folder** to edit presets.
- Use **Reload Config** after editing the JSON.

## Startup behavior (recommended)

To ensure the fan colors apply after every reboot:

- **Option A (simple):** add this app to Windows Startup (Task Manager → Startup Apps or Settings → Apps → Startup).
- **Option B (recommended for UAC):** create a Task Scheduler entry that runs the app with highest privileges at user logon.

If you choose Option B, the task should:

- Run **only when the user is logged on** (so the tray icon is available).
- Be configured to **Run with highest privileges**.

## Dependencies

- Framework Windows EC driver (CrosEC) must be installed (via Framework driver/BIOS updates).

## Build (requires .NET 8 SDK)

```
dotnet build src/FrameworkDesktopRgbService/FrameworkDesktopRgbService.csproj
```

## Known limitations

- Requires the Framework Windows EC driver to be installed and available.
- No effects/animations yet (static colors only).
