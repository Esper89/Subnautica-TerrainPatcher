using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using UWE;
using WorldStreaming;

namespace TerrainPatcher;

static class HarmonyPatches {
    [HarmonyPatch(
        typeof(LargeWorldStreamer),
        nameof(LargeWorldStreamer.GetCompiledOctreesCachePath)
    )]
    static class ParseAndReplaceBatchFilePath {
        static bool Prefix(string filename, ref string? __result, bool __runOriginal) {
            Int3 batchId;
            try {
                var match = HarmonyPatches.BATCH_NAME_PATTERN.Match(Path.GetFileName(filename));
                batchId = new Int3(
                    int.Parse(match.Groups[1].Value),
                    int.Parse(match.Groups[2].Value),
                    int.Parse(match.Groups[3].Value)
                );
            } catch (FormatException) {
                Mod.LogWarning($"Game accessed batch file with invalid filename: {filename}");
                return true;
            }

            return HarmonyPatches.GetBatchFilePath(batchId, ref __result, __runOriginal);
        }
    }

    static readonly Regex BATCH_NAME_PATTERN
        = new(@"^compiled-batch-(-?\d+)-(-?\d+)-(-?\d+)\.optoctrees$");

    [HarmonyPatch(typeof(BatchOctreesStreamer), nameof(BatchOctreesStreamer.GetPath))]
    static class ChangeBatchFilePath {
        static bool Prefix(Int3 batchId, ref string? __result, bool __runOriginal)
            => HarmonyPatches.GetBatchFilePath(batchId, ref __result, __runOriginal);
    }

    static bool GetBatchFilePath(Int3 batchId, ref string? result, bool runOriginal) {
        if (runOriginal && TerrainPatching.patchedBatches.ContainsKey(batchId)) {
            result = TerrainPatching.patchedBatches[batchId].path;
            return false;
        } else {
            return true;
        }
    }

    [HarmonyPatch(typeof(WorldStreamer), nameof(WorldStreamer.CreateStreamers))]
    static class ExtendWorldStreamerBounds {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => new CodeMatcher(instructions)
                .MatchStartForward(CodeInstruction.Call(
                    typeof(WorldStreamer), nameof(WorldStreamer.ParseStreamingSettings)
                ))
                .ThrowIfNotMatch(
                    "could not transpile " +
                    $"{typeof(WorldStreamer)}.{nameof(WorldStreamer.CreateStreamers)}: method " +
                    "does not call " +
                    $"{typeof(WorldStreamer)}.{nameof(WorldStreamer.ParseStreamingSettings)}"
                )
                .Advance(1)
                .Insert(CodeInstruction.CallClosure((LargeWorldStreamer.Settings settings) => {
                    settings.octreesSettings.centerMin = HarmonyPatches.BATCH_BOUNDS.mins;
                    settings.octreesSettings.centerMax = HarmonyPatches.BATCH_BOUNDS.maxs;
                    return settings;
                }))
                .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(BatchOctreesStreamer), MethodType.Constructor, [
        typeof(IThread), typeof(Int3.Bounds), typeof(int), typeof(int), typeof(int), typeof(int),
        typeof(string), typeof(BatchOctreesStreamer.Settings),
    ])]
    static class ExtendOctreeStreamerBounds {
        static void Prefix(ref Int3.Bounds octreeBounds, int numOctreesPerBatch)
            => octreeBounds = HarmonyPatches.BATCH_BOUNDS * numOctreesPerBatch;
    }

    static readonly Int3.Bounds BATCH_BOUNDS = new(
        new(Int16.MinValue, Int16.MinValue, Int16.MinValue),
        new(Int16.MaxValue, Int16.MaxValue, Int16.MaxValue)
    );

    [HarmonyPatch(typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.CheckBatch))]
    static class AllowOutOfBoundsBatch {
        static bool Prefix(ref bool __result) { __result = true; return false; }
    }

    [HarmonyPatch(
        typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.CheckRoot),
        [typeof(int), typeof(int), typeof(int)]
    )]
    static class AllowOutOfBoundsRoot {
        static bool Prefix(ref bool __result) { __result = true; return false; }
    }

    [HarmonyPatch(typeof(BatchOctrees), nameof(BatchOctrees.LoadOctrees))]
    static class FixOctreeScrambling {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
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
                .Insert([
                    new(OpCodes.Pop),
                    new(OpCodes.Ldc_I4_1),
                ])
                .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(CellManager), nameof(CellManager.RegisterCellEntity))]
    static class FixNegativeEntityCells {
        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        ) => new CodeMatcher(instructions)
            .MatchStartForward([
                new(OpCodes.Callvirt, AccessTools.PropertyGetter(
                    typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.blocksPerBatch)
                )),
                CodeInstruction.Call(typeof(Int3), "op_Division", [typeof(Int3), typeof(Int3)]),
            ])
            .ThrowIfNotMatch(
                "could not transpile " +
                $"{{typeof(CellManager)}}.{nameof(CellManager.RegisterCellEntity)}: method does " +
                "not divide by " +
                $"{typeof(LargeWorldStreamer)}.{nameof(LargeWorldStreamer.blocksPerBatch)} get"
            )
            .Advance(2)
            .Insert([
                new(OpCodes.Pop),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                CodeInstruction.CallClosure((CellManager cellMgr, LargeWorldEntity entity) => {
                    var block = cellMgr.streamer.GetBlock(entity.transform.position);
                    var blocksPerBatch = cellMgr.streamer.blocksPerBatch;
                    return new Int3(
                        Utils.DivFloor(block.x, blocksPerBatch.x),
                        Utils.DivFloor(block.y, blocksPerBatch.y),
                        Utils.DivFloor(block.z, blocksPerBatch.z)
                    );
                }),
            ])
            .Start()
            .MatchStartForward([
                new(OpCodes.Callvirt, AccessTools.PropertyGetter(
                    typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.blocksPerBatch)
                )),
                CodeInstruction.Call(typeof(Int3), "op_Modulus", [typeof(Int3), typeof(Int3)]),
            ])
            .ThrowIfNotMatch(
                "could not transpile " +
                $"{typeof(CellManager)}.{nameof(CellManager.RegisterCellEntity)}: method does " +
                "not take the remainder of division by " +
                $"{typeof(LargeWorldStreamer)}.{nameof(LargeWorldStreamer.blocksPerBatch)} get"
            )
            .Advance(2)
            .Insert([
                new(OpCodes.Pop),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                CodeInstruction.CallClosure((CellManager cellMgr, LargeWorldEntity entity) => {
                    var block = cellMgr.streamer.GetBlock(entity.transform.position);
                    var blocksPerBatch = cellMgr.streamer.blocksPerBatch;
                    return new Int3(
                        Utils.RemFloor(block.x, blocksPerBatch.x),
                        Utils.RemFloor(block.y, blocksPerBatch.y),
                        Utils.RemFloor(block.z, blocksPerBatch.z)
                    );
                }),
            ])
            .InstructionEnumeration();
    }
}
