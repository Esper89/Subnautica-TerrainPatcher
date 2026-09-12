using System.Collections.Concurrent;
using HarmonyLib;
using WorldStreaming;

namespace TerrainPatcher.StreamedMiniWorld;

internal class OctreeStreamer {
    internal static OctreeStreamer? Instance { get; private set; }

    private static void CreateOctreeStreamer(
        WorldStreamer worldStreamer, LargeWorldStreamer.Settings settings
    ) {
        if (Instance != null) {
            throw new InvalidOperationException("Octree streamer already initialized");
        }
        Instance = new OctreeStreamer(worldStreamer, settings);
    }

    public BatchOctreesStreamer BatchStreamer { get; }
    private static readonly FakeArrayPool allocator = new();

    private const int CACHE_CAPACITY = 40;
    private readonly LruCache<Int3, BatchOctrees> Lru;
    private readonly BlockingCollection<BatchOctrees> batchPool = new();

    private readonly ConcurrentDictionary<Guid, BuildMeshOperation>
        activeBuildRequests = new();

    private readonly ConcurrentDictionary<Int3, BatchOctrees>
        batchOctreesToUnload = new();

    private OctreeStreamer(WorldStreamer ws, LargeWorldStreamer.Settings settings) {
        BatchStreamer = new BatchOctreesStreamer(
            ws.streamingThread,
            TerrainExtender.EXTENDED_BATCH_BOUNDS,
            minLod: 0, maxLod: 3,
            ws.batchSize, ws.settings.numOctreesPerBatch,
            Path.Combine(ws.settings.worldPath, "CompiledOctreesCache"),
            settings.octreesSettings
        );
        Lru = new(CACHE_CAPACITY, ReturnOctreesToPool);
        // this array3 in the base class just wastes memory if not cleared
        BatchStreamer.batches.Clear();
    }

    // called from streaming thread
    [HarmonyPatch(typeof(BatchOctreesStreamer), nameof(BatchOctreesStreamer.GetBatch))]
    private static class OverrideGetBatch {
        private static bool Prefix(
            BatchOctreesStreamer __instance, Int3 id, ref BatchOctrees? __result
        ) {
            if (Instance is not null && __instance != Instance.BatchStreamer) {
                return true;
            }

            lock (Instance!.Lru) {
                if (Instance.Lru.TryGet(id, out __result)) return false;
            }

            // this is a fallback, normally the preload should cover this but in the case of
            // multiple maps building, the cache may be full and this is required
            Instance.batchOctreesToUnload.TryGetValue(id, out __result);
            return false;
        }
    }

    internal void EnsureStreamerHasBatchesLoadedForCell(BuildMeshOperation owner) {
        BatchStreamer.ioThread.Enqueue(BeginEnsureBatchesLoadedDelegate, this, owner);
    }

    // on i/o thread
    private static readonly UWE.Task.Function
        BeginEnsureBatchesLoadedDelegate = BeginEnsureBatchesLoaded;

    private static void BeginEnsureBatchesLoaded(object owner, object state) {
        var streamer = (OctreeStreamer)owner;
        var operation = (BuildMeshOperation)state;
        operation.batchIdsNeeded = CellUtils.BatchesToLoadForGivenCell(
            operation.cellId, MeshBuilding.cellSize, MeshBuilding.levelSettings
        );
        streamer.activeBuildRequests.TryAdd(operation.guid, operation);

        foreach (Int3 batchId in operation.batchIdsNeeded) {
            BatchOctrees? batch;
            lock (streamer.Lru) { streamer.Lru.TryGet(batchId, out batch); }
            if (batch == null) streamer.LoadBatch(batchId);
        }
        BatchOctreesStreamer batchStreamer = streamer.BatchStreamer;
        batchStreamer.streamingThread.Enqueue(EndEnsureBatchesLoadedDelegate, operation, null);
    }

    // on streaming thread
    private static readonly UWE.Task.Function
        EndEnsureBatchesLoadedDelegate = EndEnsureBatchesLoaded;

    private static void EndEnsureBatchesLoaded(object owner, object state) {
        var operation = (BuildMeshOperation)owner;
        operation.clipMapStreamer.meshingThreads.Enqueue(
            MeshBuilding.BeginBuildMiniWorldMeshDelegate, operation, null
        );
    }

    private void LoadBatch(Int3 batchID) {
        if (batchPool.TryTake(out BatchOctrees manualBatchLoad)) {
            manualBatchLoad.id = batchID;
        } else {
            manualBatchLoad = new(BatchStreamer, batchID, 
                BatchStreamer.numOctreesPerBatch, allocator
            );
        }

        if (!manualBatchLoad.LoadOctrees()) {
            manualBatchLoad.ClearOctrees(); // fill with empty octrees
        }
        manualBatchLoad.state = BatchOctrees.State.Loaded;
        lock (Lru) { Lru.Put(batchID, manualBatchLoad); }
    }

    private void ReturnOctreesToPool(BatchOctrees batchOctrees) {
        foreach (BuildMeshOperation operation in activeBuildRequests.Values) {
            if (operation.batchIdsNeeded == null) continue;
            if (operation.batchIdsNeeded.Contains(batchOctrees.id)) {
                batchOctreesToUnload.TryAdd(batchOctrees.id, batchOctrees);
                return;
            }
        }
        batchOctrees.ClearOctrees();
        batchOctrees.state = BatchOctrees.State.Unloaded;
        batchPool.Add(batchOctrees);
    }

    internal void CleanupHangingBatches(BuildMeshOperation operation) {
        activeBuildRequests.TryRemove(operation.guid, out _);

        foreach (Int3 batchId in operation.batchIdsNeeded!) {
            if (!batchOctreesToUnload.TryRemove(batchId, out BatchOctrees batch)) continue;

            batch.ClearOctrees();
            batch.state = BatchOctrees.State.Unloaded;
            batchPool.Add(batch);
        }
    }

    private static void DestroyOctreeStreamer() {
        if (Instance == null) {
            throw new InvalidOperationException("Cannot destroy nonexistant octree streamer");
        }
        lock (Instance.Lru) {
            Instance.Lru.ForEach(Instance.ReturnOctreesToPool);
        }
        Instance.batchOctreesToUnload.ForEach(batch => batch.Value.Clear());
        Instance.BatchStreamer.Stop();
        Instance = null;
    }

    [ThreadStatic] private static WorldStreamer? WORLD_INSTANCE;
    
    [HarmonyPatch(typeof(WorldStreamer), nameof(WorldStreamer.CreateStreamers))]
    private static class StoreWorldStreamerInstanceForCreateEvent {
        private static void Prefix(WorldStreamer __instance)
            => WORLD_INSTANCE = __instance;

        private static void Finalizer() => WORLD_INSTANCE = null;
    }
    [HarmonyPatch(typeof(WorldStreamer), nameof(WorldStreamer.ParseStreamingSettings))]
    private static class CreateOctreeStreamerEvent {
        private static void Postfix(LargeWorldStreamer.Settings __result) {
            if (WORLD_INSTANCE is not null) {
                CreateOctreeStreamer(WORLD_INSTANCE, __result);
            }
        }
    }
    
    [HarmonyPatch(typeof(WorldStreamer), nameof(WorldStreamer.DestroyStreamers))]
    private static class DestroyStreamerEvent {
        private static void Postfix() => DestroyOctreeStreamer();
    }
}
