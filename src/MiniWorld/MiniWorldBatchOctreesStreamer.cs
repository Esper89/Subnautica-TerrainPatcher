using System.Collections.Concurrent;
using System.Reflection.Emit;
using HarmonyLib;
using WorldStreaming;

namespace TerrainPatcher;

[HarmonyPatch(typeof(BatchOctreesStreamer))]
public class MiniWorldBatchOctreesStreamer : BatchOctreesStreamer {
    internal static MiniWorldBatchOctreesStreamer? Instance { get; private set; }

    private static readonly MiniWorldArrayPool allocator = new();

    public static void InitializeMiniWorldStreamer(
        WorldStreamer worldStreamer, LargeWorldStreamer.Settings settings
    ) {
        if (Instance != null) {
            throw new InvalidOperationException("MiniWorldBatchStreamer already initialized");
        }
        Instance = new MiniWorldBatchOctreesStreamer(worldStreamer, settings);
    }

    private const int CACHE_CAPACITY = 40;
    private readonly LeastRecentlyUsedCache<Int3, BatchOctrees> Lrc;
    private readonly BlockingCollection<BatchOctrees> batchPool = new();

    private readonly ConcurrentDictionary<Guid, StreamMiniWorldChunkOperation>
        activeBuildRequests = new();

    private readonly ConcurrentDictionary<Int3, BatchOctrees>
        batchOctreesToUnload = new();

    public MiniWorldBatchOctreesStreamer(
        WorldStreamer ws, LargeWorldStreamer.Settings settings
    ) : base(
        ws.streamingThread,
        HarmonyPatches.EXTENDED_BATCH_BOUNDS,
        minLod: 0, maxLod: 3,
        ws.batchSize, ws.settings.numOctreesPerBatch,
        Path.Combine(ws.settings.worldPath, "CompiledOctreesCache"),
        settings.octreesSettings
    ) {
        Lrc = new(CACHE_CAPACITY, ReturnOctreesToPool);
        batches.Clear(); // this array3 in the base class just wastes memory if not cleared
    }

    // called from streaming thread
    [HarmonyPatch(typeof(BatchOctreesStreamer), nameof(GetBatch))]
    [HarmonyPrefix]
    private static bool GetBatch_Postfix(
        Int3 id, BatchOctreesStreamer __instance, ref BatchOctrees? __result
    ) {
        if (__instance is not MiniWorldBatchOctreesStreamer streamer) return true;

        lock (streamer.Lrc) {
            if (streamer.Lrc.TryGet(id, out __result)) return false;
        }

        // this is a fallback, normally the preload should cover this but in the case of multiple
        // maps building, the cache may be full and this is required
        streamer.batchOctreesToUnload.TryGetValue(id, out __result);
        return false;
    }

    internal void EnsureStreamerHasBatchesLoadedForCell(StreamMiniWorldChunkOperation owner) {
        if (Instance == null) {
            throw new InvalidOperationException("MiniWorldBatchStreamer inactive");
        }

        Instance.ioThread.Enqueue(BeginEnsureBatchesLoadedDelegate, this, owner);
    }

    // on i/o thread
    private static readonly UWE.Task.Function
        BeginEnsureBatchesLoadedDelegate = BeginEnsureBatchesLoaded;

    private static void BeginEnsureBatchesLoaded(object owner, object state) {
        var streamer = (MiniWorldBatchOctreesStreamer) owner;
        var operation = (StreamMiniWorldChunkOperation) state;
        operation.batchIdsNeeded = CellUtils.BatchesToLoadForGivenCell(
            operation.cellId, MiniWorldMeshBuilding.cellSize, MiniWorldMeshBuilding.levelSettings
        );
        streamer.activeBuildRequests.TryAdd(operation.guid, operation);

        foreach (Int3 batchId in operation.batchIdsNeeded) {
            BatchOctrees? batch;
            lock (streamer.Lrc) { streamer.Lrc.TryGet(batchId, out batch); }
            if (batch == null) streamer.LoadBatch(batchId);
        }

        streamer.streamingThread.Enqueue(EndEnsureBatchesLoadedDelegate, operation, null);
    }

