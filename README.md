# ResoLinuxAlphabetizer

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that fixes file browser sorting on Linux.

## Problem

On Linux, files and folders in the file browser are not being sorted alphabetically. Additionally, the file browser doesn't automatically load the root directory ("/") when opened. This mod fixes both issues.

Related issue: [folders sorted wrong #5156](https://github.com/Yellow-Dog-Man/Resonite-Issues/issues/5156)

## Solution

This mod uses Harmony to patch the file browser with two fixes:

1. **Alphabetical Sorting**: Patches `FileBrowser.Refresh` to sort both files and directories alphabetically by their display names (filenames, not full paths). The sorting algorithm is configurable via Mod Settings with multiple options to choose from.

2. **Auto-load Root Directory**: Patches `FileBrowser.OnAttach` to automatically set the initial path to "/" on Linux when the file browser opens, similar to how Android automatically loads "/mnt/sdcard".

## Sorting Algorithms

The mod supports multiple sorting algorithms that can be changed in Mod Settings:

- **OrdinalIgnoreCase** (Default) - Case-insensitive sorting. "File.txt" and "file.txt" are treated the same. Recommended for most users.
- **InvariantCultureIgnoreCase** - Case-insensitive with culture-specific rules. Useful for international character sets.
- **Ordinal** - Case-sensitive sorting. "A.txt" comes before "a.txt".
- **NaturalSort** - Natural sorting that handles numbers correctly. Files are sorted as: "file1", "file2", "file10" instead of "file1", "file10", "file2". Great for numbered files.
- **FullPath** - Sorts by full file path instead of just the filename. Useful for organizing by directory structure.

### Changing the Sorting Algorithm

1. Open Resonite and go to **Mods** → **Mod Settings**
2. Find **ResoLinuxAlphabetizer** in the list
3. Change the **SortingMethod** dropdown to your preferred algorithm
4. **Restart Resonite** for the change to take effect (algorithm is loaded at mod initialization)

## Installation

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place `ResoLinuxAlphabetizer.dll` into your `rml_mods` folder. This folder should be at:
   - Windows: `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods`
   - Linux: `~/.steam/steam/steamapps/common/Resonite/rml_mods`
   - You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create this folder for you.
3. Start the game. If you want to verify that the mod is working, check your Resonite logs for:
   - `"ResoLinuxAlphabetizer: Patched FileBrowser.Refresh to sort files and directories alphabetically"`
   - `"ResoLinuxAlphabetizer: Using sorting algorithm: [algorithm name]"`
   - `"ResoLinuxAlphabetizer: Patched FileBrowser.OnAttach to auto-load root directory on Linux"`
   - `"ResoLinuxAlphabetizer: You can change the sorting algorithm in Mod Settings!"`

## Building

```bash
dotnet build
```

The mod will automatically be copied to your Resonite `rml_mods` folder if `CopyToMods` is set to `true` in the project file.
