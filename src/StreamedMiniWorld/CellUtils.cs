using FMODUnity;
using UnityEngine;

namespace TerrainPatcher.StreamedMiniWorld;

internal static class CellUtils {
    internal static HashSet<Int3> BatchesToLoadForGivenCell(
        Int3 cellId, int cellSize, ClipMapManager.LevelSettings settings
    ) {
        Int3 offset = cellId * cellSize;
        int shiftDownsample = (3 << settings.downsamples);
        Int3 minBlock = new(
            offset.x - shiftDownsample,
            offset.y - shiftDownsample,
            offset.z - shiftDownsample
        );

        int meshRes = (cellSize >> settings.downsamples) + settings.meshOverlap * 2;
        // pads the grid to ensure octrees needed for edges are loaded.
        const int PADDING = 6;
        meshRes += PADDING;
        Int3 size = new(meshRes, meshRes, meshRes);
        
        const int EXPECTED_BATCHES = 27; // based on the current cellSize
        HashSet<Int3> batches = new(EXPECTED_BATCHES);
        
        const int BATCH_SIZE = 160;
        Int3 minBatch = Int3.FloorDiv(minBlock, BATCH_SIZE);
        Int3 maxBatch = Int3.FloorDiv(minBlock + (size << settings.downsamples) - 1, BATCH_SIZE);
        foreach (Int3 int4 in Int3.MinMax(minBatch, maxBatch)) batches.Add(int4);
        return batches;
    }

    internal static Int3[] OrderCellsAroundCenter(
        Vector3 chunkSpaceCenter, int chunkSize, Int3 minChunk, Int3 maxChunk
    ) {
        int countX = maxChunk.x - minChunk.x + 1;
        int countY = maxChunk.y - minChunk.y + 1;
        int countZ = maxChunk.z - minChunk.z + 1;

        List<(Int3 cellID, float distanceToCenter)> batches = new(countX * countY * countZ);

        Int3.RangeEnumerator iter = Int3.Range(minChunk, maxChunk);
        while (iter.MoveNext()) {
            Int3 chunk = iter.Current;
            float sqrDistanceToCenter = PointToChunkSqdist(chunkSpaceCenter, chunkSize, chunk);
            batches.Add(new(chunk, sqrDistanceToCenter));
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
        Int3 mapCenterBlock = Int3.Floor(chunkSpaceCenter);
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
    }

    private static float PointToChunkSqdist(Vector3 point, int chunkSize, Int3 chunk) {
        Vector3 lo = (chunk * chunkSize).ToVector3() - point;
        Vector3 hi = point - ((chunk + 1) * chunkSize).ToVector3();
        Vector3 dist = new Vector3(Max(lo.x, 0, hi.x), Max(lo.y, 0, hi.y), Max(lo.z, 0, hi.z));

        return dist.sqrMagnitude;

        static float Max(float val1, float val2, float val3) 
            => Mathf.Max(Mathf.Max(val1, val2), val3);
    }
}
