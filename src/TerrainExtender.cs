using System.Reflection.Emit;
using HarmonyLib;
using UWE;
using WorldStreaming;

namespace TerrainPatcher;

internal static class TerrainExtender {
    internal static readonly Int3.Bounds EXTENDED_BATCH_BOUNDS = new(
        new(short.MinValue, short.MinValue, short.MinValue),
        new(short.MaxValue, short.MaxValue, short.MaxValue)
    );

    private static readonly Int3.Bounds VANILLA_OCTREE_BOUNDS = new(Int3.zero, new(127, 99, 127));

    [HarmonyPatch(typeof(WorldStreamer), nameof(WorldStreamer.CreateStreamers))]
    private static class ExtendWorldStreamerBounds {
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        ) => new CodeMatcher(instructions)
            .MatchStartForward(CodeInstruction.Call(
                typeof(WorldStreamer), nameof(WorldStreamer.ParseStreamingSettings)
            ))
            .ThrowIfNotMatch(
                "could not transpile " +
                $"{typeof(WorldStreamer)}.{nameof(WorldStreamer.CreateStreamers)}: method does " +
                $"not call {typeof(WorldStreamer)}.{nameof(WorldStreamer.ParseStreamingSettings)}"
            )
            .Advance(1)
            .Insert(CodeInstruction.CallClosure((LargeWorldStreamer.Settings settings) => {
                settings.octreesSettings.centerMin = EXTENDED_BATCH_BOUNDS.mins;
                settings.octreesSettings.centerMax = EXTENDED_BATCH_BOUNDS.maxs;
                return settings;
            }))
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(BatchOctreesStreamer), MethodType.Constructor, [
        typeof(IThread), typeof(Int3.Bounds), typeof(int), typeof(int), typeof(int), typeof(int),
        typeof(string), typeof(BatchOctreesStreamer.Settings),
    ])]
    private static class ExtendOctreeStreamerBounds {
        private static void Prefix(ref Int3.Bounds octreeBounds, int numOctreesPerBatch)
            => octreeBounds = EXTENDED_BATCH_BOUNDS * numOctreesPerBatch;
    }

    [HarmonyPatch(typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.CheckBatch))]
    private static class AllowOutOfBoundsBatch {
        private static bool Prefix(ref bool __result) { __result = true; return false; }
    }

    [HarmonyPatch(typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.CheckRoot), [
        typeof(int), typeof(int), typeof(int),
    ])]
    private static class AllowOutOfBoundsRoot {
        private static bool Prefix(ref bool __result) { __result = true; return false; }
    }

    /// <summary>Replace truncating division and truncating remainder (which round towards zero)
    /// with floor division and floor remainder (which round towards negative infinity) to fix the
    /// game's handling of entities in negative cells. Truncating division and remainder operations
    /// are not suitable for grid math with negative coordinates.</summary>
    [HarmonyPatch(typeof(CellManager), nameof(CellManager.RegisterCellEntity))]
    private static class FixNegativeEntityCells {
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        ) => new CodeMatcher(instructions)
            .MatchEndForward([
                new(OpCodes.Callvirt, AccessTools.PropertyGetter(
                    typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.blocksPerBatch)
                )),
                CodeInstruction.Call(typeof(Int3), "op_Division", [typeof(Int3), typeof(Int3)]),
            ])
            .ThrowIfNotMatch(
                "could not transpile " +
                $"{typeof(CellManager)}.{nameof(CellManager.RegisterCellEntity)}: method does " +
                "not divide by " +
                $"{typeof(LargeWorldStreamer)}.{nameof(LargeWorldStreamer.blocksPerBatch)} get"
            )
            .Advance(1)
            .Insert([
                new(OpCodes.Pop),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                CodeInstruction.CallClosure((CellManager cellMgr, LargeWorldEntity entity) => {
                    Int3 block = cellMgr.streamer.GetBlock(entity.transform.position);
                    Int3 blocksPerBatch = cellMgr.streamer.blocksPerBatch;
                    return Int3.FloorDiv(block, blocksPerBatch);
                }),
            ])
            .Start()
            .MatchEndForward([
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
            .Advance(1)
            .Insert([
                new(OpCodes.Pop),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                CodeInstruction.CallClosure((CellManager cellMgr, LargeWorldEntity entity) => {
                    Int3 block = cellMgr.streamer.GetBlock(entity.transform.position);
                    Int3 blocksPerBatch = cellMgr.streamer.blocksPerBatch;
                    return Int3.PositiveModulo(block, blocksPerBatch);
                }),
            ])
            .InstructionEnumeration();
    }

    /// <summary>The vanilla world is 4096 m × 3200 m × 4096 m. This leaves batches along the north
    /// and east edges of the world that are only 3 octrees wide along their Z/X dimensions rather
    /// than the usual 5 octrees wide, which is further compounded at the northeast corner of the
    /// world. Extending the world bounds causes the game's vanilla `.optoctrees` files to be
    /// improperly read, expecting 125 octrees when there are less, so swapping in the original
    /// bounds when appropriate is necessary.</summary>
    [HarmonyPatch(typeof(BatchOctrees), nameof(BatchOctrees.LoadOctrees))]
    private static class FixVanillaPositiveWorldEdge {
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        ) => new CodeMatcher(instructions)
            .MatchEndForward([
                new(OpCodes.Ldfld, AccessTools.Field(
                    typeof(BatchOctreesStreamer), nameof(BatchOctreesStreamer.octreeBounds)
                )),
                new(instr => instr.IsStloc()),
            ])
            .ThrowIfNotMatch(
                "could not transpile " +
                $"{typeof(BatchOctrees)}.{nameof(BatchOctrees.LoadOctrees)}: method does not " +
                "load " +
                $"{nameof(BatchOctreesStreamer)}.{nameof(BatchOctreesStreamer.octreeBounds)} " +
                "and store it in a local variable"
            )
            .Insert([
                new(OpCodes.Ldarg_0),
                CodeInstruction.CallClosure((Int3.Bounds currBounds, BatchOctrees self) =>
                    !TerrainPatching.PatchTerrain.patchedBatches.ContainsKey(self.id) &&
                    (self.id.z == 25 || self.id.x == 25)
                        ? VANILLA_OCTREE_BOUNDS
                        : currBounds
                ),
            ])
            .InstructionEnumeration();
    }
}
