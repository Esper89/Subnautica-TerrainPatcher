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
using System.IO;
using TerrainPatcher.Extensions;

namespace TerrainPatcher
{
    internal static class TerrainPatching
    {
        // Patches a target file, using an original file and a patch file.
        public static void Patch(Stream target, Stream original, Stream patch)
        {
            int patchedOctreeCount = patch.ReadByte();

            if (patchedOctreeCount > Constants.OCTREES_PER_BATCH)
            {
                throw new InvalidDataException(
                    "A patch contains more octrees than the batch can contain."
                );
            }

            // An array of byte arrays. Each byte array is the binary node data for one octree.
            byte[][] octrees = new byte[Constants.OCTREES_PER_BATCH][];

            // Copy the original batch into the octrees array.
            for (int i = 0; i < Constants.OCTREES_PER_BATCH; i++)
            {
                try
                {
                    octrees[i] = original.ReadBytes(original.ReadUShort() * 4);
                }
                catch (EndOfStreamException)
                {
                    break;
                }
            }

            // Apply patches on top of the octrees array.
            for (int i = 0; i < patchedOctreeCount; i++)
            {
                try
                {
                    octrees[patch.ReadByte()] = patch.ReadBytes(patch.ReadUShort() * 4);
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (IndexOutOfRangeException)
                {
                    throw new InvalidDataException(
                        "A patch contains an octree outside the bounds of the batch it applies to."
                    );
                }
            }

            // Write the octrees array to the target stream.
            for (int i = 0; i < octrees.Length; i++)
            {
                if (octrees[i] is object)
                {
                    target.WriteUShort((ushort)(octrees[i].Length / 4));
                    target.WriteBytes(octrees[i]);
                }
                else
                {
                    // Single-node empty octree.
                    target.WriteUShort(1);
                    target.WriteUInt(0);
                }
            }
        }
    }
}
