using System.Collections;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TerrainPatcher.StreamedMiniWorld;

internal static class StreamingPatches {
    private static readonly float MESH_VERTEX_SCALE = 0.25f * (
        1 << MeshBuilding.LEVEL_SETTINGS.downsamples
    );

    [HarmonyPatch(typeof(MiniWorld), nameof(MiniWorld.GetOrMakeChunk))]
    private static class PositionAndScaleOnChunkCreation {
        private static void Postfix(MiniWorld __instance, Int3 chunkId) {
            MiniWorld.Chunk chunk = __instance.loadedChunks[chunkId];
            chunk.gameObject.transform.localScale =
                Vector3.one * (__instance.chunkScale * MESH_VERTEX_SCALE);

            Vector3 miniWorldStreamingOriginOffset = LargeWorldStreamer.main.land.transform
                .InverseTransformPoint(__instance.transform.position);

            UpdateIndividualChunkPosition(
                miniWorldStreamingOriginOffset, __instance, chunkId, chunk
            );
            
            if(!__instance.updatePosition) UpdateDesiredRadius(__instance);
        }
    }

    [HarmonyPatch(typeof(MiniWorld), nameof(MiniWorld.RebuildHologram))]
    private static class RebuildHologramWithWorldStreamer {
        private static bool Prefix(MiniWorld __instance, ref IEnumerator? __result) {
            __result = RebuildHologramWithStreamingAsync(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(MiniWorld), nameof(MiniWorld.ClearUnusedChunks))]
    private static class UpdateFadingOnChunkRemoval {
        private static void Postfix(MiniWorld __instance) {
            if(!__instance.updatePosition) UpdateDesiredRadius(__instance);
        }
    }

    private static readonly ConditionalWeakTable<MiniWorld, FadeState> cwt = new();
    private class FadeState {
        internal FadeState(MiniWorld miniWorld) {
            scannerRoom = miniWorld.GetComponentInParent<MapRoomFunctionality>();
        }
        internal bool isValid;
        internal float currentWorldRadius;
        internal float maxWorldRadius;
        
        internal readonly MapRoomFunctionality? scannerRoom;
    };
        
    [HarmonyPatch(typeof(MiniWorld), nameof(MiniWorld.Update))]
    private static class CheckIfFadingNeedsToUpdate {
        private static void Postfix(MiniWorld __instance)
        {
            if (!cwt.TryGetValue(__instance, out FadeState state)) return;
            
            if (__instance.updatePosition || !state.isValid) {
                LerpFading(__instance, state);
            }
        }
    }

    private static void UpdateDesiredRadius(MiniWorld miniWorld) {
        if (!cwt.TryGetValue(miniWorld, out FadeState state)) {
            state = new FadeState(miniWorld);
            cwt.Add(miniWorld, state);
        }
        
        int maxRadius;
        if (state.scannerRoom != null) maxRadius = (int) state.scannerRoom.scanRange;
        else maxRadius = miniWorld.mapWorldRadius;
        
        Transform origin = LargeWorldStreamer.main.land.transform;
        Vector3 chunkSpaceCenter = origin.InverseTransformPoint(miniWorld.transform.position);
        
        state.maxWorldRadius = CellUtils.MinDistanceToEdge(
            chunkSpaceCenter,
            chunkSize: MeshBuilding.CELL_SIZE,
            mapRadius: maxRadius,
            loadedChunks: miniWorld.loadedChunks
        );
        state.isValid = false;
    }
    
    private static void LerpFading(MiniWorld miniWorld, FadeState state) {
        if (state.maxWorldRadius < state.currentWorldRadius) {
            state.currentWorldRadius = state.maxWorldRadius;
        }
        else state.currentWorldRadius = Mathf.Lerp(
            state.currentWorldRadius, state.maxWorldRadius,
            (miniWorld.hologramRadius * Time.deltaTime) / 4
        );

        if (Mathf.Abs(state.currentWorldRadius - state.maxWorldRadius) < 0.5f) {
            state.currentWorldRadius = state.maxWorldRadius;
            state.isValid = true;
        }
        
        float fadeRadius = WorldRadiusToFadeRadius(state.currentWorldRadius, miniWorld.hologramRadius);
        miniWorld.materialInstance.SetFloat(ShaderPropertyID._FadeRadius, fadeRadius);
        
        static float WorldRadiusToFadeRadius(float worldRadius, float hologramRadius) {
            return worldRadius * (hologramRadius * (1.0f / 750.0f) - (1.0f / 100.0f));
        }
    }
    
