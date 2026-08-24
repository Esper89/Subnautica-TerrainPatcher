using BepInEx;

namespace TerrainPatcher;

internal static class FileLoading {
    public static void FindAndLoadPatches() {
        LoadPatchFiles(GetOrderedPatchFiles());
    }

    private static string[] GetOrderedPatchFiles() {
        string? searchDir = Paths.BepInExRootPath;
        string[] paths = FindPatchFiles(searchDir).ToArray();
        return SortFiles(paths, GetLoadOrder());
    }
    
    private static string[] SortFiles(string[] paths, string[] loadOrder) {
        if (paths.Length == 0) {
            Plugin.LogInfo("No terrain patches are to be applied");
            return [];
        }

        string?[] names = new string?[paths.Length];
        for (int i = 0; i < names.Length; i++) {
            names[i] = Path.GetFileNameWithoutExtension(paths[i]);
        }

        Plugin.LogInfo("Terrain patches to be applied:");

        List<string> sorted = new();

        foreach (string entry in loadOrder) {
            for (int i = 0; i < names.Length; i++)
            {
                if (entry != names[i]) continue;
                sorted.Add(paths[i]);
                Plugin.LogInfo($"- '{names[i]}' at: {paths[i]}");
                names[i] = null;
            }
        }

        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == null) continue;
            sorted.Add(paths[i]);
            Plugin.LogInfo($"- '{names[i]}' at: {paths[i]}");
        }

        return sorted.ToArray();
    }

    private static string[] GetLoadOrder() {
        string path = Path.Combine(Plugin.AssemblyDir, LOAD_ORDER_FILE);

        HashSet<string> seen = new();
        List<string> loadOrder = new();

        if (File.Exists(path)) {
            try {
                Plugin.LogDebug("Found load order file");

                using FileStream file = File.OpenRead(path);
                StreamReader reader = new(file);

                while (!reader.EndOfStream) {
                    string? line = reader.ReadLine();

                    if (!string.IsNullOrEmpty(line) && !seen.Contains(line)) {
                        loadOrder.Add(line);
                    }
                }
            } catch (IOException ex) {
                Plugin.LogWarning($"Could not open load order file: {ex}");
            }
        } else Plugin.LogDebug("Did not find load order file");

        return loadOrder.ToArray();
    }

    private static readonly string LOAD_ORDER_FILE = "load-order.txt";

    private static readonly string[] PATCH_EXTENSIONS = [
        "optoctreepatch",
        "optoctreepatc", // Discord (in the long distant past) would concat extensions longer than 13 chars
    ];

    private static IEnumerable<string> FindPatchFiles(string path) {
        Stack<string> stack = new();
        stack.Push(path);

        while (stack.Count > 0) {
            string? dir = stack.Pop();
            if (File.Exists(Path.Combine(dir, ".terrain-patcher-ignore"))) continue;

            foreach (string ext in PATCH_EXTENSIONS) {
                foreach (string file in Directory.GetFiles(dir, $"*.{ext}")) {
                    bool skip = false;
                    try {
                        uint version = new BinaryReader(File.OpenRead(file)).ReadUInt32();
                        if (version == uint.MaxValue) skip = true;
                    } catch (Exception ex)
                        when (ex is IOException or EndOfStreamException) { }

                    if (!skip) yield return file;
                }
            }

            foreach (string? subdir in Directory.GetDirectories(dir)) stack.Push(subdir);
        }
    }

    private static void LoadPatchFiles(string[] patchFiles) {
        Plugin.LogInfo("Loading terrain patches");

        foreach (string t in patchFiles)
        {
            LoadPatch(t);
        }
    }

    private static void LoadPatch(string filepath) {
        string? patchName = Path.GetFileNameWithoutExtension(filepath);

        FileStream file;

        try { file = File.OpenRead(filepath); }
        catch (IOException ex) {
            Plugin.LogError($"Could not open patch file '{patchName}': {ex.Message}");
            Plugin.DisplayError($"Error opening terrain patch '{patchName}'");
            return;
        }

        TerrainPatching.ApplyTerrainPatch(patchName, file, forceOriginal: false);
        file.Close();
    }
}
