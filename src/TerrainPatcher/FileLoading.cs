// Copyright (c) 2021 Esper Thomson
//
// This file is part of TerrainPatcher.
//
// TerrainPatcher is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// TerrainPatcher is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with TerrainPatcher.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;

namespace TerrainPatcher
{
    // Loading patches from the filesystem.
    internal static class FileLoading
    {
        // Finds all patch files to apply and loads them.
        public static void FindAndLoadPatches()
        {
            LoadPatchFiles(GetPatchFiles());
        }

        // Search for patch files and sort them according to the config file.
        private static string[] GetPatchFiles()
        {
            string searchDir = Directory.GetParent(Constants.MOD_DIR).FullName;
            Directory.CreateDirectory(searchDir);

            string[] paths = Directory.GetFiles(
                searchDir,
                "*" + Constants.PATCH_EXTENSION,
                SearchOption.AllDirectories
            );

            return SortFiles(paths, GetLoadOrder());

            // Sorts a list of file paths according to the specified load order.
            static string[] SortFiles(string[] paths, string[] loadOrder)
            {
                string?[] names = new string[paths.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = Path.GetFileNameWithoutExtension(paths[i]);
                }

                var message = new Debug.Multiline("Patch load order:");

                var sorted = new List<string> { };

                // Loop through each entry in the load order list.
                for (int i = 0; i < loadOrder.Length; i++)
                {
                    // And check each file name to see if it matches that entry.
                    for (int j = 0; j < names.Length; j++)
                    {
                        if (loadOrder[i] == names[j])
                        {
                            sorted.Add(paths[j]);
                            message.AddLine($"{names[j]}: '{paths[j]}'");

                            names[j] = null; // Remove the name from the list.
                        }
                    }
                }

                // Add all the files that weren't on the load order list to the end of it.
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i] is object)
                    {
                        sorted.Add(paths[i]);
                        message.AddLine($"{names[i]}: '{paths[i]}'");
                    }
                }

                message.WriteDebug();

                return sorted.ToArray();
            }

            // Gets the desired load order from the config file.
            static string[] GetLoadOrder()
            {
                string path = Path.Combine(Constants.MOD_DIR, CONFIG_FILE);

                var seen = new HashSet<string> { };
                var loadOrder = new List<string> { };

                if (File.Exists(path))
                {
                    try
                    {
                        Debug.Log("Found load order file");

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
                    catch (IOException) { Debug.Log("Could not open load order file"); }
                }
                else { Debug.Log("Did not find load order file"); }

                return loadOrder.ToArray();
            }
        }

        // The name of the load order config file.
        private static readonly string CONFIG_FILE = "load-order";

        // Load terrain patch files.
        private static void LoadPatchFiles(string[] patchFiles)
        {
            Debug.Log("Loading patch files.");

            for (int i = 0; i < patchFiles.Length; i++)
            {
                LoadPatch(patchFiles[i]);
            }

            Debug.Log("Done loading patch files.");

            static void LoadPatch(string filepath)
            {
                string patchName = Path.GetFileNameWithoutExtension(filepath);

                try
                {
                    Debug.Log($"Loading '{patchName}'");

                    using (FileStream file = File.OpenRead(filepath))
                    {
                        TerrainRegistry.PatchTerrain(file);
                    }

                    Debug.Log($"Done loading '{patchName}'");
                }
                catch (Exception ex)
                {
                    string msg = $"Problem loading '{patchName}'";

                    Debug.LogError(msg, ex);
                    Debug.ErrorMessage(msg);
                }
            }
        }
    }
}
