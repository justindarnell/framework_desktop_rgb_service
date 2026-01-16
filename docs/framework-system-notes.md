# Framework system repo notes (framework_tool / RGB)

Source references pulled from the upstream repository (FrameworkComputer/framework-system) as of main branch.

## framework_tool entry point (Windows behavior)
- `framework_tool/src/main.rs` checks for a console with only one process to detect a double-click launch and, if so with no args, injects `--versions` and later prints `"Press ENTER to exit..."` and reads stdin before exiting. This is what causes the command prompt to stay open until you press Enter when the app is double-clicked or elevated in a fresh console window.

## RGB commandline handling
- `framework_lib/src/commandline/mod.rs` handles the `rgbkbd` argument by treating the first value as a start key and the rest as RGB values (0xRRGGBB). It converts each color to `RgbS { r, g, b }` bytes and calls `ec.rgbkbd_set_color(start_key, colors)`.

## EC command and packet layout
- `framework_lib/src/chromium_ec/mod.rs` implements `rgbkbd_set_color` by chunking the colors into blocks of `EC_RGBKBD_MAX_KEY_COUNT` and sending an `EcRequestRgbKbdSetColor` request for each chunk.
- `framework_lib/src/chromium_ec/commands.rs` defines `EC_RGBKBD_MAX_KEY_COUNT = 64`, the `RgbS` struct (r, g, b), and the `EcRequestRgbKbdSetColor` request format (start key, length, color array). The command ID is `EcCommands::RgbKbdSetColor`.

## Commands used to fetch upstream sources
- `curl -L --fail https://raw.githubusercontent.com/FrameworkComputer/framework-system/main/framework_tool/src/main.rs | sed -n '1,220p'`
- `curl -L --fail https://raw.githubusercontent.com/FrameworkComputer/framework-system/main/framework_lib/src/commandline/mod.rs | sed -n '1360,1435p'`
- `curl -L --fail https://raw.githubusercontent.com/FrameworkComputer/framework-system/main/framework_lib/src/chromium_ec/mod.rs | sed -n '1700,1755p'`
- `curl -L --fail https://raw.githubusercontent.com/FrameworkComputer/framework-system/main/framework_lib/src/chromium_ec/commands.rs | sed -n '1040,1125p'`
