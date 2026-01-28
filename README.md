# ResoLinuxAlphabetizer

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that fixes file browser sorting on Linux.

## Problem

On Linux, files and folders in the file browser are not being sorted alphabetically. This mod patches `FileBrowser.Refresh` to sort both files and directories alphabetically (case-insensitive) before displaying them.

Related issue: [folders sorted wrong #5156](https://github.com/Yellow-Dog-Man/Resonite-Issues/issues/5156)

## Solution

This mod uses Harmony to patch the `FileBrowser.Refresh` method and injects sorting code that sorts both the `files` and `directories` arrays using `StringComparer.OrdinalIgnoreCase` before they are displayed in the UI.

## Installation

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place `ResoLinuxAlphabetizer.dll` into your `rml_mods` folder. This folder should be at:
   - Windows: `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods`
   - Linux: `~/.steam/steam/steamapps/common/Resonite/rml_mods`
   - You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create this folder for you.
3. Start the game. If you want to verify that the mod is working, check your Resonite logs for: `"ResoLinuxAlphabetizer: Patched FileBrowser.Refresh to sort files and directories alphabetically"`

## Building

```bash
dotnet build
```

The mod will automatically be copied to your Resonite `rml_mods` folder if `CopyToMods` is set to `true` in the project file.
