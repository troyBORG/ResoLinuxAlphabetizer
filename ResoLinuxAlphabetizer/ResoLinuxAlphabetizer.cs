using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;

namespace ResoLinuxAlphabetizer;
// Fixes file browser sorting on Linux - https://github.com/Yellow-Dog-Man/Resonite-Issues/issues/5156

public class ResoLinuxAlphabetizer : ResoniteMod {
	internal const string VERSION_CONSTANT = "0.0.6";
	public override string Name => "ResoLinuxAlphabetizer";
	public override string Author => "troyBORG";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/troyBORG/ResoLinuxAlphabetizer/";

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> KEY_ENABLED =
		new ModConfigurationKey<bool>(
			"Enabled",
			"Enable/disable the mod's sorting. Turn off to compare with vanilla (random) order.",
			() => true);

	private static ModConfiguration? Config;

	// Case-insensitive sort by filename (display name)
	private static readonly StringComparer FileNameComparer = StringComparer.OrdinalIgnoreCase;

	public override void OnEngineInit() {
		Config = GetConfiguration();
		if (Config != null)
			Config.Save(true);

		new Harmony("com.troyborg.ResoLinuxAlphabetizer").PatchAll();
		Msg("ResoLinuxAlphabetizer: Patched FileBrowser.Refresh to sort files and directories alphabetically (case-insensitive by name).");
		Msg("ResoLinuxAlphabetizer: Patched FileBrowser.OnAttach to auto-load root directory on Linux.");
		Msg("ResoLinuxAlphabetizer: Toggle \"Enabled\" in Mod Settings to turn sorting on/off.");
	}

	private static bool IsSortingEnabled() {
		if (Config == null) return true;
		return Config.TryGetValue(KEY_ENABLED, out bool enabled) && enabled;
	}

	// Sort in place: case-insensitive by filename
	private static void SortByFileName(string[]? array) {
		if (array == null || array.Length == 0 || !IsSortingEnabled())
			return;
		Array.Sort(array, (a, b) => {
			string na = string.IsNullOrEmpty(Path.GetFileName(a)) ? a : Path.GetFileName(a);
			string nb = string.IsNullOrEmpty(Path.GetFileName(b)) ? b : Path.GetFileName(b);
			return FileNameComparer.Compare(na, nb);
		});
	}

	private static void SortListByFileName(List<string>? list) {
		if (list == null || list.Count == 0 || !IsSortingEnabled())
			return;
		list.Sort((a, b) => {
			string na = string.IsNullOrEmpty(Path.GetFileName(a)) ? a : Path.GetFileName(a);
			string nb = string.IsNullOrEmpty(Path.GetFileName(b)) ? b : Path.GetFileName(b);
			return FileNameComparer.Compare(na, nb);
		});
	}

	// Stupid-simple: after Refresh(), sort any string[] or List<string> on the browser instance
	[HarmonyPatch(typeof(FileBrowser), "Refresh")]
	class FileBrowser_Refresh_Patch {
		static void Postfix(FileBrowser __instance) {
			if (!IsSortingEnabled()) return;
			var t = __instance.GetType();
			foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)) {
				if (f.FieldType == typeof(string[])) {
					var arr = f.GetValue(__instance) as string[];
					SortByFileName(arr);
				} else if (f.FieldType == typeof(List<string>)) {
					var list = f.GetValue(__instance) as List<string>;
					SortListByFileName(list);
				}
			}
		}

		// Backup: inject sort calls after GetFiles/GetDirectories if reflection didn't find fields
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			var codes = instructions.ToList();
			var sortMethod = typeof(ResoLinuxAlphabetizer).GetMethod("SortByFileName", BindingFlags.NonPublic | BindingFlags.Static);
			if (sortMethod == null) return codes;

			int filesLocalIndex = -1, directoriesLocalIndex = -1;

			for (int i = 0; i < codes.Count; i++) {
				var code = codes[i];
				if (code.opcode != OpCodes.Call || code.operand?.ToString() == null) continue;

				string call = code.operand.ToString();
				if (call.Contains("GetFiles"))
					TryReadStloc(codes, i + 1, 5, ref filesLocalIndex);
				else if (call.Contains("GetDirectories"))
					TryReadStloc(codes, i + 1, 5, ref directoriesLocalIndex);
			}

			var newCodes = new List<CodeInstruction>();
			bool injected = false;
			for (int i = 0; i < codes.Count; i++) {
				newCodes.Add(codes[i]);
				if (injected) continue;
				if (i > 0 && (codes[i].opcode == OpCodes.Stloc_S || codes[i].opcode == OpCodes.Stloc)) {
					var prev = codes[i - 1];
					if (prev.opcode == OpCodes.Call && prev.operand?.ToString()?.Contains("GetDirectories") == true) {
						if (directoriesLocalIndex >= 0) {
							newCodes.Add(new CodeInstruction(OpCodes.Ldloc_S, directoriesLocalIndex));
							newCodes.Add(new CodeInstruction(OpCodes.Call, sortMethod));
						}
						if (filesLocalIndex >= 0) {
							newCodes.Add(new CodeInstruction(OpCodes.Ldloc_S, filesLocalIndex));
							newCodes.Add(new CodeInstruction(OpCodes.Call, sortMethod));
						}
						injected = true;
					}
				}
			}
			return injected ? newCodes : codes;
		}

		static void TryReadStloc(List<CodeInstruction> codes, int start, int count, ref int index) {
			for (int j = start; j < codes.Count && j < start + count; j++) {
				var c = codes[j];
				if (c.opcode == OpCodes.Stloc_S && c.operand is LocalBuilder lb) {
					index = lb.LocalIndex; break;
				}
				if (c.opcode == OpCodes.Stloc_S && c.operand is int idx) {
					index = idx; break;
				}
				if (c.opcode == OpCodes.Stloc && c.operand is LocalBuilder lb2) {
					index = lb2.LocalIndex; break;
				}
				if (c.opcode == OpCodes.Stloc && c.operand is int idx2) {
					index = idx2; break;
				}
			}
		}
	}

	[HarmonyPatch(typeof(FileBrowser), "OnAttach")]
	class FileBrowser_OnAttach_Patch {
		static void Postfix(FileBrowser __instance) {
			if (__instance.Engine.Platform == Platform.Linux &&
			    string.IsNullOrWhiteSpace(__instance.CurrentPath.Value))
				__instance.CurrentPath.Value = "/";
		}
	}
}