    private static IEnumerator RebuildHologramWithStreamingAsync(MiniWorld miniWorld) {
        yield return new WaitUntil(()
            => LargeWorldStreamer.main.streamerV2.clipmapStreamer != null
        );

        if (!miniWorld.updatePosition) {
            miniWorld.hologramHolder.rotation = Quaternion.identity;
            miniWorld.materialInstance.SetVector(
                ShaderPropertyID._MapCenterWorldPos, miniWorld.transform.position
            );
        }

        bool isPickupable = miniWorld.GetComponentInParent<Pickupable>() != null;
        while (miniWorld != null) {
            if (
                !miniWorld.gameObject.activeInHierarchy ||
                (isPickupable && miniWorld.GetComponentInParent<Player>() == null)
            ) {
                miniWorld.ClearAllChunks();
            } else if (miniWorld.gameObject.activeInHierarchy) {
                Int3 mapCenterBlock = LargeWorldStreamer.main.GetBlock(
                    miniWorld.transform.position
                );

                Int3 minBlock = mapCenterBlock - miniWorld.mapWorldRadius;
                Int3 minChunk = Int3.FloorDiv(minBlock, MeshBuilding.CELL_SIZE);

                Int3 maxBlock = mapCenterBlock + miniWorld.mapWorldRadius;
                Int3 maxChunk = Int3.FloorDiv(maxBlock, MeshBuilding.CELL_SIZE);
                
                Transform origin = LargeWorldStreamer.main.land.transform;
                Vector3 chunkSpaceCenter = origin.InverseTransformPoint(miniWorld.transform.position);
                
                Int3[] batches = CellUtils.OrderCellsAroundCenter(chunkSpaceCenter, MeshBuilding.CELL_SIZE, minChunk, maxChunk);
                
                Vector3 startedLoadingPos = miniWorld.transform.position;
                foreach (Int3 chunkId in batches) {
                    miniWorld.requestChunks.Add(chunkId);
                    if ((startedLoadingPos - miniWorld.transform.position).sqrMagnitude > 50) {
                        continue;
                    }

                    if (miniWorld.GetChunkExists(chunkId)) continue;
                    AsyncOperationHandle<Mesh> request =
                        BuildMeshOperation.Start(chunkId);
                    yield return request;

                    if (miniWorld == null) {
                        AddressablesUtility.QueueRelease(ref request);
                        yield break;
                    }

                    if (
                        request.Status == AsyncOperationStatus.Failed ||
                        miniWorld.GetChunkExists(chunkId)
                    ) continue;

                    miniWorld.GetOrMakeChunk(chunkId, request, "Streamed Modded Batch");
                }
                miniWorld.ClearUnusedChunks(miniWorld.requestChunks);
                miniWorld.requestChunks.Clear();
            }
            yield return new WaitForSeconds(1f);
        }
    }

    [HarmonyPatch(typeof(MiniWorld), nameof(MiniWorld.UpdatePosition))]
    private static class UpdatePositionWithStreamedChunkSize {
        private static bool Prefix(MiniWorld __instance) {
            __instance.hologramHolder.rotation = Quaternion.identity;
            __instance.materialInstance.SetVector(
                ShaderPropertyID._MapCenterWorldPos, __instance.transform.position
            );
            Vector3 miniWorldStreamingOriginOffset = LargeWorldStreamer.main.land.transform
                .InverseTransformPoint(__instance.transform.position);

            foreach (KeyValuePair<Int3, MiniWorld.Chunk> keyValuePair in __instance.loadedChunks) {
                MiniWorld.Chunk chunk = keyValuePair.Value;
                UpdateIndividualChunkPosition(
                    miniWorldStreamingOriginOffset, __instance, keyValuePair.Key, chunk
                );
            }
            UpdateDesiredRadius(__instance);
            return false;
        }
    }

    private static void UpdateIndividualChunkPosition(
        Vector3 miniWorldStreamingOriginOffset, MiniWorld miniWorld,
        Int3 chunkId, MiniWorld.Chunk chunk
    ) {
        Vector3 cellPosLocalSpace =
            (chunkId * MeshBuilding.CELL_SIZE).ToVector3() - miniWorldStreamingOriginOffset;
        chunk.gameObject.transform.localPosition = cellPosLocalSpace * (miniWorld.chunkScale / 4);
    }
}
