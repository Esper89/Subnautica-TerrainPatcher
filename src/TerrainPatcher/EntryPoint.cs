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
// along with TerrainPatcher.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.IO;
using HarmonyLib;
using QModManager.API.ModLoading;

namespace TerrainPatcher
{
    /// <summary> This mod's entry point. </summary>
    [QModCore]
    public static class EntryPoint
    {
        /// <summary> Initializes the mod. </summary>
        [QModPatch]
        public static void Initialize()
        {
            Harmony();
            LoadPatchFiles();
        }

        // Apply harmony patches.
        private static void Harmony()
        {
            var harmony = new Harmony(nameof(TerrainPatcher));
            harmony.PatchAll();
        }

        // Search for and load terrain patch files.
        private static void LoadPatchFiles()
        {
            string searchDir = Directory.GetParent(Constants.MOD_DIR).FullName;
            Directory.CreateDirectory(searchDir);

            string[] patchFiles = Directory.GetFiles(
                searchDir,
                "*" + Constants.PATCH_EXTENSION,
                SearchOption.AllDirectories
            );

            for (int i = 0; i < patchFiles.Length; i++)
            {
                LoadPatch(patchFiles[i]);
            }

            static void LoadPatch(string filepath)
            {
                string patchName = Path.GetFileNameWithoutExtension(filepath);

                try
                {
                    using (FileStream file = File.OpenRead(filepath))
                    {
                        TerrainRegistry.PatchTerrain(file);
                    }

                    Debug.Log($"Loaded '{patchName}'");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Problem loading '{patchName}'", ex);
                    Debug.ErrorMessage($"Could not load '{patchName}'");
                }
            }
        }
    }
}
