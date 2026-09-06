using System.Collections;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TerrainPatcher;

[HarmonyPatch(typeof(MiniWorld))]
[SuppressMessage("Method Declaration", "Harmony003:Harmony non-ref patch parameters modified")]
internal static class MiniWorldStreamingPatches {
    private static readonly float MeshVertexScale = 0.25f * (1 << MiniWorldMeshBuilding.levelSettings.downsamples);
    
    [HarmonyPatch(typeof(MiniWorld), nameof(MiniWorld.GetOrMakeChunk))]
    [HarmonyPostfix]
    private static void GetOrMakeChunk_Postfix(MiniWorld __instance, Int3 chunkId) {
        MiniWorld.Chunk chunk = __instance.loadedChunks[chunkId];
        chunk.gameObject.transform.localScale = Vector3.one * (__instance.chunkScale * MeshVertexScale);
        Vector3 miniWorldStreamingOriginOffset = LargeWorldStreamer.main.land.transform.InverseTransformPoint(__instance.transform.position);
        UpdateIndividualChunkPosition(miniWorldStreamingOriginOffset, __instance, chunkId, chunk);
    }
    
    [HarmonyPatch(typeof(MiniWorld), nameof(MiniWorld.RebuildHologram))]
    [HarmonyPrefix]
    private static bool RebuildHologram_Prefix(ref IEnumerator __result, MiniWorld __instance) {
        __result = RebuildHologram_WithStreamingAsync(__instance);
        return false;
    }
    
    private static IEnumerator RebuildHologram_WithStreamingAsync(MiniWorld __instance) {
        yield return new WaitUntil(() => LargeWorldStreamer.main.streamerV2.clipmapStreamer != null);

        if (!__instance.updatePosition) {
            __instance.hologramHolder.rotation = Quaternion.identity;
            __instance.materialInstance.SetVector(ShaderPropertyID._MapCenterWorldPos, __instance.transform.position);
        }
        
        bool isPickupable = __instance.GetComponentInParent<Pickupable>() != null;
        while (__instance != null) {
            if (!__instance.gameObject.activeInHierarchy || (isPickupable && __instance.GetComponentInParent<Player>() == null)) {
                __instance.ClearAllChunks();
            }
            else if (__instance.gameObject.activeInHierarchy) {
                Int3 mapCenterBlock = LargeWorldStreamer.main.GetBlock(__instance.transform.position);
                Int3 mapCenterCell = Int3.FloorDiv(mapCenterBlock, MiniWorldMeshBuilding.cellSize);
                
                Int3 minBlock = mapCenterBlock - __instance.mapWorldRadius;
                Int3 minCell = Int3.FloorDiv(minBlock, MiniWorldMeshBuilding.cellSize);
                
                Int3 maxBlock = mapCenterBlock + __instance.mapWorldRadius;
                Int3 maxCell = Int3.FloorDiv(maxBlock, MiniWorldMeshBuilding.cellSize);
                
                Vector3 startedLoadingPos = __instance.transform.position;
                Int3[] batches = CellUtils.OrderCellsAroundCenter(minCell, maxCell, mapCenterCell);
                foreach(Int3 chunkId in batches) {
                    __instance.requestChunks.Add(chunkId);
                    if ((startedLoadingPos - __instance.transform.position).sqrMagnitude > 50) continue;
                    
                    if(__instance.GetChunkExists(chunkId)) continue;
                    AsyncOperationHandle<Mesh> request = StreamMiniWorldChunkOperation.Start(chunkId);
                    yield return request;
                    if (__instance == null) {
                        AddressablesUtility.QueueRelease(ref request);
                        yield break;
                    }
                    if (request.Status == AsyncOperationStatus.Failed || __instance.GetChunkExists(chunkId)) {
                        continue;
                    }
                    __instance.GetOrMakeChunk(chunkId, request, "Streamed Modded Batch");
                }
                __instance.ClearUnusedChunks(__instance.requestChunks);
                __instance.requestChunks.Clear();
            }
            yield return new WaitForSeconds(1f);
        }
    }
    
    [HarmonyPatch(typeof(MiniWorld), nameof(MiniWorld.UpdatePosition))]
    [HarmonyPrefix]
    private static bool UpdatePosition_Prefix(MiniWorld __instance) {
        __instance.hologramHolder.rotation = Quaternion.identity;
        __instance.materialInstance.SetVector(ShaderPropertyID._MapCenterWorldPos, __instance.transform.position);
        Vector3 miniWorldStreamingOriginOffset = LargeWorldStreamer.main.land.transform.InverseTransformPoint(__instance.transform.position);
        foreach (KeyValuePair<Int3, MiniWorld.Chunk> keyValuePair in __instance.loadedChunks) {
            MiniWorld.Chunk chunk = keyValuePair.Value;
            UpdateIndividualChunkPosition(miniWorldStreamingOriginOffset, __instance, keyValuePair.Key, chunk);
        }
        return false;
    }

    private static void UpdateIndividualChunkPosition(Vector3 miniWorldStreamingOriginOffset, MiniWorld __instance, Int3 chunkId, MiniWorld.Chunk chunk) {
        Vector3 cellPosLocalSpace = (chunkId * MiniWorldMeshBuilding.cellSize).ToVector3() - miniWorldStreamingOriginOffset;
        chunk.gameObject.transform.localPosition = cellPosLocalSpace * (__instance.chunkScale / 4);
    }
}