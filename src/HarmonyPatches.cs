using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using WorldStreaming;

namespace TerrainPatcher;

internal static class HarmonyPatches {
    private static readonly Regex BATCH_NAME_PATTERN
        = new(@"^compiled-batch-(-?\d+)-(-?\d+)-(-?\d+)\.optoctrees$");

    internal static readonly Int3.Bounds EXTENDED_BATCH_BOUNDS = new(
        new(short.MinValue, short.MinValue, short.MinValue),
        new(short.MaxValue, short.MaxValue, short.MaxValue)
    );

    //MOVE THIS WITH THE EXTENDED BATCHES TO WHEREVER ITS HOME IS
    private static readonly Int3.Bounds VANILLA_OCTREE_BOUNDS = new(
        Int3.zero,
        new(127, 127, 127)
    );

    [HarmonyPatch(
        typeof(LargeWorldStreamer),
        nameof(LargeWorldStreamer.GetCompiledOctreesCachePath)
    )]
    private static class ParseAndReplaceBatchFilePath {
        private static bool Prefix(string filename, ref string? __result, bool __runOriginal) {
            Int3 batchId;
            try {
                var match = BATCH_NAME_PATTERN.Match(Path.GetFileName(filename));
                batchId = new Int3(
                    int.Parse(match.Groups[1].Value),
                    int.Parse(match.Groups[2].Value),
                    int.Parse(match.Groups[3].Value)
                );
            } catch (FormatException) {
                Plugin.LogWarning($"Game accessed batch file with invalid filename: {filename}");
                return true;
            }

            return GetBatchFilePath(batchId, ref __result, __runOriginal);
        }
    }

    [HarmonyPatch(typeof(BatchOctreesStreamer), nameof(BatchOctreesStreamer.GetPath))]
    private static class ChangeBatchFilePath {
        private static bool Prefix(Int3 batchId, ref string? __result, bool __runOriginal)
            => GetBatchFilePath(batchId, ref __result, __runOriginal);
    }

    private static bool GetBatchFilePath(Int3 batchId, ref string? result, bool runOriginal) {
        if (!runOriginal || !TerrainPatching.patchedBatches.TryGetValue(batchId, out var batch)) {
            return true;
        }
        result = batch.path;
        return false;
    }

    /// <summary>
    /// Subnautica's world is 4096 x 4096 x 4096m in vanilla. This leaves batches North and East
    /// edges with 3 batches on their z/x rather than the usual 5 (This is further compounded at
    /// their respective intersection). Extending world bounds causes the .optoctrees to be
    /// improperly read (expecting 125 octrees when there are less), so swapping in the original
    /// bounds when appropriate is necessary
    /// </summary>
    [HarmonyPatch(typeof(BatchOctrees), nameof(BatchOctrees.LoadOctrees))]
    private static class FixVanillaPositiveBatchEdge
    {
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions, ILGenerator generator
        ) => new CodeMatcher(instructions, generator)
            .MatchStartForward(
                new(OpCodes.Ldfld, AccessTools.Field(typeof(BatchOctreesStreamer), 
                        nameof(BatchOctreesStreamer.octreeBounds))),
                new(OpCodes.Stloc_S)
            )
            .ThrowIfNotMatch(
                "could not transpile " +
                $"{typeof(BatchOctrees)}.{nameof(BatchOctrees.LoadOctrees)}: method does not " +
                $"load {nameof(BatchOctreesStreamer)}.{nameof(BatchOctreesStreamer.octreeBounds)}"
            )
            .Advance(2)
            .CreateLabel(out Label skipVanillaOctreesOverride)
            .Insert(
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, AccessTools.PropertyGetter(typeof(BatchOctrees),
                    nameof(BatchOctrees.id))
                ),
                CodeInstruction.CallClosure(bool (Int3 batchId) => {
                    if (TerrainPatching.patchedBatches.ContainsKey(batchId)) {
                        return false;
                    }
                    if (batchId.z == 25 || batchId.x == 25) return true;
                    return false;
                }),
                new(OpCodes.Brfalse_S, skipVanillaOctreesOverride),
                CodeInstruction.LoadField(typeof(HarmonyPatches), nameof(VANILLA_OCTREE_BOUNDS)),
                new(OpCodes.Stloc_S, 8)
            )
            .InstructionEnumeration();
    }
}
