using FMODUnity;
using UnityEngine;

namespace TerrainPatcher.StreamedMiniWorld;

internal static class CellUtils
{
    internal static HashSet<Int3> BatchesToLoadForGivenCell(
        Int3 cellId, int cellSize, ClipMapManager.LevelSettings settings
    )
    {
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
        foreach (Int3 int4 in Int3.MinMax(minBatch, maxBatch))
        {
            batches.Add(int4);
        }

        return batches;
    }

    internal static Int3[] OrderCellsAroundCenter(Int3 minCell, Int3 maxCell, Int3 centerCell)
    {
        int countX = maxCell.x - minCell.x + 1;
        int countY = maxCell.y - minCell.y + 1;
        int countZ = maxCell.z - minCell.z + 1;

        List<(Int3 cellID, int distanceToCenter)> batches = new(countX * countY * countZ);

        Int3.RangeEnumerator iter = Int3.Range(minCell, maxCell);
        while (iter.MoveNext())
        {
            Int3 cell = iter.Current;
            int sqrDistanceToCenter = (cell - centerCell).SquareMagnitude();
            batches.Add(new(cell, sqrDistanceToCenter));
        }

        batches.Sort((a, b) => a.distanceToCenter.CompareTo(b.distanceToCenter));

        Int3[] sortedBatched = new Int3[batches.Count];
        for (int i = 0; i < batches.Count; i++)
        {
            sortedBatched[i] = batches[i].cellID;
        }

        return sortedBatched;
    }

    internal static float MinDistanceToEdge(
        Vector3 streamingCenter, int chunkSize, int mapRadius,
        Dictionary<Int3, MiniWorld.Chunk> loadedChunks
    ) {
        Int3 mapCenterBlock = LargeWorldStreamer.main.GetBlock(streamingCenter);
        Int3 centerChunk = Int3.FloorDiv(mapCenterBlock, chunkSize);
        float bestMinSqdist = mapRadius * mapRadius;

        for (int chebDist = 0;; chebDist++) { // chebyshev distance
            float minDistBound = Math.Max(chebDist - 1, 0) * chunkSize;
            if (minDistBound * minDistBound >= bestMinSqdist) break;

            for (int dx = -chebDist; dx <= chebDist; dx++) {
                for (int dy = -chebDist; dy <= chebDist; dy++) {
                    for (int dz = -chebDist; dz <= chebDist; dz++) {
                        if (Max3(Math.Abs(dx), Math.Abs(dy), Math.Abs(dz)) != chebDist) continue;
                        
                        Int3 chunk = centerChunk + new Int3(dx, dy, dz);

                        if (loadedChunks.ContainsKey(chunk)) continue;

                        float sqdist = pointToChunkSqdist(streamingCenter, chunkSize, chunk);
                        if (sqdist < bestMinSqdist) {
                            bestMinSqdist = sqdist;
                        }
                    }
                }
            }
        }

        return Mathf.Sqrt(bestMinSqdist);
    }

    //TODO: this singleton usage makes me want to kms. and the converstion to vector3 before AHH
    private static float pointToChunkSqdist(Vector3 point, int chunkSize, Int3 chunk) {
        Vector3 chunkMin = LargeWorldStreamer.main.land.transform.TransformPoint((chunk * chunkSize).ToVector3());
        Vector3 chunkMax = LargeWorldStreamer.main.land.transform.TransformPoint(((chunk + 1) * chunkSize).ToVector3());

        Vector3 lo = chunkMin - point;
        Vector3 hi = point - chunkMax;
        Vector3 dist = new Vector3(Max3(lo.x, 0, hi.x), Max3(lo.y, 0, hi.y), Max3(lo.z, 0, hi.z));

        return dist.sqrMagnitude;
    }
    
    //I kinda hate that we have to define these ngl
    private static float Max3(float val1, float val2, float val3) 
        => Mathf.Max(Mathf.Max(val1, val2), val3);
    private static int Max3(int val1, int val2, int val3)
        => Math.Max(Math.Max(val1, val2), val3);
}
