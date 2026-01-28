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

public enum SortingAlgorithm {
	OrdinalIgnoreCase,      // Default: Case-insensitive using OrdinalIgnoreCase
	InvariantCultureIgnoreCase, // Case-insensitive using InvariantCulture
	Ordinal,                // Case-sensitive using Ordinal
	NaturalSort,            // Natural sort (handles numbers better: file1, file2, file10)
	FullPath                // Sort by full path instead of filename
}

public class ResoLinuxAlphabetizer : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0";
	public override string Name => "ResoLinuxAlphabetizer";
	public override string Author => "troyBORG";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/troyBORG/ResoLinuxAlphabetizer/";

	// Config option: Choose sorting algorithm
	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<SortingAlgorithm> KEY_SORTING_METHOD = 
		new ModConfigurationKey<SortingAlgorithm>(
			"SortingMethod", 
			"Sorting algorithm to use:\n" +
			"OrdinalIgnoreCase - Case-insensitive (default, recommended)\n" +
			"InvariantCultureIgnoreCase - Case-insensitive with culture rules\n" +
			"Ordinal - Case-sensitive\n" +
			"NaturalSort - Natural sort (file1, file2, file10)\n" +
			"FullPath - Sort by full path instead of filename",
			() => SortingAlgorithm.OrdinalIgnoreCase);

	private ModConfiguration? Config;
	
	// Static field to store current algorithm (used by static SortStringArray method)
	private static SortingAlgorithm CurrentSortingAlgorithm = SortingAlgorithm.OrdinalIgnoreCase;

	public override void OnEngineInit() {
		// Get configuration
		Config = GetConfiguration();
		
		// Read current sorting method and store in static field
		SortingAlgorithm currentMethod = SortingAlgorithm.OrdinalIgnoreCase;
		if (Config != null && !Config.TryGetValue(KEY_SORTING_METHOD, out currentMethod)) {
			// Set default if not found
			Config.Set(KEY_SORTING_METHOD, SortingAlgorithm.OrdinalIgnoreCase);
			currentMethod = SortingAlgorithm.OrdinalIgnoreCase;
		}
		CurrentSortingAlgorithm = currentMethod;

		Harmony harmony = new("com.troyborg.ResoLinuxAlphabetizer");
		harmony.PatchAll();
		Msg($"ResoLinuxAlphabetizer: Patched FileBrowser.Refresh to sort files and directories alphabetically");
		Msg($"ResoLinuxAlphabetizer: Using sorting algorithm: {currentMethod}");
		Msg("ResoLinuxAlphabetizer: Patched FileBrowser.OnAttach to auto-load root directory on Linux");
		Msg("ResoLinuxAlphabetizer: You can change the sorting algorithm in Mod Settings!");
	}

	// Helper method to sort string arrays using the configured algorithm
	private static void SortStringArray(string[] array) {
		if (array != null && array.Length > 0) {
			// Use static field for current algorithm (updated in OnEngineInit)
			var algorithm = CurrentSortingAlgorithm;
			
			Array.Sort(array, (a, b) => {
				string nameA, nameB;
				
				// Get comparison strings based on algorithm
				if (algorithm == SortingAlgorithm.FullPath) {
					nameA = a;
					nameB = b;
				} else {
					nameA = Path.GetFileName(a);
					nameB = Path.GetFileName(b);
					if (string.IsNullOrEmpty(nameA)) nameA = a;
					if (string.IsNullOrEmpty(nameB)) nameB = b;
				}
				
				// Apply selected sorting algorithm
				return algorithm switch {
					SortingAlgorithm.OrdinalIgnoreCase => 
						string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase),
					
					SortingAlgorithm.InvariantCultureIgnoreCase => 
						string.Compare(nameA, nameB, StringComparison.InvariantCultureIgnoreCase),
					
					SortingAlgorithm.Ordinal => 
						string.Compare(nameA, nameB, StringComparison.Ordinal),
					
					SortingAlgorithm.NaturalSort => 
						NaturalCompare(nameA, nameB),
					
					SortingAlgorithm.FullPath => 
						string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase),
					
					_ => string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase)
				};
			});
		}
	}

	// Natural sort comparison (handles numbers in strings better)
	// Example: "file1", "file2", "file10" instead of "file1", "file10", "file2"
	private static int NaturalCompare(string a, string b) {
		if (a == null) return b == null ? 0 : -1;
		if (b == null) return 1;
		
		int i = 0, j = 0;
		while (i < a.Length && j < b.Length) {
			if (char.IsDigit(a[i]) && char.IsDigit(b[j])) {
				// Both are digits - compare as numbers
				int numA = 0, numB = 0;
				int startA = i, startB = j;
				
				// Parse number from a
				while (i < a.Length && char.IsDigit(a[i])) {
					numA = numA * 10 + (a[i] - '0');
					i++;
				}
				
				// Parse number from b
				while (j < b.Length && char.IsDigit(b[j])) {
					numB = numB * 10 + (b[j] - '0');
					j++;
				}
				
				if (numA != numB) {
					return numA.CompareTo(numB);
				}
			} else {
				// Compare characters case-insensitively
				int cmp = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
				if (cmp != 0) return cmp;
				i++;
				j++;
			}
		}
		
		// One string is a prefix of the other
		return (a.Length - i).CompareTo(b.Length - j);
	}

	[HarmonyPatch(typeof(FileBrowser), "Refresh")]
	class FileBrowser_Refresh_Patch {
		// Postfix to ensure sorting happens - this runs after Refresh completes
		// but we need to sort before items are displayed, so we still use transpiler
		static void Postfix(FileBrowser __instance) {
			// This won't work for local variables, but serves as a fallback check
		}

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			var codes = instructions.ToList();
			var newCodes = new List<CodeInstruction>();
			
			int filesLocalIndex = -1;
			int directoriesLocalIndex = -1;
			// Get static method
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

			// Second pass: inject sorting calls right after directories array is assigned
			// This is the most reliable approach - inject immediately after GetDirectories + stloc
			bool injectedSorting = false;
			
			for (int i = 0; i < codes.Count; i++) {
				newCodes.Add(codes[i]);
				
				// Look for GetDirectories call followed by stloc
				if (!injectedSorting && 
				    codes[i].opcode == OpCodes.Call && 
				    codes[i].operand != null &&
				    codes[i].operand.ToString()?.Contains("GetDirectories") == true) {
					// Check if next instruction is stloc for directories
					if (i + 1 < codes.Count) {
						var nextCode = codes[i + 1];
						if ((nextCode.opcode == OpCodes.Stloc_S || nextCode.opcode == OpCodes.Stloc) &&
						    directoriesLocalIndex >= 0) {
							// We've already added codes[i], now add the stloc, then inject sorting
							// But wait, we need to add the stloc first, then inject
							// Actually, let's inject after we add the stloc in the next iteration
							continue;
						}
					}
				}
				
				// If previous was GetDirectories and this is stloc, inject sorting after stloc
				if (!injectedSorting && i > 0 &&
				    (codes[i].opcode == OpCodes.Stloc_S || codes[i].opcode == OpCodes.Stloc)) {
					var prevCode = codes[i - 1];
					if (prevCode.opcode == OpCodes.Call && 
					    prevCode.operand != null &&
					    prevCode.operand.ToString()?.Contains("GetDirectories") == true) {
						// Inject sorting right after directories stloc
						if (directoriesLocalIndex >= 0) {
							newCodes.Add(new CodeInstruction(OpCodes.Ldloc_S, directoriesLocalIndex));
							newCodes.Add(new CodeInstruction(OpCodes.Call, sortMethod));
						}
						if (filesLocalIndex >= 0) {
							newCodes.Add(new CodeInstruction(OpCodes.Ldloc_S, filesLocalIndex));
							newCodes.Add(new CodeInstruction(OpCodes.Call, sortMethod));
						}
						injectedSorting = true;
					}
				}
			}

			// If we still haven't injected, try a simpler fallback: inject before first foreach
			if (!injectedSorting) {
				for (int i = codes.Count - 1; i >= 0; i--) {
					// Look for the foreach loop start (usually a GetEnumerator or similar)
					// Actually, let's just inject right before ToWorld as a last resort
					if (codes[i].opcode == OpCodes.Call && 
					    codes[i].operand != null &&
					    codes[i].operand.ToString()?.Contains("ToWorld") == true) {
						// Insert before ToWorld
						int insertPos = newCodes.Count - (codes.Count - i);
						if (insertPos < 0) insertPos = 0;
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
