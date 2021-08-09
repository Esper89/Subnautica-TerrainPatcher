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

using System.IO;

namespace TerrainPatcher
{
    // The directory containing temporary batch files.
    internal static class TempBatchStorage
    {
        static TempBatchStorage()
        {
            Directory.CreateDirectory(PATH);
            ClearTempFiles();
        }

        public static string PATH { get; } = Path.Combine(Constants.MOD_DIR, ".tmp");

        private static void ClearTempFiles()
        {
            foreach (string path in Directory.EnumerateFiles(PATH))
            {
                if (Path.GetExtension(path) == ".optoctrees") File.Delete(path);
            }
        }
    }
}
