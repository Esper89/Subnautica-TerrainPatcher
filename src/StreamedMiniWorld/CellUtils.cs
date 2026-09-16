using FMODUnity;
using UnityEngine;

namespace TerrainPatcher.StreamedMiniWorld;

internal static class CellUtils {
    internal static HashSet<Int3> BatchesToLoadForGivenCell(
        Int3 cellId, int cellSize, ClipMapManager.LevelSettings settings
    ) {
        Int3 offset = cellId * cellSize;
        Int3 minBlock = new(
            offset.x - (3 << settings.downsamples),
            offset.y - (3 << settings.downsamples),
            offset.z - (3 << settings.downsamples)
        );

        int meshRes = (cellSize >> settings.downsamples) + settings.meshOverlap * 2;
        meshRes += 6;
        Int3 size = new(meshRes, meshRes, meshRes);

        HashSet<Int3> batches = new(27);
        Int3 minBatch = Int3.FloorDiv(minBlock, 160);
        Int3 maxBatch = Int3.FloorDiv(minBlock + (size << settings.downsamples) - 1, 160);
        foreach (Int3 int4 in Int3.MinMax(minBatch, maxBatch)) batches.Add(int4);
        return batches;
    }

    internal static Int3[] OrderCellsAroundCenter(Int3 minCell, Int3 maxCell, Int3 centerCell) {
        int countX = maxCell.x - minCell.x + 1;
        int countY = maxCell.y - minCell.y + 1;
        int countZ = maxCell.z - minCell.z + 1;

        List<(Int3 cellID, int distanceToCenter)> batches = new(countX * countY * countZ);

        Int3.RangeEnumerator iter = Int3.Range(minCell, maxCell);
        while (iter.MoveNext()) {
            Int3 cell = iter.Current;
            int sqrDistanceToCenter = (cell - centerCell).SquareMagnitude();
            batches.Add(new(cell, sqrDistanceToCenter));
        }

        batches.Sort((a, b) => a.distanceToCenter.CompareTo(b.distanceToCenter));

        Int3[] sortedBatched = new Int3[batches.Count];
        for (int i = 0; i < batches.Count; i++) sortedBatched[i] = batches[i].cellID;
        return sortedBatched;
    }

    internal static float MinDistanceToEdge(
        Vector3 chunkSpaceCenter, int chunkSize, int mapRadius,
        Dictionary<Int3, MiniWorld.Chunk> loadedChunks
    ) {
        Int3 mapCenterBlock =  Int3.Floor(chunkSpaceCenter);
        Int3 centerChunk = Int3.FloorDiv(mapCenterBlock, chunkSize);
        bool hit = false;
        float bestMinSqdist = mapRadius * mapRadius;

        for (int chebDist = 0;; chebDist++) { // chebyshev distance
            int innerChebDist = Math.Max(chebDist - 1, 0);
            float minDistBound = innerChebDist * chunkSize;
            if (minDistBound * minDistBound >= bestMinSqdist) break;

            void checkRelativeChunk(int dx, int dy, int dz) {
                Int3 chunk = centerChunk + new Int3(dx, dy, dz);
                if (!loadedChunks.ContainsKey(chunk)) {
                    float sqdist = PointToChunkSqdist(chunkSpaceCenter, chunkSize, chunk);
                    if (sqdist < bestMinSqdist) {
                        bestMinSqdist = sqdist;
                        hit = true;
                    }
                }
            }

            if (chebDist == 0) {
                checkRelativeChunk(0, 0, 0);
                continue;
            }

            for (int dx = -chebDist; dx <= chebDist; dx++) {
                for (int dy = -chebDist; dy <= chebDist; dy++) {
                    checkRelativeChunk(dx, dy, -chebDist);
                    checkRelativeChunk(dx, dy, chebDist);
                }
            }

            for (int dy = -chebDist; dy <= chebDist; dy++) {
                for (int dz = -innerChebDist; dz <= innerChebDist; dz++) {
                    checkRelativeChunk(-chebDist, dy, dz);
                    checkRelativeChunk(chebDist, dy, dz);
                }
            }

            for (int dx = -innerChebDist; dx <= innerChebDist; dx++) {
                for (int dz = -innerChebDist; dz <= innerChebDist; dz++) {
                    checkRelativeChunk(dx, -chebDist, dz);
                    checkRelativeChunk(dx, chebDist, dz);
                }
            }
        }
        
        return hit ? Mathf.Sqrt(bestMinSqdist) : mapRadius;

        static float PointToChunkSqdist(Vector3 point, int chunkSize, Int3 chunk) {
            Vector3 lo = (chunk * chunkSize).ToVector3() - point;
            Vector3 hi = point - ((chunk + 1) * chunkSize).ToVector3();
            Vector3 dist = new Vector3(Max(lo.x, 0, hi.x), Max(lo.y, 0, hi.y), Max(lo.z, 0, hi.z));

            return dist.sqrMagnitude;

            static float Max(float val1, float val2, float val3)
                => Mathf.Max(Mathf.Max(val1, val2), val3);
        }
    }
}
