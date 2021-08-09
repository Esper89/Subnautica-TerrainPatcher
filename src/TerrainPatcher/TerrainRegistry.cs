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
using TerrainPatcher.Extensions;

namespace TerrainPatcher
{
    /// <summary> The global registry of terrain patches. </summary>
    public static class TerrainRegistry
    {
        /// <summary> Applies a terrain patch file to the game's terrain. </summary>
        /// <param name="patchFile"> The patch file to apply. </param>
        public static void PatchTerrain(Stream patchFile)
        {
            if (patchFile is null)
            {
                throw new ArgumentNullException(
                    $"Argument '{nameof(patchFile)}' must not be null."
                );
            }

            uint version;

            try
            {
                version = patchFile.ReadUInt();
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("Provided patch file is not large enough.");
            }

            if (version != Constants.PATCH_VERSION)
            {
                throw new InvalidDataException(
                    "Provided patch file does not have the correct version." +
                    $"The correct version is {Constants.PATCH_VERSION}, this patch file has version {version}."
                );
            }

            while (true)
            {
                try
                {
                    ApplyBatchPatch(patchFile);
                }
                catch (EndOfStreamException)
                {
                    break;
                }
            }
        }

        // All batches that have been patched.
        // The Int3 is the ID of the batch and the string is the file name.
        internal static readonly Dictionary<Int3, string> patchedBatches = new Dictionary<Int3, string> { };

        // Loads a batch into patchedBatches if necessary, then applies the patch.
        private static void ApplyBatchPatch(Stream patch)
        {
            var batchId = new Int3(patch.ReadShort(), patch.ReadShort(), patch.ReadShort());

            lock (patchedBatches)
            {
                if (!patchedBatches.ContainsKey(batchId))
                {
                    RegisterNewBatch(batchId);
                }
            }

            PatchOctrees(batchId, patch);
        }

        // Creates a new tempfile for a batch and stores it in patchedBatches.
        // Copies the original batch if possible.
        private static void RegisterNewBatch(Int3 batchId)
        {
            string newPath = Path.Combine(
                TempBatchStorage.PATH,
                $"tmp-batch-{batchId.x}-{batchId.y}-{batchId.z}.optoctrees"
            );

            string origPath = Path.Combine(
                SNUtils.InsideUnmanaged("Build18"),
                "CompiledOctreesCache",
                $"compiled-batch-{batchId.x}-{batchId.y}-{batchId.z}.optoctrees"
            );

            bool fillWithEmptyBatch = true;

            if (File.Exists(origPath))
            {
                using (FileStream file = File.OpenRead(origPath))
                {
                    if (file.ReadUInt() == Constants.BATCH_VERSION)
                    {
                        fillWithEmptyBatch = false;
                        File.Copy(origPath, newPath, overwrite: true);
                    }
                }
            }

            if (fillWithEmptyBatch)
            {
                using (FileStream file = File.Open(origPath, FileMode.Create))
                {
                    file.WriteUInt(Constants.BATCH_VERSION);

                    for (int i = 0; i < Constants.OCTREES_PER_BATCH; i++)
                    {
                        // Single-node empty octree.
                        file.WriteUShort(1);
                        file.WriteUInt(0);
                    }
                }
            }

            patchedBatches[batchId] = newPath;
        }

        // Patches the contents of a batch, assuming it's already in patchedBatches.
        private static void PatchOctrees(Int3 batchId, Stream patch)
        {
            Stream original;

            // Copy the contents of the original file into memory.
            using (FileStream file = File.OpenRead(patchedBatches[batchId]))
            {
                byte[] bytes = new byte[file.Length];

                for (int i = 0; i < file.Length; i++)
                {
                    bytes[i] = (byte)file.ReadByte();
                }

                original = new MemoryStream(buffer: bytes, writable: false);

                original.ReadUInt(); // Discard version bytes.
            }

            // Apply the patch to the target file.
            using (FileStream file = File.Open(patchedBatches[batchId], FileMode.Create))
            {
                file.WriteUInt(Constants.BATCH_VERSION); // Prepend version bytes.

                // Call the terrain patcher.
                TerrainPatching.Patch(
                    target: file,
                    original: original,
                    patch: patch
                );
            }
        }
    }
}
