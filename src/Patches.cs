using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using WorldStreaming;

namespace TerrainPatcher
{
    // Harmony patches that make the game open patched terrain files instead of the originals.
    internal static class Patches
    {
        [HarmonyPatch(
            typeof(LargeWorldStreamer),
            nameof(LargeWorldStreamer.GetCompiledOctreesCachePath)
        )]
        internal static class LargeWorldStreamer_GetCompiledOctreesCachePath_Patch
        {
            // Matches a batch file name. Supports negative batch numbers.
            private static readonly Regex pattern = new Regex(
                $@"^(?:.*(?:\/|\\))?compiled-batch-(-?\d+)-(-?\d+)-(-?\d+)\.optoctrees$"
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
                    Mod.LogError($"Game accessed batch file with invalid filename '{filename}'");
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
            if (TerrainRegistry.patchedBatches.ContainsKey(batchId) && runOriginal)
            {
                result = TerrainRegistry.patchedBatches[batchId];
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
