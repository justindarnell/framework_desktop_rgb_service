# framework_desktop_rgb_service

This project is a Windows-only tray application that keeps the Framework Desktop RGB fan set to your preferred colors after reboot. It calls `framework_tool.exe` under the hood and stores presets in a local JSON config.

## How it works

- Runs as a **Windows tray app**.
- Applies the last-used preset on startup, with retry logic.
- Uses `framework_tool.exe --rgbkbd 0 <colors...>` to set the 8-LED Cooler Master ARGB fan.

## Configuration

On first run, a default config is created at:

```
%AppData%\FrameworkDesktopRgbService\config.json
```

Example:

```json
{
  "FrameworkToolPath": "framework_tool.exe",
  "LastPresetName": "Default",
  "RetryCount": 5,
  "RetryDelaySeconds": 3,
  "RequireElevation": true,
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
- If `framework_tool.exe` is not in your PATH, set `FrameworkToolPath` to the full path.
- `RequireElevation` prompts for UAC elevation when running `framework_tool.exe`. If you disable it, the tool must already be runnable without admin privileges.

## Usage

- Launch the app (it will appear in the system tray).
- Use the **Presets** menu to apply a preset and save it as the new default.
- Use **Open Config Folder** to edit presets.
- Use **Reload Config** after editing the JSON.

## Dependencies

Install Framework's tool via winget (or download the EXE and update the config path):

```
winget install FrameworkComputer.framework_tool
```

## Build (requires .NET 8 SDK)

```
dotnet build src/FrameworkDesktopRgbService/FrameworkDesktopRgbService.csproj
```
