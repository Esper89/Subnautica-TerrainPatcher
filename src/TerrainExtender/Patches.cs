using System.Reflection.Emit;
using HarmonyLib;
using UWE;
using WorldStreaming;

namespace TerrainPatcher;

[HarmonyPatch(typeof(CellManager), nameof(CellManager.RegisterCellEntity))]
internal static class FixNegativeEntityCells
{
    private static IEnumerable<CodeInstruction> Transpiler(
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
            CodeInstruction.CallClosure((CellManager cellMgr, LargeWorldEntity entity) =>
            {
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
            CodeInstruction.CallClosure((CellManager cellMgr, LargeWorldEntity entity) =>
            {
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

[HarmonyPatch(typeof(WorldStreamer), nameof(WorldStreamer.CreateStreamers))]
internal static class ExtendWorldStreamerBounds {
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
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
                settings.octreesSettings.centerMin = HarmonyPatches.EXTENDED_BATCH_BOUNDS.mins;
                settings.octreesSettings.centerMax = HarmonyPatches.EXTENDED_BATCH_BOUNDS.maxs;
                return settings;
            }))
            .InstructionEnumeration();
}

[HarmonyPatch(typeof(BatchOctreesStreamer), MethodType.Constructor, 
    typeof(IThread), typeof(Int3.Bounds), typeof(int), typeof(int), typeof(int), 
    typeof(int), typeof(string), typeof(BatchOctreesStreamer.Settings))]
static class ExtendOctreeStreamerBounds {
    static void Prefix(ref Int3.Bounds octreeBounds, int numOctreesPerBatch) 
        => octreeBounds = HarmonyPatches.EXTENDED_BATCH_BOUNDS * numOctreesPerBatch;
}

[HarmonyPatch(typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.CheckBatch))]
static class AllowOutOfBoundsBatch {
    static bool Prefix(ref bool __result) { __result = true; return false; }
}

[HarmonyPatch(typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.CheckRoot), typeof(int), typeof(int), typeof(int))]
static class AllowOutOfBoundsRoot {
    static bool Prefix(ref bool __result) { __result = true; return false; }
}