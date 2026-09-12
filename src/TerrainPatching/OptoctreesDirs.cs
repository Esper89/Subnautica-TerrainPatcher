namespace TerrainPatcher.TerrainPatching;

internal static class OptoctreesDirs {
    private static readonly string[] ORIG_BATCH_DIRS = ["Build18", "Expansion"];

    static OptoctreesDirs() {
        foreach (string origDirName in ORIG_BATCH_DIRS) {
            string? origDir = SNUtils.InsideUnmanaged(origDirName);

            if (!Directory.Exists(origDir)) continue;
            ORIG_PATH = Path.Combine(origDir, "CompiledOctreesCache");
            PATCHED_PATH = Path.Combine(ORIG_PATH, "patches");
            Directory.CreateDirectory(PATCHED_PATH);
            break;
        }

        if (ORIG_PATH == null || PATCHED_PATH == null) {
            throw new Exception("couldn't determine the patches directory");
        }
    }

    internal static void ClearPatchesDir() {
        foreach (string? path in Directory.EnumerateFiles(PATCHED_PATH)) {
            if (Path.GetExtension(path) != ".optoctrees") continue;
            File.Delete(path);
        }
    }

    internal static readonly string ORIG_PATH;
    internal static readonly string PATCHED_PATH;
}
