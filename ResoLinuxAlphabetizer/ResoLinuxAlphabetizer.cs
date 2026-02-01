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
	internal const string VERSION_CONSTANT = "0.0.8";
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

		var harmony = new Harmony("com.troyborg.ResoLinuxAlphabetizer");
		harmony.PatchAll();

		// Decompiled Refresh is async Task: the real body (GetFiles/GetDirectories) lives in the
		// compiler-generated state machine's MoveNext only. We patch MoveNext (not Refresh).
		bool patchedStateMachine = false;
		foreach (var nested in typeof(FileBrowser).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) {
			string? name = nested.Name;
			if (name == null || !name.StartsWith("<Refresh>d__", StringComparison.Ordinal)) continue;
			var moveNext = nested.GetMethod("MoveNext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (moveNext == null) continue;
			var transpiler = typeof(FileBrowser_Refresh_Patch).GetMethod(nameof(FileBrowser_Refresh_Patch.Transpiler), BindingFlags.Static | BindingFlags.Public);
			if (transpiler != null) {
				harmony.Patch(moveNext, transpiler: new HarmonyMethod(transpiler));
				Msg($"ResoLinuxAlphabetizer: Patched async state machine {name}.MoveNext for sorting.");
				patchedStateMachine = true;
				break;
			}
		}
		if (!patchedStateMachine)
			Msg("ResoLinuxAlphabetizer: WARNING — could not find FileBrowser.Refresh state machine; sorting will not run. Engine build may differ.");

		Msg("ResoLinuxAlphabetizer: Patched FileBrowser.Refresh async body to sort files and directories alphabetically (case-insensitive by name, quotes stripped).");
		Msg("ResoLinuxAlphabetizer: Patched FileBrowser.OnAttach to auto-load root directory on Linux.");
		Msg("ResoLinuxAlphabetizer: Toggle \"Enabled\" in Mod Settings to turn sorting on/off.");
	}

	private static bool IsSortingEnabled() {
		if (Config == null) return true;
		return Config.TryGetValue(KEY_ENABLED, out bool enabled) && enabled;
	}

	// Name used for sorting: filename only, with leading/trailing quotes stripped.
	// (Wine/shell can return quoted names like '!! Resonite'; we sort by the actual name.)
	private static string GetSortName(string path) {
		string name = string.IsNullOrEmpty(Path.GetFileName(path)) ? path : Path.GetFileName(path);
		return name.Trim().Trim('\'', '"');
	}

	// Sort in place: case-insensitive by filename (quotes stripped for comparison)
	private static void SortByFileName(string[]? array) {
		if (array == null || array.Length == 0 || !IsSortingEnabled())
			return;
		Array.Sort(array, (a, b) => FileNameComparer.Compare(GetSortName(a), GetSortName(b)));
	}

	// Decompile: Refresh is async Task — GetFiles/GetDirectories live in state machine MoveNext only.
	// In async MoveNext, locals are often hoisted to state machine FIELDS (stfld), not stloc.
	class FileBrowser_Refresh_Patch {
		// Support both: (1) stloc — locals in MoveNext, (2) stfld — hoisted to state machine fields.
		// Search window: up to 12 instructions after call (compiler may insert ops).
		const int SearchWindow = 12;

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			var codes = instructions.ToList();
			var sortMethod = typeof(ResoLinuxAlphabetizer).GetMethod("SortByFileName", BindingFlags.NonPublic | BindingFlags.Static);
			if (sortMethod == null)
				return codes;

			int filesLocalIndex = -1, directoriesLocalIndex = -1;
			FieldInfo? filesField = null, directoriesField = null;
			int injectAfterIndex = -1;

			for (int i = 0; i < codes.Count; i++) {
				var code = codes[i];
				if (code.opcode != OpCodes.Call || code.operand?.ToString() == null) continue;

				string call = code.operand.ToString();
				if (call.Contains("GetFiles")) {
					TryReadStloc(codes, i + 1, SearchWindow, ref filesLocalIndex);
					TryReadStfld(codes, i + 1, SearchWindow, ref filesField);
					SetInjectAfter(codes, i + 1, SearchWindow, ref injectAfterIndex);
				} else if (call.Contains("GetDirectories")) {
					TryReadStloc(codes, i + 1, SearchWindow, ref directoriesLocalIndex);
					TryReadStfld(codes, i + 1, SearchWindow, ref directoriesField);
					SetInjectAfter(codes, i + 1, SearchWindow, ref injectAfterIndex);
				}
			}

			bool haveDirectories = directoriesField != null || directoriesLocalIndex >= 0;
			bool haveFiles = filesField != null || filesLocalIndex >= 0;
			bool canInject = injectAfterIndex >= 0 && (haveDirectories || haveFiles);

			if (!canInject)
				return codes;

			// Mixed: each array can be in a field or a local — use the right load for each.
			var newCodes = new List<CodeInstruction>();
			for (int i = 0; i < codes.Count; i++) {
				newCodes.Add(codes[i]);
				if (i != injectAfterIndex) continue;

				// Directories
				if (directoriesField != null) {
					newCodes.Add(new CodeInstruction(OpCodes.Ldarg_0));
					newCodes.Add(new CodeInstruction(OpCodes.Ldfld, directoriesField));
					newCodes.Add(new CodeInstruction(OpCodes.Call, sortMethod));
				} else if (directoriesLocalIndex >= 0) {
					EmitLdloc(newCodes, directoriesLocalIndex);
					newCodes.Add(new CodeInstruction(OpCodes.Call, sortMethod));
				}
				// Files
				if (filesField != null) {
					newCodes.Add(new CodeInstruction(OpCodes.Ldarg_0));
					newCodes.Add(new CodeInstruction(OpCodes.Ldfld, filesField));
					newCodes.Add(new CodeInstruction(OpCodes.Call, sortMethod));
				} else if (filesLocalIndex >= 0) {
					EmitLdloc(newCodes, filesLocalIndex);
					newCodes.Add(new CodeInstruction(OpCodes.Call, sortMethod));
				}
			}

			return newCodes;
		}

		static void EmitLdloc(List<CodeInstruction> list, int index) {
			// Ldloc_S is for index 0..255; larger indices need Ldloc.
			if (index >= 0 && index <= 255)
				list.Add(new CodeInstruction(OpCodes.Ldloc_S, index));
			else
				list.Add(new CodeInstruction(OpCodes.Ldloc, index));
		}

		static void SetInjectAfter(List<CodeInstruction> codes, int start, int count, ref int injectAfterIndex) {
			for (int j = start; j < codes.Count && j < start + count; j++) {
				var c = codes[j];
				if (c.opcode == OpCodes.Stloc_S || c.opcode == OpCodes.Stloc || c.opcode == OpCodes.Stfld) {
					injectAfterIndex = Math.Max(injectAfterIndex, j);
					break;
				}
			}
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

		static void TryReadStfld(List<CodeInstruction> codes, int start, int count, ref FieldInfo? field) {
			for (int j = start; j < codes.Count && j < start + count; j++) {
				var c = codes[j];
				if (c.opcode == OpCodes.Stfld && c.operand is FieldInfo f && f.FieldType == typeof(string[]))
					field = f;
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
