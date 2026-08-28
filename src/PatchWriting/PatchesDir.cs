namespace TerrainPatcher;

internal static class PatchesDir {
    internal static readonly string[] ORIG_BATCH_DIRS = ["Build18", "Expansion"];
    
    static PatchesDir() {
        foreach (string origDirName in ORIG_BATCH_DIRS) {
            string? origDir = SNUtils.InsideUnmanaged(origDirName);

            if (!Directory.Exists(origDir)) continue;
            
            Path = System.IO.Path.Combine(origDir, "CompiledOctreesCache", "patches");
            Directory.CreateDirectory(Path);
            break;
        }
        if(Path == null) throw new Exception("couldn't determine the patches directory");
    }

    internal static void ClearPatchesDir()
    {
        foreach (string? path in Directory.EnumerateFiles(Path)) {
            if (System.IO.Path.GetExtension(path) != ".optoctrees") continue;
            File.Delete(path);
        }
    }
    
    internal static readonly string Path;
}