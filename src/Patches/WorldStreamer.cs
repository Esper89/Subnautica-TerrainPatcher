using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using WorldStreaming;

namespace TerrainPatcher
{
    [HarmonyPatch(typeof(WorldStreamer))]
    internal static class WorldStreamerPatches
    {
        internal static void Patch(Harmony harmony)
        {
            harmony.PatchAll(typeof(WorldStreamerPatches));
        }

        [HarmonyPatch(nameof(WorldStreamer.CreateStreamers))]
        [HarmonyPrefix]
        private static void CreateStreamersPrefix(WorldStreamer __instance)
        {
            // Allows the streamer to stream octrees as far as the patched batches go.

            var biggestBatch =
                BiggestBatch(TerrainRegistry.patchedBatches.Keys, __instance.settings.numOctrees) +
                __instance.settings.numOctreesPerBatch *
                (__instance.settings.numOctreesPerBatch * 2);

            __instance.settings.numOctrees = biggestBatch;
        }

        [HarmonyPatch(nameof(WorldStreamer.CreateStreamers))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CreateStreamersTranspiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            // Changes minimum and maximum center positions. Also necessary to stream batches.

            var int3Zero = AccessTools.Field(typeof(Int3), nameof(Int3.zero));
            var found = false;
            var found2 = false;
            foreach (var instruction in instructions)
            {
                if (instruction.Is(OpCodes.Ldsfld, int3Zero))
                {
                    yield return CodeInstruction.Call(
                        typeof(WorldStreamerPatches), nameof(MinimumBoundary)
                    );
                    found = true;
                }
                else if (instruction.Calls(AccessTools.Method(typeof(WorldStreamer),
                             nameof(WorldStreamer.ParseStreamingSettings))))
                {
                    yield return instruction;
                    yield return CodeInstruction.Call(
                        typeof(WorldStreamerPatches), nameof(ParseStreamingSettings)
                    );
                    found2 = true;
                }
                else
                {
                    yield return instruction;
                }
            }

            if (found && found2)
            {
                Mod.LogDebug($"{nameof(CreateStreamersTranspiler)} has been patched successfully.");
            }
            else
            {
                Mod.LogError($"{nameof(CreateStreamersTranspiler)} failed patching.");
            }
        }

        private static LargeWorldStreamer.Settings ParseStreamingSettings(
            LargeWorldStreamer.Settings settings
        )
        {
            var patchedBatches = TerrainRegistry.patchedBatches.Keys;
            settings.octreesSettings.centerMin = SmallestBatch(patchedBatches) * 5 - 10;
            settings.octreesSettings.centerMax =
                BiggestBatch(patchedBatches, settings.octreesSettings.centerMax) * 5 + 15;
            return settings;
        }

        private static Int3 MinimumBoundary()
        {
            return SmallestBatch(TerrainRegistry.patchedBatches.Keys) * 5 - 10;
        }

        private static Int3 SmallestBatch(IEnumerable<Int3> batches)
        {
            var result = Int3.zero;

            foreach (var batch in batches)
            {
                result = Int3.Min(result, batch);
            }

            var horizontalMin = Mathf.Min(result.x, result.z);
            return new Int3(horizontalMin, result.y, horizontalMin);
        }

        private static Int3 BiggestBatch(IEnumerable<Int3> batches, Int3 minimumBatch)
        {
            var result = minimumBatch;

            foreach (var batch in batches)
            {
                var batchSize = batch;
                result = Int3.Max(result, batchSize);
            }

            var horizontalMax = Mathf.Max(result.x, result.z);
            return new Int3(horizontalMax, result.y, horizontalMax);
        }
    }
}
