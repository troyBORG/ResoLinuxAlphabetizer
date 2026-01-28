using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;

namespace ResoLinuxAlphabetizer;
// Fixes file browser sorting on Linux - https://github.com/Yellow-Dog-Man/Resonite-Issues/issues/5156
public class ResoLinuxAlphabetizer : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0";
	public override string Name => "ResoLinuxAlphabetizer";
	public override string Author => "troyBORG";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/troyBORG/ResoLinuxAlphabetizer/";

	public override void OnEngineInit() {
		Harmony harmony = new("com.troyborg.ResoLinuxAlphabetizer");
		harmony.PatchAll();
		Msg("ResoLinuxAlphabetizer: Patched FileBrowser.Refresh to sort files and directories alphabetically");
		Msg("ResoLinuxAlphabetizer: Patched FileBrowser.OnAttach to auto-load root directory on Linux");
	}

	// Helper method to sort string arrays
	private static void SortStringArray(string[] array) {
		if (array != null && array.Length > 0) {
			Array.Sort(array, StringComparer.OrdinalIgnoreCase);
		}
	}

	[HarmonyPatch(typeof(FileBrowser), "Refresh")]
	class FileBrowser_Refresh_Patch {
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			var codes = instructions.ToList();
			var newCodes = new List<CodeInstruction>();
			
			int filesLocalIndex = -1;
			int directoriesLocalIndex = -1;
			var sortMethod = typeof(ResoLinuxAlphabetizer).GetMethod("SortStringArray", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

			// First pass: find local variable indices
			for (int i = 0; i < codes.Count; i++) {
				var code = codes[i];
				
				// Find Directory.GetFiles and its stloc
				if (code.opcode == OpCodes.Call && 
				    code.operand != null && 
				    code.operand.ToString()?.Contains("GetFiles") == true) {
					for (int j = i + 1; j < codes.Count && j < i + 5; j++) {
						if (codes[j].opcode == OpCodes.Stloc_S) {
							if (codes[j].operand is LocalBuilder lb) {
								filesLocalIndex = lb.LocalIndex;
							} else if (codes[j].operand is int idx) {
								filesLocalIndex = idx;
							}
							break;
						} else if (codes[j].opcode == OpCodes.Stloc) {
							if (codes[j].operand is LocalBuilder lb) {
								filesLocalIndex = lb.LocalIndex;
							} else if (codes[j].operand is int idx) {
								filesLocalIndex = idx;
							}
							break;
						}
					}
				}
				
				// Find Directory.GetDirectories and its stloc
				if (code.opcode == OpCodes.Call && 
				    code.operand != null && 
				    code.operand.ToString()?.Contains("GetDirectories") == true) {
					for (int j = i + 1; j < codes.Count && j < i + 5; j++) {
						if (codes[j].opcode == OpCodes.Stloc_S) {
							if (codes[j].operand is LocalBuilder lb) {
								directoriesLocalIndex = lb.LocalIndex;
							} else if (codes[j].operand is int idx) {
								directoriesLocalIndex = idx;
							}
							break;
						} else if (codes[j].opcode == OpCodes.Stloc) {
							if (codes[j].operand is LocalBuilder lb) {
								directoriesLocalIndex = lb.LocalIndex;
							} else if (codes[j].operand is int idx) {
								directoriesLocalIndex = idx;
							}
							break;
						}
					}
				}
			}

			// Second pass: inject sorting calls
			bool injectedSorting = false;
			for (int i = 0; i < codes.Count; i++) {
				newCodes.Add(codes[i]);
				
				// Look for "await default(ToWorld)" - inject sorting right after
				if (!injectedSorting && 
				    codes[i].opcode == OpCodes.Call && 
				    codes[i].operand != null &&
				    codes[i].operand.ToString()?.Contains("ToWorld") == true) {
					// Inject sorting after ToWorld
					if (filesLocalIndex >= 0) {
						newCodes.Add(new CodeInstruction(OpCodes.Ldloc_S, filesLocalIndex));
						newCodes.Add(new CodeInstruction(OpCodes.Call, sortMethod));
					}
					if (directoriesLocalIndex >= 0) {
						newCodes.Add(new CodeInstruction(OpCodes.Ldloc_S, directoriesLocalIndex));
						newCodes.Add(new CodeInstruction(OpCodes.Call, sortMethod));
					}
					injectedSorting = true;
				}
			}

			return injectedSorting ? newCodes : codes;
		}
	}

	[HarmonyPatch(typeof(FileBrowser), "OnAttach")]
	class FileBrowser_OnAttach_Patch {
		static void Postfix(FileBrowser __instance) {
			// Auto-load root directory on Linux if path is empty
			if (__instance.Engine.Platform == Platform.Linux) {
				if (string.IsNullOrWhiteSpace(__instance.CurrentPath.Value)) {
					__instance.CurrentPath.Value = "/";
				}
			}
		}
	}
}
