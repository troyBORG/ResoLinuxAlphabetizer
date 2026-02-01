# ResoLinuxAlphabetizer

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that fixes file browser sorting on Linux.

## Problem

On Linux, files and folders in the file browser are not sorted alphabetically (they appear in a random order). Linux filesystems do not guarantee any order when listing a directory—[`readdir()` provides no ordering guarantee](https://stackoverflow.com/questions/8977441/does-readdir-guarantee-an-order)—so the application must sort the list itself, [like `ls` does](https://serverfault.com/questions/406229/ensuring-a-repeatable-directory-ordering-in-linux). This mod does that. The file browser also doesn't automatically load the root directory ("/") when opened; the mod fixes both.

The weird ordering is filesystem-driver dependent (e.g. FAT32 doesn’t have the issue; ext4/Linux does). You can see the raw listing as given by `readdir` with e.g. `find .` in the directory. You can’t force the filesystem or `readdir()` to return alphabetical order—there’s no POSIX or driver option for that. The only way is to sort the list after reading it; this mod does that.

Related issue: [folders sorted wrong #5156](https://github.com/Yellow-Dog-Man/Resonite-Issues/issues/5156)

## Solution

1. **Alphabetical sorting**: After the file browser refreshes, the mod sorts files and directories by display name (filename), case-insensitive. One simple sort—no algorithm options.
2. **Quote stripping**: When sorting, leading and trailing single (`'`) and double (`"`) quotes are stripped from names. That way we sort by the actual name even if the API or Wine returns quoted paths (e.g. like `ls` quoting names with spaces), so quoted vs unquoted entries don’t end up in the wrong order.
3. **Auto-load root on Linux**: When the file browser opens on Linux with an empty path, it sets the path to "/" so you start at root.

## Configuration

- **Enabled** (default: on) – When **on**, the mod sorts the file browser. When **off**, the mod does nothing so you get vanilla (random) order. Toggle off to compare; no restart needed—change it and refresh the file browser (reload or navigate).

## Installation

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Put `ResoLinuxAlphabetizer.dll` in your `rml_mods` folder (e.g. `~/.steam/steam/steamapps/common/Resonite/rml_mods` on Linux).
3. Start the game. In the log you should see:
   - `ResoLinuxAlphabetizer: Patched async state machine <Refresh>d__XX.MoveNext for sorting.`
   - `ResoLinuxAlphabetizer: Patched FileBrowser.OnAttach to auto-load root directory on Linux.`

## Troubleshooting (if sorting doesn’t appear)

Check the log: you should see **"Patched async state machine &lt;Refresh&gt;d__XX.MoveNext"**. If not, the engine build may differ and the patch didn’t apply. When asking for help, share expected vs actual behavior, steps to reproduce, and the `ResoLinuxAlphabetizer:` lines from the log.

## Building

```bash
dotnet build
```

The mod can be copied to `rml_mods` automatically if `CopyToMods` is set in the project file.
