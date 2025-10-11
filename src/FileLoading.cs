using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace TerrainPatcher
{
    // Loading patches from the filesystem.
    internal static class FileLoading
    {
        // Finds all patch files to apply and loads them.
        public static void FindAndLoadPatches()
        {
            Mod.LogInfo("Loading terrain patch files");
            LoadPatchFiles(GetPatchFiles());
        }

        // Search for patch files and sort them according to the config file.
        private static string[] GetPatchFiles()
        {
            string searchDir = Paths.BepInExRootPath;
            string[] paths = RecursiveFindPatchFiles(searchDir).ToArray();
            return SortFiles(paths, GetLoadOrder());

            // Sorts a list of file paths according to the specified load order.
            static string[] SortFiles(string[] paths, string[] loadOrder)
            {
                string?[] names = new string[paths.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = Path.GetFileNameWithoutExtension(paths[i]);
                }

                Mod.LogInfo("Patch load order:");

                var sorted = new List<string> { };

                // Loop through each entry in the load order list.
                foreach (string entry in loadOrder)
                {
                    // And check each file name to see if it matches that entry.
                    for (int i = 0; i < names.Length; i++)
                    {
                        if (entry == names[i])
                        {
                            sorted.Add(paths[i]);
                            Mod.LogInfo($"{sorted.Count}. '{names[i]}': '{paths[i]}'");

                            names[i] = null; // Remove the name from the list.
                        }
                    }
                }

                // Add all the files that weren't on the load order list to the end of it.
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i] is object)
                    {
                        sorted.Add(paths[i]);
                        Mod.LogInfo($"{sorted.Count}. '{names[i]}': '{paths[i]}'");
                    }
                }

                return sorted.ToArray();
            }

            // Gets the desired load order from the config file.
            static string[] GetLoadOrder()
            {
                string path = Path.Combine(Constants.MOD_DIR, LOAD_ORDER_FILE);

                var seen = new HashSet<string> { };
                var loadOrder = new List<string> { };

                if (File.Exists(path))
                {
                    try
                    {
                        Mod.LogInfo("Found load order file");

                        using (FileStream file = File.OpenRead(path))
                        {
                            var reader = new StreamReader(file);

                            while (!reader.EndOfStream)
                            {
                                string line = reader.ReadLine();

                                if (!string.IsNullOrEmpty(line) && !seen.Contains(line))
                                {
                                    loadOrder.Add(line);
                                }
                            }
                        }
                    }
                    catch (IOException ex)
                    {
                        Mod.LogWarning($"Could not open load order file: {ex}");
                    }
                }
                else { Mod.LogWarning("Could not find load order file"); }

                return loadOrder.ToArray();
            }
        }

        // Recursively find all patch files to load in the specified directory.
        private static IEnumerable<string> RecursiveFindPatchFiles(string path)
        {
            var stack = new Stack<string>();
            stack.Push(path);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                if (File.Exists(Path.Combine(dir, ".terrain-patcher-ignore"))) continue;

                foreach (var ext in Constants.PATCH_EXTENSIONS)
                {
                    foreach (var file in Directory.GetFiles(dir, $"*.{ext}"))
                    {
                        var skip = true;
                        try
                        {
                            var version = new BinaryReader(File.OpenRead(file)).ReadUInt32();
                            if (version != Constants.SKIP_VERSION) skip = false;
                        }
                        catch (Exception ex)
                        when (ex is IOException || ex is EndOfStreamException) { }

                        if (!skip) yield return file;
                    }
                }

                foreach (var subdir in Directory.GetDirectories(dir)) stack.Push(subdir);
            }
        }

        // The name of the load order file.
        private static readonly string LOAD_ORDER_FILE = "load-order.txt";

        // Load terrain patch files.
        private static void LoadPatchFiles(string[] patchFiles)
        {
            Mod.LogInfo("Loading patch files");

            for (int i = 0; i < patchFiles.Length; i++)
            {
                LoadPatch(patchFiles[i]);
            }

            static void LoadPatch(string filepath)
            {
                string patchName = Path.GetFileNameWithoutExtension(filepath);

                FileStream file;

                try { file = File.OpenRead(filepath); }
                catch (IOException ex)
                {
                    Mod.LogError($"Could not open patch file '{patchName}': {ex.Message}");
                    Mod.DisplayError($"Error opening terrain patch '{patchName}'!");
                    return;
                }

                TerrainRegistry.ApplyTerrainPatch(patchName, file, forceOriginal: false);
                file.Close();
            }
        }
    }
}
