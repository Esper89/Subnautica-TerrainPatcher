namespace TerrainPatcher;

internal static class OpoctreesDirs {
    internal static readonly string[] ORIG_BATCH_DIRS = ["Build18", "Expansion"];
    
    static OpoctreesDirs() {
        foreach (string origDirName in ORIG_BATCH_DIRS) {
            string? origDir = SNUtils.InsideUnmanaged(origDirName);

            if (!Directory.Exists(origDir)) continue;
            OriginalPath = Path.Combine(origDir, "CompiledOctreesCache");
            PatchesPath = Path.Combine(OriginalPath, "patches");
            Directory.CreateDirectory(PatchesPath);
            break;
        }
        if(OriginalPath == null || PatchesPath == null) throw new Exception("couldn't determine the patches directory");
    }

    internal static void ClearPatchesDir() {
        foreach (string? path in Directory.EnumerateFiles(PatchesPath)) {
            if (Path.GetExtension(path) != ".optoctrees") continue;
            File.Delete(path);
        }
    }

    internal static readonly string OriginalPath;
    internal static readonly string PatchesPath;
}