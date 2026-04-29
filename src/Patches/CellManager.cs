using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace TerrainPatcher;

internal static class CellManagerPatches
{
    internal static void Patch(Harmony harmony)
    {
        harmony.PatchAll(typeof(CellManager_RegisterCellEntity_Patch));
    }

    [HarmonyPatch(typeof(CellManager), nameof(CellManager.RegisterCellEntity))]
    private static class CellManager_RegisterCellEntity_Patch
    {
        // replace div and rem by blocksPerBatch with floor div and floor rem
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        ) => new CodeMatcher(instructions)
            .MatchStartForward([
                new(OpCodes.Callvirt, AccessTools.PropertyGetter(
                    typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.blocksPerBatch)
                )),
                new(OpCodes.Call, AccessTools.Method(
                    typeof(Int3), "op_Division", [typeof(Int3), typeof(Int3)]
                )),
            ])
            .ThrowIfNotMatch(
                $"Could not transpile {typeof(CellManager)}." +
                $"{nameof(CellManager.RegisterCellEntity)}: method does not contain expected code"
            )
            .Advance(2)
            .Insert([
                new(OpCodes.Pop),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Call, AccessTools.Method(
                    typeof(CellManagerPatches), nameof(CellManagerPatches.GetBatchId)
                )),
            ])
            .Start()
            .MatchStartForward([
                new(OpCodes.Callvirt, AccessTools.PropertyGetter(
                    typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.blocksPerBatch)
                )),
                new(OpCodes.Call, AccessTools.Method(
                    typeof(Int3), "op_Modulus", [typeof(Int3), typeof(Int3)]
                )),
            ])
            .ThrowIfNotMatch(
                $"Could not transpile {typeof(CellManager)}." +
                $"{nameof(CellManager.RegisterCellEntity)}: method does not contain expected code"
            )
            .Advance(2)
            .Insert([
                new(OpCodes.Pop),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Call, AccessTools.Method(
                    typeof(CellManagerPatches), nameof(CellManagerPatches.GetLocalPos)
                )),
            ])
            .InstructionEnumeration();
    }

    private static Int3 GetBatchId(CellManager cellManager, LargeWorldEntity entity)
    {
        var block = cellManager.streamer.GetBlock(entity.transform.position);
        var blocksPerBatch = cellManager.streamer.blocksPerBatch;
        return new(
            Utils.DivFloor(block.x, blocksPerBatch.x),
            Utils.DivFloor(block.y, blocksPerBatch.y),
            Utils.DivFloor(block.z, blocksPerBatch.z)
        );
    }

    private static Int3 GetLocalPos(CellManager cellManager, LargeWorldEntity entity)
    {
        var block = cellManager.streamer.GetBlock(entity.transform.position);
        var blocksPerBatch = cellManager.streamer.blocksPerBatch;
        return new(
            Utils.RemFloor(block.x, blocksPerBatch.x),
            Utils.RemFloor(block.y, blocksPerBatch.y),
            Utils.RemFloor(block.z, blocksPerBatch.z)
        );
    }
}
