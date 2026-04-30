using BepInEx;

namespace TerrainPatcher;

static class FileLoading {
    public static void FindAndLoadPatches() {
        FileLoading.LoadPatchFiles(FileLoading.GetOrderedPatchFiles());
    }

    static string[] GetOrderedPatchFiles() {
        var searchDir = Paths.BepInExRootPath;
        var paths = FileLoading.FindPatchFiles(searchDir).ToArray();
        return SortFiles(paths, GetLoadOrder());

        static string[] SortFiles(string[] paths, string[] loadOrder) {
            if (paths.Length == 0) {
                Mod.LogInfo("No terrain patches are to be applied");
                return [];
            }

            var names = new string?[paths.Length];
            for (var i = 0; i < names.Length; i++) {
                names[i] = Path.GetFileNameWithoutExtension(paths[i]);
            }

            Mod.LogInfo("Terrain patches to be applied:");

            var sorted = new List<string>();

            foreach (var entry in loadOrder) {
                for (var i = 0; i < names.Length; i++) {
                    if (entry == names[i]) {
                        sorted.Add(paths[i]);
                        Mod.LogInfo($"- '{names[i]}' at: {paths[i]}");
                        names[i] = null;
                    }
                }
            }

            for (var i = 0; i < names.Length; i++) {
                if (names[i] is object) {
                    sorted.Add(paths[i]);
                    Mod.LogInfo($"- '{names[i]}' at: {paths[i]}");
                }
            }

            return sorted.ToArray();
        }

        static string[] GetLoadOrder() {
            var path = Path.Combine(Mod.AssemblyDir, FileLoading.LOAD_ORDER_FILE);

            var seen = new HashSet<string>();
            var loadOrder = new List<string>();

            if (File.Exists(path)) {
                try {
                    Mod.LogDebug("Found load order file");

                    using (var file = File.OpenRead(path)) {
                        var reader = new StreamReader(file);

                        while (!reader.EndOfStream) {
                            var line = reader.ReadLine();

                            if (!string.IsNullOrEmpty(line) && !seen.Contains(line)) {
                                loadOrder.Add(line);
                            }
                        }
                    }
                } catch (IOException ex) {
                    Mod.LogWarning($"Could not open load order file: {ex}");
                }
            } else Mod.LogDebug("Did not find load order file");

            return loadOrder.ToArray();
        }
    }

    static readonly string LOAD_ORDER_FILE = "load-order.txt";

    static readonly string[] PATCH_EXTENSIONS = [
        "optoctreepatch",
        "optoctreepatc",
    ];

    static IEnumerable<string> FindPatchFiles(string path) {
        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0) {
            var dir = stack.Pop();
            if (File.Exists(Path.Combine(dir, ".terrain-patcher-ignore"))) continue;

            foreach (var ext in FileLoading.PATCH_EXTENSIONS) {
                foreach (var file in Directory.GetFiles(dir, $"*.{ext}")) {
                    var skip = false;
                    try {
                        var version = new BinaryReader(File.OpenRead(file)).ReadUInt32();
                        if (version == uint.MaxValue) skip = true;
                    } catch (Exception ex)
                        when (ex is IOException || ex is EndOfStreamException) { }

                    if (!skip) yield return file;
                }
            }

            foreach (var subdir in Directory.GetDirectories(dir)) stack.Push(subdir);
        }
    }

    static void LoadPatchFiles(string[] patchFiles) {
        Mod.LogInfo("Loading terrain patches");

        for (var i = 0; i < patchFiles.Length; i++) {
            LoadPatch(patchFiles[i]);
        }

        static void LoadPatch(string filepath) {
            var patchName = Path.GetFileNameWithoutExtension(filepath);

            FileStream file;

            try { file = File.OpenRead(filepath); }
            catch (IOException ex) {
                Mod.LogError($"Could not open patch file '{patchName}': {ex.Message}");
                Mod.DisplayError($"Error opening terrain patch '{patchName}'");
                return;
            }

            TerrainPatching.ApplyTerrainPatch(patchName, file, forceOriginal: false);
            file.Close();
        }
    }
}
