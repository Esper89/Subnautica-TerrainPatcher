using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using WorldStreaming;

namespace TerrainPatcher
{
    // Harmony patches that make the game open patched terrain files instead of the originals.
    internal static class BatchPatches
    {
        internal static void Patch(Harmony harmony)
        {
            harmony.PatchAll(typeof(LargeWorldStreamer_GetCompiledOctreesCachePath_Patch));
            harmony.PatchAll(typeof(BatchOctreesStreamer_GetPath_Patch));
            harmony.PatchAll(typeof(LargeWorldStreamer_CheckBatch_Patches));
        }

        [HarmonyPatch(
            typeof(LargeWorldStreamer),
            nameof(LargeWorldStreamer.GetCompiledOctreesCachePath)
        )]
        internal static class LargeWorldStreamer_GetCompiledOctreesCachePath_Patch
        {
            // Matches a batch file name. Supports negative batch numbers.
            private static readonly Regex pattern = new Regex(
                @"^(?:.*(?:\/|\\))?compiled-batch-(-?\d+)-(-?\d+)-(-?\d+)\.optoctrees$"
            );

            private static bool Prefix(string filename, ref string __result, bool __runOriginal)
            {
                // We need to parse the filename into a batch ID before we can run SetResult.

                var match = pattern.Match(filename);
                Int3 batchId;

                try
                {
                    batchId = new Int3(
                        int.Parse(match.Groups[1].Value),
                        int.Parse(match.Groups[2].Value),
                        int.Parse(match.Groups[3].Value)
                    );
                }
                catch (FormatException)
                {
                    Mod.LogWarning($"Game accessed batch file with invalid filename '{filename}'");
                    return false;
                }

                return SetResult(batchId, ref __result, __runOriginal);
            }
        }

        [HarmonyPatch(typeof(BatchOctreesStreamer), nameof(BatchOctreesStreamer.GetPath))]
        internal static class BatchOctreesStreamer_GetPath_Patch
        {
            private static bool Prefix(Int3 batchId, ref string __result, bool __runOriginal)
                => SetResult(batchId, ref __result, __runOriginal);
        }

        // Sets the result of the method and skips the original if needed.
        private static bool SetResult(Int3 batchId, ref string result, bool runOriginal)
        {
            if (runOriginal &&
                Mod.Settings.IncludePatches &&
                TerrainRegistry.patchedBatches.ContainsKey(batchId))
            {
                result = TerrainRegistry.patchedBatches[batchId].fileName;
                return false;
            }
            else
            {
                return true;
            }
        }

        // Permits any batch location.
        [HarmonyPatch(typeof(LargeWorldStreamer))]
        internal static class LargeWorldStreamer_CheckBatch_Patches
        {
            [HarmonyPatch(nameof(LargeWorldStreamer.CheckBatch))]
            [HarmonyPatch(
                nameof(LargeWorldStreamer.CheckRoot),
                typeof(int), typeof(int), typeof(int)
            )]
            [HarmonyPrefix]
            private static bool AllowOutOfBounds(ref bool __result)
            {
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(BatchOctrees), nameof(BatchOctrees.LoadOctrees))]
        internal static class BatchOctrees_LoadOctrees_Patch
        {
            private static IEnumerable<CodeInstruction> Transpiler(
                IEnumerable<CodeInstruction> instructions
            ) => new CodeMatcher(instructions)
                .MatchStartForward([
                    new(OpCodes.Call, AccessTools.Method(
                        typeof(Int3.Bounds), nameof(Int3.Bounds.Contains)
                    )),
                ])
                .ThrowIfNotMatch(
                    $"Could not transpile {typeof(BatchOctrees)}." +
                    $"{nameof(BatchOctrees.LoadOctrees)}: method does not call " +
                    $"{typeof(Int3.Bounds)}.{nameof(Int3.Bounds.Contains)}"
                )
                .Advance(1)
                .Insert([
                    new(OpCodes.Pop),
                    new(OpCodes.Ldc_I4_1),
                ])
                .InstructionEnumeration();
        }
    }
}
