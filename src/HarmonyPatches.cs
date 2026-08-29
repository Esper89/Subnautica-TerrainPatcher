using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using WorldStreaming;

namespace TerrainPatcher;

internal static class HarmonyPatches {
    private static readonly Regex BATCH_NAME_PATTERN = new(@"^compiled-batch-(-?\d+)-(-?\d+)-(-?\d+)\.optoctrees$");
    
    internal static readonly Int3.Bounds EXTENDED_BATCH_BOUNDS = new(
        new(short.MinValue, short.MinValue, short.MinValue),
        new(short.MaxValue, short.MaxValue, short.MaxValue)
    );
    
    [HarmonyPatch(typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.GetCompiledOctreesCachePath))]
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
        if (!runOriginal || !TerrainPatching.patchedBatches.TryGetValue(batchId, out var batch)) return true;
        result = batch.path;
        return false;
    }

    [HarmonyPatch(typeof(BatchOctrees), nameof(BatchOctrees.LoadOctrees))]
    private static class FixOctreeScrambling {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => new CodeMatcher(instructions)
                .MatchStartForward(CodeInstruction.Call(
                    typeof(Int3.Bounds), nameof(Int3.Bounds.Contains), [typeof(Int3)]
                ))
                .ThrowIfNotMatch(
                    "could not transpile " +
                    $"{typeof(BatchOctrees)}.{nameof(BatchOctrees.LoadOctrees)}: method does not " +
                    $"call {typeof(Int3.Bounds)}.{nameof(Int3.Bounds.Contains)}({typeof(Int3)})"
                )
                .Advance(1)
                .Insert(
                    new(OpCodes.Pop),
                    new(OpCodes.Ldc_I4_1)
                )
                .InstructionEnumeration();
    }
}
