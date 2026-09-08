using System.Diagnostics.Contracts;

namespace TerrainPatcher;

internal static class CellUtils {
    [Pure]
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
        foreach (Int3 int4 in Int3.MinMax(minBatch, maxBatch)) {
            batches.Add(int4);
        }
        return batches;
    }

    [Pure]
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
        for (int i = 0; i < batches.Count; i++) {
            sortedBatched[i] = batches[i].cellID;
        }
        return sortedBatched;
    }
}
