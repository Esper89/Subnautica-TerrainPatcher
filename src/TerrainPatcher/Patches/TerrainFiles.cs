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
using System.Text.RegularExpressions;
using HarmonyLib;
using WorldStreaming;

namespace TerrainPatcher.Patches
{
    // Harmony patches that make the game open patched terrain files instead of the originals.
    internal static class TerrainFiles
    {
        // Patches LargeWorldStreamer.GetCompiledOctreesCachePath.
        [HarmonyPatch(typeof(LargeWorldStreamer), nameof(LargeWorldStreamer.GetCompiledOctreesCachePath))]
        internal static class LargeWorldStreamer_GetCompiledOctreesCachePath_Patch
        {
            // Matches a batch file. Supports negative batch numbers.
            private static readonly Regex pattern
                = new Regex($@"^(?:[^\0]*(?:\/|\\))?(compiled-batch-(?<x>-?\d+)-(?<y>-?\d+)-(?<z>-?\d+)\.optoctrees)$");

            private static bool Prefix(string filename, ref string __result, bool __runOriginal)
            {
                // We need to parse the filename into a batch ID before we can run SetResult.

                Match match = pattern.Match(filename);
                Int3 batchId;

                try
                {
                    int parse(string g) => int.Parse(match.Groups[g].Value);
                    batchId = new Int3(parse("x"), parse("y"), parse("z"));
                }
                catch (FormatException ex)
                {
                    Debug.LogError($"Game accessed batch file with invalid filename '{filename}'", ex);
                    return false;
                }

                return SetResult(batchId, ref __result, __runOriginal);
            }
        }

        // Patches BatchOctreesStreamer.GetPath.
        [HarmonyPatch(typeof(BatchOctreesStreamer), nameof(BatchOctreesStreamer.GetPath))]
        internal static class BatchOctreesStreamer_GetPath_Patch
        {
            private static bool Prefix(Int3 batchId, ref string __result, bool __runOriginal)
                => SetResult(batchId, ref __result, __runOriginal);
        }

        // Sets the result of the method and skips the original if needed.
        private static bool SetResult(Int3 batchId, ref string result, bool runOriginal)
        {
            if (TerrainRegistry.patchedBatches.ContainsKey(batchId) && runOriginal)
            {
                result = TerrainRegistry.patchedBatches[batchId];
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
