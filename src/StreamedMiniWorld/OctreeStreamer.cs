using System.Collections.Concurrent;
using HarmonyLib;
using WorldStreaming;

namespace TerrainPatcher.StreamedMiniWorld;

internal sealed class OctreeStreamer {
    internal static OctreeStreamer? INSTANCE { get; private set; }

    private static void CreateOctreeStreamer(
        WorldStreamer worldStreamer, LargeWorldStreamer.Settings settings
    ) {
        if (INSTANCE != null) {
            throw new InvalidOperationException("Octree streamer already initialized");
        }
        INSTANCE = new OctreeStreamer(worldStreamer, settings);
    }

    public BatchOctreesStreamer BatchStreamer { get; }
    private static readonly FakeArrayPool ALLOCATOR = new();

    private const int CACHE_CAPACITY = 128;
    private readonly LruCache<Int3, BatchOctrees> Lru;
    private readonly BlockingCollection<BatchOctrees> batchPool = new();

    //lock in this order: lock(batchOctreesToUnload) { lock(activeBuildRequests) {...} }
    private readonly Dictionary<Int3, BatchOctrees> batchOctreesToUnload = new();
    private readonly Dictionary<Guid, BuildMeshOperation> activeBuildRequests = new();

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
        )
        {
            if (INSTANCE == null || __instance != INSTANCE.BatchStreamer) return true;

            lock (INSTANCE.Lru)
            {
                if (INSTANCE.Lru.TryGet(id, out __result)) return false;
            }

            // this is a fallback, normally the preload should cover this but in the case of
            // multiple maps building, the cache may be full and this is required
            lock (INSTANCE.batchOctreesToUnload) {
                INSTANCE.batchOctreesToUnload.TryGetValue(id, out __result);
            }
            return false;
        }
    }

    internal void EnsureStreamerHasBatchesLoadedForCell(BuildMeshOperation owner) {
        BatchStreamer.ioThread.Enqueue(BEGIN_ENSURE_BATCHES_LOADED_DELEGATE, this, owner);
    }

    // on i/o thread
    private static readonly UWE.Task.Function
        BEGIN_ENSURE_BATCHES_LOADED_DELEGATE = BeginEnsureBatchesLoaded;

    private static void BeginEnsureBatchesLoaded(object owner, object state) {
        var streamer = (OctreeStreamer)owner;
        var operation = (BuildMeshOperation)state;
        operation.batchIdsNeeded = CellUtils.BatchesToLoadForGivenCell(
            operation.cellId, MeshBuilding.CELL_SIZE, MeshBuilding.LEVEL_SETTINGS
        );
        lock (streamer.activeBuildRequests)
        {
            streamer.activeBuildRequests.Add(operation.guid, operation);
        }
        

        foreach (Int3 batchId in operation.batchIdsNeeded) {
            BatchOctrees? batch;
            lock (streamer.Lru) { streamer.Lru.TryGet(batchId, out batch); }
            if (batch == null) streamer.LoadBatch(batchId);
        }
        BatchOctreesStreamer batchStreamer = streamer.BatchStreamer;
        batchStreamer.streamingThread.Enqueue(END_ENSURE_BATCHES_LOADED_DELEGATE, operation, null);
    }

    // on streaming thread
    private static readonly UWE.Task.Function
        END_ENSURE_BATCHES_LOADED_DELEGATE = EndEnsureBatchesLoaded;

    private static void EndEnsureBatchesLoaded(object owner, object state) {
        var operation = (BuildMeshOperation)owner;
        operation.meshStreamer.meshingThreads.Enqueue(
            MeshBuilding.BEGIN_BUILD_MINI_WORLD_MESH_DELEGATE, operation, null
        );
    }

    private void LoadBatch(Int3 batchID) {
        if (batchPool.TryTake(out BatchOctrees manualBatchLoad)) {
            manualBatchLoad.id = batchID;
        } else {
            manualBatchLoad = new(
                BatchStreamer, batchID, BatchStreamer.numOctreesPerBatch, ALLOCATOR
            );
        }

        if (!manualBatchLoad.LoadOctrees()) {
            manualBatchLoad.ClearOctrees(); // fill with empty octrees
        }
        manualBatchLoad.state = BatchOctrees.State.Loaded;
        lock (Lru) { Lru.Put(batchID, manualBatchLoad); }
    }

    private void ReturnOctreesToPool(BatchOctrees batchOctrees) {
        lock (batchOctreesToUnload) {
            if (BatchInUse(batchOctrees.id) && !batchOctreesToUnload.ContainsKey(batchOctrees.id)){
                batchOctreesToUnload.Add(batchOctrees.id, batchOctrees);
                return;
            }
        }
        
        batchOctrees.ClearOctrees();
        batchOctrees.state = BatchOctrees.State.Unloaded;
        batchPool.Add(batchOctrees);
    }

    internal void CleanupHangingBatches(BuildMeshOperation operation) {
        lock(activeBuildRequests) activeBuildRequests.Remove(operation.guid);

        foreach (Int3 batchId in operation.batchIdsNeeded!) {
            BatchOctrees batch;
            lock (batchOctreesToUnload) {
                if (BatchInUse(batchId)) return;
                if (!batchOctreesToUnload.TryGetValue(batchId, out batch)) continue;
                batchOctreesToUnload.Remove(batchId);
            }
            
            batch.ClearOctrees();
            batch.state = BatchOctrees.State.Unloaded;
            batchPool.Add(batch);
        }
    }

    private bool BatchInUse(Int3 id) {
        lock (activeBuildRequests) {
            foreach (BuildMeshOperation operation in activeBuildRequests.Values) {
                if (!operation.batchIdsNeeded!.Contains(id)) continue;
                return true;
            }
        }
        return false;
    }

    private static void DestroyOctreeStreamer() {
        if (INSTANCE == null) {
            throw new InvalidOperationException("Cannot destroy nonexistant octree streamer");
        }
        lock (INSTANCE.Lru) {
            INSTANCE.Lru.ForEach(INSTANCE.ReturnOctreesToPool);
        }
        lock (INSTANCE.batchOctreesToUnload) {
            INSTANCE.batchOctreesToUnload.ForEach(batch => batch.Value.Clear());
        }
        INSTANCE.BatchStreamer.Stop();
        INSTANCE = null;
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
            if (WORLD_INSTANCE != null) {
                CreateOctreeStreamer(WORLD_INSTANCE, __result);
            }
        }
    }

    [HarmonyPatch(typeof(WorldStreamer), nameof(WorldStreamer.DestroyStreamers))]
    private static class DestroyStreamerEvent {
        private static void Postfix() => DestroyOctreeStreamer();
    }
}