    // on streaming thread
    private static readonly UWE.Task.Function
        EndEnsureBatchesLoadedDelegate = EndEnsureBatchesLoaded;

    private static void EndEnsureBatchesLoaded(object owner, object state) {
        var operation = (StreamMiniWorldChunkOperation) owner;
        operation.clipMapStreamer.meshingThreads.Enqueue(
            MiniWorldMeshBuilding.BeginBuildMiniWorldMeshDelegate, operation, null
        );
    }

    private void LoadBatch(Int3 batchID) {
        if (batchPool.TryTake(out BatchOctrees manualBatchLoad)) {
            manualBatchLoad.id = batchID;
        } else {
            manualBatchLoad = new(Instance, batchID, Instance!.numOctreesPerBatch, allocator);
        }

        if (!manualBatchLoad.LoadOctrees()) {
            manualBatchLoad.ClearOctrees(); // fill with empty octrees
        }
        manualBatchLoad.state = BatchOctrees.State.Loaded;
        lock (Lrc) { Lrc.Put(batchID, manualBatchLoad); }
    }

    private void ReturnOctreesToPool(BatchOctrees batchOctrees) {
        foreach (StreamMiniWorldChunkOperation operation in activeBuildRequests.Values) {
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

    internal void CleanupHangingBatches(StreamMiniWorldChunkOperation operation) {
        activeBuildRequests.TryRemove(operation.guid, out _);

        foreach (Int3 batchId in operation.batchIdsNeeded!) {
            if (!batchOctreesToUnload.TryRemove(batchId, out BatchOctrees batch)) continue;

            batch.ClearOctrees();
            batch.state = BatchOctrees.State.Unloaded;
            batchPool.Add(batch);
        }
    }

    internal static void DestroyStreamer() {
        if (Instance == null) {
            throw new InvalidOperationException(
                "Cannot stop streaming when the streamer doesn't exist"
            );
        }
        lock (Instance.Lrc) {
            Instance.Lrc.ForEach(Instance.ReturnOctreesToPool);
        }
        Instance.batchOctreesToUnload.ForEach(batch => batch.Value.Clear());
        Instance.Stop();
        Instance = null!;
    }

    private double EstimateMemoryUsage() {
        int usedBytes = 0;
        lock (Lrc) {
            foreach (BatchOctrees batchOctrees in Lrc)
            foreach (Octree batchOctreesOctree in batchOctrees.octrees) {
                usedBytes += batchOctreesOctree.data.Length;
            }
        }
        return usedBytes / 1000.0 / 1000.0;
    }
}

[HarmonyPatch(typeof(WorldStreamer))]
public class WorldStreamerEventPatches {
    [HarmonyPatch(typeof(WorldStreamer), nameof(WorldStreamer.CreateStreamers))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CreateStreamers_Transpiler(
        IEnumerable<CodeInstruction> instructions
    ) => new CodeMatcher(instructions)
        .MatchStartForward([new(OpCodes.Ret)])
        .ThrowIfNotMatch(
            "could not transpile " +
            $"{typeof(WorldStreamer)}.{nameof(WorldStreamer.CreateStreamers)}: method does not " +
            "return"
        )
        .Advance(-1)
        .Insert([
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldloc_3),
            CodeInstruction.CallClosure(MiniWorldBatchOctreesStreamer.InitializeMiniWorldStreamer),
        ])
        .InstructionEnumeration();

    [HarmonyPatch(typeof(WorldStreamer), nameof(WorldStreamer.DestroyStreamers))]
    [HarmonyPostfix]
    private static void DestroyStreamers_Postfix(WorldStreamer __instance)
        => MiniWorldBatchOctreesStreamer.DestroyStreamer();
}
