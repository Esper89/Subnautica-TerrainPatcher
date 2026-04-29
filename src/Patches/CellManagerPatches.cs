using System;
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
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo get_blocksPerBatch = AccessTools.Method(typeof(LargeWorldStreamer), "get_blocksPerBatch");
            return new CodeMatcher(instructions)
                .MatchStartForward([
                    new CodeMatch(OpCodes.Callvirt, get_blocksPerBatch),
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Int3), "op_Division", [typeof(Int3), typeof(Int3)]))
                ])
                .ThrowIfNotMatch(
                    $"Could not transpile {typeof(CellManager)}." +
                    $"{nameof(CellManager.RegisterCellEntity)}: method does not use " +
                    $"{typeof(LargeWorldStreamer)}.get_blocksPerBatch")
                .Advance(1)//advance onto the division operation to replace it
                .SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CellManagerPatches), nameof(VoxelPosToBatchId))))
                
                .MatchStartForward([
                    new CodeMatch(OpCodes.Callvirt, get_blocksPerBatch),
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Int3), "op_Modulus", [typeof(Int3), typeof(Int3)]))
                ])
                .Advance(1)//advance onto the Callvirt to replace it
                .Insert([new CodeInstruction(OpCodes.Ldloc_1)])// load the batch index onto the evaluation stack for use in GlobalVoxelToLocalVoxelPos
                .Advance(1)//advance onto the modulus operation to replace it
                .SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CellManagerPatches), nameof(GlobalVoxelToLocalVoxelPos))))
                .InstructionEnumeration();
        }
    }
    
    /// <summary>
    /// Converts a voxel position (must be relative to Voxeland origin) into a batch id.
    /// Works for negative and positive voxel positions.
    /// </summary>
    /// <remarks>
    /// The original simply divided by the blocks per batch, but integer division rounds towards 0.
    /// For positive values this works perfectly fine but for negative values it always results in a batch index off by 1 towards 0 as a result.
    /// For example, if an entity was at x = -40: -40 / 160 = -0.25 as a float. But when it converts to an integer, it rounds up to 0.
    /// Logically this entity has to be in a negative batch with a negative voxel position, but the original rounded up to 0.
    /// Flooring the value works in both positive and negative as negative values round down to negatives whole negatives, while positives behave the same.
    /// </remarks>
    private static Int3 VoxelPosToBatchId(Int3 block,
        Int3 blocksPerBatch) => new Int3(
            Mathf.FloorToInt((float) block.x / blocksPerBatch.x),
            Mathf.FloorToInt((float) block.y / blocksPerBatch.y),
            Mathf.FloorToInt((float) block.z / blocksPerBatch.z)
        );


    /// <summary>
    /// Converts a voxel position to a local position within the given batch.
    /// Works for negative and positive voxel positions
    /// </summary>
    /// <remarks>
    /// The original used a modulus operator but that behaves incorrectly for negative voxel positions.
    /// For example, a voxel at x = -1 would undergo the operation -1 % 160 should result in positive 159 for local indexes to work properly, but
    /// this actually equals -1. Negative local batch positions break how entities are stored so in the fix we ensure that local batch positions are always positive,
    /// which makes more sense at the end of the day anyway.
    /// </remarks>
    private static Int3 GlobalVoxelToLocalVoxelPos(Int3 block, Int3 batchId, Int3 blocksPerBatch) => block - (batchId * blocksPerBatch);
}