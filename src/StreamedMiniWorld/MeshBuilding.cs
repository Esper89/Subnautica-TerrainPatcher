using HarmonyLib;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UWE;
using WorldStreaming;

namespace TerrainPatcher.StreamedMiniWorld;

internal static class MeshBuilding {
    internal static readonly ClipMapManager.LevelSettings LEVEL_SETTINGS = new() {
        downsamples = 1,
        maxBlockTypes = 1,
        visual = new VoxelandVisualMeshSimplifier.Settings {
            useLowMesh = true,
            simplify = new SimplifyMeshPlugin.Settings {
                antiSliverWeight = 0.0005f,
                maxError = 1
            }
        }
    };

    internal const int CELL_SIZE = 128;

    // on meshing thread
    internal static readonly UWE.Task.Function
        BEGIN_BUILD_MINI_WORLD_MESH_DELEGATE = BeginBuildMiniWorldMesh;

    private static void BeginBuildMiniWorldMesh(object owner, object state) {
        var operation = (BuildMeshOperation)owner;
        MeshStreamer streamer = operation.meshStreamer;

        // redundant, does nothing for our use case of the mesh builder but must supply a number
        const int LEVEL_ID = 0;

        BatchOctreesStreamer octreesStreamer = operation.octreeStreamer.BatchStreamer;
        MeshBuilder meshBuilder = streamer.sharedBuilderPool.Get();
        meshBuilder.Reset(
            LEVEL_ID, operation.cellId, CELL_SIZE, LEVEL_SETTINGS, streamer.blockTypes
        );
        meshBuilder.DoThreadablePart(octreesStreamer, null);

        operation.meshStreamer.buildLayersThread.Enqueue(
            BEGIN_BUILD_MINI_WORLD_LAYERS_DELEGATE, operation, meshBuilder
        );
    }

    // on unity main thread
    private static readonly UWE.Task.Function
        BEGIN_BUILD_MINI_WORLD_LAYERS_DELEGATE = BeginBuildMiniWorldLayers;

    private static void BeginBuildMiniWorldLayers(object owner, object state) {
        var operation = (BuildMeshOperation)owner;
        var meshBuilder = (MeshBuilder)state;

        Mesh mesh = GetMeshOut(meshBuilder);
        operation.meshStreamer.sharedBuilderPool.Return(meshBuilder);
        operation.Complete(mesh, true, null);
        operation.octreeStreamer.CleanupHangingBatches(operation);
    }

    private static Mesh GetMeshOut(MeshBuilder meshBuilder) {
        Mesh returnMesh = new();
        var voxelandVisualMeshSimplifier = meshBuilder.visualMeshSimplifier;
        if (meshBuilder.chunkWorkspace.visibleFaces.Count > 0) {
            // we only generate one material type so we can assume it's all in the first layer
            MeshBuffer meshBuffer = voxelandVisualMeshSimplifier.builtLayers[0];
            meshBuffer.Upload(returnMesh);
            meshBuffer.Return();
        }
        for (int i = 1; i < voxelandVisualMeshSimplifier.builtLayers.Length; i++) {
            // return the empty other layers though, the base game does it so might as well be safe
            // to ensure the pools don't dry
            MeshBuffer meshBuffer = voxelandVisualMeshSimplifier.builtLayers[i];
            meshBuffer?.Return();
        }
        return returnMesh;
    }
}

/// <summary>Creates separate threads for miniworld mesh building. The game cannot save while the
/// regular world streamer is doing work. Separate threads do not block operations that require the
/// world to be settled.</summary>
/// <remarks>The <c>MeshBuilder</c>s are shared with the world streamer to save memory. These are
/// not checked to determine if the world is settled.</remarks>
internal sealed class MeshStreamer {
    internal static MeshStreamer? INSTANCE { get; private set; }

    private static void CreateMeshStreamer(WorldStreamer worldStreamer) {
        if (INSTANCE != null) {
            throw new InvalidOperationException("Miniworld mesh streamer already initialized");
        }
        INSTANCE = new MeshStreamer(worldStreamer);
    }

