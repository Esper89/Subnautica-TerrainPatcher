using System.Diagnostics;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using WorldStreaming;

namespace TerrainPatcher.StreamedMiniWorld;


internal static class ProblemBatchFixer {
    private static readonly HashSet<Int3> PROBLEM_BATCHES;

    /// <summary>Replaces the random empty batches throughout the world for the MiniWorld only
    /// with solid terrain. This means on the map you won't see random squares that shouldn't be
    /// there.</summary>
    [HarmonyPatch(typeof(BatchOctrees), nameof(BatchOctrees.LoadOctrees))]
    private static class ReplaceUglyEmptyBatchesWithSolidForMap {
        private static bool Prefix(BatchOctrees __instance, ref bool __result) {
            if (__instance.streamer != OctreeStreamer.INSTANCE!.BatchStreamer ||
                !PROBLEM_BATCHES.Contains(__instance.id) ||
                TerrainPatching.PatchTerrain.GetPatchedBatchBlocking(__instance.id, out _)
            ) return true;
            
            FakeArrayPool allocator = (FakeArrayPool) __instance.allocator;
            foreach (Octree octree in __instance.octrees) {
                octree.Clear(allocator);
                octree.data = allocator.SolidOctree;
            }
            __result = true;
            return false;
        }
    }

    static ProblemBatchFixer() {
        string processName = Process.GetCurrentProcess().ProcessName;
        string assetName = processName switch {
            "Subnautica" => "sn-batches",
            "SubnauticaZero" => "bz-batches",
            _ => string.Empty 
        };

        if(assetName.IsNullOrWhiteSpace()) {
            Plugin.LogError("Failed to determine process for MiniWorld problem batch fixing!");
            PROBLEM_BATCHES = [];
            return;
        }

        try { 
            PROBLEM_BATCHES = ReadProblemBatches(assetName);
        } catch (Exception ex) {
            Plugin.LogError($"Failed to read: '{assetName}' for MiniWorld problem batch fixing" +
                            ex);
            PROBLEM_BATCHES = [];
        }
    }

    private static HashSet<Int3> ReadProblemBatches(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream($"TerrainPatcher.Assets.{name}");
        if (stream == null) {
            throw new FileNotFoundException($"Embedded Asset: '{name}' not found!");
        }

        HashSet<Int3> problemBatches = new();
        using StreamReader reader = new(stream);
        
        while(!reader.EndOfStream) {
            string line = reader.ReadLine()!;
            string[] coords = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if(coords.Length != 3) {
                throw new InvalidOperationException($"Expected 3 arguments," +
                                                    $" got {coords.Length}: {coords}");
            }
            Int3 batch = new(
                int.Parse(coords[0]),
                int.Parse(coords[1]),
                int.Parse(coords[2])
            );
            problemBatches.Add(batch);
        }
        return problemBatches;
    }
}