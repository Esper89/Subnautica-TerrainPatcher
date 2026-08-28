namespace TerrainPatcher;

internal static class PatchesDir {
    internal static readonly string[] ORIG_BATCH_DIRS = ["Build18", "Expansion"];
    
    static PatchesDir() {
        foreach (string origDirName in ORIG_BATCH_DIRS) {
            string? origDir = SNUtils.InsideUnmanaged(origDirName);

            if (!Directory.Exists(origDir)) continue;
            
            Path = System.IO.Path.Combine(origDir, "CompiledOctreesCache", "patches");
            Directory.CreateDirectory(Path);
            //TODO: Arguably the clearing of the old patches dir should be more explicit, within its own method rather than some automatic process
            //   Otherwise tho the checking of game versions is fine to do in a static constructor imo :/
            foreach (string? path in Directory.EnumerateFiles(Path)) {
                if (System.IO.Path.GetExtension(path) != ".optoctrees") continue;
                File.Delete(path);
            }
            break;
        }
    }
    
    internal static readonly string? Path;
}