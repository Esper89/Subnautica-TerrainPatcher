using System.Collections.Concurrent;
using System.Reflection.Emit;
using HarmonyLib;
using WorldStreaming;

namespace TerrainPatcher;

[HarmonyPatch(typeof(BatchOctreesStreamer))]
public class MiniWorldBatchOctreesStreamer : BatchOctreesStreamer {
    internal static MiniWorldBatchOctreesStreamer? MiniWorldBatchStreamer { get; private set; }
    private static readonly MiniWorldArrayPool allocator = new();
    
    public static void InitializeMiniWorldStreamer(WorldStreamer worldStreamer, LargeWorldStreamer.Settings settings) {
        if(MiniWorldBatchStreamer != null) throw new InvalidOperationException("MiniWorldBatchStreamer already initialized!");
        MiniWorldBatchStreamer = new MiniWorldBatchOctreesStreamer(worldStreamer, settings);
    }
    
    private const int CACHE_CAPACITY = 40;
    private readonly LeastRecentlyUsedCache<Int3, BatchOctrees> LRC;
    private readonly BlockingCollection<BatchOctrees> batchPool = new();

    private readonly ConcurrentDictionary<Guid, StreamMiniWorldChunkOperation> activeBuildRequests = new();
    private readonly ConcurrentDictionary<Int3, BatchOctrees> batchOctreesToUnload = new();
    
    public MiniWorldBatchOctreesStreamer(WorldStreamer ws, LargeWorldStreamer.Settings settings) 
        : base(ws.streamingThread, HarmonyPatches.EXTENDED_BATCH_BOUNDS, 0, 3, ws.batchSize, ws.settings.numOctreesPerBatch, 
            Path.Combine(ws.settings.worldPath, "CompiledOctreesCache"), settings.octreesSettings) {
        LRC = new(CACHE_CAPACITY, ReturnOctreesToPool);
        batches.Clear();//this array 3 in the base class just wastes memory if not cleared
    }
    
    // called from streaming thread
    [HarmonyPatch(typeof(BatchOctreesStreamer), nameof(GetBatch))]
    [HarmonyPrefix]
    private static bool GetBatch_Postfix(Int3 id, BatchOctreesStreamer __instance, ref BatchOctrees? __result) {
        if (__instance is not MiniWorldBatchOctreesStreamer streamer) return true;

        lock (streamer.LRC) {
            if (streamer.LRC.TryGet(id, out __result)) return false;
        }
        // This is a fallback, normally the preload should cover this but in the case of multiple maps building,
        // the cache may be full and this is required
        streamer.batchOctreesToUnload.TryGetValue(id, out __result);
        return false;
    }

    internal void EnsureStreamerHasBatchesLoadedForCell(StreamMiniWorldChunkOperation owner) {
        if (MiniWorldBatchStreamer == null) throw new InvalidOperationException("MiniWorldBatchStreamer inactive!");
        MiniWorldBatchStreamer.ioThread.Enqueue(BeginEnsureBatchesLoadedDelegate, this, owner);
    }
    
    // On IO Thread
    private static readonly UWE.Task.Function BeginEnsureBatchesLoadedDelegate = BeginEnsureBatchesLoaded;
    private static void BeginEnsureBatchesLoaded(object owner, object state) {
        var streamer = (MiniWorldBatchOctreesStreamer) owner;
        var operation = (StreamMiniWorldChunkOperation) state;
        operation.batchIdsNeeded = CellUtils.BatchesToLoadForGivenCell(operation.cellId, MiniWorldMeshBuilding.cellSize, MiniWorldMeshBuilding.levelSettings);
        streamer.activeBuildRequests.TryAdd(operation.guid, operation);
        
        foreach (Int3 batchId in operation.batchIdsNeeded) {
            BatchOctrees? batch;
            lock (streamer.LRC) { streamer.LRC.TryGet(batchId, out batch); }
            if(batch == null) streamer.LoadBatch(batchId);
        }
        
        streamer.streamingThread.Enqueue(EndEnsureBatchesLoadedDelegate, operation, null); 
    }

    // On Streaming Thread
    private static readonly UWE.Task.Function EndEnsureBatchesLoadedDelegate = EndEnsureBatchesLoaded;
    private static void EndEnsureBatchesLoaded(object owner, object state) {
        var operation = (StreamMiniWorldChunkOperation) owner;
        operation.clipMapStreamer.meshingThreads.Enqueue(MiniWorldMeshBuilding.BeginBuildMiniWorldMeshDelegate, operation, null); 
    }

    private void LoadBatch(Int3 batchID) {
        if (batchPool.TryTake(out BatchOctrees manualBatchLoad)) {
            manualBatchLoad.id = batchID;
        } else {
            manualBatchLoad = new(MiniWorldBatchStreamer, batchID, MiniWorldBatchStreamer!.numOctreesPerBatch, allocator);
        }
        
        if (!manualBatchLoad.LoadOctrees()) {
            manualBatchLoad.ClearOctrees(); // Fill with empty octrees
        }
        manualBatchLoad.state = BatchOctrees.State.Loaded;
        lock (LRC) { LRC.Put(batchID, manualBatchLoad); }
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
        if (MiniWorldBatchStreamer == null) throw new InvalidOperationException("Cannot stop streaming when the streamer doesnt exist!");
        lock (MiniWorldBatchStreamer.LRC) {
            MiniWorldBatchStreamer.LRC.ForEach(MiniWorldBatchStreamer.ReturnOctreesToPool);
        }
        MiniWorldBatchStreamer.batchOctreesToUnload.ForEach(batch => batch.Value.Clear());
        MiniWorldBatchStreamer.Stop();
        MiniWorldBatchStreamer = null!;
    }

    private double EstimateMemoryUsage() {
        int usedBytes = 0;
        lock (LRC) {
            foreach (BatchOctrees batchOctrees in LRC)
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
    private static IEnumerable<CodeInstruction> CreateStreamers_Transpiler(IEnumerable<CodeInstruction> instructions)
        => new CodeMatcher(instructions)
            .MatchStartForward([new(OpCodes.Ret)])
            .ThrowIfNotMatch(
                $"could not transpile {typeof(WorldStreamer)}.{nameof(WorldStreamer.CreateStreamers)}: " +
                "method does not return"
            )
            .Advance(-1)
            .Insert([
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldloc_3),
                CodeInstruction.CallClosure(MiniWorldBatchOctreesStreamer.InitializeMiniWorldStreamer)
            ])
            .InstructionEnumeration();

    [HarmonyPatch(typeof(WorldStreamer), nameof(WorldStreamer.DestroyStreamers))]
    [HarmonyPostfix]
    private static void DestroyStreamers_Postfix(WorldStreamer __instance)  
        => MiniWorldBatchOctreesStreamer.DestroyStreamer();
}