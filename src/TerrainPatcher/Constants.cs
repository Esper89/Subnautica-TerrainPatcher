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
using System.Reflection;

namespace TerrainPatcher
{
    internal static class Constants
    {
        // The current version number of the optoctreepatch format.
        internal const uint PATCH_VERSION = 0;

        // The current file extension for the optoctreepatch format.
        internal static readonly string PATCH_EXTENSION = ".optoctreepatch";

        // The current version number of the game's batch format.
        internal const uint BATCH_VERSION = 4;

        // The number of octrees in a batch.
        internal const byte OCTREES_PER_BATCH = 125;

        // The mod's directory.
        internal static readonly string MOD_DIR = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }
}
