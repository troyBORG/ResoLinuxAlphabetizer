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

	// Helper method to sort string arrays by filename (not full path)
	// This ensures files are sorted by their display name, not their full path
	private static void SortStringArray(string[] array) {
		if (array != null && array.Length > 0) {
			Array.Sort(array, (a, b) => {
				string nameA = Path.GetFileName(a);
				string nameB = Path.GetFileName(b);
				if (string.IsNullOrEmpty(nameA)) nameA = a;
				if (string.IsNullOrEmpty(nameB)) nameB = b;
				return StringComparer.OrdinalIgnoreCase.Compare(nameA, nameB);
			});
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
			// Look for the pattern: await ToWorld, then check fetchedPath, then GenerateContent
			// We want to inject sorting right after ToWorld but before GenerateContent
			bool injectedSorting = false;
			bool foundToWorld = false;
			
			for (int i = 0; i < codes.Count; i++) {
				newCodes.Add(codes[i]);
				
				// Look for "await default(ToWorld)" call
				if (!foundToWorld && 
				    codes[i].opcode == OpCodes.Call && 
				    codes[i].operand != null &&
				    codes[i].operand.ToString()?.Contains("ToWorld") == true) {
					foundToWorld = true;
					// Continue to find a good insertion point after ToWorld
					continue;
				}
				
				// After ToWorld, look for GenerateContent call - inject sorting right before it
				if (foundToWorld && !injectedSorting && 
				    codes[i].opcode == OpCodes.Call && 
				    codes[i].operand != null &&
				    codes[i].operand.ToString()?.Contains("GenerateContent") == true) {
					// Inject sorting before GenerateContent
					if (filesLocalIndex >= 0) {
						newCodes.Insert(newCodes.Count - 1, new CodeInstruction(OpCodes.Ldloc_S, filesLocalIndex));
						newCodes.Insert(newCodes.Count - 1, new CodeInstruction(OpCodes.Call, sortMethod));
					}
					if (directoriesLocalIndex >= 0) {
						newCodes.Insert(newCodes.Count - 1, new CodeInstruction(OpCodes.Ldloc_S, directoriesLocalIndex));
						newCodes.Insert(newCodes.Count - 1, new CodeInstruction(OpCodes.Call, sortMethod));
					}
					injectedSorting = true;
				}
			}

			// Fallback: if we didn't find GenerateContent, inject after ToWorld
			if (foundToWorld && !injectedSorting) {
				// Find the position right after ToWorld in newCodes
				for (int i = newCodes.Count - 1; i >= 0; i--) {
					if (newCodes[i].opcode == OpCodes.Call && 
					    newCodes[i].operand != null &&
					    newCodes[i].operand.ToString()?.Contains("ToWorld") == true) {
						// Insert after ToWorld
						int insertPos = i + 1;
						if (directoriesLocalIndex >= 0) {
							newCodes.Insert(insertPos++, new CodeInstruction(OpCodes.Ldloc_S, directoriesLocalIndex));
							newCodes.Insert(insertPos++, new CodeInstruction(OpCodes.Call, sortMethod));
						}
						if (filesLocalIndex >= 0) {
							newCodes.Insert(insertPos++, new CodeInstruction(OpCodes.Ldloc_S, filesLocalIndex));
							newCodes.Insert(insertPos++, new CodeInstruction(OpCodes.Call, sortMethod));
						}
						injectedSorting = true;
						break;
					}
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