    private const int THREAD_INITIAL_CAPACITY = 128;
    private const int THREAD_COUNT = 3;

    internal readonly VoxelandBlockType[] blockTypes;
    internal readonly BoundedObjectPool<MeshBuilder> sharedBuilderPool;
    internal readonly UWE.ThreadPool meshingThreads;
    internal readonly UnityThread buildLayersThread;

    private MeshStreamer(WorldStreamer host) {
        blockTypes = [];
        sharedBuilderPool = host.clipmapStreamer.meshBuilderPool;

        meshingThreads = new UWE.ThreadPool(
            name: "MeshingThreadsMiniWorld",
            numWorkers: THREAD_COUNT,
            System.Threading.ThreadPriority.BelowNormal,
            coreAffinityMask: -2,
            THREAD_INITIAL_CAPACITY
        );

        buildLayersThread = new UnityThread("BuildLayersMiniWorld", THREAD_INITIAL_CAPACITY);
        host.StartCoroutine(WorldStreamer.PumpUnityThread(
            thread: buildLayersThread,
            numPerFrame: () => WorldStreamer.CalculateNumPerFrame(THREAD_INITIAL_CAPACITY, false)
        ));
    }

    private static void DestroyMeshStreamer() {
        if (INSTANCE == null) {
            throw new InvalidOperationException("Cannot destroy nonexistant mesh streamer");
        }
        INSTANCE.buildLayersThread.Stop();
        INSTANCE.meshingThreads.Stop();
        INSTANCE = null;
    }

    [HarmonyPatch(typeof(WorldStreamer), nameof(WorldStreamer.CreateStreamers))]
    private static class CreateStreamerEvent {
        private static void Postfix(WorldStreamer __instance) => CreateMeshStreamer(__instance);
    }

    [HarmonyPatch(typeof(WorldStreamer), nameof(WorldStreamer.DestroyStreamers))]
    private static class DestroyStreamerEvent {
        private static void Postfix() => DestroyMeshStreamer();
    }
    
    /// <summary>The <c>MiniWorld</c>'s <c>MeshStreamer</c> uses an empty block type list but
    /// this function reads block type values which causes errors. The gloss value is unused
    /// with only 1 layer</summary>
    [HarmonyPatch(typeof(VoxelandChunk.VoxelandVert), 
        nameof(VoxelandChunk.VoxelandVert.CacheGloss))] 
    private static class RemoveStupidSHit___ {
        private static bool Prefix(VoxelandBlockType[] types) 
            => types != INSTANCE!.blockTypes;
    }
}

/// <summary>We use an <c>AsyncOperationBase</c> to mimic the <c>MiniWorld</c>'s requests to
/// addressable loading. Also, conveniently gives an event when the mesh is no longer
/// needed.</summary>
internal sealed class BuildMeshOperation : AsyncOperationBase<Mesh> {
    internal readonly Guid guid;
    internal readonly Int3 cellId;
    internal readonly MeshStreamer meshStreamer;
    internal readonly OctreeStreamer octreeStreamer;
    internal HashSet<Int3>? batchIdsNeeded;

    private BuildMeshOperation(Int3 cellId) {
        this.cellId = cellId;
        guid = Guid.NewGuid();
        meshStreamer = MeshStreamer.INSTANCE!;
        octreeStreamer = OctreeStreamer.INSTANCE!;
    }

    internal static AsyncOperationHandle<Mesh> Start(Int3 cellId) {
        BuildMeshOperation operation = new(cellId);
        return Addressables.ResourceManager.StartOperation(operation, default);
    }

    protected override void Execute() {
        octreeStreamer.EnsureStreamerHasBatchesLoadedForCell(this);
    }

    protected override void Destroy() {
        if (Result != null) {
            UnityEngine.Object.Destroy(Result);
        }
        base.Destroy();
    }
}
