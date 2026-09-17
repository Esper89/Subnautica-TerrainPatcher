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
        ClipmapStreamer streamer = operation.clipMapStreamer;

        // redundant, does nothing for our use case of the mesh builder but must supply a number
        const int LEVEL_ID = 0;

        BatchOctreesStreamer octreesStreamer = OctreeStreamer.INSTANCE!.BatchStreamer;
        MeshBuilder meshBuilder = streamer.meshBuilderPool.Get();
        meshBuilder.Reset(
            LEVEL_ID, operation.cellId, CELL_SIZE, LEVEL_SETTINGS, streamer.host.blockTypes
        );
        meshBuilder.DoThreadablePart(octreesStreamer, streamer.settings.collision);

        streamer.streamingThread.Enqueue(
            END_BUILD_MINI_WORLD_MESH_DELEGATE, operation, meshBuilder
        );
    }

    // on streaming thread
    private static readonly UWE.Task.Function
        END_BUILD_MINI_WORLD_MESH_DELEGATE = EndBuildMiniWorldMesh;

    private static void EndBuildMiniWorldMesh(object owner, object state) {
        var operation = (BuildMeshOperation)owner;
        ClipmapStreamer streamer = operation.clipMapStreamer;
        var meshBuilder = (MeshBuilder)state;

        streamer.buildLayersThread.Enqueue(
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
        operation.clipMapStreamer.meshBuilderPool.Return(meshBuilder);
        operation.Complete(mesh, true, null);
        OctreeStreamer.INSTANCE!.CleanupHangingBatches(operation);
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

/// <summary>We use an `AsyncOperationBase` to mimic the `MiniWorld`'s requests to addressable
/// loading. Also conveniently gives an event when the mesh is no longer needed.</summary>
internal sealed class BuildMeshOperation : AsyncOperationBase<Mesh> {
    internal readonly Guid guid;
    internal readonly Int3 cellId;
    internal readonly ClipmapStreamer clipMapStreamer;
    internal HashSet<Int3>? batchIdsNeeded;

    private BuildMeshOperation(Int3 cellId) {
        this.cellId = cellId;
        guid = Guid.NewGuid();
        clipMapStreamer = LargeWorldStreamer.main.streamerV2.clipmapStreamer;
    }

    internal static AsyncOperationHandle<Mesh> Start(Int3 cellId) {
        BuildMeshOperation operation = new(cellId);
        return Addressables.ResourceManager.StartOperation(operation, default);
    }

    protected override void Execute() {
        OctreeStreamer.INSTANCE!.EnsureStreamerHasBatchesLoadedForCell(this);
    }

    protected override void Destroy() {
        if (Result != null) {
            UnityEngine.Object.Destroy(Result);
        }
        base.Destroy();
    }
}
