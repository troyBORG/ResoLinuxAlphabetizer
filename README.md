# ResoLinuxAlphabetizer

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that fixes file browser sorting on Linux.

## Problem

On Linux, files and folders in the file browser are not sorted alphabetically (they appear in a random order). The file browser also doesn't automatically load the root directory ("/") when opened. This mod fixes both.

Related issue: [folders sorted wrong #5156](https://github.com/Yellow-Dog-Man/Resonite-Issues/issues/5156)

## Solution

1. **Alphabetical sorting**: After the file browser refreshes, the mod sorts files and directories by display name (filename), case-insensitive. One simple sort—no algorithm options.
2. **Auto-load root on Linux**: When the file browser opens on Linux with an empty path, it sets the path to "/" so you start at root.

## Configuration

- **Enabled** (default: on) – When **on**, the mod sorts the file browser. When **off**, the mod does nothing so you get vanilla (random) order. Toggle off to compare; no restart needed—change it and refresh the file browser (reload or navigate).

## Installation

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Put `ResoLinuxAlphabetizer.dll` in your `rml_mods` folder (e.g. `~/.steam/steam/steamapps/common/Resonite/rml_mods` on Linux).
3. Start the game. In the log you should see:
   - `ResoLinuxAlphabetizer: Patched FileBrowser.Refresh to sort files and directories alphabetically (case-insensitive by name).`
   - `ResoLinuxAlphabetizer: Patched FileBrowser.OnAttach to auto-load root directory on Linux.`

## Building

```bash
dotnet build
```

The mod can be copied to `rml_mods` automatically if `CopyToMods` is set in the project file.
