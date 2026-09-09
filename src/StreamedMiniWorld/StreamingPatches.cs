using System.Collections;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TerrainPatcher.StreamedMiniWorld;

internal static class StreamingPatches {
    private static readonly float MeshVertexScale = 0.25f * (
        1 << MeshBuilding.levelSettings.downsamples
    );

    [HarmonyPatch(typeof(MiniWorld), nameof(MiniWorld.GetOrMakeChunk))]
    private static class PostfixGetOrMakeChunk { // TODO: descriptive name
        private static void Postfix(MiniWorld __instance, Int3 chunkId) {
            MiniWorld.Chunk chunk = __instance.loadedChunks[chunkId];
            chunk.gameObject.transform.localScale =
                Vector3.one * (__instance.chunkScale * MeshVertexScale);

            Vector3 miniWorldStreamingOriginOffset = LargeWorldStreamer.main.land.transform
                .InverseTransformPoint(__instance.transform.position);

            UpdateIndividualChunkPosition(
                miniWorldStreamingOriginOffset, __instance, chunkId, chunk
            );
        }
    }

    [HarmonyPatch(typeof(MiniWorld), nameof(MiniWorld.RebuildHologram))]
    private static class PrefixRebuildHologram { // TODO: descriptive name
        private static bool Prefix(MiniWorld __instance, ref IEnumerator __result) {
            __result = RebuildHologramWithStreamingAsync(__instance);
            return false;
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
                Int3 mapCenterCell = Int3.FloorDiv(mapCenterBlock, MeshBuilding.cellSize);

                Int3 minBlock = mapCenterBlock - miniWorld.mapWorldRadius;
                Int3 minCell = Int3.FloorDiv(minBlock, MeshBuilding.cellSize);

                Int3 maxBlock = mapCenterBlock + miniWorld.mapWorldRadius;
                Int3 maxCell = Int3.FloorDiv(maxBlock, MeshBuilding.cellSize);

                Vector3 startedLoadingPos = miniWorld.transform.position;
                Int3[] batches = CellUtils.OrderCellsAroundCenter(minCell, maxCell, mapCenterCell);
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
    private static class PrefixUpdatePosition { // TODO: descriptive name
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
            return false;
        }
    }

    private static void UpdateIndividualChunkPosition(
        Vector3 miniWorldStreamingOriginOffset, MiniWorld miniWorld,
        Int3 chunkId, MiniWorld.Chunk chunk
    ) {
        Vector3 cellPosLocalSpace =
            (chunkId * MeshBuilding.cellSize).ToVector3() - miniWorldStreamingOriginOffset;
        chunk.gameObject.transform.localPosition = cellPosLocalSpace * (miniWorld.chunkScale / 4);
    }
}
