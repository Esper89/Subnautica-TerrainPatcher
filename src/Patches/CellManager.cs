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
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            MethodInfo getBlocksPerBatch = AccessTools.PropertyGetter(
                typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.blocksPerBatch)
            );

            return new CodeMatcher(instructions)
                .MatchStartForward([
                    new(OpCodes.Callvirt, getBlocksPerBatch),
                    new(OpCodes.Call, AccessTools.Method(
                        typeof(Int3), "op_Division", [typeof(Int3), typeof(Int3)]
                    ))
                ])
                .ThrowIfNotMatch(
                    $"Could not transpile {typeof(CellManager)}." +
                    $"{nameof(CellManager.RegisterCellEntity)}: first match failed"
                )
                .Advance(1)
                .SetInstruction(new(OpCodes.Call, AccessTools.Method(
                    typeof(CellManagerPatches), nameof(CellManagerPatches.VoxelPosToBatchId)
                )))
                .MatchStartForward([
                    new(OpCodes.Callvirt, getBlocksPerBatch),
                    new(OpCodes.Call, AccessTools.Method(
                        typeof(Int3), "op_Modulus", [typeof(Int3), typeof(Int3)]
                    ))
                ])
                .ThrowIfNotMatch(
                    $"Could not transpile {typeof(CellManager)}." +
                    $"{nameof(CellManager.RegisterCellEntity)}: second match failed"
                )
                .Advance(1)
                .Insert([new(OpCodes.Ldloc_1)])
                .Advance(1)
                .SetInstruction(new(OpCodes.Call, AccessTools.Method(
                    typeof(CellManagerPatches),
                    nameof(CellManagerPatches.GlobalVoxelToLocalVoxelPos)
                )))
                .InstructionEnumeration();
        }
    }

    // Converts a voxel position (relative to Voxeland origin) into a batch ID. Works for positive
    // and negative voxel positions.
    private static Int3 VoxelPosToBatchId(Int3 block, Int3 blocksPerBatch) => new Int3(
        Mathf.FloorToInt((float) block.x / blocksPerBatch.x),
        Mathf.FloorToInt((float) block.y / blocksPerBatch.y),
        Mathf.FloorToInt((float) block.z / blocksPerBatch.z)
    );

    // Converts a voxel position to a local position within the given batch. Works for positive and
    // negative voxel positions.
    private static Int3 GlobalVoxelToLocalVoxelPos(Int3 block, Int3 batchId, Int3 blocksPerBatch)
        => block - (batchId * blocksPerBatch);
}
