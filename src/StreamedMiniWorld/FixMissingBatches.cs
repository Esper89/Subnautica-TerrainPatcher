using System.Diagnostics;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using WorldStreaming;

namespace TerrainPatcher.StreamedMiniWorld;

internal static class FixMissingBatches {
    private static readonly HashSet<Int3> MISSING_BATCHES;

    /// <summary>Replace the random missing/empty batches throughout the world with solid terrain,
    /// on the miniworld map only. This should prevent the player from seeing random cubes on the
    /// map that shouldn't be there.</summary>
    [HarmonyPatch(typeof(BatchOctrees), nameof(BatchOctrees.LoadOctrees))]
    private static class ReplaceUglyEmptyBatchesWithSolidForMap {
        private static bool Prefix(BatchOctrees __instance, ref bool __result) {
            if (
                __instance.streamer != OctreeStreamer.INSTANCE!.BatchStreamer ||
                !MISSING_BATCHES.Contains(__instance.id) ||
                TerrainPatching.PatchTerrain.GetPatchedBatchBlocking(__instance.id, out _)
            ) return true;

            FakeArrayPool allocator = (FakeArrayPool)__instance.allocator;
            foreach (Octree octree in __instance.octrees) {
                octree.Clear(allocator);
                octree.data = allocator.SolidOctree;
            }
            __result = true;
            return false;
        }
    }

    static FixMissingBatches() {
        string assetName = Process.GetCurrentProcess().ProcessName switch {
            "Subnautica" => "missing-batches-sn",
            "SubnauticaZero" => "missing-batches-bz",
            _ => string.Empty
        };

        if (assetName.IsNullOrWhiteSpace()) {
            Plugin.LogError("Failed to determine game process for miniworld missing batch fixing");
            MISSING_BATCHES = [];
            return;
        }

        try {
            MISSING_BATCHES = ReadMissingBatches(assetName);
        } catch (Exception ex) {
            Plugin.LogError(
                $"Failed to read '{assetName}' for miniworld missing batch fixing: {ex}"
            );
            MISSING_BATCHES = [];
        }
    }

    private static HashSet<Int3> ReadMissingBatches(string assetName) {
        using Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"TerrainPatcher.assets.{assetName}");
        if (stream == null) {
            throw new FileNotFoundException($"Embedded asset '{assetName}' not found");
        }

        HashSet<Int3> missingBatches = new();
        using StreamReader reader = new(stream);

        while (reader.ReadLine() is string line) {
            string[] coords = line.Split([' ']);
            if (coords.Length != 3) throw new InvalidDataException(
                $"Expected 3 coordinates, got {coords.Length}: {coords}"
            );
            Int3 batch = new(int.Parse(coords[0]), int.Parse(coords[1]), int.Parse(coords[2]));
            missingBatches.Add(batch);
        }
        return missingBatches;
    }
}
